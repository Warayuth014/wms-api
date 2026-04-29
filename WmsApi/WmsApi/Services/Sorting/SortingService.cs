using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Hubs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Sorting;

public class SortingService(
    WmsDbContext db,
    IHubContext<SortingHub> hub) : ISortingService
{
    private const int TickIntervalSec = 2;

    // ── 1) Station grid (10 cards) ────────────────────────
    public async Task<ServiceResult> GetStationsAsync()
    {
        var stations = await db.SortingStations
            .Include(s => s.CurrentPallet)
            .OrderBy(s => s.StationId)
            .ToListAsync();

        var result = stations.Select(s =>
        {
            var status = !s.Enabled
                ? "DISABLED"
                : s.CurrentPalletId == null ? "AVAILABLE" : "BUSY";

            var p = s.CurrentPallet;
            return new SortingStationView(
                StationId: s.StationId,
                Enabled: s.Enabled,
                Status: status,
                PalletId: p?.PalletId,
                CartonsCount: p?.CartonsCount,
                MaxCapacity: p?.MaxCapacity,
                IsFull: p != null && p.CartonsCount >= p.MaxCapacity,
                StartedAt: p?.CreatedAt,
                DisabledBy: s.DisabledBy,
                DisabledAt: s.DisabledAt,
                DisableReason: s.DisableReason
            );
        }).ToList();

        return ServiceResult.Ok(result);
    }

    // ── 2) Station detail (cartons list) ──────────────────
    public async Task<ServiceResult> GetStationDetailAsync(int stationId)
    {
        var station = await db.SortingStations
            .Include(s => s.CurrentPallet)
            .FirstOrDefaultAsync(s => s.StationId == stationId);

        if (station is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Station {stationId}"));

        var p = station.CurrentPallet;
        var status = !station.Enabled
            ? "DISABLED"
            : p == null ? "AVAILABLE" : "BUSY";

        var cartons = new List<SortingStationCarton>();
        var pendingCount = 0;

        if (p != null)
        {
            // packs ที่ Sort เข้า pallet นี้แล้ว
            var packs = await db.Packings
                .Where(x => x.SortingPalletId == p.PalletId && x.Status != "DONE")
                .OrderBy(x => x.SortedAt)
                .ToListAsync();

            var packIds = packs.Select(x => x.PackingId).ToList();
            var itemCounts = await db.PackingPartScans
                .Where(s => packIds.Contains(s.PackingId))
                .GroupBy(s => s.PackingId)
                .Select(g => new { g.Key, Count = g.Sum(x => x.ScannedQty) })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            // sequence: หาจาก SortingPalletPack
            var seqMap = await db.SortingPalletPacks
                .Where(q => q.PalletId == p.PalletId)
                .ToDictionaryAsync(q => q.PackingId, q => q.SequenceNo);

            cartons = packs.Select(x => new SortingStationCarton(
                PackingId: x.PackingId,
                Owner: x.Owner,
                WeightGram: x.WeightGram ?? 0,
                ItemCount: itemCounts.GetValueOrDefault(x.PackingId, 0),
                SortedAt: x.SortedAt ?? x.CreatedAt,
                SequenceNo: seqMap.GetValueOrDefault(x.PackingId, 0)
            )).ToList();

            pendingCount = await db.SortingPalletPacks
                .CountAsync(q => q.PalletId == p.PalletId && q.Status == "PENDING");
        }

        var detail = new SortingStationDetail(
            StationId: station.StationId,
            Enabled: station.Enabled,
            Status: status,
            PalletId: p?.PalletId,
            CartonsCount: p?.CartonsCount ?? 0,
            MaxCapacity: p?.MaxCapacity ?? 0,
            IsFull: p != null && p.CartonsCount >= p.MaxCapacity,
            StartedAt: p?.CreatedAt,
            FullAt: p?.SealedAt,
            Cartons: cartons,
            PendingCount: pendingCount
        );

        return ServiceResult.Ok(detail);
    }

    // ── 3) Toggle enable/disable station ──────────────────
    public async Task<ServiceResult> ToggleStationAsync(ToggleStationRequest req)
    {
        var station = await db.SortingStations
            .FirstOrDefaultAsync(s => s.StationId == req.StationId);

        if (station is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Station {req.StationId}"));

        if (req.Enable && station.Enabled)
            return ServiceResult.BadRequest(new ApiError($"Station {req.StationId} เปิดใช้งานอยู่แล้ว"));
        if (!req.Enable && !station.Enabled)
            return ServiceResult.BadRequest(new ApiError($"Station {req.StationId} ปิดใช้งานอยู่แล้ว"));

        // Q7: disable ขณะมี active batch ไม่ได้
        if (!req.Enable && station.CurrentPalletId != null)
            return ServiceResult.BadRequest(new ApiError(
                $"ปิด Station {req.StationId} ไม่ได้ — กำลังมี Pallet '{station.CurrentPalletId}' ทำงานอยู่ " +
                "(ต้องรอจนครบ + Clear ก่อน)"));

        station.Enabled = req.Enable;
        if (req.Enable)
        {
            station.DisabledBy = null;
            station.DisabledAt = null;
            station.DisableReason = null;
        }
        else
        {
            station.DisabledBy = req.OperatorId;
            station.DisabledAt = DateTime.UtcNow;
            station.DisableReason = req.Reason;
        }

        db.StationAuditLogs.Add(new StationAuditLog
        {
            StationId = req.StationId,
            Action = req.Enable ? "ENABLE" : "DISABLE",
            OperatorId = req.OperatorId,
            Reason = req.Reason,
            At = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("StationToggled", new
        {
            stationId = req.StationId,
            enabled = req.Enable,
        });

        return ServiceResult.Ok(new { stationId = req.StationId, enabled = req.Enable });
    }

    // ── 4) Clear station (FULL → free) ────────────────────
    public async Task<ServiceResult> ClearStationAsync(ClearStationRequest req)
    {
        var station = await db.SortingStations
            .Include(s => s.CurrentPallet)
            .FirstOrDefaultAsync(s => s.StationId == req.StationId);

        if (station is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Station {req.StationId}"));

        var pallet = station.CurrentPallet;
        if (pallet is null)
            return ServiceResult.BadRequest(new ApiError($"Station {req.StationId} ว่างอยู่แล้ว"));

        if (pallet.CartonsCount < pallet.MaxCapacity)
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet ยังไม่ครบ ({pallet.CartonsCount}/{pallet.MaxCapacity}) — clear ไม่ได้"));

        // mark pallet sealed (ของจริงคือถูกย้ายไป docking)
        pallet.Status = "SEALED";
        pallet.SealedAt ??= DateTime.UtcNow;

        // free station
        station.CurrentPalletId = null;

        db.StationAuditLogs.Add(new StationAuditLog
        {
            StationId = req.StationId,
            Action = "CLEAR",
            OperatorId = req.OperatorId,
            PalletId = pallet.PalletId,
            At = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("StationCleared", new
        {
            stationId = req.StationId,
            palletId = pallet.PalletId,
        });

        return ServiceResult.Ok(new { stationId = req.StationId, palletId = pallet.PalletId });
    }

    // ── 5) Test: available packs ──────────────────────────
    public async Task<ServiceResult> GetAvailablePacksAsync()
    {
        var packs = await db.Packings
            .Where(p => p.Status == "DONE" && p.SortingPalletId == null)
            .OrderBy(p => p.CompletedAt)
            .ToListAsync();

        var packIds = packs.Select(p => p.PackingId).ToList();

        var itemCounts = await db.PackingPartScans
            .Where(s => packIds.Contains(s.PackingId))
            .GroupBy(s => s.PackingId)
            .Select(g => new { g.Key, Count = g.Sum(x => x.ScannedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var orderCounts = await db.PackingDetails
            .Where(d => packIds.Contains(d.PackingId))
            .GroupBy(d => d.PackingId)
            .Select(g => new { g.Key, Count = g.Select(d => d.PickOrderId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var customerOrderMap = await db.PackingDetails
            .Where(d => packIds.Contains(d.PackingId))
            .Join(db.PickOrders, d => d.PickOrderId, po => po.PickOrderId, (d, po) => new { d.PackingId, po.CustomerOrderId })
            .Where(x => x.CustomerOrderId != null)
            .GroupBy(x => x.PackingId)
            .Select(g => new { g.Key, COId = g.Select(z => z.CustomerOrderId).First() })
            .ToDictionaryAsync(x => x.Key, x => x.COId);

        var result = packs.Select(p => new AvailablePackForSortingItem(
            PackingId: p.PackingId,
            Owner: p.Owner,
            CustomerOrderId: customerOrderMap.GetValueOrDefault(p.PackingId),
            ItemCount: itemCounts.GetValueOrDefault(p.PackingId, 0),
            OrderCount: orderCounts.GetValueOrDefault(p.PackingId, 0),
            CompletedAt: p.CompletedAt ?? p.CreatedAt
        )).ToList();

        return ServiceResult.Ok(result);
    }

    // ── 6) Test: create sorting batch ─────────────────────
    public async Task<ServiceResult> CreateTestBatchAsync(CreateSortingBatchRequest req)
    {
        if (req.PackingIds is null || req.PackingIds.Count == 0)
            return ServiceResult.BadRequest(new ApiError("กรุณาเลือก Packing อย่างน้อย 1 รายการ"));

        var ids = req.PackingIds.Select(x => x.Trim().ToUpper()).Distinct().ToList();

        // ตรวจ pack ทั้งหมด: ต้อง Status=DONE และยังไม่ถูก assign
        var packs = await db.Packings
            .Where(p => ids.Contains(p.PackingId))
            .ToListAsync();

        if (packs.Count != ids.Count)
        {
            var missing = ids.Except(packs.Select(p => p.PackingId)).ToList();
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pack: {string.Join(", ", missing)}"));
        }

        var notDone = packs.Where(p => p.Status != "DONE").ToList();
        if (notDone.Any())
            return ServiceResult.BadRequest(new ApiError(
                $"Pack ที่ไม่อยู่ในสถานะ DONE: {string.Join(", ", notDone.Select(p => $"{p.PackingId}({p.Status})"))}"));

        var alreadyAssigned = packs.Where(p => p.SortingPalletId != null).ToList();
        if (alreadyAssigned.Any())
            return ServiceResult.BadRequest(new ApiError(
                $"Pack ถูก sort ไปแล้ว: {string.Join(", ", alreadyAssigned.Select(p => p.PackingId))}"));

        // หา station ว่าง (Enabled + ไม่มี pallet)
        var freeStation = await db.SortingStations
            .Where(s => s.Enabled && s.CurrentPalletId == null)
            .OrderBy(s => s.StationId)
            .FirstOrDefaultAsync();

        if (freeStation is null)
        {
            // Q8: queue ไว้ — simulator จะ assign เมื่อมี station ว่าง
            var queue = new SortingBatchQueue
            {
                CreatedBy = req.OperatorId,
                QueuedAt = DateTime.UtcNow,
                PackingIdsJson = JsonSerializer.Serialize(ids),
                Status = "WAITING",
            };
            db.SortingBatchQueues.Add(queue);
            await db.SaveChangesAsync();

            await hub.Clients.All.SendAsync("BatchQueued", new
            {
                queueId = queue.Id,
                batchSize = ids.Count,
            });

            return ServiceResult.Ok(new CreateSortingBatchResponse(
                Outcome: "QUEUED",
                StationId: null,
                PalletId: null,
                BatchSize: ids.Count,
                QueueId: queue.Id,
                Message: $"Station เต็ม — Batch ถูก queue ไว้ ({ids.Count} packs)"
            ));
        }

        // create pallet ผูกกับ station
        var pallet = await CreatePalletForBatchAsync(freeStation.StationId, ids, req.OperatorId);

        return ServiceResult.Ok(new CreateSortingBatchResponse(
            Outcome: "ASSIGNED",
            StationId: freeStation.StationId,
            PalletId: pallet.PalletId,
            BatchSize: ids.Count,
            QueueId: null,
            Message: $"Batch {ids.Count} packs → Station {freeStation.StationId} ({pallet.PalletId})"
        ));
    }

    // ── Internal helper: create pallet + queue packs ──────
    public async Task<SortingPallet> CreatePalletForBatchAsync(
        int stationId, List<string> packingIds, string operatorId)
    {
        var nextNo = await GetNextPalletNumberAsync();
        var palletId = $"SP-{nextNo:D3}";
        var now = DateTime.UtcNow;

        var pallet = new SortingPallet
        {
            PalletId = palletId,
            Status = "OPEN",
            CartonsCount = 0,
            MaxCapacity = packingIds.Count,
            StationId = stationId,
            CreatedBy = operatorId,
            CreatedAt = now,
        };
        db.SortingPallets.Add(pallet);

        // queue packs ทีละ 1 ต่อ 2 วินาที
        for (var i = 0; i < packingIds.Count; i++)
        {
            db.SortingPalletPacks.Add(new SortingPalletPack
            {
                PalletId = palletId,
                PackingId = packingIds[i],
                SequenceNo = i + 1,
                ScheduledAt = now.AddSeconds((i + 1) * TickIntervalSec),
                Status = "PENDING",
            });
        }

        // ผูก pallet กับ station
        var station = await db.SortingStations.FindAsync(stationId);
        if (station != null)
            station.CurrentPalletId = palletId;

        db.StationAuditLogs.Add(new StationAuditLog
        {
            StationId = stationId,
            Action = "ASSIGN",
            OperatorId = operatorId,
            PalletId = palletId,
            At = now,
        });

        await db.SaveChangesAsync();
        return pallet;
    }

    private async Task<int> GetNextPalletNumberAsync()
    {
        var existing = await db.SortingPallets
            .Where(s => s.PalletId.StartsWith("SP-"))
            .Select(s => s.PalletId)
            .ToListAsync();
        var usedNos = existing
            .Select(id =>
            {
                var idx = id.LastIndexOf('-');
                if (idx < 0) return 0;
                return int.TryParse(id[(idx + 1)..], out var n) ? n : 0;
            })
            .Where(n => n > 0)
            .ToHashSet();

        var next = 1;
        while (usedNos.Contains(next)) next++;
        return next;
    }
}
