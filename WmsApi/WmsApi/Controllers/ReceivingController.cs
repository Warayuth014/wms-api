using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/receiving")]
public class ReceivingController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // GET /api/receiving/po/{poId}
    // สแกน PO ดูรายการสินค้า
    // =============================================
    [HttpGet("po/{poId}")]
    public async Task<IActionResult> GetPO(string poId)
    {
        if (string.IsNullOrWhiteSpace(poId))
            return BadRequest(new ApiError("กรุณาระบุ PO ID"));

        var po = await db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(p => p.POId == poId.Trim().ToUpper());

        if (po is null)
            return NotFound(new ApiError(
                $"ไม่พบ PO '{poId}' ในระบบ",
                "กรุณาตรวจสอบ PO ID อีกครั้ง"));

        return Ok(new POResponse(
            POId: po.POId,
            SupplierId: po.SupplierId,
            SupplierName: po.Supplier!.FullName,
            Status: po.Status,
            CreatedAt: po.CreatedAt,
            Items: po.Items.Select(i => new POItemResponse(
                Id: i.Id,
                PartId: i.PartId,
                Owner: i.Part!.Owner,
                Brand: i.Part!.Brand,
                ItemDesc: i.Part!.ItemDesc,
                ImageUrl: i.Part!.ImageUrl,
                QtyOrdered: i.QtyOrdered,
                QtyReceived: i.QtyReceived,
                QtyRemaining: i.QtyRemaining,
                Status: i.Status,
                Condition: i.Condition,
                LotNumber: i.LotNumber,
                ExpiredDate: i.ExpiredDate?.ToString("yyyy-MM-dd")
            )).ToList()
        ));
    }

    // =============================================
    // GET /api/receiving/active-session/{poId}
    // ดึง session ที่ยังเปิดอยู่ของ PO (พร้อม lines ที่รอผูก pallet)
    // =============================================
    [HttpGet("active-session/{poId}")]
    public async Task<IActionResult> GetActiveSession(string poId)
    {
        if (string.IsNullOrWhiteSpace(poId))
            return BadRequest(new ApiError("กรุณาระบุ PO ID"));

        var session = await db.ReceivingSessions
            .Include(s => s.PurchaseOrder)
                .ThenInclude(p => p!.Supplier)
            .Include(s => s.PurchaseOrder)
                .ThenInclude(p => p!.Items)
                    .ThenInclude(i => i.Part)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Part)
            .FirstOrDefaultAsync(s => s.POId == poId && s.Status == "OPEN");

        if (session is null)
            return NotFound(new ApiError($"ไม่พบ Session ที่เปิดอยู่สำหรับ PO '{poId}'"));

        var po = session.PurchaseOrder!;
        var poItemsDict = po.Items.ToDictionary(i => i.PartId);

        var pendingItems = po.Items
            .Where(i => i.Status != "RECEIVED")
            .Select(i => new POItemResponse(
                Id: i.Id,
                PartId: i.PartId,
                Owner: i.Part!.Owner,
                Brand: i.Part!.Brand,
                ItemDesc: i.Part!.ItemDesc,
                ImageUrl: i.Part!.ImageUrl,
                QtyOrdered: i.QtyOrdered,
                QtyReceived: i.QtyReceived,
                QtyRemaining: i.QtyRemaining,
                Status: i.Status,
                Condition: i.Condition,
                LotNumber: i.LotNumber,
                ExpiredDate: i.ExpiredDate?.ToString("yyyy-MM-dd")
            )).ToList();

        var pendingLines = session.Lines
            .Where(l => l.Status == "PENDING")
            .Select(l =>
            {
                var poItem = poItemsDict.GetValueOrDefault(l.PartId);
                return new ScanReceiptPartResponse(
                    LineId: l.LineId,
                    PartId: l.PartId,
                    Owner: l.Part!.Owner,
                    Brand: l.Part!.Brand,
                    ItemDesc: l.Part!.ItemDesc,
                    ImageUrl: l.Part!.ImageUrl,
                    QtyOrdered: poItem?.QtyOrdered ?? 0,
                    QtyReceived: l.QtyReceived,
                    QtyRemaining: poItem?.QtyRemaining ?? 0,
                    Condition: l.Condition,
                    LotNumber: l.LotNumber,
                    POItemStatus: poItem?.Status ?? "PENDING",
                    Message: "Resumed"
                );
            }).ToList();

        return Ok(new ActiveReceivingSessionResponse(
            SessionId: session.SessionId,
            POId: po.POId,
            SupplierName: po.Supplier!.FullName,
            Status: session.Status,
            PendingItems: pendingItems,
            PendingLines: pendingLines
        ));
    }

    // =============================================
    // POST /api/receiving/open-session
    // เปิด session รับของ
    // =============================================
    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenReceivingRequest req)
    {
        // ── Validate request ──────────────────────
        if (string.IsNullOrWhiteSpace(req.POId))
            return BadRequest(new ApiError("กรุณาระบุ PO ID"));

        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        // ── ตรวจ PO ──────────────────────────────
        var po = await db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(p => p.POId == req.POId);

        if (po is null)
            return NotFound(new ApiError(
                $"ไม่พบ PO '{req.POId}' ในระบบ",
                "กรุณาตรวจสอบ PO ID อีกครั้ง"));

        if (po.Status == "RECEIVED")
            return BadRequest(new ApiError(
                $"PO '{req.POId}' รับสินค้าครบแล้ว ไม่สามารถเปิด Session ใหม่ได้"));

        // ── ตรวจ Operator ─────────────────────────
        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError(
                $"ไม่พบผู้ใช้ '{req.OperatorId}' ในระบบ",
                "กรุณาล็อกอินใหม่อีกครั้ง"));

        if (!operator_.IsActive)
            return BadRequest(new ApiError(
                $"ผู้ใช้ '{req.OperatorId}' ถูกระงับการใช้งาน"));

        // ── ตรวจ Session ที่ยังเปิดอยู่ ──────────
        var existingOpen = await db.ReceivingSessions
            .FirstOrDefaultAsync(s => s.POId == req.POId && s.Status == "OPEN");

        if (existingOpen is not null)
            return BadRequest(new ApiError(
                $"PO '{req.POId}' มี Session {existingOpen.SessionId} เปิดอยู่แล้ว",
                "กรุณาเลือก 'ทำต่อจาก Session เดิม' หรือปิด Session นั้นก่อน"));

        // ── สร้าง Session ─────────────────────────
        var session = new ReceivingSession
        {
            POId = req.POId,
            OperatorId = req.OperatorId,
            Status = "OPEN",
            OpenedAt = DateTime.UtcNow
        };

        db.ReceivingSessions.Add(session);

        po.Status = "RECEIVING";
        po.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var pendingItems = po.Items
            .Where(i => i.Status != "RECEIVED")
            .Select(i => new POItemResponse(
                Id: i.Id,
                PartId: i.PartId,
                Owner: i.Part!.Owner,
                Brand: i.Part!.Brand,
                ItemDesc: i.Part!.ItemDesc,
                ImageUrl: i.Part!.ImageUrl,
                QtyOrdered: i.QtyOrdered,
                QtyReceived: i.QtyReceived,
                QtyRemaining: i.QtyRemaining,
                Status: i.Status,
                Condition: i.Condition,
                LotNumber: i.LotNumber,
                ExpiredDate: i.ExpiredDate?.ToString("yyyy-MM-dd")
            )).ToList();

        return Ok(new OpenReceivingResponse(
            SessionId: session.SessionId,
            POId: po.POId,
            SupplierName: po.Supplier!.FullName,
            Status: session.Status,
            PendingItems: pendingItems
        ));
    }

    // =============================================
    // POST /api/receiving/scan-part
    // สแกน Part + บันทึก qty/lot/expired
    // =============================================
    [HttpPost("scan-part")]
    public async Task<IActionResult> ScanPart([FromBody] ScanReceiptPartRequest req)
    {
        // ── Validate request ──────────────────────
        if (string.IsNullOrWhiteSpace(req.PartId))
            return BadRequest(new ApiError("กรุณาระบุ Part ID"));

        if (string.IsNullOrWhiteSpace(req.POId))
            return BadRequest(new ApiError("กรุณาระบุ PO ID"));

        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        if (req.QtyReceived <= 0)
            return BadRequest(new ApiError(
                $"จำนวนที่รับ ({req.QtyReceived}) ต้องมากกว่า 0"));

        // ── ตรวจ Session ──────────────────────────
        var session = await db.ReceivingSessions.FindAsync(req.SessionId);
        if (session is null)
            return BadRequest(new ApiError(
                $"ไม่พบ Session ID {req.SessionId}"));

        if (session.Status != "OPEN")
            return BadRequest(new ApiError(
                $"Session {req.SessionId} ถูกปิดแล้ว ไม่สามารถสแกน Part ได้"));

        // ── ตรวจว่า Part อยู่ใน PO ──────────────
        var poItem = await db.POItems
            .Include(i => i.Part)
            .FirstOrDefaultAsync(i => i.POId == req.POId && i.PartId == req.PartId);

        if (poItem is null)
            return BadRequest(new ApiError(
                $"Part '{req.PartId}' ไม่อยู่ใน PO '{req.POId}'",
                "กรุณาตรวจสอบว่าสแกนสินค้าถูกชิ้น"));

        // ── ตรวจสแกนซ้ำใน Session ──────────────────
        // ถ้ามี PENDING อยู่ (ยังไม่ผูก Pallet) → ต้องจัดการอันเดิมก่อน
        // ถ้า PALLETIZED หมดแล้ว → สแกนเพิ่มได้ (เอา Pallet อื่นมารับ)
        var existingPending = await db.ReceiptLines
            .FirstOrDefaultAsync(l => l.SessionId == req.SessionId
                                   && l.PartId == req.PartId
                                   && l.Status == "PENDING");

        if (existingPending is not null)
            return BadRequest(new ApiError(
                $"Part '{req.PartId}' มีรายการ PENDING อยู่แล้ว",
                "กรุณาผูก Pallet ให้รายการเดิมก่อน หรือยกเลิกรายการเดิม"));

        // ── คำนวณ qty ────────────────────────────
        var newTotal = poItem.QtyReceived + req.QtyReceived;
        var remaining = Math.Max(0, poItem.QtyOrdered - newTotal);
        var isOver = newTotal > poItem.QtyOrdered;
        var poStatus = newTotal >= poItem.QtyOrdered ? "RECEIVED"
                      : newTotal > 0 ? "PARTIAL"
                                     : "PENDING";
        var message = isOver
            ? $"⚠️ รับเกิน: สั่ง {poItem.QtyOrdered} ชิ้น รวมรับ {newTotal} ชิ้น"
            : poStatus == "RECEIVED"
            ? $"✅ รับครบแล้ว ({newTotal}/{poItem.QtyOrdered} ชิ้น)"
            : $"รับบางส่วน ({newTotal}/{poItem.QtyOrdered} ชิ้น) ยังขาด {remaining} ชิ้น";

        // ── สร้าง ReceiptLine ──────────────────────
        var line = new ReceiptLine
        {
            SessionId = req.SessionId,
            POId = req.POId,
            PartId = req.PartId,
            QtyReceived = req.QtyReceived,
            Condition = poItem.Condition,
            LotNumber = poItem.LotNumber,
            ExpiredDate = poItem.ExpiredDate,
            Status = "PENDING",
            OperatorId = req.OperatorId
        };

        db.ReceiptLines.Add(line);

        poItem.QtyReceived = newTotal;
        poItem.QtyRemaining = remaining;
        poItem.Status = isOver ? "OVER" : poStatus;

        await db.SaveChangesAsync();

        return Ok(new ScanReceiptPartResponse(
            LineId: line.LineId,
            PartId: poItem.PartId,
            Owner: poItem.Part!.Owner,
            Brand: poItem.Part!.Brand,
            ItemDesc: poItem.Part!.ItemDesc,
            ImageUrl: poItem.Part!.ImageUrl,
            QtyOrdered: poItem.QtyOrdered,
            QtyReceived: req.QtyReceived,
            QtyRemaining: remaining,
            Condition: poItem.Condition,
            LotNumber: poItem.LotNumber,
            POItemStatus: poItem.Status,
            Message: message
        ));
    }

    // =============================================
    // POST /api/receiving/assign-pallet
    // สแกน Pallet ผูกสินค้า
    // =============================================
    [HttpPost("assign-pallet")]
    public async Task<IActionResult> AssignPallet([FromBody] AssignPalletRequest req)
    {
        // ── Validate request ──────────────────────
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return BadRequest(new ApiError("กรุณาระบุ Pallet ID"));

        if (req.LineIds.Count == 0)
            return BadRequest(new ApiError("ต้องระบุ Line ID อย่างน้อย 1 รายการ"));

        if (req.PalletType is not ("FG" or "PW"))
            return BadRequest(new ApiError(
                $"ประเภท Pallet '{req.PalletType}' ไม่ถูกต้อง (ต้องเป็น FG หรือ PW)"));

        // ── ตรวจ Session ──────────────────────────
        var session = await db.ReceivingSessions.FindAsync(req.SessionId);
        if (session is null)
            return BadRequest(new ApiError($"ไม่พบ Session ID {req.SessionId}"));

        if (session.Status != "OPEN")
            return BadRequest(new ApiError(
                $"Session {req.SessionId} ถูกปิดแล้ว ไม่สามารถผูก Pallet ได้"));

        // ── ตรวจ Pallet ──────────────────────────
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError(
                $"ไม่พบ Pallet '{req.PalletId}' ในระบบ",
                "กรุณาตรวจสอบ Pallet ID อีกครั้ง"));

        // Pallet ต้องอยู่ใน status ที่ผูกได้ (AVAILABLE, FG, PW)
        if (pallet.Status == "AVAILABLE")
        {
            // Pallet ว่าง → ถ้ามี Type กำหนดไว้แล้วต้องตรงกัน
            if (pallet.Type != null && pallet.Type != req.PalletType)
                return BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' ถูกกำหนดไว้สำหรับสินค้าประเภท {pallet.Type} " +
                    $"ไม่สามารถใส่สินค้าประเภท {req.PalletType} ได้",
                    $"กรุณาใช้ Pallet ประเภท {req.PalletType} หรือ Pallet ที่ไม่มี Type กำหนด"));

            pallet.Type = req.PalletType;
            pallet.Status = req.PalletType;
        }
        else if (pallet.Status is "FG" or "PW")
        {
            // Pallet มีสินค้าอยู่แล้ว → type ต้องตรงกัน
            if (pallet.Type != req.PalletType)
                return BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' เป็นประเภท {pallet.Type} " +
                    $"ไม่สามารถใส่สินค้าประเภท {req.PalletType} ได้",
                    $"ต้องใช้ Pallet ประเภท {req.PalletType} หรือ Pallet ใหม่ที่ยังว่างอยู่"));
        }
        else
        {
            // Pallet อยู่ใน ASRS / PREWORK / IN_TRANSIT / ฯลฯ ใช้ไม่ได้
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' มีสถานะ '{pallet.Status}' ไม่สามารถผูกสินค้าได้",
                "ใช้ได้เฉพาะ Pallet ที่มีสถานะ AVAILABLE, FG หรือ PW เท่านั้น"));
        }

        // ── ดึง Lines ที่ระบุมา ───────────────────
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => req.LineIds.Contains(l.LineId)
                     && l.SessionId == req.SessionId
                     && l.Status == "PENDING")
            .ToListAsync();

        if (lines.Count == 0)
            return BadRequest(new ApiError(
                "ไม่พบรายการที่รอผูก Pallet ตาม Line ID ที่ระบุ",
                "อาจถูกผูกไปแล้ว หรือ Line ID ไม่ตรงกับ Session นี้"));

        // ── ตรวจ Condition ของ Lines ต้องตรงกันทั้งหมด (อ่านจาก Part master) ──
        var distinctConditions = lines.Select(l => l.Condition).Distinct().ToList();
        if (distinctConditions.Count > 1)
            return BadRequest(new ApiError(
                "ไม่สามารถผูกสินค้าที่มี Condition หลายประเภทพร้อมกันได้",
                $"รายการที่เลือกมีทั้ง {string.Join(" และ ", distinctConditions)} กรุณาแยกผูกทีละประเภท"));

        // ── ตรวจ Condition ของ Lines ต้องตรงกับ PalletType ──
        var lineCondition = distinctConditions[0]; // FG หรือ PW จาก Part master
        if (lineCondition != req.PalletType)
            return BadRequest(new ApiError(
                $"Condition ของสินค้า ({lineCondition}) ไม่ตรงกับ Pallet Type ({req.PalletType}) ที่ระบุ",
                "ตรวจสอบว่าสินค้าและ Pallet เป็นประเภทเดียวกัน"));

        // ── ตรวจ Condition ของของที่อยู่ใน Pallet จริง (อ่านจาก Part master) ──
        var existingLinesInPallet = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
            .ToListAsync();

        if (existingLinesInPallet.Count > 0)
        {
            var palletActualCondition = existingLinesInPallet.Select(l => l.Condition).Distinct().First();
            if (lineCondition != palletActualCondition)
                return BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' มีสินค้า Condition '{palletActualCondition}' อยู่แล้ว ไม่สามารถเพิ่มสินค้า Condition '{lineCondition}' ได้",
                    "ของใน Pallet ต้องเป็น Condition เดียวกันทั้งหมด"));

            // ── ตรวจ Owner: สินค้าใน Pallet ต้องเป็นของบริษัทเดียวกัน ──
            var existingOwners = existingLinesInPallet
                .Where(l => l.Part != null)
                .Select(l => l.Part!.Owner)
                .Distinct()
                .ToList();

            var newOwners = lines
                .Where(l => l.Part != null)
                .Select(l => l.Part!.Owner)
                .Distinct()
                .ToList();

            foreach (var newOwner in newOwners)
            {
                if (existingOwners.Count > 0 && !existingOwners.Contains(newOwner))
                    return BadRequest(new ApiError(
                        $"Pallet '{req.PalletId}' มีสินค้าของ '{existingOwners[0]}' อยู่แล้ว ไม่สามารถเพิ่มสินค้าของ '{newOwner}' ได้",
                        "สินค้าใน Pallet ต้องเป็นของบริษัท (Owner) เดียวกันเท่านั้น"));
            }

            // ── ตรวจ Part ซ้ำ: ถ้ามี Part เดียวกันใน Pallet แล้ว ต้องเป็น Batch เดียวกัน ──
            foreach (var line in lines)
            {
                var duplicateInPallet = existingLinesInPallet
                    .Where(l => l.PartId == line.PartId)
                    .ToList();

                if (duplicateInPallet.Count > 0)
                {
                    var existingBatch = duplicateInPallet.First().LotNumber;
                    if (line.LotNumber != existingBatch)
                        return BadRequest(new ApiError(
                            $"Pallet '{req.PalletId}' มีสินค้า '{line.PartId}' Batch '{existingBatch}' อยู่แล้ว ไม่สามารถเพิ่ม Batch '{line.LotNumber}' ได้",
                            "สินค้าชนิดเดียวกันใน Pallet ต้องเป็น Batch เดียวกันเท่านั้น"));
                }
            }
        }

        // ── ผูก Pallet ────────────────────────────
        foreach (var line in lines)
        {
            line.PalletId = req.PalletId;
            line.Status = "PALLETIZED";
            line.UpdatedAt = DateTime.UtcNow;
        }

        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── Auto-close: ตรวจว่ารับครบ + ผูก Pallet หมดแล้วหรือยัง ──
        var autoClosed = false;
        string? poStatus = null;
        string? closeMessage = null;

        var fullSession = await db.ReceivingSessions
            .Include(s => s.PurchaseOrder)
                .ThenInclude(p => p!.Items)
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId);

        if (fullSession is not null)
        {
            var hasPendingLines = fullSession.Lines.Any(l => l.Status == "PENDING");
            var po = fullSession.PurchaseOrder!;
            var allItemsReceived = po.Items.All(i => i.Status is "RECEIVED" or "OVER");

            if (!hasPendingLines && allItemsReceived)
            {
                // ── Finalize QtyRemaining ทุก item ────────
                foreach (var item in po.Items)
                    item.QtyRemaining = Math.Max(0, item.QtyOrdered - item.QtyReceived);

                var hasPartial = po.Items.Any(i => i.Status is "PARTIAL" or "PENDING");
                po.Status = hasPartial ? "PARTIAL" : "RECEIVED";
                po.UpdatedAt = DateTime.UtcNow;

                fullSession.Status = "CLOSED";
                fullSession.ClosedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();

                autoClosed = true;
                poStatus = po.Status;

                var total = po.Items.Count;
                var received = po.Items.Count(i => i.Status is "RECEIVED" or "OVER");
                closeMessage = po.Status == "RECEIVED"
                    ? $"PO '{po.POId}' รับสินค้าครบแล้ว"
                    : $"PO '{po.POId}' รับบางส่วน ({received}/{total} รายการ)";
            }
        }

        return Ok(new AssignPalletResponse(
            Success: true,
            PalletId: pallet.PalletId,
            PalletType: pallet.Type!,
            LinesAssigned: lines.Count,
            PartsAssigned: lines.Select(l => l.PartId).ToList(),
            Message: $"ผูก Pallet '{req.PalletId}' ({req.PalletType}) สำเร็จ {lines.Count} รายการ",
            AutoClosed: autoClosed,
            POStatus: poStatus,
            CloseMessage: closeMessage
        ));
    }

    // =============================================
    // GET /api/receiving/pending-pallet-lines
    // ดึงทุก ReceiptLine ที่ยังไม่ผูก Pallet (Status = PENDING)
    // =============================================
    [HttpGet("pending-pallet-lines")]
    public async Task<IActionResult> GetPendingPalletLines()
    {
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.Status == "PENDING")
            .OrderBy(l => l.ReceivedAt)
            .ToListAsync();

        var result = lines.Select(l => new PendingPalletLineResponse(
            LineId: l.LineId,
            SessionId: l.SessionId,
            POId: l.POId,
            PartId: l.PartId,
            Owner: l.Part!.Owner,
            Brand: l.Part!.Brand,
            ItemDesc: l.Part!.ItemDesc,
            ImageUrl: l.Part!.ImageUrl,
            QtyReceived: l.QtyReceived,
            Condition: l.Condition,
            LotNumber: l.LotNumber,
            ReceivedAt: l.ReceivedAt
        )).ToList();

        return Ok(new PendingPalletLinesResponse(Count: result.Count, Lines: result));
    }

    // =============================================
    // POST /api/receiving/close-session/{sessionId}
    // ปิด session
    // =============================================
    [HttpPost("close-session/{sessionId}")]
    public async Task<IActionResult> CloseSession(int sessionId)
    {
        var session = await db.ReceivingSessions
            .Include(s => s.PurchaseOrder)
                .ThenInclude(p => p!.Items)
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session is null)
            return NotFound(new ApiError(
                $"ไม่พบ Session ID {sessionId}"));

        if (session.Status == "CLOSED")
            return BadRequest(new ApiError(
                $"Session {sessionId} ปิดไปแล้ว"));

        // ── ตรวจ Lines ที่ยังไม่ผูก Pallet ─────────
        var pendingCount = session.Lines.Count(l => l.Status == "PENDING");
        if (pendingCount > 0)
            return BadRequest(new ApiError(
                $"ยังมีสินค้า {pendingCount} รายการที่ยังไม่ได้ผูก Pallet",
                "กรุณาผูก Pallet ให้ครบก่อนปิด Session"));

        var po = session.PurchaseOrder!;
        var items = po.Items.ToList();
        var total = items.Count;
        var received = items.Count(i => i.Status == "RECEIVED" || i.Status == "OVER");
        var hasPartial = items.Any(i => i.Status == "PARTIAL" || i.Status == "PENDING");

        // ── Finalize QtyRemaining ทุก item ────────
        foreach (var item in items)
            item.QtyRemaining = Math.Max(0, item.QtyOrdered - item.QtyReceived);

        po.Status = hasPartial ? "PARTIAL" : "RECEIVED";
        po.UpdatedAt = DateTime.UtcNow;

        session.Status = "CLOSED";
        session.ClosedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── รายการที่รับไม่ครบ ────────────────────
        var partialItems = items
            .Where(i => i.QtyRemaining > 0)
            .Select(i => new PartialItemSummary(
                PartId: i.PartId,
                ItemDesc: i.Part!.ItemDesc,
                QtyOrdered: i.QtyOrdered,
                QtyReceived: i.QtyReceived,
                QtyRemaining: i.QtyRemaining
            )).ToList();

        return Ok(new CloseReceivingResponse(
            Success: true,
            POStatus: po.Status,
            Message: po.Status == "RECEIVED"
                ? $"PO '{po.POId}' รับสินค้าครบแล้ว"
                : $"PO '{po.POId}' รับบางส่วน ({received}/{total} รายการ) ยังขาดอีก {partialItems.Count} ชนิด",
            TotalParts: total,
            ReceivedParts: received,
            PartialItems: partialItems
        ));
    }
}
