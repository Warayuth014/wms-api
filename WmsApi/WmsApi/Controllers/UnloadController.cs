using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/unload")]
public class UnloadController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // GET /api/unload/scan-pallet/{palletId}
    // สแกน Pallet ตรวจ FG/PW
    // =============================================
    [HttpGet("scan-pallet/{palletId}")]
    public async Task<IActionResult> ScanPallet(string palletId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{palletId}' not found."));

        // ปฏิเสธเฉพาะสถานะที่ไม่มีของบน Pallet
        if (pallet.Status is "AVAILABLE" or "DONE" or "RETURNING")
            return BadRequest(new ApiError(
                $"Pallet '{palletId}' ไม่พร้อม Unload (สถานะ: {pallet.Status})"));

        // ดึงสินค้าที่อยู่บน Pallet
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == palletId
                     && l.Status == "PALLETIZED")
            .ToListAsync();

        var items = lines.Select(l => new UnloadItemResponse(
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

        bool needsLabeling = pallet.Type == "PW";

        return Ok(new ScanPalletForUnloadResponse(
            PalletId: pallet.PalletId,
            Type: pallet.Type ?? "-",
            Status: pallet.Status,
            NeedsLabeling: needsLabeling,
            Items: items,
            Message: needsLabeling
                           ? "⚠️ Pallet ยังไม่ติดสติ๊กเกอร์ กรุณาส่งไปจุด Labeling ก่อน"
                           : "✅ Pallet พร้อม unload"
        ));
    }

    // =============================================
    // POST /api/unload/confirm-labeling
    // ติดสติ๊กเกอร์แล้ว PW → FG
    // =============================================
    [HttpPost("confirm-labeling")]
    public async Task<IActionResult> ConfirmLabeling([FromBody] ConfirmLabelingRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status != "PW")
            return BadRequest(new ApiError(
                $"Pallet is not PW status (current: {pallet.Status})."));

        pallet.Type = "FG";
        pallet.Status = "FG";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            $"Pallet '{req.PalletId}' changed to FG ✅ Ready to unload."));
    }

    // =============================================
    // POST /api/unload/open-session
    // เปิด session unload + สร้าง UnloadLines อัตโนมัติ
    // =============================================
    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenUnloadRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        // ── Resume: ถ้า pallet กำลัง UNLOADING → คืน session เดิม (STEP1 หรือ STEP2) ──
        if (pallet.Status == "UNLOADING")
        {
            var existing = await db.UnloadSessions
                .Include(s => s.UnloadLines)
                    .ThenInclude(l => l.Part)
                .FirstOrDefaultAsync(s => s.PalletId == req.PalletId
                                       && (s.Status == "STEP1" || s.Status == "STEP2"));

            if (existing is null)
                return BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' is UNLOADING but no active session found."));

            var existingItems = existing.UnloadLines.Select(l => new UnloadItemResponse(
                PartId: l.PartId,
                Owner: l.Part!.Owner,
                Brand: l.Part!.Brand,
                ItemDesc: l.Part!.ItemDesc,
                ImageUrl: l.Part!.ImageUrl,
                LotNumber: l.LotNumber,
                ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
                Qty: l.QtyUnloaded,
                Condition: "NORMAL"
            )).ToList();

            var confirmedPartIds = existing.UnloadLines
                .Where(l => l.Status == "CONFIRMED")
                .Select(l => l.PartId)
                .ToList();

            return Ok(new OpenUnloadResponse(
                SessionId: existing.SessionId,
                PalletId: req.PalletId,
                Status: existing.Status,
                Items: existingItems,
                ConfirmedPartIds: confirmedPartIds
            ));
        }

        // อนุญาต FG, PW, IN_TRANSIT (มาจาก ASRS)
        if (pallet.Status is not ("FG" or "PW" or "IN_TRANSIT"))
            return BadRequest(new ApiError(
                $"Pallet must be FG / PW / IN_TRANSIT to unload (current: {pallet.Status})."));

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        // ดึงสินค้าบน Pallet
        var receiptLines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId
                     && l.Status == "PALLETIZED")
            .ToListAsync();

        if (receiptLines.Count == 0)
            return BadRequest(new ApiError($"No items on pallet '{req.PalletId}'."));

        // สร้าง UnloadSession
        var session = new UnloadSession
        {
            PalletId = req.PalletId,
            OperatorId = req.OperatorId,
            Status = "STEP1",
            CreatedAt = DateTime.UtcNow
        };

        db.UnloadSessions.Add(session);
        await db.SaveChangesAsync();

        // Group ReceiptLines by PartId (Part เดียวกันอาจมีหลาย ReceiptLine ต่าง Lot)
        var grouped = receiptLines
            .GroupBy(rl => rl.PartId)
            .ToList();

        var itemsList = new List<UnloadItemResponse>();

        foreach (var g in grouped)
        {
            var partId = g.Key;
            var firstLine = g.First();
            var totalOnPallet = g.Sum(rl => rl.QtyReceived);

            // คำนวณจำนวนที่เคย unload ไปแล้วจาก session ก่อนหน้า
            var alreadyUnloaded = await db.UnloadLines
                .Where(l => l.PalletId == req.PalletId
                          && l.PartId == partId
                          && (l.Status == "CONFIRMED" || l.Status == "LOADED" || l.Status == "RETURNED"))
                .SumAsync(l => (int?)l.QtyUnloaded) ?? 0;

            var remaining = totalOnPallet - alreadyUnloaded;
            if (remaining <= 0) continue; // ไม่มีของเหลือให้ unload

            db.UnloadLines.Add(new UnloadLine
            {
                SessionId = session.SessionId,
                PalletId = req.PalletId,
                PartId = partId,
                LotNumber = firstLine.LotNumber,
                ExpiredDate = firstLine.ExpiredDate,
                QtyUnloaded = remaining,
                Status = "PENDING",
                OperatorId = req.OperatorId
            });

            itemsList.Add(new UnloadItemResponse(
                PartId: partId,
                Owner: firstLine.Part!.Owner,
                Brand: firstLine.Part!.Brand,
                ItemDesc: firstLine.Part!.ItemDesc,
                ImageUrl: firstLine.Part!.ImageUrl,
                LotNumber: firstLine.LotNumber,
                ExpiredDate: firstLine.ExpiredDate?.ToString("yyyy-MM-dd"),
                Qty: remaining,
                Condition: firstLine.Condition
            ));
        }

        if (itemsList.Count == 0)
            return BadRequest(new ApiError($"No remaining items to unload on pallet '{req.PalletId}'."));

        pallet.Status = "UNLOADING";
        pallet.Location = "UNLOAD";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var items = itemsList;

        return Ok(new OpenUnloadResponse(
            SessionId: session.SessionId,
            PalletId: req.PalletId,
            Status: session.Status,
            Items: items,
            ConfirmedPartIds: []
        ));
    }

    // =============================================
    // POST /api/unload/confirm-unload
    // Step 1: Confirm นำสินค้าออกจาก Pallet
    // =============================================
    [HttpPost("confirm-unload")]
    public async Task<IActionResult> ConfirmUnload([FromBody] ConfirmUnloadRequest req)
    {
        var session = await db.UnloadSessions
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId
                                   && s.Status == "STEP1");

        if (session is null)
            return BadRequest(new ApiError("Invalid session or not in STEP1."));

        // หา PENDING line ของ Part นี้ (อาจมีหลายรอบ → เอาตัวแรก)
        var line = await db.UnloadLines
            .FirstOrDefaultAsync(l => l.SessionId == req.SessionId
                                   && l.PartId == req.PartId
                                   && l.Status == "PENDING");

        if (line is null)
        {
            // เช็คว่า confirm หมดแล้วหรือเปล่า
            var hasConfirmed = await db.UnloadLines
                .AnyAsync(l => l.SessionId == req.SessionId
                            && l.PartId == req.PartId
                            && l.Status == "CONFIRMED");
            return hasConfirmed
                ? BadRequest(new ApiError($"Part '{req.PartId}' ไม่มีของเหลือให้ unload แล้ว"))
                : NotFound(new ApiError($"Part '{req.PartId}' not found in session."));
        }

        // ถ้า client ส่ง QtyUnloaded มา → ใช้ค่านั้น (ต้อง > 0 และ <= ค่าเดิม)
        var originalQty = line.QtyUnloaded;
        if (req.QtyUnloaded.HasValue)
        {
            if (req.QtyUnloaded.Value <= 0)
                return BadRequest(new ApiError("จำนวนต้องมากกว่า 0"));
            if (req.QtyUnloaded.Value > line.QtyUnloaded)
                return BadRequest(new ApiError($"จำนวนเกินที่มีบน Pallet ({line.QtyUnloaded})"));
            line.QtyUnloaded = req.QtyUnloaded.Value;
        }

        line.Status = "CONFIRMED";
        line.ConfirmedAt = DateTime.UtcNow;
        line.UpdatedAt = DateTime.UtcNow;

        // ── Partial unload → สร้าง PENDING line ใหม่สำหรับส่วนที่เหลือ ──
        var remainder = originalQty - line.QtyUnloaded;
        if (remainder > 0)
        {
            db.UnloadLines.Add(new UnloadLine
            {
                SessionId = req.SessionId,
                PalletId = line.PalletId,
                PartId = req.PartId,
                LotNumber = line.LotNumber,
                ExpiredDate = line.ExpiredDate,
                QtyUnloaded = remainder,
                Status = "PENDING",
                OperatorId = line.OperatorId
            });
        }

        // อัปเดต ReceiptLine — ไม่แก้ QtyReceived เด็ดขาด
        // เช็คจาก UnloadLines ว่า unload รวมครบ QtyReceived หรือยัง
        var receiptLines = await db.ReceiptLines
            .Where(r => r.PalletId == line.PalletId
                     && r.PartId == req.PartId
                     && r.Status == "PALLETIZED")
            .ToListAsync();

        var totalQtyOnPallet = receiptLines.Sum(r => r.QtyReceived);

        if (receiptLines.Count > 0)
        {
            // รวม qty ที่ unload ไปแล้วจาก DB (ไม่รวม line ปัจจุบันที่ยังไม่ save)
            var previouslyUnloaded = await db.UnloadLines
                .Where(l => l.PalletId == line.PalletId
                          && l.PartId == req.PartId
                          && l.LineId != line.LineId
                          && (l.Status == "CONFIRMED" || l.Status == "LOADED" || l.Status == "RETURNED"))
                .SumAsync(l => (int?)l.QtyUnloaded) ?? 0;

            // บวก line ปัจจุบันที่เพิ่ง confirm (ยังไม่ save ลง DB)
            var totalUnloaded = previouslyUnloaded + line.QtyUnloaded;

            if (totalUnloaded >= totalQtyOnPallet)
            {
                foreach (var rl in receiptLines)
                {
                    rl.Status = "UNLOADED";
                    rl.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync();

        // นับว่าครบไหม (ไม่นับ PENDING ที่เหลือ)
        var allLines = await db.UnloadLines
            .Where(l => l.SessionId == req.SessionId).ToListAsync();
        var pendingCount = allLines.Count(l => l.Status == "PENDING");
        var confirmedCount = allLines.Count(l => l.Status == "CONFIRMED");
        var totalCount = allLines.Count;
        var allConfirmed = pendingCount == 0;

        // ครบ → เปลี่ยนเป็น STEP2 อัตโนมัติ
        if (allConfirmed)
        {
            session.Status = "STEP2";
            session.Step1DoneAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok(new ConfirmUnloadResponse(
            Success: true,
            Message: allConfirmed
                            ? "✅ All confirmed. Proceed to Step 2."
                            : $"Confirmed {confirmedCount}/{totalCount}.",
            ConfirmedCount: confirmedCount,
            TotalCount: totalCount,
            AllConfirmed: allConfirmed
        ));
    }

    // =============================================
    // GET /api/unload/scan-basket/{basketId}
    // สแกน Basket
    // =============================================
    [HttpGet("scan-basket/{basketId}")]
    public async Task<IActionResult> ScanBasket(string basketId)
    {
        var basket = await db.Baskets.FindAsync(basketId);
        if (basket is null)
            return NotFound(new ApiError($"Basket '{basketId}' not found."));

        if (basket.Status == "DONE")
            return BadRequest(new ApiError($"Basket '{basketId}' is already done."));

        // เช็ค BasketLines ด้วยว่ามีสินค้าค้างอยู่หรือเปล่า
        var hasItems = await db.BasketLines
            .AnyAsync(b => b.BasketId == basketId);

        if (hasItems)
        {
            // ซ่อม status ให้ตรงความจริง
            basket.Status = "IN_USE";
            basket.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return BadRequest(new ApiError($"Basket '{basketId}' มีสินค้าอยู่แล้ว"));
        }

        return Ok(new ScanBasketResponse(
            BasketId: basket.BasketId,
            Label: basket.Label,
            Zone: basket.Zone,
            Destination: basket.Destination,
            Status: basket.Status,
            Message: $"✅ Basket ready → {basket.Destination ?? "No destination"}"
        ));
    }

    // =============================================
    // POST /api/unload/load-to-basket
    // Step 2: Map สินค้าเข้าตะกร้า
    // =============================================
    [HttpPost("load-to-basket")]
    public async Task<IActionResult> LoadToBasket([FromBody] LoadToBasketRequest req)
    {
        var session = await db.UnloadSessions
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId
                                   && s.Status == "STEP2");

        if (session is null)
            return BadRequest(new ApiError("Invalid session or not in STEP2."));

        var basket = await db.Baskets.FindAsync(req.BasketId);
        if (basket is null)
            return NotFound(new ApiError($"Basket '{req.BasketId}' not found."));

        // ตรวจว่า Part นี้ confirm unload แล้ว
        var unloadLine = await db.UnloadLines
            .FirstOrDefaultAsync(l => l.SessionId == req.SessionId
                                   && l.PartId == req.PartId
                                   && l.Status == "CONFIRMED");

        if (unloadLine is null)
            return BadRequest(new ApiError(
                $"Part '{req.PartId}' not confirmed from pallet yet."));

        // ตรวจว่า load ไปแล้วหรือยัง
        var alreadyLoaded = await db.BasketLines
            .AnyAsync(b => b.SessionId == req.SessionId
                        && b.PartId == req.PartId
                        && b.Status == "LOADED");

        if (alreadyLoaded)
            return BadRequest(new ApiError($"Part '{req.PartId}' already loaded."));

        // สร้าง BasketLine
        db.BasketLines.Add(new BasketLine
        {
            SessionId = req.SessionId,
            BasketId = req.BasketId,
            PartId = req.PartId,
            PalletId = req.PalletId,
            LotNumber = unloadLine.LotNumber,
            ExpiredDate = unloadLine.ExpiredDate,
            QtyLoaded = unloadLine.QtyUnloaded,
            Status = "LOADED",
            OperatorId = req.OperatorId
        });

        if (basket.Status == "AVAILABLE")
        {
            basket.Status = "IN_USE";
            basket.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // นับว่าครบไหม
        var totalUnload = await db.UnloadLines
            .CountAsync(l => l.SessionId == req.SessionId
                          && l.Status == "CONFIRMED");
        var totalLoaded = await db.BasketLines
            .CountAsync(b => b.SessionId == req.SessionId
                          && b.Status == "LOADED");
        var allLoaded = totalLoaded >= totalUnload;

        // ครบ → COMPLETED
        if (allLoaded)
        {
            session.Status = "COMPLETED";
            session.CompletedAt = DateTime.UtcNow;

            // Pallet → AVAILABLE พร้อมใช้ใหม่
            var pallet = await db.Pallets.FindAsync(req.PalletId);
            if (pallet is not null)
            {
                pallet.Type = null;
                pallet.Status = "AVAILABLE";
                pallet.Location = "ASRS";  // ส่งกลับ ASRS หลัง unload ครบ
                pallet.UpdatedAt = DateTime.UtcNow;
            }

            basket.Status = "DONE";
            basket.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }

        return Ok(new LoadToBasketResponse(
            Success: true,
            Message: allLoaded
                        ? "✅ All loaded. Pallet is now AVAILABLE."
                        : $"Loaded {totalLoaded}/{totalUnload}.",
            LoadedCount: totalLoaded,
            TotalCount: totalUnload,
            AllLoaded: allLoaded
        ));
    }

    // =============================================
    // POST /api/unload/return-pallet-to-asis
    // คืน Pallet ให้ AGV รับกลับ ASRS
    // =============================================
    [HttpPost("return-pallet-to-asis")]
    public async Task<IActionResult> ReturnPalletToAsis(
        [FromBody] ReturnPalletToAsisRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        // ปิด UnloadSession ที่ยังเปิดอยู่
        if (req.SessionId.HasValue)
        {
            var session = await db.UnloadSessions
                .FirstOrDefaultAsync(s => s.SessionId == req.SessionId.Value
                                       && (s.Status == "STEP1" || s.Status == "STEP2"));
            if (session is not null)
            {
                // Cancel UnloadLines ที่ยัง PENDING (ยังอยู่บน pallet)
                var pendingLines = await db.UnloadLines
                    .Where(l => l.SessionId == req.SessionId.Value
                             && l.Status == "PENDING")
                    .ToListAsync();
                foreach (var l in pendingLines)
                {
                    l.Status = "CANCELLED";
                    l.UpdatedAt = DateTime.UtcNow;
                }

                session.Status = "COMPLETED";
                session.CompletedAt = DateTime.UtcNow;
            }
        }

        // นับว่ายังมีของบน pallet ไหม
        var remainingLines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
            .ToListAsync();

        if (remainingLines.Count > 0)
        {
            // ยังมีของเหลือ → set Type ตาม condition ของสินค้าที่ยังอยู่
            var condition = remainingLines.First().Condition; // FG หรือ PW
            pallet.Type = condition;
            pallet.Status = "IN_TRANSIT";
            pallet.Location = "ASRS";
        }
        else
        {
            // ไม่มีของเหลือ → ว่างเปล่า พร้อมใช้ใหม่
            pallet.Type = null;
            pallet.Status = "AVAILABLE";
            pallet.Location = "ASRS";
        }
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            remainingLines.Count > 0
                ? $"✅ Pallet '{req.PalletId}' → AGV กำลังรับกลับ ASRS"
                : $"✅ Pallet '{req.PalletId}' ว่างแล้ว"));
    }

    // asis-dispatch ย้ายไป SimulationController แล้ว
    // ใช้ POST /api/simulate/asrs/retrieve-pallet แทน

    // =============================================
    // GET /api/unload/confirmed-items
    // ดึงสินค้าทั้งหมดที่ unload แล้ว รอ load basket
    // =============================================
    [HttpGet("confirmed-items")]
    public async Task<IActionResult> GetConfirmedItems()
    {
        var lines = await db.UnloadLines
            .Include(l => l.Part)
            .Where(l => l.Status == "CONFIRMED")
            .OrderBy(l => l.PartId)
            .ToListAsync();

        // Group by PartId — ของอยู่นอก pallet แล้ว ไม่ต้องแยกตาม line
        var grouped = lines
            .GroupBy(l => l.PartId)
            .Select(g => new GroupedConfirmedItemResponse(
                PartId: g.Key,
                Owner: g.First().Part!.Owner,
                Brand: g.First().Part!.Brand,
                ItemDesc: g.First().Part!.ItemDesc,
                ImageUrl: g.First().Part!.ImageUrl,
                LotNumber: string.Join(", ", g.Select(l => l.LotNumber).Where(l => l != null).Distinct()),
                TotalQty: g.Sum(l => l.QtyUnloaded)
            ))
            .ToList();

        return Ok(grouped);
    }

    // =============================================
    // POST /api/unload/load-basket
    // load สินค้าเข้า basket — รับ PartId + Qty
    // consume UnloadLines แบบ FIFO (oldest first)
    // =============================================
    [HttpPost("load-basket")]
    public async Task<IActionResult> LoadBasket(
        [FromBody] LoadBasketRequest req)
    {
        if (req.Qty <= 0)
            return BadRequest(new ApiError("จำนวนต้องมากกว่า 0"));

        // ── ตรวจ Basket ───────────────────────
        var basket = await db.Baskets.FindAsync(req.BasketId);
        if (basket is null)
            return NotFound(new ApiError($"Basket '{req.BasketId}' not found."));

        if (basket.Status != "AVAILABLE")
            return BadRequest(new ApiError(
                $"Basket '{req.BasketId}' ไม่ว่าง สถานะ: {basket.Status}"));

        var existingBL = await db.BasketLines
            .AnyAsync(b => b.BasketId == req.BasketId && b.Status == "LOADED");
        if (existingBL)
            return BadRequest(new ApiError(
                $"Basket '{req.BasketId}' มีสินค้าอยู่แล้ว"));

        // ── ตรวจ Operator ─────────────────────
        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        // ── หา CONFIRMED UnloadLines สำหรับ Part นี้ (FIFO) ──
        var confirmedLines = await db.UnloadLines
            .Where(l => l.PartId == req.PartId && l.Status == "CONFIRMED")
            .OrderBy(l => l.LineId)
            .ToListAsync();

        var totalAvailable = confirmedLines.Sum(l => l.QtyUnloaded);
        if (req.Qty > totalAvailable)
            return BadRequest(new ApiError(
                $"จำนวนเกินที่มี ({totalAvailable} ชิ้น)"));

        // ── Consume FIFO ──────────────────────
        int remaining = req.Qty;
        int? firstSessionId = null;
        string? firstPalletId = null;

        foreach (var line in confirmedLines)
        {
            if (remaining <= 0) break;

            firstSessionId ??= line.SessionId;
            firstPalletId ??= line.PalletId;

            if (line.QtyUnloaded <= remaining)
            {
                // ใช้หมด line นี้
                remaining -= line.QtyUnloaded;
                line.Status = "LOADED";
                line.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // ใช้บางส่วน — ลด qty ที่เหลือ ยังคง CONFIRMED
                line.QtyUnloaded -= remaining;
                line.UpdatedAt = DateTime.UtcNow;
                remaining = 0;
            }
        }

        // ── สร้าง BasketLine ──────────────────
        var basketLine = new BasketLine
        {
            SessionId = firstSessionId!.Value,
            BasketId = req.BasketId,
            PartId = req.PartId,
            PalletId = firstPalletId!,
            QtyLoaded = req.Qty,
            Status = "LOADED",
            LoadedAt = DateTime.UtcNow,
            OperatorId = req.OperatorId,
        };

        db.BasketLines.Add(basketLine);

        // ── Update Basket ─────────────────────
        basket.Status = "IN_USE";
        basket.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // คำนวณ qty ที่เหลือ
        var qtyRemaining = totalAvailable - req.Qty;

        return Ok(new LoadBasketResponse2(
            Success: true,
            BasketId: req.BasketId,
            PartId: req.PartId,
            QtyLoaded: req.Qty,
            QtyRemaining: qtyRemaining,
            Message: $"✅ Load {req.PartId} x{req.Qty} เข้า {req.BasketId} สำเร็จ"
        ));
    }

    // =============================================
    // GET /api/unload/loaded-items
    // ดึงสินค้าที่ load ลง basket แล้ว แต่ยังไม่ได้คืนตะกร้า
    // =============================================
    [HttpGet("loaded-items")]
    public async Task<IActionResult> GetLoadedItems()
    {
        var items = await (
            from bl in db.BasketLines
            join b in db.Baskets on bl.BasketId equals b.BasketId
            join p in db.Parts on bl.PartId equals p.PartId
            where bl.Status == "LOADED"
            orderby bl.PartId
            select new LoadedBasketItemResponse(
                bl.LineId,
                bl.PartId,
                bl.PalletId,
                p.Owner,
                p.ItemDesc,
                p.ImageUrl,
                bl.QtyLoaded,
                null,
                bl.BasketId,
                b.Label,
                b.Destination
            )
        ).ToListAsync();

        return Ok(items);
    }

    // =============================================
    // POST /api/unload/return-basket
    // คืนตะกร้า → Basket กลับเป็น AVAILABLE
    // =============================================
    [HttpPost("return-basket")]
    public async Task<IActionResult> ReturnBasket([FromBody] ReturnBasketRequest req)
    {
        var basket = await db.Baskets.FindAsync(req.BasketId);
        if (basket is null)
            return NotFound(new ApiError($"Basket '{req.BasketId}' not found."));

        if (basket.Status != "IN_USE")
            return BadRequest(new ApiError(
                $"Basket '{req.BasketId}' is not IN_USE (current: {basket.Status})."));

        // Mark BasketLines เป็น RETURNED
        var basketLines = await db.BasketLines
            .Where(bl => bl.BasketId == req.BasketId && bl.Status == "LOADED")
            .ToListAsync();

        foreach (var bl in basketLines)
            bl.Status = "RETURNED";

        // Mark UnloadLines เป็น RETURNED + เก็บ palletIds ที่เกี่ยวข้อง
        var affectedPalletIds = new HashSet<string>();
        foreach (var bl in basketLines)
        {
            var ul = await db.UnloadLines
                .FirstOrDefaultAsync(l => l.SessionId == bl.SessionId
                                       && l.PartId == bl.PartId
                                       && l.Status == "LOADED");
            if (ul is not null)
            {
                ul.Status = "RETURNED";
                affectedPalletIds.Add(ul.PalletId);
            }
        }

        basket.Status = "AVAILABLE";
        basket.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ตรวจ Pallet ที่เกี่ยวข้อง — ถ้าไม่มี UnloadLine active เหลือแล้ว → AVAILABLE
        foreach (var palletId in affectedPalletIds)
        {
            var hasActive = await db.UnloadLines
                .AnyAsync(l => l.PalletId == palletId
                            && (l.Status == "PENDING"
                             || l.Status == "CONFIRMED"
                             || l.Status == "LOADED"));

            if (!hasActive)
            {
                var pallet = await db.Pallets.FindAsync(palletId);
                if (pallet is not null && pallet.Status == "UNLOADING")
                {
                    pallet.Type = null;
                    pallet.Status = "AVAILABLE";
                    pallet.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            $"✅ Basket '{req.BasketId}' returned and ready for next use."));
    }
}