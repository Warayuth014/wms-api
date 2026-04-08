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
            var actualQty = Math.Min(item.Qty, Math.Min(sourceLine.QtyReceived, sub.AllocatedQty - sub.PickedQty));

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
        destPallet.Location = "ZONE_PACK";
        destPallet.UpdatedAt = DateTime.UtcNow;

        var allDetails = await db.PickOrderDetails
            .Include(d => d.Part)
            .Where(d => d.PickOrderId == req.PickOrderId)
            .ToListAsync();

        var isComplete = allDetails.All(d => d.ReservedQty >= d.RequiredQty);
        if (isComplete)
        {
            order.Status = "COMPLETED";
            order.CompletedAt = DateTime.UtcNow;

            // Auto-create Packing record (1 PickOrder → 1 Packing)
            await CreatePackingForOrderAsync(req.PickOrderId, req.DestPalletId, req.OperatorId);
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

        var hasItems = await db.ReceiptLines.AnyAsync(
            l => l.PalletId == req.PalletId
              && l.QtyReceived > 0
              && (l.Status == "PALLETIZED" || l.Status == "PICKING"));

        pallet.Status = hasItems ? "IN_TRANSIT" : "AVAILABLE";
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
            hasItems
                ? $"📦 Pallet '{req.PalletId}' ส่งไป {dest} (IN_TRANSIT — ยังมีของเหลือ)"
                : $"📦 Pallet '{req.PalletId}' ส่งไป {dest} (AVAILABLE — ว่างเลย)"));
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

        return ServiceResult.Ok(result);
    }

    public async Task<ServiceResult> CreateTestOrderAsync(CreateTestOrderRequest req)
    {
        if (req.Items.Count == 0)
            return ServiceResult.BadRequest(new ApiError("กรุณาเลือกสินค้าอย่างน้อย 1 รายการ"));

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

        return ServiceResult.Ok(new
        {
            Success = true,
            PickOrderId = orderId,
            Message = $"สร้าง Pick Order '{orderId}' สำเร็จ ({grouped.Count} รายการ)"
        });
    }

    private async Task CreatePackingForOrderAsync(string pickOrderId, string palletId, string operatorId)
    {
        // กันเคสซ้ำ — ถ้ามี Packing detail ที่อ้าง pickOrderId นี้แล้ว ข้ามไป
        var exists = await db.PackingDetails.AnyAsync(d => d.PickOrderId == pickOrderId);
        if (exists) return;

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
