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
            .Where(p => p.PalletId == pid && p.Status == "OPEN")
            .Include(p => p.Details)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        if (packs.Count == 0)
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{pid}' ไม่มี Pack ที่ยังเปิดอยู่"));

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
        // Pack ที่ "ปิดแล้ว" = DONE / STAGED / SHIPPED
        var siblingPackIds = await db.Packings
            .Where(p => p.PalletId == pack.PalletId
                     && p.PackingId != packingId
                     && p.Status != "OPEN")
            .Select(p => p.PackingId)
            .ToListAsync();

        var scannedInOtherPacks = siblingPackIds.Count > 0
            ? await db.PackingPartScans
                .Where(s => siblingPackIds.Contains(s.PackingId) && s.PickOrderId == pickOrderId)
                .GroupBy(s => s.PartId)
                .Select(g => new { PartId = g.Key, Total = g.Sum(x => x.ScannedQty) })
                .ToListAsync()
            : [];

        // ดึง serials ที่พร้อม pack บน Pallet นี้ (ยังไม่โดน pack/ship) group ตาม PartId
        var availableSerials = await db.PartSerials
            .Where(s => s.PalletId == pack.PalletId
                     && s.Status != "PACKED"
                     && s.Status != "SHIPPED")
            .OrderBy(s => s.SerialNo)
            .GroupBy(s => s.PartId)
            .Select(g => new { PartId = g.Key, Serials = g.Select(x => x.SerialNo).ToList() })
            .ToListAsync();

        var items = orderDetails.Select(d =>
        {
            // ใช้จำนวนที่อยู่บน Pallet นี้จริง (ReceiptLines ปลายทาง) เป็นหลัก
            // เพราะ Order เดียวอาจถูก Pick แยกไปหลาย Pallet ปลายทาง
            var totalQty = palletQtyPerPart.FirstOrDefault(p => p.PartId == d.PartId)?.Qty ?? 0;

            // หักจำนวนที่ Pack ไปแล้วในกล่องก่อนหน้า
            var packedInOthers = scannedInOtherPacks
                .FirstOrDefault(s => s.PartId == d.PartId)?.Total ?? 0;
            var remainingQty = totalQty - packedInOthers;

            var serials = availableSerials
                .FirstOrDefault(s => s.PartId == d.PartId)?.Serials ?? new List<string>();

            return new PackingPartItem(
                PartId: d.PartId,
                Owner: d.Part?.Owner ?? string.Empty,
                Brand: d.Part?.Brand ?? string.Empty,
                ItemDesc: d.Part?.ItemDesc ?? string.Empty,
                ImageUrl: d.Part?.ImageUrl,
                RequiredQty: remainingQty,
                ScannedQty: scans.FirstOrDefault(s => s.PartId == d.PartId)?.Total ?? 0,
                AvailableSerials: serials
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
        // Pack ที่ "ปิดแล้ว" = DONE / STAGED / SHIPPED
        var otherPackIds = await db.Packings
            .Where(p => p.PalletId == pack.PalletId
                     && p.PackingId != req.PackingId
                     && p.Status != "OPEN")
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

        // ── Bind Serial Numbers (if provided) ──
        if (req.SerialNumbers is { Count: > 0 })
        {
            if (req.SerialNumbers.Count != req.Qty)
                return ServiceResult.BadRequest(new ApiError(
                    $"จำนวน S/N ({req.SerialNumbers.Count}) ไม่ตรงกับ Qty ({req.Qty})"));

            if (req.SerialNumbers.Distinct().Count() != req.SerialNumbers.Count)
                return ServiceResult.BadRequest(new ApiError("S/N ซ้ำกันในคำขอ"));

            var serials = await db.PartSerials
                .Where(s => s.PartId == req.PartId
                         && req.SerialNumbers.Contains(s.SerialNo))
                .ToListAsync();

            if (serials.Count != req.SerialNumbers.Count)
            {
                var foundSet = serials.Select(s => s.SerialNo).ToHashSet();
                var missing = req.SerialNumbers.Where(sn => !foundSet.Contains(sn)).ToList();
                return ServiceResult.BadRequest(new ApiError(
                    $"ไม่พบ S/N: {string.Join(", ", missing)}"));
            }

            var alreadyPacked = serials.Where(s => s.Status == "PACKED" || s.Status == "SHIPPED").ToList();
            if (alreadyPacked.Count > 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ถูก pack ไปแล้ว: {string.Join(", ", alreadyPacked.Select(s => s.SerialNo))}"));

            var wrongPallet = serials.Where(s => s.PalletId != pack.PalletId).ToList();
            if (wrongPallet.Count > 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ไม่ได้อยู่บน Pallet '{pack.PalletId}': {string.Join(", ", wrongPallet.Select(s => s.SerialNo))}"));

            var now = DateTime.UtcNow;
            foreach (var s in serials)
            {
                s.Status = "PACKED";
                s.PackingId = req.PackingId;
                s.PackedAt = now;
                s.UpdatedAt = now;
            }
        }

        // เช็คว่า Part ที่อยู่บน Pallet นี้ (ของ Order นี้) สแกนครบหรือยัง
        // ไม่ใช้ ReservedQty เพราะ Order อาจกระจายหลาย Pallet → Pack นี้รับผิดชอบเฉพาะของบน Pallet นี้
        var partsOnThisPallet = await db.ReceiptLines
            .Where(l => l.PalletId == pack.PalletId
                     && l.Status == "PALLETIZED"
                     && l.QtyReceived > 0)
            .GroupBy(l => l.PartId)
            .Select(g => new { PartId = g.Key, Qty = g.Sum(l => l.QtyReceived) })
            .ToListAsync();

        // filter เอาเฉพาะ Part ที่อยู่ใน Order นี้ (intersect กับ PickOrderDetails)
        var orderPartIds = await db.PickOrderDetails
            .Where(d => d.PickOrderId == req.PickOrderId && d.ReservedQty > 0)
            .Select(d => d.PartId)
            .ToListAsync();

        var requiredOnPallet = partsOnThisPallet
            .Where(p => orderPartIds.Contains(p.PartId))
            .ToList();

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

        var packComplete = requiredOnPallet.All(p =>
            simulated.GetValueOrDefault(p.PartId, 0) >= p.Qty);

        if (packComplete && detail.Status != "DONE")
        {
            detail.Status = "DONE";
            detail.CompletedAt = DateTime.UtcNow;
        }

        // ถ้าทุก Detail ใน Pack นี้ DONE แล้ว → auto-finalize (ไม่ต้องรอกด confirm)
        bool packFinalized = false;
        string? finalizedTrackingId = null;
        bool palletReleased = false;

        if (pack.Details.All(d => d.Status == "DONE"))
        {
            (finalizedTrackingId, palletReleased, _) = await FinalizePackInternalAsync(pack);
            packFinalized = true;
        }

        await db.SaveChangesAsync();

        // คืนข้อมูล Order ที่อัปเดตแล้ว + finalize info (ถ้า Pack เพิ่งปิด)
        var orderResult = await GetOrderAsync(req.PackingId, req.PickOrderId);
        if (packFinalized && orderResult.StatusCode == 200 && orderResult.Payload is PackingOrderResponse baseResp)
        {
            return ServiceResult.Ok(baseResp with
            {
                PackFinalized = true,
                TrackingId = finalizedTrackingId,
                PalletReleased = palletReleased,
            });
        }

        return orderResult;
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

        var (trackingId, palletReleased, completedAt) = await FinalizePackInternalAsync(pack);
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

    // Finalize = Pack → DONE + gen TrackingId + release Pallet ถ้าหมด
    // ไม่เรียก SaveChanges — ให้ caller จัดการ
    private async Task<(string trackingId, bool palletReleased, DateTime completedAt)>
        FinalizePackInternalAsync(Models.Packing pack)
    {
        var trackingId = $"TRK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        var completedAt = DateTime.UtcNow;

        pack.Status = "DONE";
        pack.CompletedAt = completedAt;
        pack.TrackingId = trackingId;

        var siblingPacksOpen = await db.Packings
            .Where(p => p.PalletId == pack.PalletId
                     && p.PackingId != pack.PackingId
                     && p.Status == "OPEN")
            .AnyAsync();

        bool palletReleased = false;
        if (!siblingPacksOpen)
        {
            var pallet = await db.Pallets.FindAsync(pack.PalletId);
            if (pallet != null)
            {
                pallet.Status = "AVAILABLE";
                pallet.Location = null;
                pallet.Type = null;
                pallet.TrackingId = null;
                pallet.UpdatedAt = completedAt;
                palletReleased = true;

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

        return (trackingId, palletReleased, completedAt);
    }

}
