using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Picking;

public class PickingService(WmsDbContext db) : IPickingService
{
    // ── หน้า 1: list orders แบบ summary (WAITING + PICKING) ────
    public async Task<ServiceResult> ListOrdersAsync()
    {
        var orders = await db.PickOrders
            .Include(o => o.Details).ThenInclude(d => d.Part)
            .Include(o => o.Details).ThenInclude(d => d.Subs).ThenInclude(s => s.ReceiptLine)
            .Where(o => o.Status == "WAITING" || o.Status == "PICKING")
            .OrderBy(o => o.Status == "PICKING" ? 0 : 1)   // PICKING ขึ้นบน
            .ThenByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(o =>
        {
            var owner = o.Details.SelectMany(d => new[] { d.Part?.Owner ?? string.Empty })
                .FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? string.Empty;
            var pallets = o.Details
                .SelectMany(d => d.Subs)
                .Select(s => s.ReceiptLine?.PalletId)
                .Where(p => p != null)
                .Distinct()
                .ToList();
            return new PickOrderListItem(
                PickOrderId: o.PickOrderId,
                Status: o.Status,
                Owner: owner,
                CustomerOrderId: o.CustomerOrderId,
                PartCount: o.Details.Count,
                TotalRequiredQty: o.Details.Sum(d => d.RequiredQty),
                PalletCount: pallets.Count,
                CreatedAt: o.CreatedAt
            );
        }).ToList();

        return ServiceResult.Ok(result);
    }

