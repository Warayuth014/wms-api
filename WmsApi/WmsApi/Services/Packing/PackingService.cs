using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Packing;

public class PackingService(WmsDbContext db) : IPackingService
{
    public async Task<ServiceResult> ScanPalletAsync(string palletId)
    {
        if (string.IsNullOrWhiteSpace(palletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));

        var pid = palletId.Trim().ToUpper();
        var pallet = await db.Pallets.FindAsync(pid);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pallet '{pid}'"));

        if (pallet.Status != "PACKING" && pallet.Status != "PACKED")
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{pid}' ไม่พร้อม Pack (สถานะ: {pallet.Status})"));

        var packs = await db.Packings
            .Where(p => p.PalletId == pid)
            .Include(p => p.Details)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        if (packs.Count == 0)
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{pid}' ยังไม่มี Packing"));

        var summaries = packs.Select(p => new PackingSummary(
            PackingId: p.PackingId,
            Status: p.Status,
            CreatedAt: p.CreatedAt,
            CompletedAt: p.CompletedAt,
            OrderCount: p.Details.Count,
            OrderDoneCount: p.Details.Count(d => d.Status == "DONE")
        )).ToList();

        return ServiceResult.Ok(new PackingPalletResponse(
            PalletId: pallet.PalletId,
            Status: pallet.Status,
            Location: pallet.Location,
            Packs: summaries,
            Message: $"พบ {packs.Count} Pack"
        ));
    }

    public async Task<ServiceResult> GetPackAsync(string packingId)
    {
        var pack = await db.Packings
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.PackingId == packingId);

        if (pack is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pack '{packingId}'"));

        var orderIds = pack.Details.Select(d => d.PickOrderId).ToList();

        // โหลด required + scanned ของแต่ละ order ทีเดียว
        var requiredByOrder = await db.PickOrderDetails
            .Where(d => orderIds.Contains(d.PickOrderId))
            .GroupBy(d => d.PickOrderId)
            .Select(g => new { OrderId = g.Key, PartCount = g.Count() })
            .ToListAsync();

        var scannedByOrder = await db.PackingPartScans
            .Where(s => s.PackingId == packingId)
            .GroupBy(s => new { s.PickOrderId, s.PartId })
            .Select(g => new
            {
                g.Key.PickOrderId,
                g.Key.PartId,
                Total = g.Sum(x => x.ScannedQty),
            })
            .ToListAsync();

        // ดึง required qty ทุก part เพื่อเทียบ "ครบ" จริง
        var requiredParts = await db.PickOrderDetails
            .Where(d => orderIds.Contains(d.PickOrderId))
            .Select(d => new { d.PickOrderId, d.PartId, d.RequiredQty })
            .ToListAsync();

        var orderSummaries = pack.Details
            .OrderBy(d => d.PickOrderId)
            .Select(d =>
            {
                var partTotal = requiredByOrder
                    .FirstOrDefault(x => x.OrderId == d.PickOrderId)?.PartCount ?? 0;

                var doneCount = requiredParts
                    .Where(rp => rp.PickOrderId == d.PickOrderId)
                    .Count(rp =>
                    {
                        var scanned = scannedByOrder
                            .FirstOrDefault(s => s.PickOrderId == rp.PickOrderId
                                              && s.PartId == rp.PartId)?.Total ?? 0;
                        return scanned >= rp.RequiredQty;
                    });

                return new PackingOrderSummary(
                    PickOrderId: d.PickOrderId,
                    Status: d.Status,
                    PartCount: partTotal,
                    PartDoneCount: doneCount
                );
            }).ToList();

        return ServiceResult.Ok(new PackingDetailResponse(
            PackingId: pack.PackingId,
            PalletId: pack.PalletId,
            Status: pack.Status,
            CreatedAt: pack.CreatedAt,
            CompletedAt: pack.CompletedAt,
            TrackingId: pack.TrackingId,
            Orders: orderSummaries
        ));
    }

    public async Task<ServiceResult> GetOrderAsync(string packingId, string pickOrderId)
    {
        var pack = await db.Packings
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.PackingId == packingId);

        if (pack is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pack '{packingId}'"));

        var detail = pack.Details.FirstOrDefault(d => d.PickOrderId == pickOrderId);
        if (detail is null)
            return ServiceResult.NotFound(new ApiError(
                $"Pack นี้ไม่มี Order '{pickOrderId}'"));

        var parts = await db.PickOrderDetails
            .Include(d => d.Part)
            .Where(d => d.PickOrderId == pickOrderId)
            .ToListAsync();

        var scans = await db.PackingPartScans
            .Where(s => s.PackingId == packingId && s.PickOrderId == pickOrderId)
            .GroupBy(s => s.PartId)
            .Select(g => new { PartId = g.Key, Total = g.Sum(x => x.ScannedQty) })
            .ToListAsync();

        var items = parts.Select(p => new PackingPartItem(
            PartId: p.PartId,
            Owner: p.Part?.Owner ?? string.Empty,
            Brand: p.Part?.Brand ?? string.Empty,
            ItemDesc: p.Part?.ItemDesc ?? string.Empty,
            ImageUrl: p.Part?.ImageUrl,
            RequiredQty: p.RequiredQty,
            ScannedQty: scans.FirstOrDefault(s => s.PartId == p.PartId)?.Total ?? 0
        )).ToList();

        return ServiceResult.Ok(new PackingOrderResponse(
            PackingId: packingId,
            PickOrderId: pickOrderId,
            Status: detail.Status,
            Parts: items
        ));
    }

