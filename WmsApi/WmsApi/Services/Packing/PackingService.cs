using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Packing;

public class PackingService(WmsDbContext db) : IPackingService
{
    public async Task<ServiceResult> ScanPalletAsync(string palletId)
    {
        if (string.IsNullOrWhiteSpace(palletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));

        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pallet '{palletId}'"));

        if (pallet.Status != "PACKED")
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{palletId}' ไม่พร้อมสำหรับ Pack (สถานะ: {pallet.Status})"));

        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == palletId && l.QtyReceived > 0)
            .ToListAsync();

        if (lines.Count == 0)
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{palletId}' ไม่มีสินค้า"));

        var pickOrderId = await db.PickOrderSubs
            .Include(s => s.ReceiptLine)
            .Include(s => s.PickOrderDetail)
            .Where(s => s.ReceiptLine!.PalletId == palletId)
            .Select(s => s.PickOrderDetail!.PickOrderId)
            .FirstOrDefaultAsync();

        var items = lines.Select(l => new PackingItem(
            PartId: l.PartId,
            Owner: l.Part?.Owner ?? string.Empty,
            Brand: l.Part?.Brand ?? string.Empty,
            ItemDesc: l.Part?.ItemDesc ?? string.Empty,
            ImageUrl: l.Part?.ImageUrl,
            LotNumber: l.LotNumber,
            Qty: l.QtyReceived,
            Condition: l.Condition
        )).ToList();

        return ServiceResult.Ok(new PackingScanResponse(
            PalletId: pallet.PalletId,
            Status: pallet.Status,
            Location: pallet.Location,
            PickOrderId: pickOrderId,
            Items: items,
            Message: $"พร้อม Pack ({items.Count} รายการ)"
        ));
    }

    public async Task<ServiceResult> ConfirmPackAsync(ConfirmPackRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pallet '{req.PalletId}'"));

        if (pallet.Status != "PACKED")
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่พร้อม Confirm Pack (สถานะ: {pallet.Status})"));

        // Mock SHIP-X tracking ID
        var trackingId = $"TRK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        var packedAt = DateTime.UtcNow;

        pallet.Status = "SHIPPED";
        pallet.Location = "ZONE_SORT";
        pallet.TrackingId = trackingId;
        pallet.UpdatedAt = packedAt;

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new ConfirmPackResponse(
            PalletId: pallet.PalletId,
            TrackingId: trackingId,
            Status: pallet.Status,
            PackedAt: packedAt,
            Message: $"Pack สำเร็จ Tracking: {trackingId}"
        ));
    }
}