    // ── หน้า 2: order detail (pallets + parts) ────────────────
    public async Task<ServiceResult> GetOrderDetailAsync(string pickOrderId)
    {
        var order = await db.PickOrders
            .Include(o => o.Details).ThenInclude(d => d.Part)
            .Include(o => o.Details).ThenInclude(d => d.Subs).ThenInclude(s => s.ReceiptLine)
            .FirstOrDefaultAsync(o => o.PickOrderId == pickOrderId);

        if (order is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pick Order '{pickOrderId}'"));

        var owner = order.Details.Select(d => d.Part?.Owner ?? string.Empty)
            .FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? string.Empty;

        // ── Pallets ────────────────────────────────────────
        // จัดกลุ่ม PickOrderSub ตาม PalletId ของ ReceiptLine
        var subsByPallet = order.Details
            .SelectMany(d => d.Subs)
            .Where(s => s.ReceiptLine?.PalletId != null)
            .GroupBy(s => s.ReceiptLine!.PalletId!)
            .ToList();

        var palletIds = subsByPallet.Select(g => g.Key).ToList();

        var palletMap = await db.Pallets
            .Where(p => palletIds.Contains(p.PalletId))
            .ToDictionaryAsync(p => p.PalletId);

        var stationMap = await db.PickStations
            .Where(s => s.CurrentPalletId != null && palletIds.Contains(s.CurrentPalletId))
            .ToDictionaryAsync(s => s.CurrentPalletId!);

        // Part master data ของ part ที่อยู่บน pallet เหล่านี้
        var allPartIds = subsByPallet
            .SelectMany(g => g.Select(s => s.ReceiptLine!.PartId))
            .Distinct()
            .ToList();
        var partMaster = await db.Parts
            .Where(p => allPartIds.Contains(p.PartId))
            .ToDictionaryAsync(p => p.PartId);

        var palletInfos = subsByPallet.Select(g =>
        {
            var pallet = palletMap.GetValueOrDefault(g.Key);
            var station = stationMap.GetValueOrDefault(g.Key);

            // Group subs by part within this pallet
            var partsOnPallet = g
                .GroupBy(s => s.ReceiptLine!.PartId)
                .Select(pg =>
                {
                    var part = partMaster.GetValueOrDefault(pg.Key);
                    return new PickOrderPalletPartInfo(
                        PartId: pg.Key,
                        Owner: part?.Owner ?? string.Empty,
                        Brand: part?.Brand ?? string.Empty,
                        ItemDesc: part?.ItemDesc ?? string.Empty,
                        ImageUrl: part?.ImageUrl,
                        AllocatedQty: pg.Sum(s => s.AllocatedQty),
                        PickedQty: pg.Sum(s => s.PickedQty),
                        Status: pg.All(s => s.Status == "PICKED")
                            ? "PICKED"
                            : pg.Any(s => s.Status == "PICKED") ? "PARTIAL" : "PENDING"
                    );
                })
                .OrderBy(p => p.PartId)
                .ToList();

            return new PickOrderPalletInfo(
                PalletId: g.Key,
                PalletStatus: pallet?.Status ?? "UNKNOWN",
                StationId: station?.StationId,
                StationName: station?.Name,
                PartCount: partsOnPallet.Count,
                TotalQty: partsOnPallet.Sum(p => p.AllocatedQty),
                Parts: partsOnPallet
            );
        }).OrderBy(p => p.PalletId).ToList();

        // ── Parts ──────────────────────────────────────────
        var partInfos = order.Details.Select(d => new PickOrderPartInfo(
            PartId: d.PartId,
            Owner: d.Part?.Owner ?? string.Empty,
            Brand: d.Part?.Brand ?? string.Empty,
            ItemDesc: d.Part?.ItemDesc ?? string.Empty,
            ImageUrl: d.Part?.ImageUrl,
            RequiredQty: d.RequiredQty,
            ReservedQty: d.ReservedQty,
            RemainingQty: d.RequiredQty - d.ReservedQty,
            Status: d.Status
        )).OrderBy(p => p.PartId).ToList();

        return ServiceResult.Ok(new PickOrderDetailFull(
            PickOrderId: order.PickOrderId,
            Status: order.Status,
            Owner: owner,
            CustomerOrderId: order.CustomerOrderId,
            CreatedAt: order.CreatedAt,
            Pallets: palletInfos,
            Parts: partInfos
        ));
    }

    // ── Suggest dest pallet ─────────────────────────────────
    // 1) ถ้ามี Packing OPEN ของ pickOrder นี้อยู่ → ใช้ pallet เดิมต่อ (ยังไม่ confirm pack)
    // 2) ไม่งั้น → pallet ว่างถัดไป (Type=NULL + AVAILABLE)
    public async Task<ServiceResult> SuggestDestPalletsAsync(string? pickOrderId)
    {
        // 1) มี packing record ของ order นี้ที่ยัง OPEN + pallet ยัง active (ยังไม่ PACKED ปิดกล่อง)?
        if (!string.IsNullOrWhiteSpace(pickOrderId))
        {
            var ongoing = await db.Packings
                .Include(p => p.Pallet)
                .Where(p => p.PickOrderId == pickOrderId
                         && p.Status == "OPEN"
                         && p.Pallet != null
                         && p.Pallet.Status != "PACKED"
                         && p.Pallet.Location == "PICK")
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.PalletId,
                    Status = p.Pallet!.Status,
                    Location = p.Pallet!.Location,
                    Continued = true,
                })
                .FirstOrDefaultAsync();

            if (ongoing != null)
                return ServiceResult.Ok(new[] { ongoing });
        }

        // 2) Default — pallet เปล่ารอที่ Pick Zone (top 1)
        var pallets = await db.Pallets
            .Where(p => p.Status == "AVAILABLE"
                     && p.Type == null
                     && p.Location == "PICK")
            .OrderBy(p => p.PalletId)
            .Take(1)
            .Select(p => new
            {
                p.PalletId,
                p.Status,
                p.Location,
                Continued = false,
            })
            .ToListAsync();

