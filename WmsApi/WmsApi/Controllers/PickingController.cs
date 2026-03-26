using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/picking")]
public class PickingController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // POST /api/picking/open-session
    // เปิด Picking Session → ผูกกับ Pack Pallet
    // =============================================
    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenPickingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PackPalletId))
            return BadRequest(new ApiError("กรุณาระบุ Pack Pallet ID"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        // ตรวจสอบ Pack Pallet — ถ้ายังไม่มีในระบบให้สร้างอัตโนมัติ (Pallet เปล่า)
        var packPallet = await db.Pallets.FindAsync(req.PackPalletId);
        if (packPallet is null)
        {
            packPallet = new Pallet
            {
                PalletId = req.PackPalletId,
                Type = "FG",
                Status = "AVAILABLE",
            };
            db.Pallets.Add(packPallet);
        }

        if (packPallet.Status != "AVAILABLE" && packPallet.Status != "PACKING")
            return BadRequest(new ApiError(
                $"Pack Pallet '{req.PackPalletId}' ไม่พร้อมใช้งาน (สถานะ: {packPallet.Status})"));

        // เช็คว่ามี session เปิดค้างอยู่หรือไม่
        var existing = await db.PickingSessions
            .Include(s => s.Lines)
                .ThenInclude(l => l.Part)
            .FirstOrDefaultAsync(s => s.PackPalletId == req.PackPalletId
                                   && s.Status == "OPEN");

        if (existing is not null)
        {
            var existingLines = existing.Lines
                .Where(l => l.Status == "PICKED")
                .Select(l => new PickingLineResponse(
                    LineId: l.LineId,
                    SourceId: l.SourceType == "BASKET" ? l.BasketId! : l.PickPalletId!,
                    SourceType: l.SourceType,
                    PartId: l.PartId,
                    Owner: l.Part!.Owner,
                    Brand: l.Part!.Brand,
                    ItemDesc: l.Part!.ItemDesc,
                    ImageUrl: l.Part!.ImageUrl,
                    LotNumber: l.LotNumber,
                    ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
                    QtyPicked: l.QtyPicked,
                    Status: l.Status
                )).ToList();

            return Ok(new OpenPickingResponse(
                SessionId: existing.SessionId,
                PackPalletId: existing.PackPalletId,
                Status: existing.Status,
                PickedLines: existingLines
            ));
        }

        // สร้าง session ใหม่
        var session = new PickingSession
        {
            PackPalletId = req.PackPalletId,
            OperatorId = req.OperatorId,
        };

        packPallet.Status = "PACKING";
        packPallet.UpdatedAt = DateTime.UtcNow;

        db.PickingSessions.Add(session);
        await db.SaveChangesAsync();

        return Ok(new OpenPickingResponse(
            SessionId: session.SessionId,
            PackPalletId: session.PackPalletId,
            Status: session.Status,
            PickedLines: []
        ));
    }

    // =============================================
    // GET /api/picking/active-session/{packPalletId}
    // ดึง session ที่เปิดค้างอยู่
    // =============================================
    [HttpGet("active-session/{packPalletId}")]
    public async Task<IActionResult> GetActiveSession(string packPalletId)
    {
        var session = await db.PickingSessions
            .Include(s => s.Lines)
                .ThenInclude(l => l.Part)
            .FirstOrDefaultAsync(s => s.PackPalletId == packPalletId
                                   && s.Status == "OPEN");

        if (session is null)
            return NotFound(new ApiError("ไม่มี session ที่เปิดค้างอยู่"));

        var lines = session.Lines
            .Where(l => l.Status == "PICKED")
            .Select(l => new PickingLineResponse(
                LineId: l.LineId,
                SourceId: l.SourceType == "BASKET" ? l.BasketId! : l.PickPalletId!,
                SourceType: l.SourceType,
                PartId: l.PartId,
                Owner: l.Part!.Owner,
                Brand: l.Part!.Brand,
                ItemDesc: l.Part!.ItemDesc,
                ImageUrl: l.Part!.ImageUrl,
                LotNumber: l.LotNumber,
                ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
                QtyPicked: l.QtyPicked,
                Status: l.Status
            )).ToList();

        return Ok(new OpenPickingResponse(
            SessionId: session.SessionId,
            PackPalletId: session.PackPalletId,
            Status: session.Status,
            PickedLines: lines
        ));
    }

    // =============================================
    // GET /api/picking/scan-source/{sourceId}
    // สแกน Pallet หรือ Basket → ดูของข้างใน
    // =============================================
    [HttpGet("scan-source/{sourceId}")]
    public async Task<IActionResult> ScanSource(string sourceId)
    {
        // 1. เช็ค Pallet ก่อน
        var pallet = await db.Pallets.FindAsync(sourceId);
        if (pallet is not null)
        {
            // Block pallet ที่กำลังใช้เป็น pack pallet
            if (pallet.Status is "PACKING" or "PACKED")
                return BadRequest(new ApiError(
                    $"Pallet '{sourceId}' กำลังใช้งานเป็น Pack Pallet (สถานะ: {pallet.Status})"));

            // ดึงสินค้าบน Pallet (PALLETIZED)
            var palletLines = await db.ReceiptLines
                .Include(l => l.Part)
                .Where(l => l.PalletId == sourceId
                         && l.Status == "PALLETIZED")
                .ToListAsync();

            var palletItems = palletLines.Select(l => new SourceItemResponse(
                PartId: l.PartId,
                Owner: l.Part!.Owner,
                Brand: l.Part!.Brand,
                ItemDesc: l.Part!.ItemDesc,
                ImageUrl: l.Part!.ImageUrl,
                LotNumber: l.LotNumber,
                ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
                Qty: l.QtyReceived,
                Condition: l.Condition
            )).ToList();

            return Ok(new ScanSourceResponse(
                SourceId: pallet.PalletId,
                SourceType: "PALLET",
                Type: pallet.Type ?? "-",
                Status: pallet.Status,
                Items: palletItems,
                Message: palletItems.Count == 0
                    ? $"📦 Pallet '{sourceId}' ว่าง — พร้อมส่งคืน"
                    : $"✅ พบ {palletItems.Count} รายการบน Pallet"
            ));
        }

        // 2. เช็ค Basket
        var basket = await db.Baskets.FindAsync(sourceId);
        if (basket is not null)
        {
            // ดึงสินค้าใน Basket (LOADED)
            var basketLines = await db.BasketLines
                .Include(l => l.Part)
                .Where(l => l.BasketId == sourceId
                         && l.Status == "LOADED")
                .ToListAsync();

            var basketItems = basketLines.Select(l => new SourceItemResponse(
                PartId: l.PartId,
                Owner: l.Part!.Owner,
                Brand: l.Part!.Brand,
                ItemDesc: l.Part!.ItemDesc,
                ImageUrl: l.Part!.ImageUrl,
                LotNumber: l.LotNumber,
                ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
                Qty: l.QtyLoaded,
                Condition: "NORMAL"
            )).ToList();

            return Ok(new ScanSourceResponse(
                SourceId: basket.BasketId,
                SourceType: "BASKET",
                Type: basket.Label,
                Status: basket.Status,
                Items: basketItems,
                Message: basketItems.Count == 0
                    ? $"🧺 Basket '{sourceId}' ว่าง — พร้อมส่งคืน"
                    : $"✅ พบ {basketItems.Count} รายการใน Basket"
            ));
        }

        return NotFound(new ApiError($"ไม่พบ Pallet หรือ Basket '{sourceId}'"));
    }

    // =============================================
    // POST /api/picking/pick-item
    // หยิบสินค้าจาก Source (Pallet/Basket) → Pack Pallet
    // =============================================
    [HttpPost("pick-item")]
    public async Task<IActionResult> PickItem([FromBody] PickItemRequest req)
    {
        if (req.QtyPicked <= 0)
            return BadRequest(new ApiError("จำนวนที่ pick ต้องมากกว่า 0"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        var session = await db.PickingSessions.FindAsync(req.SessionId);
        if (session is null || session.Status != "OPEN")
            return BadRequest(new ApiError("Session ไม่ถูกต้องหรือปิดไปแล้ว"));

        int remaining;

        if (req.SourceType == "BASKET")
        {
            // ── Pick จาก Basket ─────────────────
            var basket = await db.Baskets.FindAsync(req.SourceId);
            if (basket is null)
                return NotFound(new ApiError($"Basket '{req.SourceId}' not found."));

            var basketLine = await db.BasketLines
                .Include(l => l.Part)
                .FirstOrDefaultAsync(l => l.BasketId == req.SourceId
                                       && l.PartId == req.PartId
                                       && l.Status == "LOADED");

            if (basketLine is null)
                return BadRequest(new ApiError(
                    $"ไม่พบ Part '{req.PartId}' ใน Basket '{req.SourceId}'"));

            if (req.QtyPicked > basketLine.QtyLoaded)
                return BadRequest(new ApiError(
                    $"จำนวนที่ pick ({req.QtyPicked}) มากกว่าจำนวนใน basket ({basketLine.QtyLoaded})"));

            // สร้าง picking line
            var pickLine = new PickingLine
            {
                SessionId = req.SessionId,
                SourceType = "BASKET",
                BasketId = req.SourceId,
                PartId = req.PartId,
                LotNumber = basketLine.LotNumber,
                ExpiredDate = basketLine.ExpiredDate,
                QtyPicked = req.QtyPicked,
                OperatorId = req.OperatorId,
            };
            db.PickingLines.Add(pickLine);

            // ลดจำนวนใน basket line
            basketLine.QtyLoaded -= req.QtyPicked;
            if (basketLine.QtyLoaded <= 0)
            {
                basketLine.Status = "PICKED";
                basketLine.QtyLoaded = 0;
            }

            await db.SaveChangesAsync();

            // คำนวณจำนวนที่เหลือใน basket
            remaining = await db.BasketLines
                .Where(l => l.BasketId == req.SourceId
                         && l.Status == "LOADED")
                .SumAsync(l => l.QtyLoaded);

            return Ok(new PickItemResponse(
                Success: true,
                LineId: pickLine.LineId,
                PartId: req.PartId,
                QtyPicked: req.QtyPicked,
                RemainingOnSource: remaining,
                Message: remaining == 0
                    ? "🧺 Basket ว่างแล้ว พร้อมส่งกลับ"
                    : $"✅ Pick สำเร็จ เหลืออีก {remaining} ชิ้นใน Basket"
            ));
        }
        else
        {
            // ── Pick จาก Pallet (default) ──────
            var pickPallet = await db.Pallets.FindAsync(req.SourceId);
            if (pickPallet is null)
                return NotFound(new ApiError($"Pallet '{req.SourceId}' not found."));

            var receiptLine = await db.ReceiptLines
                .Include(l => l.Part)
                .FirstOrDefaultAsync(l => l.PalletId == req.SourceId
                                       && l.PartId == req.PartId
                                       && l.Status == "PALLETIZED");

            if (receiptLine is null)
                return BadRequest(new ApiError(
                    $"ไม่พบ Part '{req.PartId}' บน Pallet '{req.SourceId}'"));

            if (req.QtyPicked > receiptLine.QtyReceived)
                return BadRequest(new ApiError(
                    $"จำนวนที่ pick ({req.QtyPicked}) มากกว่าจำนวนบน pallet ({receiptLine.QtyReceived})"));

            // สร้าง picking line
            var pickLine = new PickingLine
            {
                SessionId = req.SessionId,
                SourceType = "PALLET",
                PickPalletId = req.SourceId,
                PartId = req.PartId,
                LotNumber = receiptLine.LotNumber,
                ExpiredDate = receiptLine.ExpiredDate,
                QtyPicked = req.QtyPicked,
                OperatorId = req.OperatorId,
            };
            db.PickingLines.Add(pickLine);

            // ลดจำนวนบน receipt line
            receiptLine.QtyReceived -= req.QtyPicked;
            receiptLine.UpdatedAt = DateTime.UtcNow;
            if (receiptLine.QtyReceived <= 0)
            {
                receiptLine.Status = "PICKED";
                receiptLine.QtyReceived = 0;
            }

            await db.SaveChangesAsync();

            // คำนวณจำนวนที่เหลือบน pallet
            remaining = await db.ReceiptLines
                .Where(l => l.PalletId == req.SourceId
                         && l.Status == "PALLETIZED")
                .SumAsync(l => l.QtyReceived);

            return Ok(new PickItemResponse(
                Success: true,
                LineId: pickLine.LineId,
                PartId: req.PartId,
                QtyPicked: req.QtyPicked,
                RemainingOnSource: remaining,
                Message: remaining == 0
                    ? "📦 Pallet ว่างแล้ว พร้อมส่งกลับ"
                    : $"✅ Pick สำเร็จ เหลืออีก {remaining} ชิ้นบน Pallet"
            ));
        }
    }

    // =============================================
    // POST /api/picking/return-source
    // ส่ง Pallet/Basket กลับ (ASRS หรือ ZONE PICK)
    // =============================================
    [HttpPost("return-source")]
    public async Task<IActionResult> ReturnSource([FromBody] ReturnSourceRequest req)
    {
        var dest = string.IsNullOrWhiteSpace(req.Destination) ? "ASRS" : req.Destination.ToUpper();
        var destLabel = dest == "ZONE_PICK" ? "ZONE PICK" : "ASRS";

        if (req.SourceType == "BASKET")
        {
            // ── Return Basket ──────────────────
            var basket = await db.Baskets.FindAsync(req.SourceId);
            if (basket is null)
                return NotFound(new ApiError($"Basket '{req.SourceId}' not found."));

            var hasItems = await db.BasketLines
                .AnyAsync(l => l.BasketId == req.SourceId
                            && l.Status == "LOADED"
                            && l.QtyLoaded > 0);

            basket.Status = hasItems ? "RETURNING" : "AVAILABLE";
            basket.Destination = destLabel;
            basket.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new ApiSuccess(true,
                hasItems
                    ? $"🧺 Basket '{req.SourceId}' ส่งกลับ {destLabel} (ยังมีของเหลือ)"
                    : $"✅ Basket '{req.SourceId}' ว่างแล้ว ส่งกลับ {destLabel}"
            ));
        }
        else
        {
            // ── Return Pallet ──────────────────
            var pallet = await db.Pallets.FindAsync(req.SourceId);
            if (pallet is null)
                return NotFound(new ApiError($"Pallet '{req.SourceId}' not found."));

            var hasItems = await db.ReceiptLines
                .AnyAsync(l => l.PalletId == req.SourceId
                            && l.Status == "PALLETIZED"
                            && l.QtyReceived > 0);

            pallet.Status = hasItems ? "RETURNING" : "AVAILABLE";
            pallet.Location = destLabel;
            pallet.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return Ok(new ApiSuccess(true,
                hasItems
                    ? $"📦 Pallet '{req.SourceId}' ส่งกลับ {destLabel} (ยังมีของเหลือ)"
                    : $"✅ Pallet '{req.SourceId}' ว่างแล้ว ส่งกลับ {destLabel}"
            ));
        }
    }

    // =============================================
    // ── v2 Pick Order Flow ────────────────────
    // =============================================

    // GET /api/picking/orders
    // ดึงรายการ Pick Order ที่ยังเปิดอยู่ทั้งหมด
    // =============================================
    [HttpGet("orders")]
    public async Task<IActionResult> GetPickOrders()
    {
        var orders = await db.PickOrders
            .Include(o => o.Details).ThenInclude(d => d.Part)
            .Include(o => o.Details).ThenInclude(d => d.Subs).ThenInclude(s => s.ReceiptLine)
            .Where(o => o.Status == "OPEN")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(BuildPickOrderResponse).ToList();
        return Ok(result);
    }

    // =============================================
    // GET /api/picking/order/{pickOrderId}
    // ดึง Pick Order เดี่ยว
    // =============================================
    [HttpGet("order/{pickOrderId}")]
    public async Task<IActionResult> GetPickOrder(string pickOrderId)
    {
        var o = await db.PickOrders
            .Include(o => o.Details).ThenInclude(d => d.Part)
            .Include(o => o.Details).ThenInclude(d => d.Subs).ThenInclude(s => s.ReceiptLine)
            .FirstOrDefaultAsync(o => o.PickOrderId == pickOrderId);

        if (o is null)
            return NotFound(new ApiError($"Pick Order '{pickOrderId}' not found."));

        return Ok(BuildPickOrderResponse(o));
    }

    private static PickOrderResponse BuildPickOrderResponse(PickOrder o)
    {
        var details = o.Details.Select(d =>
        {
            var subs = d.Subs.Select(s => new PickOrderSubResponse(
                Id: s.Id,
                PickOrderDetailId: s.PickOrderDetailId,
                ReceiptLineId: s.ReceiptLineId,
                PalletId: s.ReceiptLine?.PalletId,
                AllocatedQty: s.AllocatedQty,
                PickedQty: s.PickedQty,
                Status: s.Status
            )).ToList();

            return new PickOrderDetailResponse(
                Id: d.Id,
                PartId: d.PartId,
                Owner: d.Part!.Owner,
                Brand: d.Part!.Brand,
                ItemDesc: d.Part!.ItemDesc,
                ImageUrl: d.Part!.ImageUrl,
                RequiredQty: d.RequiredQty,
                ReservedQty: d.ReservedQty,
                RemainingQty: d.RequiredQty - d.ReservedQty,
                Status: d.Status,
                Subs: subs
            );
        }).ToList();

        return new PickOrderResponse(o.PickOrderId, o.Status, o.CreatedAt, details);
    }

    // =============================================
    // POST /api/picking/assign-station
    // สแกน Source Pallet → หา Station ว่าง → Map pallet ↔ station
    // =============================================
    [HttpPost("assign-station")]
    public async Task<IActionResult> AssignStation([FromBody] AssignPickStationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return BadRequest(new ApiError("กรุณาระบุ Pallet ID"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"ไม่พบ Pallet '{req.PalletId}'"));

        if (pallet.Status != "AVAILABLE" && pallet.Status != "STORED" && pallet.Status != "PICKING")
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่พร้อมสำหรับ Pick (สถานะ: {pallet.Status})"));

        // Auto-detect pickOrderId จาก PickOrderSub ที่เชื่อมกับ ReceiptLine บน pallet นี้
        var pickOrderId = req.PickOrderId;
        if (string.IsNullOrWhiteSpace(pickOrderId))
        {
            pickOrderId = await db.PickOrderSubs
                .Include(s => s.ReceiptLine)
                .Include(s => s.PickOrderDetail)
                .Where(s => s.ReceiptLine!.PalletId == req.PalletId
                         && s.Status == "PENDING"
                         && s.ReceiptLine!.QtyReceived > 0)
                .Select(s => s.PickOrderDetail!.PickOrderId)
                .FirstOrDefaultAsync();

            if (pickOrderId is null)
                return BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' ไม่มีสินค้าที่ผูกกับ Pick Order ใดๆ"));
        }

        var order = await db.PickOrders
            .FirstOrDefaultAsync(o => o.PickOrderId == pickOrderId && o.Status == "OPEN");

        if (order is null)
            return BadRequest(new ApiError($"Pick Order '{pickOrderId}' ไม่ถูกต้องหรือปิดแล้ว"));

        // หา PickOrderSub ที่เชื่อมกับ ReceiptLine บน pallet นี้ สำหรับ order นี้
        var palletSubs = await db.PickOrderSubs
            .Include(s => s.ReceiptLine).ThenInclude(l => l!.Part)
            .Include(s => s.PickOrderDetail)
            .Where(s => s.PickOrderDetail!.PickOrderId == pickOrderId
                     && s.ReceiptLine!.PalletId == req.PalletId
                     && s.Status == "PENDING"
                     && s.ReceiptLine!.QtyReceived > 0)
            .ToListAsync();

        if (palletSubs.Count == 0)
            return BadRequest(new ApiError($"Pallet '{req.PalletId}' ไม่มีสินค้าสำหรับ Pick Order '{pickOrderId}'"));

        // หา station ที่ map pallet นี้อยู่แล้ว หรือหา station ว่าง
        var station = await db.PickStations
            .FirstOrDefaultAsync(s => s.CurrentPalletId == req.PalletId);

        if (station is null)
        {
            station = await db.PickStations
                .FirstOrDefaultAsync(s => s.CurrentPalletId == null);

            if (station is null)
                return BadRequest(new ApiError("ไม่มี Pick Station ว่าง กรุณารอหรือ clear station ก่อน"));

            station.CurrentPalletId = req.PalletId;
            pallet.Status = "PICKING";
            pallet.Location = station.StationId;
            pallet.UpdatedAt = DateTime.UtcNow;

            // เปลี่ยน ReceiptLine ที่เกี่ยวข้องเป็น PICKING
            foreach (var sub in palletSubs.Where(s => s.ReceiptLine!.Status == "PALLETIZED"))
                sub.ReceiptLine!.Status = "PICKING";

            await db.SaveChangesAsync();
        }

        // สร้าง response — แนะนำจำนวนที่ควร pick = min(QtyOnPallet, Allocated - Picked)
        var palletItems = palletSubs.Select(s => new PickItemOnPallet(
            PartId: s.ReceiptLine!.PartId,
            Owner: s.ReceiptLine!.Part!.Owner,
            Brand: s.ReceiptLine!.Part!.Brand,
            ItemDesc: s.ReceiptLine!.Part!.ItemDesc,
            ImageUrl: s.ReceiptLine!.Part!.ImageUrl,
            QtyOnPallet: s.ReceiptLine!.QtyReceived,
            QtyToPickSuggested: Math.Max(0, Math.Min(s.ReceiptLine!.QtyReceived, s.AllocatedQty - s.PickedQty)),
            Condition: s.ReceiptLine!.Condition
        )).ToList();

        // รวม remaining ทั้ง order — จาก PickOrderDetail
        var allDetails = await db.PickOrderDetails
            .Include(d => d.Part)
            .Where(d => d.PickOrderId == pickOrderId && d.RequiredQty > d.ReservedQty)
            .ToListAsync();

        var pickOrderItems = allDetails.Select(d => new PickOrderDetailResponse(
            Id: d.Id,
            PartId: d.PartId,
            Owner: d.Part!.Owner,
            Brand: d.Part!.Brand,
            ItemDesc: d.Part!.ItemDesc,
            ImageUrl: d.Part!.ImageUrl,
            RequiredQty: d.RequiredQty,
            ReservedQty: d.ReservedQty,
            RemainingQty: d.RequiredQty - d.ReservedQty,
            Status: d.Status,
            Subs: []
        )).ToList();

        return Ok(new AssignPickStationResponse(
            StationId: station.StationId,
            StationName: station.Name,
            PalletId: req.PalletId,
            PickOrderId: pickOrderId,
            PalletItems: palletItems,
            PickOrderItems: pickOrderItems,
            Message: $"✅ Pallet '{req.PalletId}' อยู่ที่ {station.Name} (Pick Order: {pickOrderId})"
        ));
    }

    // =============================================
    // POST /api/picking/confirm-pick
    // ยืนยันการหยิบ: โอนสินค้าจาก Source → Dest Pallet
    // อัพเดท PickOrderSub.PickedQty + PickOrderDetail.ReservedQty
    // =============================================
    [HttpPost("confirm-pick")]
    public async Task<IActionResult> ConfirmPick([FromBody] ConfirmPickRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SourcePalletId))
            return BadRequest(new ApiError("กรุณาระบุ Source Pallet ID"));
        if (string.IsNullOrWhiteSpace(req.DestPalletId))
            return BadRequest(new ApiError("กรุณาระบุ Dest Pallet ID"));
        if (req.SourcePalletId == req.DestPalletId)
            return BadRequest(new ApiError("Source และ Dest Pallet ต้องไม่ใช่ตัวเดียวกัน"));
        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new ApiError("กรุณาระบุรายการที่จะ pick"));

        var order = await db.PickOrders
            .FirstOrDefaultAsync(o => o.PickOrderId == req.PickOrderId && o.Status == "OPEN");

        if (order is null)
            return BadRequest(new ApiError($"Pick Order '{req.PickOrderId}' ไม่ถูกต้องหรือปิดแล้ว"));

        var sourcePallet = await db.Pallets.FindAsync(req.SourcePalletId);
        if (sourcePallet is null)
            return NotFound(new ApiError($"ไม่พบ Source Pallet '{req.SourcePalletId}'"));

        var destPallet = await db.Pallets.FindAsync(req.DestPalletId);
        if (destPallet is null)
        {
            destPallet = new Pallet
            {
                PalletId = req.DestPalletId,
                Type = "FG",
                Status = "AVAILABLE",
            };
            db.Pallets.Add(destPallet);
        }

        // โอนแต่ละ item ผ่าน PickOrderSub
        foreach (var item in req.Items)
        {
            if (item.Qty <= 0) continue;

            // หา Sub ที่ allocate Part นี้จาก Receipt Line บน source pallet
            var sub = await db.PickOrderSubs
                .Include(s => s.ReceiptLine).ThenInclude(l => l!.Part)
                .Include(s => s.PickOrderDetail)
                .FirstOrDefaultAsync(s => s.PickOrderDetail!.PickOrderId == req.PickOrderId
                                       && s.ReceiptLine!.PalletId == req.SourcePalletId
                                       && s.ReceiptLine!.PartId == item.PartId
                                       && s.Status == "PENDING"
                                       && s.ReceiptLine!.QtyReceived > 0);

            if (sub is null)
                return BadRequest(new ApiError(
                    $"ไม่พบ Part '{item.PartId}' บน Pallet '{req.SourcePalletId}' สำหรับ Pick Order นี้"));

            var sourceLine = sub.ReceiptLine!;
            var detail = sub.PickOrderDetail!;
            var actualQty = Math.Min(item.Qty, Math.Min(sourceLine.QtyReceived, sub.AllocatedQty - sub.PickedQty));

            // อัพเดท Sub
            sub.PickedQty += actualQty;
            if (sub.PickedQty >= sub.AllocatedQty)
                sub.Status = "PICKED";

            // อัพเดท Detail
            detail.ReservedQty += actualQty;
            if (detail.ReservedQty >= detail.RequiredQty)
                detail.Status = "COMPLETED";
            else if (detail.ReservedQty > 0)
                detail.Status = "PARTIAL";

            // ลดจาก source ReceiptLine
            sourceLine.QtyReceived -= actualQty;
            sourceLine.UpdatedAt = DateTime.UtcNow;
            if (sourceLine.QtyReceived <= 0)
            {
                sourceLine.QtyReceived = 0;
                sourceLine.Status = "PICKED";
            }

            // เพิ่มไปยัง dest pallet (receipt line ใหม่)
            db.ReceiptLines.Add(new ReceiptLine
            {
                SessionId = sourceLine.SessionId,
                POId = sourceLine.POId,
                PartId = item.PartId,
                PalletId = req.DestPalletId,
                QtyReceived = actualQty,
                Status = "PALLETIZED",
                OperatorId = req.OperatorId,
            });
        }

        await db.SaveChangesAsync();

        // เช็คว่า source pallet ยังมีของที่ต้อง pick (สำหรับ order นี้) หรือไม่
        var sourceHasPickItems = await db.PickOrderSubs
            .Include(s => s.ReceiptLine)
            .Include(s => s.PickOrderDetail)
            .AnyAsync(s => s.PickOrderDetail!.PickOrderId == req.PickOrderId
                        && s.ReceiptLine!.PalletId == req.SourcePalletId
                        && s.Status == "PENDING"
                        && s.ReceiptLine!.QtyReceived > 0);

        // เช็ค pallet ว่างเลยหรือยัง (รวมทุก order)
        var sourceHasAnyItems = await db.ReceiptLines.AnyAsync(
            l => l.PalletId == req.SourcePalletId
              && l.QtyReceived > 0
              && (l.Status == "PALLETIZED" || l.Status == "PICKING"));

        destPallet.Status = "PACKED";
        destPallet.UpdatedAt = DateTime.UtcNow;

        // ตรวจสอบว่า pick order ครบหรือยัง (ทุก detail ครบ)
        var allDetails = await db.PickOrderDetails
            .Include(d => d.Part)
            .Where(d => d.PickOrderId == req.PickOrderId)
            .ToListAsync();

        var isComplete = allDetails.All(d => d.ReservedQty >= d.RequiredQty);
        if (isComplete)
        {
            order.Status = "COMPLETED";
            order.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var remaining = allDetails.Select(d => new PickRemainingItem(
            PartId: d.PartId,
            ItemDesc: d.Part!.ItemDesc,
            RequiredQty: d.RequiredQty,
            PickedQty: d.ReservedQty,
            RemainingQty: d.RequiredQty - d.ReservedQty
        )).ToList();

        return Ok(new ConfirmPickResponse(
            IsPickOrderComplete: isComplete,
            SourcePalletEmpty: !sourceHasAnyItems,
            SourcePickDone: !sourceHasPickItems,
            PickOrderStatus: order.Status,
            RemainingItems: remaining,
            Message: isComplete
                ? $"🎉 Pick Order '{req.PickOrderId}' ครบแล้ว!"
                : "✅ Pick สำเร็จ"
        ));
    }

    // =============================================
    // POST /api/picking/return-pallet
    // ส่ง Source Pallet กลับ (ASRS / ZONE_PACK) หลัง pick เสร็จ
    // =============================================
    [HttpPost("return-pallet")]
    public async Task<IActionResult> ReturnPallet([FromBody] ReturnPalletRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return BadRequest(new ApiError("กรุณาระบุ Pallet ID"));

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"ไม่พบ Pallet '{req.PalletId}'"));

        var dest = string.IsNullOrWhiteSpace(req.Destination) ? "ASRS" : req.Destination.ToUpper();

        // เช็คว่า pallet ยังมีของเหลือไหม
        var hasItems = await db.ReceiptLines.AnyAsync(
            l => l.PalletId == req.PalletId
              && l.QtyReceived > 0
              && (l.Status == "PALLETIZED" || l.Status == "PICKING"));

        // มีของ → IN_TRANSIT + FG, ว่าง → AVAILABLE + Type null
        pallet.Status = hasItems ? "IN_TRANSIT" : "AVAILABLE";
        pallet.Type = hasItems ? "FG" : null;
        pallet.Location = dest;
        pallet.UpdatedAt = DateTime.UtcNow;

        // Clear station mapping
        var station = await db.PickStations
            .FirstOrDefaultAsync(s => s.CurrentPalletId == req.PalletId);
        if (station is not null)
            station.CurrentPalletId = null;

        // เปลี่ยน ReceiptLines กลับเป็น PALLETIZED (ถ้ายังมีของเหลือ)
        if (hasItems)
        {
            var pickingLines = await db.ReceiptLines
                .Where(l => l.PalletId == req.PalletId
                         && l.Status == "PICKING"
                         && l.QtyReceived > 0)
                .ToListAsync();
            foreach (var line in pickingLines)
                line.Status = "PALLETIZED";
        }

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            hasItems
                ? $"📦 Pallet '{req.PalletId}' ส่งไป {dest} (IN_TRANSIT — ยังมีของเหลือ)"
                : $"📦 Pallet '{req.PalletId}' ส่งไป {dest} (AVAILABLE — ว่างเลย)"));
    }

    // =============================================
    // POST /api/picking/complete-session/{sessionId}
    // ปิด Session เมื่อ Pack Pallet ครบ
    // =============================================
    [HttpPost("complete-session/{sessionId}")]
    public async Task<IActionResult> CompleteSession(int sessionId)
    {
        var session = await db.PickingSessions
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session is null)
            return NotFound(new ApiError("Session not found."));

        if (session.Status != "OPEN")
            return BadRequest(new ApiError("Session ปิดไปแล้ว"));

        var pickedLines = session.Lines.Where(l => l.Status == "PICKED").ToList();
        if (pickedLines.Count == 0)
            return BadRequest(new ApiError("ไม่มีรายการที่ pick — ไม่สามารถปิด Session ได้"));

        session.Status = "COMPLETED";
        session.CompletedAt = DateTime.UtcNow;

        // อัพเดท pack pallet status
        var packPallet = await db.Pallets.FindAsync(session.PackPalletId);
        if (packPallet is not null)
        {
            packPallet.Status = "PACKED";
            packPallet.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var totalPicked = pickedLines.Sum(l => l.QtyPicked);

        return Ok(new CompletePickingResponse(
            Success: true,
            TotalItemsPicked: totalPicked,
            PackPalletId: session.PackPalletId,
            Message: $"✅ Picking เสร็จสิ้น รวม {totalPicked} ชิ้น บน Pack Pallet '{session.PackPalletId}'"
        ));
    }

    // =============================================
    // TEST: GET /api/picking/available-lines
    // ดึง ReceiptLines ที่ PALLETIZED (พร้อม pick)
    // =============================================
    [HttpGet("available-lines")]
    public async Task<IActionResult> GetAvailableLines()
    {
        // ReceiptLines ที่ยังมี UnloadLine PENDING → ยังไม่พร้อม pick
        var pendingUnloadPallets = await db.UnloadLines
            .Where(u => u.Status == "PENDING")
            .Select(u => u.PalletId)
            .Distinct()
            .ToListAsync();

        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Include(l => l.Pallet)
            .Where(l => l.Status == "PALLETIZED" && l.PalletId != null
                     && l.Pallet!.Status != "PACKED"
                     && l.Pallet!.Type == "FG"
                     && l.Pallet!.Location != "ZONE_PACK"          // pick ไปแล้ว
                     && !pendingUnloadPallets.Contains(l.PalletId!)) // ยังไม่ unload เสร็จ
            .OrderBy(l => l.PartId)
            .ToListAsync();

        // หา qty ที่ถูก allocate ไปแล้วใน PickOrderSubs ที่ยัง PENDING
        var allocatedMap = await db.PickOrderSubs
            .Where(s => s.Status == "PENDING" || s.Status == "PICKED")
            .GroupBy(s => s.ReceiptLineId)
            .Select(g => new { ReceiptLineId = g.Key, AllocatedQty = g.Sum(s => s.AllocatedQty) })
            .ToDictionaryAsync(x => x.ReceiptLineId, x => x.AllocatedQty);

        var result = lines.Select(l =>
        {
            var allocated = allocatedMap.GetValueOrDefault(l.LineId, 0);
            var available = l.QtyReceived - allocated;
            return new
            {
                l.LineId,
                l.PartId,
                Owner = l.Part!.Owner,
                Brand = l.Part!.Brand,
                ItemDesc = l.Part!.ItemDesc,
                l.PalletId,
                PalletType = l.Pallet?.Type ?? "-",
                ImageUrl = l.Part!.ImageUrl,
                l.QtyReceived,
                AllocatedQty = allocated,
                AvailableQty = Math.Max(0, available),
                Condition = l.Condition,
                LotNumber = l.LotNumber,
            };
        })
        .Where(x => x.AvailableQty > 0)
        .ToList();

        return Ok(result);
    }

    // =============================================
    // TEST: POST /api/picking/create-test-order
    // สร้าง PickOrder + Details + Subs จาก ReceiptLines ที่เลือก
    // =============================================
    [HttpPost("create-test-order")]
    public async Task<IActionResult> CreateTestOrder([FromBody] CreateTestOrderRequest req)
    {
        if (req.Items.Count == 0)
            return BadRequest(new ApiError("กรุณาเลือกสินค้าอย่างน้อย 1 รายการ"));

        // สร้าง PickOrder ID
        var orderId = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var pickOrder = new PickOrder
        {
            PickOrderId = orderId,
            Status = "OPEN",
            CreatedBy = req.OperatorId,
            CreatedAt = DateTime.UtcNow,
        };

        db.PickOrders.Add(pickOrder);
        await db.SaveChangesAsync();

        // Group items by PartId → สร้าง PickOrderDetail ต่อ Part
        var grouped = req.Items.GroupBy(i => i.PartId).ToList();

        foreach (var g in grouped)
        {
            var totalQty = g.Sum(i => i.Qty);

            var detail = new PickOrderDetail
            {
                PickOrderId = orderId,
                PartId = g.Key,
                RequiredQty = totalQty,
                ReservedQty = 0,
                Status = "PENDING",
            };

            db.PickOrderDetails.Add(detail);
            await db.SaveChangesAsync(); // ต้อง save เพื่อได้ detail.Id

            // สร้าง PickOrderSub ต่อ ReceiptLine
            foreach (var item in g)
            {
                // ตรวจว่า ReceiptLine มีอยู่จริง
                var rl = await db.ReceiptLines.FindAsync(item.LineId);
                if (rl is null || rl.Status != "PALLETIZED")
                    return BadRequest(new ApiError(
                        $"ReceiptLine {item.LineId} ไม่พร้อม (status: {rl?.Status ?? "not found"})"));

                if (item.Qty <= 0 || item.Qty > rl.QtyReceived)
                    return BadRequest(new ApiError(
                        $"จำนวน {item.Qty} ไม่ถูกต้องสำหรับ Line {item.LineId} (มี {rl.QtyReceived})"));

                db.PickOrderSubs.Add(new PickOrderSub
                {
                    PickOrderDetailId = detail.Id,
                    ReceiptLineId = item.LineId,
                    AllocatedQty = item.Qty,
                    PickedQty = 0,
                    Status = "PENDING",
                });
            }
        }

        await db.SaveChangesAsync();

        // TEST: เปลี่ยน Pallet status → PICKING, Location → PICK (จำลองว่า AGV ส่งมาแล้ว)
        var palletIds = req.Items
            .Select(i => i.LineId)
            .Distinct()
            .ToList();

        var affectedLines = await db.ReceiptLines
            .Where(l => palletIds.Contains(l.LineId) && l.PalletId != null)
            .Select(l => l.PalletId!)
            .Distinct()
            .ToListAsync();

        var palletsToUpdate = await db.Pallets
            .Where(p => affectedLines.Contains(p.PalletId) && p.Status != "PICKING")
            .ToListAsync();

        foreach (var p in palletsToUpdate)
        {
            p.Status = "PICKING";
            p.Location = "PICK";
            p.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        return Ok(new { Success = true, PickOrderId = orderId, Message = $"สร้าง Pick Order '{orderId}' สำเร็จ ({grouped.Count} รายการ, {palletsToUpdate.Count} pallets → PICKING)" });
    }
}