    public async Task<ServiceResult> ScanPartAsync(ScanPackPartRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PackingId)
            || string.IsNullOrWhiteSpace(req.PickOrderId)
            || string.IsNullOrWhiteSpace(req.PartId))
            return ServiceResult.BadRequest(new ApiError("ข้อมูลไม่ครบ"));

        if (req.Qty <= 0)
            return ServiceResult.BadRequest(new ApiError("จำนวนต้องมากกว่า 0"));

        var pack = await db.Packings
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.PackingId == req.PackingId);

        if (pack is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pack '{req.PackingId}'"));

        if (pack.Status != "OPEN")
            return ServiceResult.BadRequest(new ApiError("Pack นี้ปิดแล้ว"));

        var detail = pack.Details.FirstOrDefault(d => d.PickOrderId == req.PickOrderId);
        if (detail is null)
            return ServiceResult.NotFound(new ApiError(
                $"Pack นี้ไม่มี Order '{req.PickOrderId}'"));

        var orderPart = await db.PickOrderDetails
            .FirstOrDefaultAsync(d => d.PickOrderId == req.PickOrderId
                                   && d.PartId == req.PartId);

        if (orderPart is null)
            return ServiceResult.BadRequest(new ApiError(
                $"Order '{req.PickOrderId}' ไม่มี Part '{req.PartId}'"));

        var alreadyScanned = await db.PackingPartScans
            .Where(s => s.PackingId == req.PackingId
                     && s.PickOrderId == req.PickOrderId
                     && s.PartId == req.PartId)
            .SumAsync(s => (int?)s.ScannedQty) ?? 0;

        if (alreadyScanned + req.Qty > orderPart.RequiredQty)
            return ServiceResult.BadRequest(new ApiError(
                $"จำนวนเกิน — ต้องการ {orderPart.RequiredQty}, สแกนแล้ว {alreadyScanned}"));

        db.PackingPartScans.Add(new PackingPartScan
        {
            PackingId = req.PackingId,
            PickOrderId = req.PickOrderId,
            PartId = req.PartId,
            ScannedQty = req.Qty,
            ScannedBy = req.OperatorId,
            ScannedAt = DateTime.UtcNow,
        });

        // เช็คว่า Order นี้ครบทุก Part หรือยัง → mark DONE
        var orderParts = await db.PickOrderDetails
            .Where(d => d.PickOrderId == req.PickOrderId)
            .ToListAsync();

        var allScans = await db.PackingPartScans
            .Where(s => s.PackingId == req.PackingId && s.PickOrderId == req.PickOrderId)
            .ToListAsync();

        // เพิ่ม scan ใหม่เข้า set จำลอง (ยังไม่ Save)
        var simulated = allScans.Concat(new[] { new PackingPartScan
        {
            PartId = req.PartId, ScannedQty = req.Qty,
        }}).GroupBy(s => s.PartId)
           .ToDictionary(g => g.Key, g => g.Sum(x => x.ScannedQty));

        var orderComplete = orderParts.All(op =>
            simulated.GetValueOrDefault(op.PartId, 0) >= op.RequiredQty);

        if (orderComplete && detail.Status != "DONE")
        {
            detail.Status = "DONE";
            detail.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // คืนข้อมูล Order ที่อัปเดตแล้ว
        return await GetOrderAsync(req.PackingId, req.PickOrderId);
    }

    public async Task<ServiceResult> ConfirmPackAsync(ConfirmPackRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PackingId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Packing ID"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        var pack = await db.Packings
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.PackingId == req.PackingId);

        if (pack is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pack '{req.PackingId}'"));

        if (pack.Status != "OPEN")
            return ServiceResult.BadRequest(new ApiError("Pack นี้ปิดแล้ว"));

        if (pack.Details.Any(d => d.Status != "DONE"))
            return ServiceResult.BadRequest(new ApiError(
                "ยัง pack ไม่ครบทุก Order ใน Pack นี้"));

        var trackingId = $"TRK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        var completedAt = DateTime.UtcNow;

        pack.Status = "DONE";
        pack.CompletedAt = completedAt;
        pack.TrackingId = trackingId;

        // เช็คว่า Pallet นี้มี Pack ค้างอยู่อีกไหม → ถ้าหมดแล้ว auto-ship
        bool palletShipped = false;
        var siblingPacksOpen = await db.Packings
            .Where(p => p.PalletId == pack.PalletId
                     && p.PackingId != pack.PackingId
                     && p.Status != "DONE")
            .AnyAsync();

        if (!siblingPacksOpen)
        {
            var pallet = await db.Pallets.FindAsync(pack.PalletId);
            if (pallet != null)
            {
                pallet.Status = "SHIPPED";
                pallet.Location = "ZONE_SORT";
                pallet.TrackingId = trackingId;
                pallet.UpdatedAt = completedAt;
                palletShipped = true;
            }
        }

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new ConfirmPackResponse(
            PackingId: pack.PackingId,
            Status: pack.Status,
            TrackingId: trackingId,
            PalletShipped: palletShipped,
            CompletedAt: completedAt,
            Message: palletShipped
                ? $"Pack สำเร็จ + Pallet ส่งไป Sort แล้ว"
                : $"Pack สำเร็จ (ยังเหลือ Pack อื่นใน Pallet)"
        ));
    }
}
