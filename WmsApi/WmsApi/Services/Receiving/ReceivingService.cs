using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Receiving;

public class ReceivingService(WmsDbContext db) : IReceivingService
{
    public async Task<ServiceResult> GetPOAsync(string poId)
    {
        if (string.IsNullOrWhiteSpace(poId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ PO ID"));

        var normalizedPoId = poId.Trim().ToUpper();

        var po = await db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(p => p.POId == normalizedPoId);

        if (po is null)
        {
            return ServiceResult.NotFound(new ApiError(
                $"ไม่พบ PO '{poId}' ในระบบ",
                "กรุณาตรวจสอบ PO ID อีกครั้ง"));
        }

        var poItemsDict = po.Items.ToDictionary(i => i.PartId);

        // ── PendingLines: line ที่รับแล้วแต่ยังไม่ผูก pallet (สำหรับ resume) ──
        var pendingLines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.POId == normalizedPoId && l.Status == "PENDING")
            .OrderBy(l => l.ReceivedAt)
            .ToListAsync();

        var pendingLineDtos = pendingLines
            .Select(l => ToScanReceiptPartResponse(l, poItemsDict.GetValueOrDefault(l.PartId), "Resumed"))
            .ToList();

        return ServiceResult.Ok(new POResponse(
            POId: po.POId,
            SupplierId: po.SupplierId,
            SupplierName: po.Supplier!.FullName,
            Status: po.Status,
            CreatedAt: po.CreatedAt,
            Items: po.Items.Select(ToPOItemResponse).ToList(),
            PendingLines: pendingLineDtos
        ));
    }

    public async Task<ServiceResult> ValidateSerialAsync(string partId, string serialNo)
    {
        if (string.IsNullOrWhiteSpace(partId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Part ID"));

        if (string.IsNullOrWhiteSpace(serialNo))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ S/N"));

        var normalizedPartId = partId.Trim().ToUpperInvariant();
        var normalizedSerialNo = serialNo.Trim().ToUpperInvariant();

        var serial = await db.PartSerials
            .FirstOrDefaultAsync(s =>
                s.PartId == normalizedPartId &&
                s.SerialNo == normalizedSerialNo);

        if (serial is null)
        {
            return ServiceResult.NotFound(new ApiError(
                $"ไม่พบ S/N '{normalizedSerialNo}' สำหรับ Part '{normalizedPartId}'",
                "ตรวจสอบว่า Part ID และ S/N ตรงกับสินค้าที่สแกน"));
        }

        if (serial.ReceiptLineId != null || serial.PalletId != null || serial.PackingId != null)
        {
            return ServiceResult.BadRequest(new ApiError(
                $"S/N '{normalizedSerialNo}' ถูกใช้งานแล้ว",
                "กรุณาตรวจสอบสินค้าหรือใช้ S/N ที่ยังไม่ถูกรับเข้าระบบ"));
        }

        return ServiceResult.Ok(new ValidateReceivingSerialResponse(
            PartId: serial.PartId,
            SerialNo: serial.SerialNo,
            Status: serial.Status
        ));
    }

    public async Task<ServiceResult> ScanPartAsync(ScanReceiptPartRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PartId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Part ID"));

        if (string.IsNullOrWhiteSpace(req.POId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ PO ID"));

        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        if (req.QtyReceived <= 0)
        {
            return ServiceResult.BadRequest(new ApiError(
                $"จำนวนที่รับ ({req.QtyReceived}) ต้องมากกว่า 0"));
        }

        var scannedSerials = req.SerialNumbers?
            .Select(s => s?.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)
            .ToList() ?? [];

        if (req.SerialNumbers is { Count: > 0 } &&
            scannedSerials.Count != req.SerialNumbers.Count)
        {
            return ServiceResult.BadRequest(new ApiError("Serial Number ต้องไม่เป็นค่าว่าง"));
        }

        if (scannedSerials.Count > 0 && scannedSerials.Count != req.QtyReceived)
        {
            return ServiceResult.BadRequest(new ApiError(
                $"จำนวน Serial Number ({scannedSerials.Count}) ต้องเท่ากับจำนวนรับ ({req.QtyReceived})"));
        }

        if (scannedSerials.Distinct(StringComparer.OrdinalIgnoreCase).Count() != scannedSerials.Count)
        {
            return ServiceResult.BadRequest(new ApiError("Serial Number ซ้ำในรายการที่สแกน"));
        }

        var po = await db.PurchaseOrders.FindAsync(req.POId);
        if (po is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ PO '{req.POId}' ในระบบ"));

        if (po.Status == "RECEIVED")
        {
            return ServiceResult.BadRequest(new ApiError(
                $"PO '{req.POId}' รับสินค้าครบแล้ว ไม่สามารถสแกนเพิ่มได้"));
        }

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
        {
            return ServiceResult.NotFound(new ApiError(
                $"ไม่พบผู้ใช้ '{req.OperatorId}' ในระบบ"));
        }

        if (!operator_.IsActive)
        {
            return ServiceResult.BadRequest(new ApiError(
                $"ผู้ใช้ '{req.OperatorId}' ถูกระงับการใช้งาน"));
        }

        var poItem = await db.POItems
            .Include(i => i.Part)
            .FirstOrDefaultAsync(i => i.POId == req.POId && i.PartId == req.PartId);

        if (poItem is null)
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Part '{req.PartId}' ไม่อยู่ใน PO '{req.POId}'",
                "กรุณาตรวจสอบว่าสแกนสินค้าถูกชิ้น"));
        }

        var scannedSerialEntities = new List<PartSerial>();
        if (scannedSerials.Count > 0)
        {
            scannedSerialEntities = await db.PartSerials
                .Where(s => s.PartId == req.PartId && scannedSerials.Contains(s.SerialNo))
                .ToListAsync();

            if (scannedSerialEntities.Count != scannedSerials.Count)
            {
                var found = scannedSerialEntities
                    .Select(s => s.SerialNo)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missing = scannedSerials.Where(s => !found.Contains(s)).ToList();
                return ServiceResult.BadRequest(new ApiError(
                    $"ไม่พบ S/N สำหรับ Part '{req.PartId}': {string.Join(", ", missing)}",
                    "ตรวจสอบว่า Part ID และ S/N ตรงกับสินค้าที่สแกน"));
            }

            var usedSerials = scannedSerialEntities
                .Where(s => s.ReceiptLineId != null || s.PalletId != null || s.PackingId != null)
                .Select(s => s.SerialNo)
                .ToList();

            if (usedSerials.Count > 0)
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ถูกใช้งานแล้ว: {string.Join(", ", usedSerials)}",
                    "กรุณาตรวจสอบสินค้าหรือใช้ S/N ที่ยังไม่ถูกรับเข้าระบบ"));
            }
        }

        var existingPending = await db.ReceiptLines
            .FirstOrDefaultAsync(l => l.POId == req.POId
                                   && l.PartId == req.PartId
                                   && l.Status == "PENDING");

        if (existingPending is not null)
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Part '{req.PartId}' มีรายการ PENDING อยู่แล้ว",
                "กรุณาผูก Pallet ให้รายการเดิมก่อน หรือยกเลิกรายการเดิม"));
        }

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

        var line = new ReceiptLine
        {
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

        // เริ่มรับสินค้าครั้งแรก → PO=RECEIVING
        if (po.Status == "OPEN")
        {
            po.Status = "RECEIVING";
            po.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // ── Serial Numbers ──
        if (scannedSerials.Count > 0)
            ApplyScannedSerials(scannedSerialEntities, line.LineId, null);
        else
            await GenerateSerialsAsync(req.PartId, req.QtyReceived, line.LineId, null);
        await db.SaveChangesAsync();

        return ServiceResult.Ok(new ScanReceiptPartResponse(
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

    public async Task<ServiceResult> AssignPalletAsync(AssignPalletRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));

        if (req.LineIds.Count == 0)
            return ServiceResult.BadRequest(new ApiError("ต้องระบุ Line ID อย่างน้อย 1 รายการ"));

        if (req.PalletType is not ("FG" or "PW"))
        {
            return ServiceResult.BadRequest(new ApiError(
                $"ประเภท Pallet '{req.PalletType}' ไม่ถูกต้อง (ต้องเป็น FG หรือ PW)"));
        }

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
        {
            return ServiceResult.NotFound(new ApiError(
                $"ไม่พบ Pallet '{req.PalletId}' ในระบบ",
                "กรุณาตรวจสอบ Pallet ID อีกครั้ง"));
        }

        if (pallet.Status == "AVAILABLE")
        {
            if (pallet.Type != null && pallet.Type != req.PalletType)
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' ถูกกำหนดไว้สำหรับสินค้าประเภท {pallet.Type} " +
                    $"ไม่สามารถใส่สินค้าประเภท {req.PalletType} ได้",
                    $"กรุณาใช้ Pallet ประเภท {req.PalletType} หรือ Pallet ที่ไม่มี Type กำหนด"));
            }

            pallet.Type = req.PalletType;
            pallet.Status = req.PalletType;
        }
        else if (pallet.Status is "FG" or "PW")
        {
            if (pallet.Type != req.PalletType)
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' เป็นประเภท {pallet.Type} " +
                    $"ไม่สามารถใส่สินค้าประเภท {req.PalletType} ได้",
                    $"ต้องใช้ Pallet ประเภท {req.PalletType} หรือ Pallet ใหม่ที่ยังว่างอยู่"));
            }
        }
        else
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' มีสถานะ '{pallet.Status}' ไม่สามารถผูกสินค้าได้",
                "ใช้ได้เฉพาะ Pallet ที่มีสถานะ AVAILABLE, FG หรือ PW เท่านั้น"));
        }

        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => req.LineIds.Contains(l.LineId) && l.Status == "PENDING")
            .ToListAsync();

        if (lines.Count == 0)
        {
            return ServiceResult.BadRequest(new ApiError(
                "ไม่พบรายการที่รอผูก Pallet ตาม Line ID ที่ระบุ",
                "อาจถูกผูกไปแล้ว หรือ Line ID ไม่ถูกต้อง"));
        }

        var distinctConditions = lines.Select(l => l.Condition).Distinct().ToList();
        if (distinctConditions.Count > 1)
        {
            return ServiceResult.BadRequest(new ApiError(
                "ไม่สามารถผูกสินค้าที่มี Condition หลายประเภทพร้อมกันได้",
                $"รายการที่เลือกมีทั้ง {string.Join(" และ ", distinctConditions)} กรุณาแยกผูกทีละประเภท"));
        }

        var lineCondition = distinctConditions[0];
        if (lineCondition != req.PalletType)
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Condition ของสินค้า ({lineCondition}) ไม่ตรงกับ Pallet Type ({req.PalletType}) ที่ระบุ",
                "ตรวจสอบว่าสินค้าและ Pallet เป็นประเภทเดียวกัน"));
        }

        var existingLinesInPallet = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
            .ToListAsync();

        if (existingLinesInPallet.Count > 0)
        {
            var palletActualCondition = existingLinesInPallet.Select(l => l.Condition).Distinct().First();
            if (lineCondition != palletActualCondition)
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' มีสินค้า Condition '{palletActualCondition}' อยู่แล้ว ไม่สามารถเพิ่มสินค้า Condition '{lineCondition}' ได้",
                    "ของใน Pallet ต้องเป็น Condition เดียวกันทั้งหมด"));
            }

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
                {
                    return ServiceResult.BadRequest(new ApiError(
                        $"Pallet '{req.PalletId}' มีสินค้าของ '{existingOwners[0]}' อยู่แล้ว ไม่สามารถเพิ่มสินค้าของ '{newOwner}' ได้",
                        "สินค้าใน Pallet ต้องเป็นของบริษัท (Owner) เดียวกันเท่านั้น"));
                }
            }

            foreach (var line in lines)
            {
                var duplicateInPallet = existingLinesInPallet
                    .Where(l => l.PartId == line.PartId)
                    .ToList();

                if (duplicateInPallet.Count > 0)
                {
                    var existingBatch = duplicateInPallet.First().LotNumber;
                    if (line.LotNumber != existingBatch)
                    {
                        return ServiceResult.BadRequest(new ApiError(
                            $"Pallet '{req.PalletId}' มีสินค้า '{line.PartId}' Batch '{existingBatch}' อยู่แล้ว ไม่สามารถเพิ่ม Batch '{line.LotNumber}' ได้",
                            "สินค้าชนิดเดียวกันใน Pallet ต้องเป็น Batch เดียวกันเท่านั้น"));
                    }
                }
            }
        }

        foreach (var line in lines)
        {
            line.PalletId = req.PalletId;
            line.Status = "PALLETIZED";
            line.UpdatedAt = DateTime.UtcNow;
        }

        // ── Update serial PalletId for these lines ──
        var lineIds = lines.Select(l => (int?)l.LineId).ToList();
        var serialsToUpdate = await db.PartSerials
            .Where(s => s.ReceiptLineId != null && lineIds.Contains(s.ReceiptLineId))
            .ToListAsync();
        var nowTs = DateTime.UtcNow;
        foreach (var s in serialsToUpdate)
        {
            s.PalletId = req.PalletId;
            s.UpdatedAt = nowTs;
        }

        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── Auto-close: ถ้า PO นี้รับครบ + ไม่มี PENDING line เหลือ → PO=RECEIVED ──
        var autoClosed = false;
        string? poStatus = null;
        string? closeMessage = null;

        var distinctPoIds = lines.Select(l => l.POId).Distinct().ToList();
        foreach (var poId in distinctPoIds)
        {
            var po = await db.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.POId == poId);

            if (po is null) continue;

            var hasPendingLines = await db.ReceiptLines
                .AnyAsync(l => l.POId == poId && l.Status == "PENDING");
            var allItemsReceived = po.Items.All(i => i.Status is "RECEIVED" or "OVER");

            if (!hasPendingLines && allItemsReceived)
            {
                foreach (var item in po.Items)
                    item.QtyRemaining = Math.Max(0, item.QtyOrdered - item.QtyReceived);

                var hasPartial = po.Items.Any(i => i.Status is "PARTIAL" or "PENDING");
                po.Status = hasPartial ? "PARTIAL" : "RECEIVED";
                po.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();

                autoClosed = true;
                poStatus = po.Status;

                var total = po.Items.Count;
                var received = po.Items.Count(i => i.Status is "RECEIVED" or "OVER");
                closeMessage = po.Status == "RECEIVED"
                    ? $"PO '{po.POId}' รับสินค้าครบแล้ว"
                    : $"PO '{po.POId}' รับบางส่วน ({received}/{total} รายการ)";

                break; // assign-pallet ปัจจุบันรองรับ line จาก PO เดียว (constraint Owner/condition กรองไว้แล้ว)
            }
        }

        return ServiceResult.Ok(new AssignPalletResponse(
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

    public async Task<ServiceResult> GetPendingPalletLinesAsync()
    {
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.Status == "PENDING")
            .OrderBy(l => l.ReceivedAt)
            .ToListAsync();

        var result = lines.Select(l => new PendingPalletLineResponse(
            LineId: l.LineId,
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

        return ServiceResult.Ok(new PendingPalletLinesResponse(Count: result.Count, Lines: result));
    }

    private static POItemResponse ToPOItemResponse(POItem item) =>
        new(
            Id: item.Id,
            PartId: item.PartId,
            Owner: item.Part!.Owner,
            Brand: item.Part!.Brand,
            ItemDesc: item.Part!.ItemDesc,
            ImageUrl: item.Part!.ImageUrl,
            QtyOrdered: item.QtyOrdered,
            QtyReceived: item.QtyReceived,
            QtyRemaining: item.QtyRemaining,
            Status: item.Status,
            Condition: item.Condition,
            LotNumber: item.LotNumber,
            ExpiredDate: item.ExpiredDate?.ToString("yyyy-MM-dd")
        );

    private static ScanReceiptPartResponse ToScanReceiptPartResponse(
        ReceiptLine line,
        POItem? poItem,
        string message) =>
        new(
            LineId: line.LineId,
            PartId: line.PartId,
            Owner: line.Part!.Owner,
            Brand: line.Part!.Brand,
            ItemDesc: line.Part!.ItemDesc,
            ImageUrl: line.Part!.ImageUrl,
            QtyOrdered: poItem?.QtyOrdered ?? 0,
            QtyReceived: line.QtyReceived,
            QtyRemaining: poItem?.QtyRemaining ?? 0,
            Condition: line.Condition,
            LotNumber: line.LotNumber,
            POItemStatus: poItem?.Status ?? "PENDING",
            Message: message
        );

    private static void ApplyScannedSerials(
        IReadOnlyCollection<PartSerial> serials,
        int? receiptLineId,
        string? palletId)
    {
        if (serials.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var serial in serials)
        {
            serial.ReceiptLineId = receiptLineId;
            serial.PalletId = palletId;
            serial.Status = "STORED";
            serial.UpdatedAt = now;
        }
    }

    // ── Generate N serial numbers for a part ──
    private async Task GenerateSerialsAsync(string partId, int qty, int? receiptLineId, string? palletId)
    {
        if (qty <= 0) return;

        var lastSeq = await db.PartSerials
            .Where(s => s.PartId == partId)
            .CountAsync();

        var now = DateTime.UtcNow;
        for (int i = 1; i <= qty; i++)
        {
            db.PartSerials.Add(new PartSerial
            {
                PartId = partId,
                SerialNo = $"SN-{partId}-{lastSeq + i:D6}",
                ReceiptLineId = receiptLineId,
                PalletId = palletId,
                Status = "STORED",
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }
}
