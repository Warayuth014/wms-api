using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.Hubs;

namespace WmsApi.Services.Sorting;

/// <summary>
/// Background service ที่ tick ทุก ~1 วินาที เพื่อ:
///   1) ประมวลผล SortingPalletPack ที่ Status=PENDING และถึง ScheduledAt
///      → set Pack.Status = SORTED + เพิ่ม CartonsCount + push SignalR
///      → ถ้า pallet เต็ม → mark FULL + push StationFull
///   2) ประมวลผล SortingBatchQueue ที่ WAITING + มี station ว่าง
///      → assign + push BatchAssigned
/// </summary>
public class SortingFlowSimulator(
    IServiceScopeFactory scopeFactory,
    IHubContext<SortingHub> hub,
    ILogger<SortingFlowSimulator> logger) : BackgroundService
{
    private const int LoopIntervalMs = 1000;   // tick ทุก 1s ความ resolution พอ

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("SortingFlowSimulator started (tick {Ms} ms)", LoopIntervalMs);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessTickAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SortingFlowSimulator tick failed");
            }

            try { await Task.Delay(LoopIntervalMs, ct); }
            catch (TaskCanceledException) { break; }
        }

        logger.LogInformation("SortingFlowSimulator stopped");
    }

    private async Task ProcessTickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        var sorting = scope.ServiceProvider.GetRequiredService<ISortingService>();

        var now = DateTime.UtcNow;

        // ── 1) Process pending pack queue ────────────────────────
        var due = await db.SortingPalletPacks
            .Where(q => q.Status == "PENDING" && q.ScheduledAt <= now)
            .OrderBy(q => q.ScheduledAt).ThenBy(q => q.SequenceNo)
            .Take(20)
            .ToListAsync(ct);

        foreach (var item in due)
        {
            var pack = await db.Packings.FirstOrDefaultAsync(p => p.PackingId == item.PackingId, ct);
            var pallet = await db.SortingPallets.FirstOrDefaultAsync(p => p.PalletId == item.PalletId, ct);

            if (pack is null || pallet is null)
            {
                item.Status = "CANCELLED";
                item.ProcessedAt = now;
                continue;
            }

            // skip ถ้า pack ถูก sort ไปแล้ว (race condition)
            if (pack.Status != "DONE")
            {
                item.Status = "CANCELLED";
                item.ProcessedAt = now;
                continue;
            }

            pack.Status = "SORTED";
            pack.SortingPalletId = pallet.PalletId;
            pack.WeightGram = Random.Shared.Next(500, 20001);
            pack.SortedAt = now;

            pallet.CartonsCount += 1;
            item.Status = "PROCESSED";
            item.ProcessedAt = now;

            var becameFull = pallet.CartonsCount >= pallet.MaxCapacity;
            if (becameFull)
            {
                pallet.Status = "FULL";
                pallet.SealedAt = now;
            }

            await db.SaveChangesAsync(ct);

            await hub.Clients.All.SendAsync("StationCounterUpdated", new
            {
                stationId = pallet.StationId,
                palletId = pallet.PalletId,
                packingId = pack.PackingId,
                current = pallet.CartonsCount,
                total = pallet.MaxCapacity,
                isFull = becameFull,
            }, ct);

            if (becameFull)
            {
                await hub.Clients.All.SendAsync("StationFull", new
                {
                    stationId = pallet.StationId,
                    palletId = pallet.PalletId,
                    cartonsCount = pallet.CartonsCount,
                }, ct);
            }
        }

        // ── 2) Assign queued batches to free stations ────────────
        var waiting = await db.SortingBatchQueues
            .Where(b => b.Status == "WAITING")
            .OrderBy(b => b.QueuedAt)
            .ToListAsync(ct);

        foreach (var batch in waiting)
        {
            var freeStation = await db.SortingStations
                .Where(s => s.Enabled && s.CurrentPalletId == null)
                .OrderBy(s => s.StationId)
                .FirstOrDefaultAsync(ct);

            if (freeStation is null) break;   // ไม่มี station ว่าง — รอรอบถัดไป

            var ids = JsonSerializer.Deserialize<List<string>>(batch.PackingIdsJson) ?? new();
            if (ids.Count == 0)
            {
                batch.Status = "CANCELLED";
                batch.AssignedAt = now;
                await db.SaveChangesAsync(ct);
                continue;
            }

            // re-check pack ทั้งหมดยังว่างไหม
            var packs = await db.Packings
                .Where(p => ids.Contains(p.PackingId))
                .ToListAsync(ct);

            if (packs.Any(p => p.Status != "DONE" || p.SortingPalletId != null))
            {
                logger.LogWarning("Batch {Id} has invalid packs — cancelled", batch.Id);
                batch.Status = "CANCELLED";
                batch.AssignedAt = now;
                await db.SaveChangesAsync(ct);
                continue;
            }

            var pallet = await sorting.CreatePalletForBatchAsync(
                freeStation.StationId, ids, batch.CreatedBy);

            batch.Status = "ASSIGNED";
            batch.AssignedAt = DateTime.UtcNow;
            batch.AssignedPalletId = pallet.PalletId;
            await db.SaveChangesAsync(ct);

            await hub.Clients.All.SendAsync("BatchAssigned", new
            {
                queueId = batch.Id,
                stationId = freeStation.StationId,
                palletId = pallet.PalletId,
                batchSize = ids.Count,
            }, ct);
        }
    }
}
