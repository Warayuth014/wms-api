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

        if (pallet.Status != "PACKED" || pallet.Location != "ZONE_PACK")
            return ServiceResult.BadRequest(new ApiError(
                pallet.Status == "PACKED"
                    ? $"Pallet '{pid}' ยังไม่ได้ส่งมาที่ ZONE PACK (Location: {pallet.Location ?? "ไม่ระบุ"})"
                    : $"Pallet '{pid}' ไม่พร้อม Pack (สถานะ: {pallet.Status})"));

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

        // ดึง Part + จำนวนจาก PickOrderDetails (ของ Order นี้เท่านั้น)
        var orderDetails = await db.PickOrderDetails
            .Include(d => d.Part)
            .Where(d => d.PickOrderId == pickOrderId)
            .ToListAsync();

        // คำนวณจำนวนที่ pick มาจริงบน Pallet นี้ ต่อ Part
        // = PickOrderSubs ที่ PICKED + ReceiptLine ปลายทาง = Pallet นี้
        var pickedQtyPerPart = await db.PickOrderSubs
            .Include(s => s.PickOrderDetail)
            .Where(s => s.PickOrderDetail!.PickOrderId == pickOrderId
                     && s.Status == "PICKED")
            .GroupBy(s => s.PickOrderDetail!.PartId)
            .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.PickedQty) })
            .ToListAsync();

        // ถ้าหา picked qty ไม่ได้ ก็ fallback ใช้ ReceiptLines บน Pallet
        var palletQtyPerPart = await db.ReceiptLines
            .Where(l => l.PalletId == pack.PalletId
                     && l.Status == "PALLETIZED"
                     && l.QtyReceived > 0)
            .GroupBy(l => l.PartId)
            .Select(g => new { PartId = g.Key, Qty = g.Sum(l => l.QtyReceived) })
            .ToListAsync();

        var scans = await db.PackingPartScans
            .Where(s => s.PackingId == packingId && s.PickOrderId == pickOrderId)
            .GroupBy(s => s.PartId)
            .Select(g => new { PartId = g.Key, Total = g.Sum(x => x.ScannedQty) })
            .ToListAsync();

        // สแกนข้ามกล่อง: นับที่สแกนไปแล้วใน Pack อื่นของ Pallet เดียวกัน + Order เดียวกัน
        var siblingPackIds = await db.Packings
            .Where(p => p.PalletId == pack.PalletId
                     && p.PackingId != packingId
                     && p.Status == "DONE")
            .Select(p => p.PackingId)
            .ToListAsync();

        var scannedInOtherPacks = siblingPackIds.Count > 0
            ? await db.PackingPartScans
                .Where(s => siblingPackIds.Contains(s.PackingId) && s.PickOrderId == pickOrderId)
                .GroupBy(s => s.PartId)
                .Select(g => new { PartId = g.Key, Total = g.Sum(x => x.ScannedQty) })
                .ToListAsync()
            : [];

        var items = orderDetails.Select(d =>
        {
            // ใช้ ReservedQty จาก PickOrderDetail (จำนวนที่ pick สำเร็จจริง)
            var totalQty = d.ReservedQty > 0
                ? d.ReservedQty
                : palletQtyPerPart.FirstOrDefault(p => p.PartId == d.PartId)?.Qty ?? 0;

            // หักจำนวนที่ Pack ไปแล้วในกล่องก่อนหน้า
            var packedInOthers = scannedInOtherPacks
                .FirstOrDefault(s => s.PartId == d.PartId)?.Total ?? 0;
            var remainingQty = totalQty - packedInOthers;

            return new PackingPartItem(
                PartId: d.PartId,
                Owner: d.Part?.Owner ?? string.Empty,
                Brand: d.Part?.Brand ?? string.Empty,
                ItemDesc: d.Part?.ItemDesc ?? string.Empty,
                ImageUrl: d.Part?.ImageUrl,
                RequiredQty: remainingQty,
                ScannedQty: scans.FirstOrDefault(s => s.PartId == d.PartId)?.Total ?? 0
            );
        }).Where(i => i.RequiredQty > 0).ToList();

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

        // เช็คว่า Part นี้อยู่ใน Order นี้จริง + ดึง qty ที่ pick มา
        var orderDetail = await db.PickOrderDetails
            .FirstOrDefaultAsync(d => d.PickOrderId == req.PickOrderId && d.PartId == req.PartId);

        if (orderDetail is null)
            return ServiceResult.BadRequest(new ApiError(
                $"Order '{req.PickOrderId}' ไม่มี Part '{req.PartId}'"));

        var qtyRequired = orderDetail.ReservedQty > 0
            ? orderDetail.ReservedQty
            : await db.ReceiptLines
                .Where(l => l.PalletId == pack.PalletId
                         && l.PartId == req.PartId
                         && l.Status == "PALLETIZED"
                         && l.QtyReceived > 0)
                .SumAsync(l => (int?)l.QtyReceived) ?? 0;

        if (qtyRequired == 0)
            return ServiceResult.BadRequest(new ApiError(
                $"Order '{req.PickOrderId}' ไม่มี Part '{req.PartId}' ที่ต้อง pack"));

        // นับที่สแกนไปแล้วใน Pack นี้
        var scannedInThisPack = await db.PackingPartScans
            .Where(s => s.PackingId == req.PackingId
                     && s.PickOrderId == req.PickOrderId
                     && s.PartId == req.PartId)
            .SumAsync(s => (int?)s.ScannedQty) ?? 0;

        // นับที่ Pack ไปแล้วในกล่องก่อนหน้า (Order เดียวกัน, Pallet เดียวกัน)
        var otherPackIds = await db.Packings
            .Where(p => p.PalletId == pack.PalletId
                     && p.PackingId != req.PackingId
                     && p.Status == "DONE")
            .Select(p => p.PackingId)
            .ToListAsync();

        var scannedInOthers = otherPackIds.Count > 0
            ? await db.PackingPartScans
                .Where(s => otherPackIds.Contains(s.PackingId)
                         && s.PickOrderId == req.PickOrderId
                         && s.PartId == req.PartId)
                .SumAsync(s => (int?)s.ScannedQty) ?? 0
            : 0;

        var remainingForThisPack = qtyRequired - scannedInOthers;

        if (scannedInThisPack + req.Qty > remainingForThisPack)
            return ServiceResult.BadRequest(new ApiError(
                $"จำนวนเกิน — กล่องนี้ต้อง pack {remainingForThisPack}, สแกนแล้ว {scannedInThisPack}"));

        db.PackingPartScans.Add(new PackingPartScan
        {
            PackingId = req.PackingId,
            PickOrderId = req.PickOrderId,
            PartId = req.PartId,
            ScannedQty = req.Qty,
            ScannedBy = req.OperatorId,
            ScannedAt = DateTime.UtcNow,
        });

        // เช็คว่า Part ทุกตัวใน Order นี้สแกนครบหรือยัง (นับข้ามกล่อง)
        var orderParts = await db.PickOrderDetails
            .Where(d => d.PickOrderId == req.PickOrderId && d.ReservedQty > 0)
            .Select(d => new { d.PartId, Qty = d.ReservedQty })
            .ToListAsync();

        // รวม scan ทุก Pack ของ Order นี้บน Pallet เดียวกัน
        var allPackIdsOnPallet = await db.Packings
            .Where(p => p.PalletId == pack.PalletId)
            .Select(p => p.PackingId)
            .ToListAsync();

        var allScansAcrossPacks = await db.PackingPartScans
            .Where(s => allPackIdsOnPallet.Contains(s.PackingId) && s.PickOrderId == req.PickOrderId)
            .ToListAsync();

        // เพิ่ม scan ใหม่เข้า set จำลอง (ยังไม่ Save)
        var simulated = allScansAcrossPacks.Concat(new[] { new PackingPartScan
        {
            PartId = req.PartId, ScannedQty = req.Qty,
        }}).GroupBy(s => s.PartId)
           .ToDictionary(g => g.Key, g => g.Sum(x => x.ScannedQty));

        var orderComplete = orderParts.All(p =>
            simulated.GetValueOrDefault(p.PartId, 0) >= p.Qty);

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

        // เช็คว่า Pallet นี้มี Pack ค้างอยู่อีกไหม → ถ้าหมดแล้ว Pallet เปล่า → AVAILABLE
        bool palletReleased = false;
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
                // ของออกจาก Pallet ไปอยู่ในกล่องหมดแล้ว → Pallet เปล่าพร้อมใช้ใหม่
                pallet.Status = "AVAILABLE";
                pallet.Location = null;
                pallet.Type = null;
                pallet.TrackingId = null;
                pallet.UpdatedAt = completedAt;
                palletReleased = true;

                // เคลียร์ ReceiptLines เก่า → PACKED เพื่อให้ Pallet รับของใหม่ได้
                var oldLines = await db.ReceiptLines
                    .Where(l => l.PalletId == pack.PalletId && l.Status == "PALLETIZED")
                    .ToListAsync();

                foreach (var line in oldLines)
                {
                    line.Status = "PACKED";
                    line.UpdatedAt = completedAt;
                }
            }
        }

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new ConfirmPackResponse(
            PackingId: pack.PackingId,
            Status: pack.Status,
            TrackingId: trackingId,
            PalletShipped: palletReleased,
            CompletedAt: completedAt,
            Message: palletReleased
                ? $"Pack สำเร็จ — Pallet เปล่าพร้อมใช้ใหม่"
                : $"Pack สำเร็จ (ยังเหลือ Pack อื่นใน Pallet)"
        ));
    }

    public async Task<ServiceResult> SplitPackAsync(SplitPackRequest req)
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
            return ServiceResult.BadRequest(new ApiError("Pack นี้ปิดแล้ว ไม่สามารถแบ่งกล่องได้"));

        // ต้องมีของสแกนอย่างน้อย 1 ชิ้นในกล่องนี้
        var scannedInThisPack = await db.PackingPartScans
            .Where(s => s.PackingId == req.PackingId)
            .SumAsync(s => (int?)s.ScannedQty) ?? 0;

        if (scannedInThisPack == 0)
            return ServiceResult.BadRequest(new ApiError("ยังไม่ได้สแกนของใส่กล่องนี้เลย"));

        // เช็คว่ายังมีของเหลืออีกไหม
        var orderIds = pack.Details.Select(d => d.PickOrderId).ToList();
        var requiredParts = await db.PickOrderDetails
            .Where(d => orderIds.Contains(d.PickOrderId) && d.ReservedQty > 0)
            .Select(d => new { d.PickOrderId, d.PartId, d.ReservedQty })
            .ToListAsync();

        // รวม scan ทุก Pack ของ Pallet นี้
        var allPackIds = await db.Packings
            .Where(p => p.PalletId == pack.PalletId)
            .Select(p => p.PackingId)
            .ToListAsync();

        var totalScanned = await db.PackingPartScans
            .Where(s => allPackIds.Contains(s.PackingId)
                     && orderIds.Contains(s.PickOrderId))
            .GroupBy(s => new { s.PickOrderId, s.PartId })
            .Select(g => new { g.Key.PickOrderId, g.Key.PartId, Total = g.Sum(x => x.ScannedQty) })
            .ToListAsync();

        var totalRequired = requiredParts.Sum(p => p.ReservedQty);
        var totalDone = totalScanned.Sum(s => s.Total);
        var remaining = totalRequired - totalDone;

        if (remaining <= 0)
            return ServiceResult.BadRequest(new ApiError("ของครบแล้ว ไม่ต้องแบ่งกล่อง"));

        // ── ปิดกล่องปัจจุบัน ──
        var now = DateTime.UtcNow;
        pack.Status = "DONE";
        pack.CompletedAt = now;

        // Mark PackingDetails as DONE (เฉพาะ Order ที่ Part ครบข้ามกล่องแล้ว)
        foreach (var detail in pack.Details)
        {
            // เช็คว่า Order นี้ Part ครบทุกตัวรึยัง (นับข้ามกล่อง)
            var partsForOrder = requiredParts.Where(p => p.PickOrderId == detail.PickOrderId).ToList();
            var allDone = partsForOrder.All(p =>
            {
                var scanned = totalScanned
                    .FirstOrDefault(s => s.PickOrderId == p.PickOrderId && s.PartId == p.PartId)?.Total ?? 0;
                return scanned >= p.ReservedQty;
            });

            detail.Status = "DONE";
            detail.CompletedAt = now;
        }

        // ── สร้างกล่องใหม่ ──
        var beYear = now.Year + 543;
        var prefix = $"PK-{now:ddMM}{beYear}";
        var todayCount = await db.Packings.CountAsync(p => p.PackingId.StartsWith(prefix));
        var newPackingId = $"{prefix}-{(todayCount + 1):D3}";

        var newPack = new Models.Packing
        {
            PackingId = newPackingId,
            PalletId = pack.PalletId,
            PickOrderId = pack.PickOrderId,
            Status = "OPEN",
            CreatedBy = req.OperatorId,
            CreatedAt = now,
        };
        db.Packings.Add(newPack);

        // เพิ่ม PackingDetail สำหรับ Order ที่ยังมีของเหลือ
        foreach (var detail in pack.Details)
        {
            var partsForOrder = requiredParts.Where(p => p.PickOrderId == detail.PickOrderId).ToList();
            var hasRemaining = partsForOrder.Any(p =>
            {
                var scanned = totalScanned
                    .FirstOrDefault(s => s.PickOrderId == p.PickOrderId && s.PartId == p.PartId)?.Total ?? 0;
                return scanned < p.ReservedQty;
            });

            if (hasRemaining)
            {
                db.PackingDetails.Add(new PackingDetail
                {
                    PackingId = newPackingId,
                    PickOrderId = detail.PickOrderId,
                    Status = "PENDING",
                });
            }
        }

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new SplitPackResponse(
            ClosedPackingId: pack.PackingId,
            NewPackingId: newPackingId,
            ItemsInClosedPack: scannedInThisPack,
            RemainingItems: remaining,
            Message: $"ปิดกล่อง {pack.PackingId} ({scannedInThisPack} ชิ้น) → เปิดกล่องใหม่ {newPackingId} (เหลือ {remaining} ชิ้น)"
        ));
    }
}