        return ServiceResult.Ok(pallets);
    }

    // ── Robot simulator: WAITING → PICKING + assign pallets ──
    public async Task<ServiceResult> NotifyArrivalAsync(string pickOrderId)
    {
        var order = await db.PickOrders
            .FirstOrDefaultAsync(o => o.PickOrderId == pickOrderId);

        if (order is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pick Order '{pickOrderId}'"));

        if (order.Status == "PICKING")
            return ServiceResult.BadRequest(new ApiError(
                $"Pick Order '{pickOrderId}' อยู่ในสถานะ PICKING แล้ว"));

        if (order.Status != "WAITING")
            return ServiceResult.BadRequest(new ApiError(
                $"Pick Order '{pickOrderId}' ไม่อยู่ในสถานะ WAITING (สถานะ: {order.Status})"));

        // หา pallet ทั้งหมดที่ผูกกับ order
        var palletIds = await db.PickOrderSubs
            .Include(s => s.ReceiptLine)
            .Where(s => s.PickOrderDetail!.PickOrderId == pickOrderId
                     && s.ReceiptLine!.PalletId != null)
            .Select(s => s.ReceiptLine!.PalletId!)
            .Distinct()
            .ToListAsync();

        if (palletIds.Count == 0)
            return ServiceResult.BadRequest(new ApiError(
                $"Pick Order '{pickOrderId}' ไม่มี pallet ผูกไว้"));

        var availableStations = await db.PickStations
            .Where(s => s.CurrentPalletId == null)
            .OrderBy(s => s.StationId)
            .ToListAsync();

        var assignments = new List<NotifyArrivalAssignment>();
        var stationIndex = 0;
        var anyAssigned = false;

        foreach (var pid in palletIds)
        {
            var pallet = await db.Pallets.FindAsync(pid);
            if (pallet is null) continue;

            var existingStation = await db.PickStations
                .FirstOrDefaultAsync(s => s.CurrentPalletId == pid);

            if (existingStation is not null)
            {
                pallet.Status = "PICKING";
                pallet.UpdatedAt = DateTime.UtcNow;
                assignments.Add(new NotifyArrivalAssignment(
                    PalletId: pid,
                    StationId: existingStation.StationId,
                    Outcome: "ALREADY_AT_STATION"));
                anyAssigned = true;
                continue;
            }

            if (stationIndex >= availableStations.Count)
            {
                assignments.Add(new NotifyArrivalAssignment(
                    PalletId: pid,
                    StationId: null,
                    Outcome: "NO_FREE_STATION"));
                continue;
            }

            var station = availableStations[stationIndex++];
            station.CurrentPalletId = pid;
            pallet.Status = "PICKING";
            pallet.Location = station.StationId;
            pallet.UpdatedAt = DateTime.UtcNow;

            // ReceiptLines บน pallet → PICKING
            var receiptLines = await db.ReceiptLines
                .Where(l => l.PalletId == pid && l.Status == "PALLETIZED")
                .ToListAsync();
            foreach (var rl in receiptLines)
                rl.Status = "PICKING";

            assignments.Add(new NotifyArrivalAssignment(
                PalletId: pid,
                StationId: station.StationId,
                Outcome: "ASSIGNED"));
            anyAssigned = true;
        }

        if (anyAssigned)
        {
            order.Status = "PICKING";
        }

        await db.SaveChangesAsync();

        var msg = anyAssigned
            ? $"Robot ส่ง pallet ของ '{pickOrderId}' มาถึงแล้ว — เริ่ม picking ได้"
            : $"ไม่มี station ว่างให้ assign pallet ของ '{pickOrderId}' — รออีกครั้ง";

        return ServiceResult.Ok(new NotifyArrivalResponse(
            PickOrderId: pickOrderId,
            Status: order.Status,
            Assignments: assignments,
            Message: msg
        ));
    }

    public async Task<ServiceResult> AssignStationAsync(AssignPickStationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));

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
            .FirstOrDefaultAsync(o => o.PickOrderId == pickOrderId
                && (o.Status == "WAITING" || o.Status == "PICKING"));

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

        // Group by PartId: ถ้า pallet เดียวกันมีหลาย ReceiptLine ของ Part เดียวกัน → รวมเป็นการ์ดเดียว
        var palletItems = palletSubs
            .GroupBy(s => s.ReceiptLine!.PartId)
            .Select(g =>
            {
                var first = g.First();
                var qtyOnPallet = g.Sum(s => s.ReceiptLine!.QtyReceived);
                var qtyToPickSuggested = g.Sum(s =>
                    Math.Max(0, Math.Min(s.ReceiptLine!.QtyReceived, s.AllocatedQty - s.PickedQty)));
                return new PickItemOnPallet(
                    PartId: g.Key,
                    Owner: first.ReceiptLine!.Part!.Owner,
                    Brand: first.ReceiptLine!.Part!.Brand,
                    ItemDesc: first.ReceiptLine!.Part!.ItemDesc,
                    ImageUrl: first.ReceiptLine!.Part!.ImageUrl,
                    QtyOnPallet: qtyOnPallet,
                    QtyToPickSuggested: qtyToPickSuggested,
                    Condition: first.ReceiptLine!.Condition,
                    AvailableSerials: availableSerials.GetValueOrDefault(g.Key, new List<string>())
                );
            })
            .OrderBy(p => p.PartId)
            .ToList();

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
            Status: d.Status
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
            .FirstOrDefaultAsync(o => o.PickOrderId == req.PickOrderId
                && (o.Status == "WAITING" || o.Status == "PICKING"));

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

            // Auto-ส่ง dest pallet ไป ZONE_PACK
            destPallet.Location = "ZONE_PACK";
            destPallet.UpdatedAt = DateTime.UtcNow;

            // Auto-return source pallets ของ order นี้ที่ค้างที่ stations → ASRS
            await ReturnSourcePalletsToAsrsAsync(req.PickOrderId, req.DestPalletId);
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

    /// Auto-return source pallets ของ pick order → ASRS หลัง pick ครบ
    /// (กันค้างที่ Pick Station)
    private async Task ReturnSourcePalletsToAsrsAsync(
        string pickOrderId, string destPalletId)
    {
        // หา pallet ทั้งหมดที่เป็น source ของ order นี้
        var sourcePalletIds = await db.PickOrderSubs
            .Include(s => s.ReceiptLine)
            .Where(s => s.PickOrderDetail!.PickOrderId == pickOrderId
                     && s.ReceiptLine!.PalletId != null
                     && s.ReceiptLine!.PalletId != destPalletId)
            .Select(s => s.ReceiptLine!.PalletId!)
            .Distinct()
            .ToListAsync();

        if (sourcePalletIds.Count == 0) return;

        // Clear station references
        var stations = await db.PickStations
            .Where(s => s.CurrentPalletId != null
                     && sourcePalletIds.Contains(s.CurrentPalletId))
            .ToListAsync();
        foreach (var st in stations)
            st.CurrentPalletId = null;

        // Pallets → STORED + ASRS
        var now = DateTime.UtcNow;
        var pallets = await db.Pallets
            .Where(p => sourcePalletIds.Contains(p.PalletId)
                     && p.Status == "PICKING")
            .ToListAsync();
        foreach (var p in pallets)
        {
            p.Status = "STORED";
            p.Location = "ASRS";
            p.UpdatedAt = now;
        }

        // ReceiptLines: PICKING → PALLETIZED (ถ้ายังมี qty) หรือ PICKED (ถ้าหมด)
        var receiptLines = await db.ReceiptLines
            .Where(l => sourcePalletIds.Contains(l.PalletId!) && l.Status == "PICKING")
            .ToListAsync();
        foreach (var rl in receiptLines)
        {
            rl.Status = rl.QtyReceived > 0 ? "PALLETIZED" : "PICKED";
        }
    }

    public async Task<ServiceResult> ReturnPalletAsync(ReturnPalletRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pallet '{req.PalletId}'"));

        // Auto-decide ถ้า client ไม่ระบุ destination:
        // - PACKED → ZONE_PACK (dest pallet มีของ pick พร้อมส่ง pack)
        // - อื่นๆ → ASRS (clear station + กลับเก็บ)
        var dest = !string.IsNullOrWhiteSpace(req.Destination)
            ? req.Destination!.ToUpper()
            : pallet.Status == "PACKED" ? "ZONE_PACK" : "ASRS";

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
            Status = "WAITING",   // รอ robot ขนของมาที่ station (notify-arrival)
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

        // หมายเหตุ: ไม่ assign Pallet → Station ที่ขั้น create
        // → ต้องเรียก /picking/orders/{id}/notify-arrival เพื่อจำลอง robot ขนของมาถึง station
        return ServiceResult.Ok(new
        {
            Success = true,
            PickOrderId = orderId,
            Status = "WAITING",
            Message = $"สร้าง Pick Order '{orderId}' สำเร็จ ({grouped.Count} รายการ) — รอ robot จัดส่ง pallet"
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
                     && (d.PickOrder!.Status == "WAITING" || d.PickOrder!.Status == "PICKING")
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

}
