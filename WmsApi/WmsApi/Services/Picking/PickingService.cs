using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Picking;

public class PickingService(WmsDbContext db) : IPickingService
{
    public async Task<ServiceResult> GetPickOrdersAsync()
    {
        var orders = await db.PickOrders
            .Include(o => o.Details).ThenInclude(d => d.Part)
            .Include(o => o.Details).ThenInclude(d => d.Subs).ThenInclude(s => s.ReceiptLine)
            .Where(o => o.Status == "OPEN")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(BuildPickOrderResponse).ToList();
        return ServiceResult.Ok(result);
    }

    public async Task<ServiceResult> GetPickOrderAsync(string pickOrderId)
    {
        var order = await db.PickOrders
            .Include(o => o.Details).ThenInclude(d => d.Part)
            .Include(o => o.Details).ThenInclude(d => d.Subs).ThenInclude(s => s.ReceiptLine)
            .FirstOrDefaultAsync(o => o.PickOrderId == pickOrderId);

        if (order is null)
            return ServiceResult.NotFound(new ApiError($"Pick Order '{pickOrderId}' not found."));

        return ServiceResult.Ok(BuildPickOrderResponse(order));
    }

    public async Task<ServiceResult> AssignStationAsync(AssignPickStationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pallet '{req.PalletId}'"));

        if (pallet.Status == "PACKED")
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ถูก Pack แล้ว — กรุณาส่งไป ZONE PACK หรือสแกน Pallet อื่น",
                "ต้องการส่ง Pallet กลับหรือไม่?"));

        if (pallet.Status != "AVAILABLE" && pallet.Status != "STORED" && pallet.Status != "PICKING")
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่พร้อมสำหรับ Pick (สถานะ: {pallet.Status})"));

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
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' ไม่มีสินค้าที่ผูกกับ Pick Order ใดๆ"));
        }

        var order = await db.PickOrders
            .FirstOrDefaultAsync(o => o.PickOrderId == pickOrderId && o.Status == "OPEN");

        if (order is null)
            return ServiceResult.BadRequest(new ApiError($"Pick Order '{pickOrderId}' ไม่ถูกต้องหรือปิดแล้ว"));

        var palletSubs = await db.PickOrderSubs
            .Include(s => s.ReceiptLine).ThenInclude(l => l!.Part)
            .Include(s => s.PickOrderDetail)
            .Where(s => s.PickOrderDetail!.PickOrderId == pickOrderId
                     && s.ReceiptLine!.PalletId == req.PalletId
                     && s.Status == "PENDING"
                     && s.ReceiptLine!.QtyReceived > 0)
            .ToListAsync();

        if (palletSubs.Count == 0)
            return ServiceResult.BadRequest(new ApiError($"Pallet '{req.PalletId}' ไม่มีสินค้าสำหรับ Pick Order '{pickOrderId}'"));

        var station = await db.PickStations
            .FirstOrDefaultAsync(s => s.CurrentPalletId == req.PalletId);

        if (station is null)
        {
            station = await db.PickStations
                .FirstOrDefaultAsync(s => s.CurrentPalletId == null);

            if (station is null)
                return ServiceResult.BadRequest(new ApiError("ไม่มี Pick Station ว่าง กรุณารอหรือ clear station ก่อน"));

            station.CurrentPalletId = req.PalletId;
            pallet.Status = "PICKING";
            pallet.Location = station.StationId;
            pallet.UpdatedAt = DateTime.UtcNow;

            foreach (var sub in palletSubs.Where(s => s.ReceiptLine!.Status == "PALLETIZED"))
                sub.ReceiptLine!.Status = "PICKING";

            await db.SaveChangesAsync();
        }

        var partIdsOnPallet = palletSubs
            .Select(s => s.ReceiptLine!.PartId)
            .Distinct()
            .ToList();

        var availableSerials = await db.PartSerials
            .Where(s => s.PalletId == req.PalletId
                     && partIdsOnPallet.Contains(s.PartId)
                     && s.Status == "STORED")
            .OrderBy(s => s.SerialNo)
            .GroupBy(s => s.PartId)
            .Select(g => new { PartId = g.Key, Serials = g.Select(x => x.SerialNo).ToList() })
            .ToDictionaryAsync(x => x.PartId, x => x.Serials);

        var palletItems = palletSubs.Select(s => new PickItemOnPallet(
            PartId: s.ReceiptLine!.PartId,
            Owner: s.ReceiptLine!.Part!.Owner,
            Brand: s.ReceiptLine!.Part!.Brand,
            ItemDesc: s.ReceiptLine!.Part!.ItemDesc,
            ImageUrl: s.ReceiptLine!.Part!.ImageUrl,
            QtyOnPallet: s.ReceiptLine!.QtyReceived,
            QtyToPickSuggested: Math.Max(0, Math.Min(s.ReceiptLine!.QtyReceived, s.AllocatedQty - s.PickedQty)),
            Condition: s.ReceiptLine!.Condition,
            AvailableSerials: availableSerials.GetValueOrDefault(s.ReceiptLine!.PartId, new List<string>())
        )).ToList();

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

        return ServiceResult.Ok(new AssignPickStationResponse(
            StationId: station.StationId,
            StationName: station.Name,
            PalletId: req.PalletId,
            PickOrderId: pickOrderId,
            PalletItems: palletItems,
            PickOrderItems: pickOrderItems,
            Message: $"✅ Pallet '{req.PalletId}' อยู่ที่ {station.Name} (Pick Order: {pickOrderId})"
        ));
    }

    public async Task<ServiceResult> ConfirmPickAsync(ConfirmPickRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SourcePalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Source Pallet ID"));
        if (string.IsNullOrWhiteSpace(req.DestPalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Dest Pallet ID"));
        if (req.SourcePalletId == req.DestPalletId)
            return ServiceResult.BadRequest(new ApiError("Source และ Dest Pallet ต้องไม่ใช่ตัวเดียวกัน"));
        if (req.Items == null || req.Items.Count == 0)
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุรายการที่จะ pick"));

        var order = await db.PickOrders
            .FirstOrDefaultAsync(o => o.PickOrderId == req.PickOrderId && o.Status == "OPEN");

        if (order is null)
            return ServiceResult.BadRequest(new ApiError($"Pick Order '{req.PickOrderId}' ไม่ถูกต้องหรือปิดแล้ว"));

        var sourcePallet = await db.Pallets.FindAsync(req.SourcePalletId);
        if (sourcePallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Source Pallet '{req.SourcePalletId}'"));

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

        foreach (var item in req.Items)
        {
            if (item.Qty <= 0)
                continue;

            var serialNumbers = item.SerialNumbers?
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList() ?? new List<string>();

            if (serialNumbers.Count == 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"กรุณาสแกน S/N สำหรับ Part '{item.PartId}'"));

            if (serialNumbers.Count != item.Qty)
                return ServiceResult.BadRequest(new ApiError(
                    $"จำนวน S/N ({serialNumbers.Count}) ไม่ตรงกับ Qty ({item.Qty}) สำหรับ Part '{item.PartId}'"));

            if (serialNumbers.Distinct().Count() != serialNumbers.Count)
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ซ้ำกันสำหรับ Part '{item.PartId}'"));

            var sub = await db.PickOrderSubs
                .Include(s => s.ReceiptLine).ThenInclude(l => l!.Part)
                .Include(s => s.PickOrderDetail)
                .FirstOrDefaultAsync(s => s.PickOrderDetail!.PickOrderId == req.PickOrderId
                                       && s.ReceiptLine!.PalletId == req.SourcePalletId
                                       && s.ReceiptLine!.PartId == item.PartId
                                       && s.Status == "PENDING"
                                       && s.ReceiptLine!.QtyReceived > 0);

            if (sub is null)
                return ServiceResult.BadRequest(new ApiError(
                    $"ไม่พบ Part '{item.PartId}' บน Pallet '{req.SourcePalletId}' สำหรับ Pick Order นี้"));

            var sourceLine = sub.ReceiptLine!;
            var detail = sub.PickOrderDetail!;
            var actualQty = serialNumbers.Count;
            var maxPickQty = Math.Min(sourceLine.QtyReceived, sub.AllocatedQty - sub.PickedQty);
            if (actualQty > maxPickQty)
                return ServiceResult.BadRequest(new ApiError(
                    $"จำนวน S/N เกินจำนวนที่ Pick ได้สำหรับ Part '{item.PartId}' (สแกน {actualQty}, ได้สูงสุด {maxPickQty})"));

            var serialsToMove = await db.PartSerials
                .Where(s => s.PartId == item.PartId
                         && serialNumbers.Contains(s.SerialNo))
                .ToListAsync();

            if (serialsToMove.Count != serialNumbers.Count)
            {
                var foundSet = serialsToMove.Select(s => s.SerialNo).ToHashSet();
                var missing = serialNumbers.Where(sn => !foundSet.Contains(sn)).ToList();
                return ServiceResult.BadRequest(new ApiError(
                    $"ไม่พบ S/N: {string.Join(", ", missing)}"));
            }

            var wrongPallet = serialsToMove
                .Where(s => s.PalletId != req.SourcePalletId)
                .Select(s => s.SerialNo)
                .ToList();
            if (wrongPallet.Count > 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ไม่ได้อยู่บน Pallet '{req.SourcePalletId}': {string.Join(", ", wrongPallet)}"));

            var wrongLine = serialsToMove
                .Where(s => s.ReceiptLineId != sourceLine.LineId)
                .Select(s => s.SerialNo)
                .ToList();
            if (wrongLine.Count > 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ไม่ตรงกับรายการ Pick นี้: {string.Join(", ", wrongLine)}"));

            var unavailable = serialsToMove
                .Where(s => s.Status != "STORED")
                .Select(s => $"{s.SerialNo}({s.Status})")
                .ToList();
            if (unavailable.Count > 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ไม่พร้อม Pick: {string.Join(", ", unavailable)}"));

            sub.PickedQty += actualQty;
            if (sub.PickedQty >= sub.AllocatedQty)
                sub.Status = "PICKED";

            detail.ReservedQty += actualQty;
            if (detail.ReservedQty >= detail.RequiredQty)
                detail.Status = "COMPLETED";
            else if (detail.ReservedQty > 0)
                detail.Status = "PARTIAL";

            sourceLine.QtyReceived -= actualQty;
            sourceLine.UpdatedAt = DateTime.UtcNow;
            if (sourceLine.QtyReceived <= 0)
            {
                sourceLine.QtyReceived = 0;
                sourceLine.Status = "PICKED";
            }

            var destLine = new ReceiptLine
            {
                SessionId = sourceLine.SessionId,
                POId = sourceLine.POId,
                PartId = item.PartId,
                PalletId = req.DestPalletId,
                QtyReceived = actualQty,
                Status = "PALLETIZED",
                OperatorId = req.OperatorId,
            };
            db.ReceiptLines.Add(destLine);

            var nowTs = DateTime.UtcNow;
            foreach (var s in serialsToMove)
            {
                s.PalletId = req.DestPalletId;
                s.ReceiptLine = destLine;
                s.Status = "PICKED";
                s.UpdatedAt = nowTs;
            }
        }

        await db.SaveChangesAsync();

        var sourceHasPickItems = await db.PickOrderSubs
            .Include(s => s.ReceiptLine)
            .Include(s => s.PickOrderDetail)
            .AnyAsync(s => s.PickOrderDetail!.PickOrderId == req.PickOrderId
                        && s.ReceiptLine!.PalletId == req.SourcePalletId
                        && s.Status == "PENDING"
                        && s.ReceiptLine!.QtyReceived > 0);

        var sourceHasAnyItems = await db.ReceiptLines.AnyAsync(
            l => l.PalletId == req.SourcePalletId
              && l.QtyReceived > 0
              && (l.Status == "PALLETIZED" || l.Status == "PICKING"));

        destPallet.Status = "PACKED";
        destPallet.UpdatedAt = DateTime.UtcNow;

        var allDetails = await db.PickOrderDetails
            .Include(d => d.Part)
            .Where(d => d.PickOrderId == req.PickOrderId)
            .ToListAsync();

        // Auto-create Packing สำหรับ (DestPallet+Order) นี้ — สร้างทุกรอบ
        await CreatePackingForOrderAsync(req.PickOrderId, req.DestPalletId, req.OperatorId);

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

        return ServiceResult.Ok(new ConfirmPickResponse(
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

    public async Task<ServiceResult> ReturnPalletAsync(ReturnPalletRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pallet '{req.PalletId}'"));

        var dest = string.IsNullOrWhiteSpace(req.Destination) ? "ASRS" : req.Destination.ToUpper();

        // PACKED pallet → แค่เปลี่ยน Location ไม่แตะ Status/Type
        if (pallet.Status == "PACKED")
        {
            pallet.Location = dest;
            pallet.UpdatedAt = DateTime.UtcNow;

            // clear station ถ้ามี
            var packedStation = await db.PickStations
                .FirstOrDefaultAsync(s => s.CurrentPalletId == req.PalletId);
            if (packedStation is not null)
                packedStation.CurrentPalletId = null;

            await db.SaveChangesAsync();
            return ServiceResult.Ok(new ApiSuccess(true,
                $"📦 Pallet '{req.PalletId}' ส่งไป {dest} (PACKED)"));
        }

        var hasItems = await db.ReceiptLines.AnyAsync(
            l => l.PalletId == req.PalletId
              && l.QtyReceived > 0
              && (l.Status == "PALLETIZED" || l.Status == "PICKING"));

        // ASRS = กลับไป storage พร้อมใช้ pick ต่อ → STORED
        // ที่อื่น (ZONE_PACK ฯลฯ) = ยังเดินทางอยู่ → IN_TRANSIT
        string nextStatus;
        if (!hasItems)
            nextStatus = "AVAILABLE";
        else if (dest == "ASRS")
            nextStatus = "STORED";
        else
            nextStatus = "IN_TRANSIT";

        pallet.Status = nextStatus;
        pallet.Type = hasItems ? "FG" : null;
        pallet.Location = dest;
        pallet.UpdatedAt = DateTime.UtcNow;

        var station = await db.PickStations
            .FirstOrDefaultAsync(s => s.CurrentPalletId == req.PalletId);
        if (station is not null)
            station.CurrentPalletId = null;

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

        return ServiceResult.Ok(new ApiSuccess(true,
            $"📦 Pallet '{req.PalletId}' ส่งไป {dest} ({nextStatus})"));
    }

    public async Task<ServiceResult> GetAvailableLinesAsync()
    {
        var pendingUnloadPallets = await db.UnloadLines
            .Where(u => u.Status == "PENDING")
            .Select(u => u.PalletId)
            .Distinct()
            .ToListAsync();

        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Include(l => l.Pallet)
            .Where(l => l.Status == "PALLETIZED"
                     && l.PalletId != null
                     && l.Pallet!.Type == "FG"
                     && (l.Pallet!.Status == "AVAILABLE" || l.Pallet!.Status == "STORED")
                     && !pendingUnloadPallets.Contains(l.PalletId!))
            .OrderBy(l => l.PartId)
            .ToListAsync();

        // นับเฉพาะ sub ที่ยัง PENDING (allocate รออยู่แต่ยังไม่ pick)
        // sub Status=PICKED แล้วไม่นับ เพราะ ReceiptLine.QtyReceived ของ source ลดไปแล้ว
        var allocatedMap = await db.PickOrderSubs
            .Where(s => s.Status == "PENDING")
            .GroupBy(s => s.ReceiptLineId)
            .Select(g => new { ReceiptLineId = g.Key, AllocatedQty = g.Sum(s => s.AllocatedQty - s.PickedQty) })
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

        return ServiceResult.Ok(result);
    }

    public async Task<ServiceResult> CreateTestOrderAsync(CreateTestOrderRequest req)
    {
        if (req.Items.Count == 0)
            return ServiceResult.BadRequest(new ApiError("กรุณาเลือกสินค้าอย่างน้อย 1 รายการ"));

        var orderId = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var partIdsForOwner = req.Items.Select(i => i.PartId).Distinct().ToList();
        var owner = await db.Parts
            .Where(p => partIdsForOwner.Contains(p.PartId))
            .Select(p => p.Owner)
            .FirstOrDefaultAsync() ?? string.Empty;

        var customerOrderId = string.IsNullOrEmpty(owner)
            ? null
            : await EnsureActiveCustomerOrderAsync(owner);

        var pickOrder = new PickOrder
        {
            PickOrderId = orderId,
            Status = "OPEN",
            CreatedBy = req.OperatorId,
            CustomerOrderId = customerOrderId,
            CreatedAt = DateTime.UtcNow,
        };

        db.PickOrders.Add(pickOrder);
        await db.SaveChangesAsync();

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
            await db.SaveChangesAsync();

            foreach (var item in g)
            {
                var rl = await db.ReceiptLines.FindAsync(item.LineId);
                if (rl is null || rl.Status != "PALLETIZED")
                {
                    return ServiceResult.BadRequest(new ApiError(
                        $"ReceiptLine {item.LineId} ไม่พร้อม (status: {rl?.Status ?? "not found"})"));
                }

                if (item.Qty <= 0 || item.Qty > rl.QtyReceived)
                {
                    return ServiceResult.BadRequest(new ApiError(
                        $"จำนวน {item.Qty} ไม่ถูกต้องสำหรับ Line {item.LineId} (มี {rl.QtyReceived})"));
                }

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

        // ── อัปเดต Pallet: Status → PICKING, assign Station ──
        var palletIds = await db.ReceiptLines
            .Where(l => db.PickOrderSubs
                .Where(s => s.PickOrderDetail!.PickOrderId == orderId)
                .Select(s => s.ReceiptLineId)
                .Contains(l.LineId))
            .Select(l => l.PalletId)
            .Distinct()
            .ToListAsync();

        var availableStations = await db.PickStations
            .Where(s => s.CurrentPalletId == null)
            .OrderBy(s => s.StationId)
            .ToListAsync();

        var stationIndex = 0;
        var assignedPallets = new List<string>();

        foreach (var pid in palletIds)
        {
            if (pid is null) continue;

            var pallet = await db.Pallets.FindAsync(pid);
            if (pallet is null) continue;

            // เช็คว่า pallet อยู่ station อยู่แล้วหรือยัง
            var existingStation = await db.PickStations
                .FirstOrDefaultAsync(s => s.CurrentPalletId == pid);

            if (existingStation is not null)
            {
                // อยู่ station แล้ว แค่อัปเดต status
                pallet.Status = "PICKING";
                pallet.UpdatedAt = DateTime.UtcNow;
                assignedPallets.Add($"{pid}→{existingStation.StationId}");
                continue;
            }

            if (stationIndex >= availableStations.Count)
            {
                // Station เต็ม — ไม่ assign แต่ยังสร้าง order ได้
                continue;
            }

            var station = availableStations[stationIndex++];
            station.CurrentPalletId = pid;

            pallet.Status = "PICKING";
            pallet.Location = station.StationId;
            pallet.UpdatedAt = DateTime.UtcNow;

            // อัปเดต ReceiptLines บน Pallet นี้ด้วย
            var receiptLines = await db.ReceiptLines
                .Where(l => l.PalletId == pid && l.Status == "PALLETIZED")
                .ToListAsync();
            foreach (var rl in receiptLines)
                rl.Status = "PICKING";

            assignedPallets.Add($"{pid}→{station.StationId}");
        }

        await db.SaveChangesAsync();

        var stationMsg = assignedPallets.Count > 0
            ? $" | Pallet: {string.Join(", ", assignedPallets)}"
            : "";

        return ServiceResult.Ok(new
        {
            Success = true,
            PickOrderId = orderId,
            Message = $"สร้าง Pick Order '{orderId}' สำเร็จ ({grouped.Count} รายการ){stationMsg}"
        });
    }

    // ── Auto-allocate: สร้าง PickOrderSub ให้ PickOrderDetail ที่ยังขาด ──
    // จากของที่ว่างอยู่ใน ReceiptLines (pallet STORED/AVAILABLE ใน ASRS)
    // Returns: (allocationsCreated, qtyAllocated)
    public async Task<(int allocationsCreated, int qtyAllocated)> AllocatePendingForPartAsync(string partId)
    {
        var openDetails = await db.PickOrderDetails
            .Include(d => d.PickOrder)
            .Include(d => d.Subs)
            .Where(d => d.PartId == partId
                     && d.PickOrder!.Status == "OPEN"
                     && d.RequiredQty > d.Subs.Sum(s => s.AllocatedQty))
            .OrderBy(d => d.PickOrder!.CreatedAt)
            .ToListAsync();

        if (openDetails.Count == 0)
            return (0, 0);

        // ReceiptLines ของ part นี้ใน pallet ที่พร้อมเบิก (ASRS, STORED/AVAILABLE)
        var lines = await db.ReceiptLines
            .Include(l => l.Pallet)
            .Where(l => l.PartId == partId
                     && l.Status == "PALLETIZED"
                     && l.QtyReceived > 0
                     && l.PalletId != null
                     && (l.Pallet!.Status == "STORED" || l.Pallet!.Status == "AVAILABLE"))
            .OrderBy(l => l.ReceivedAt)
            .ToListAsync();

        if (lines.Count == 0)
            return (0, 0);

        var lineIds = lines.Select(l => l.LineId).ToList();
        var allocatedPerLine = await db.PickOrderSubs
            .Where(s => lineIds.Contains(s.ReceiptLineId)
                     && (s.Status == "PENDING" || s.Status == "PICKED"))
            .GroupBy(s => s.ReceiptLineId)
            .Select(g => new { ReceiptLineId = g.Key, Allocated = g.Sum(x => x.AllocatedQty) })
            .ToDictionaryAsync(x => x.ReceiptLineId, x => x.Allocated);

        var subsCreated = 0;
        var totalAllocated = 0;

        foreach (var detail in openDetails)
        {
            var gap = detail.RequiredQty - detail.Subs.Sum(s => s.AllocatedQty);
            if (gap <= 0) continue;

            foreach (var line in lines)
            {
                if (gap <= 0) break;

                var used = allocatedPerLine.GetValueOrDefault(line.LineId, 0);
                var lineAvailable = line.QtyReceived - used;
                if (lineAvailable <= 0) continue;

                var take = Math.Min(gap, lineAvailable);
                db.PickOrderSubs.Add(new PickOrderSub
                {
                    PickOrderDetailId = detail.Id,
                    ReceiptLineId = line.LineId,
                    AllocatedQty = take,
                    PickedQty = 0,
                    Status = "PENDING",
                });

                allocatedPerLine[line.LineId] = used + take;
                gap -= take;
                totalAllocated += take;
                subsCreated++;
            }
        }

        if (subsCreated > 0)
            await db.SaveChangesAsync();

        return (subsCreated, totalAllocated);
    }

    // ── สร้าง Pick Order จริง (ไม่ต้องระบุ LineId) — auto-allocate จาก stock ที่มีอยู่ ──
    public async Task<ServiceResult> CreatePickOrderAsync(CreatePickOrderRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุสินค้าอย่างน้อย 1 รายการ"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        var grouped = req.Items
            .GroupBy(i => i.PartId)
            .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.Qty) })
            .ToList();

        if (grouped.Any(g => g.Qty <= 0))
            return ServiceResult.BadRequest(new ApiError("จำนวนต้องมากกว่า 0"));

        var partIds = grouped.Select(g => g.PartId).ToList();
        var existingParts = await db.Parts
            .Where(p => partIds.Contains(p.PartId))
            .Select(p => p.PartId)
            .ToListAsync();

        var missing = partIds.Except(existingParts).ToList();
        if (missing.Count > 0)
            return ServiceResult.BadRequest(new ApiError(
                $"ไม่พบ Part: {string.Join(", ", missing)}"));

        var orderId = $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

        var owner = await db.Parts
            .Where(p => partIds.Contains(p.PartId))
            .Select(p => p.Owner)
            .FirstOrDefaultAsync() ?? string.Empty;

        var customerOrderId = string.IsNullOrEmpty(owner)
            ? null
            : await EnsureActiveCustomerOrderAsync(owner);

        db.PickOrders.Add(new PickOrder
        {
            PickOrderId = orderId,
            Status = "OPEN",
            CreatedBy = req.OperatorId,
            CustomerOrderId = customerOrderId,
            CreatedAt = DateTime.UtcNow,
        });

        var details = new List<PickOrderDetail>();
        foreach (var g in grouped)
        {
            var detail = new PickOrderDetail
            {
                PickOrderId = orderId,
                PartId = g.PartId,
                RequiredQty = g.Qty,
                ReservedQty = 0,
                Status = "PENDING",
            };
            db.PickOrderDetails.Add(detail);
            details.Add(detail);
        }

        await db.SaveChangesAsync();

        // auto-allocate ทีละ part
        var allocations = new List<PickOrderDetailAllocation>();
        var totalAllocated = 0;
        foreach (var detail in details)
        {
            var (_, qty) = await AllocatePendingForPartAsync(detail.PartId);
            allocations.Add(new PickOrderDetailAllocation(
                PartId: detail.PartId,
                RequiredQty: detail.RequiredQty,
                AllocatedQty: qty,
                ShortageQty: Math.Max(0, detail.RequiredQty - qty)
            ));
            totalAllocated += qty;
        }

        var totalRequired = grouped.Sum(g => g.Qty);
        var shortage = totalRequired - totalAllocated;
        var msg = shortage > 0
            ? $"สร้าง Pick Order '{orderId}' — จัดสรรได้ {totalAllocated}/{totalRequired} (ขาด {shortage}, รอ PO/stock เพิ่ม)"
            : $"สร้าง Pick Order '{orderId}' — จัดสรรครบ {totalAllocated}/{totalRequired}";

        return ServiceResult.Ok(new CreatePickOrderResponse(
            PickOrderId: orderId,
            TotalRequired: totalRequired,
            TotalAllocated: totalAllocated,
            Details: allocations,
            Message: msg
        ));
    }

    public async Task<ServiceResult> SendToPackAsync(string palletId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pallet '{palletId}'"));

        if (pallet.Status != "PACKED")
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{palletId}' สถานะ '{pallet.Status}' — ต้องเป็น PACKED"));

        pallet.Location = "ZONE_PACK";
        pallet.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ServiceResult.Ok(new
        {
            Success = true,
            PalletId = palletId,
            Message = $"✅ Pallet '{palletId}' ส่งไป ZONE_PACK เรียบร้อย"
        });
    }

    // ── หา/สร้าง CustomerOrder ที่ยังเปิดอยู่ของ Owner นี้ ──
    // กฎ: 1 Owner = 1 active CustomerOrder. ปิดเมื่อ Slot SHIPPED
    private async Task<string> EnsureActiveCustomerOrderAsync(string owner)
    {
        var existing = await db.CustomerOrders
            .Where(c => c.Owner == owner && c.Status == "ACTIVE")
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing is not null)
            return existing.CustomerOrderId;

        var coId = $"CO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
        db.CustomerOrders.Add(new CustomerOrder
        {
            CustomerOrderId = coId,
            Owner = owner,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return coId;
    }

    private async Task CreatePackingForOrderAsync(string pickOrderId, string palletId, string operatorId)
    {
        // ── Rule: 1 Pallet N Packs (ตาม Owner), 1 Pack N Orders (Owner เดียวกัน) ──
        // Order นี้อยู่ใน OPEN Pack บน Pallet นี้แล้ว → skip
        var existsInOpen = await db.PackingDetails.AnyAsync(d =>
            d.PickOrderId == pickOrderId
            && d.Packing != null
            && d.Packing.PalletId == palletId
            && d.Packing.Status == "OPEN");
        if (existsInOpen) return;

        // หา Owner ของ Order นี้ (จาก Part ตัวแรก)
        var owner = await db.PickOrderDetails
            .Where(d => d.PickOrderId == pickOrderId)
            .Include(d => d.Part)
            .Select(d => d.Part!.Owner)
            .FirstOrDefaultAsync() ?? string.Empty;

        // มี OPEN Pack บน Pallet นี้ + Owner ตรงกัน → เพิ่ม Detail เข้าไป
        var openPack = await db.Packings
            .FirstOrDefaultAsync(p => p.PalletId == palletId
                                   && p.Status == "OPEN"
                                   && p.Owner == owner);

        if (openPack is not null)
        {
            db.PackingDetails.Add(new PackingDetail
            {
                PackingId = openPack.PackingId,
                PickOrderId = pickOrderId,
                Status = "PENDING",
            });
            return;
        }

        // ไม่มี OPEN Pack ของ Owner นี้ → สร้าง Pack ใหม่
        // PackingId format: PK-DDMMYYYY-NNN (ปี พ.ศ.)
        var now = DateTime.UtcNow;
        var beYear = now.Year + 543;
        var prefix = $"PK-{now:ddMM}{beYear}";
        var todayCount = await db.Packings.CountAsync(p => p.PackingId.StartsWith(prefix));
        var packingId = $"{prefix}-{(todayCount + 1):D3}";

        db.Packings.Add(new Models.Packing
        {
            PackingId = packingId,
            PalletId = palletId,
            Owner = owner,
            PickOrderId = pickOrderId,
            Status = "OPEN",
            CreatedBy = operatorId,
            CreatedAt = now,
        });

        db.PackingDetails.Add(new PackingDetail
        {
            PackingId = packingId,
            PickOrderId = pickOrderId,
            Status = "PENDING",
        });
    }

    private static PickOrderResponse BuildPickOrderResponse(PickOrder order)
    {
        var details = order.Details.Select(d =>
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

        return new PickOrderResponse(order.PickOrderId, order.Status, order.CreatedAt, details);
    }
}
