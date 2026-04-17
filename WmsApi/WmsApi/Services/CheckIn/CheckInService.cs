using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.CheckIn;

public class CheckInService(WmsDbContext db) : ICheckInService
{
    // ── 1) สแกน Carton (PackingId) → assign Slot ตาม Owner ───────
    public async Task<ServiceResult> ScanCartonAsync(ScanCheckInRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PackingId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Packing ID"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        var packingId = req.PackingId.Trim().ToUpper();

        var pack = await db.Packings
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.PackingId == packingId);

        if (pack is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pack '{packingId}'"));

        if (pack.Status != "DONE")
            return ServiceResult.BadRequest(new ApiError(
                $"Pack '{packingId}' ยัง Pack ไม่เสร็จ (สถานะ: {pack.Status})"));

        // เช็คว่า Carton นี้ยัง check-in ไม่แล้ว
        var existing = await db.CheckInEntries
            .Include(e => e.Slot)
            .FirstOrDefaultAsync(e => e.PackingId == packingId);

        if (existing is not null)
            return ServiceResult.BadRequest(new ApiError(
                $"Pack '{packingId}' ถูก check-in แล้วที่ช่อง '{existing.SlotId}'"));

        // หา Owner จาก Part ของ Order ใน Pack นี้
        var orderIds = pack.Details.Select(d => d.PickOrderId).ToList();
        var owners = await db.PickOrderDetails
            .Where(d => orderIds.Contains(d.PickOrderId))
            .Join(db.Parts, d => d.PartId, p => p.PartId, (d, p) => p.Owner)
            .Distinct()
            .ToListAsync();

        if (owners.Count == 0)
            return ServiceResult.BadRequest(new ApiError(
                $"ไม่พบข้อมูล Owner ของ Pack '{packingId}'"));

        // ในเคสที่ Pack เดียวมีหลาย Owner → ใช้ตัวแรก (ปกติเป็น Owner เดียว)
        var owner = owners.First();

        // หา Slot ที่ยังเปิดอยู่ของ Owner นี้ (OPEN หรือ READY)
        var slot = await db.CheckInSlots
            .FirstOrDefaultAsync(s => s.Owner == owner && s.Status == "OPEN");

        if (slot is null)
        {
            // สร้าง Slot ใหม่ — หาหมายเลขถัดไป
            var nextNo = await GetNextSlotNumberAsync();
            slot = new CheckInSlot
            {
                SlotId = $"SLOT-{nextNo:D2}",
                Owner = owner,
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow,
            };
            db.CheckInSlots.Add(slot);
        }

        // เพิ่ม CheckInEntry
        db.CheckInEntries.Add(new CheckInEntry
        {
            SlotId = slot.SlotId,
            PackingId = packingId,
            Owner = owner,
            Status = "CHECKED_IN",
            ScannedBy = req.OperatorId,
            ScannedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        var (cartonsInSlot, expectedCartons) = await ComputeSlotProgressAsync(slot.SlotId, owner);
        var isReady = cartonsInSlot >= expectedCartons && expectedCartons > 0;

        return ServiceResult.Ok(new ScanCheckInResponse(
            SlotId: slot.SlotId,
            Owner: owner,
            PackingId: packingId,
            CartonsInSlot: cartonsInSlot,
            ExpectedCartons: expectedCartons,
            IsReadyToComplete: isReady,
            Message: isReady
                ? $"ของลูกค้า {owner} ครบแล้ว ({cartonsInSlot}/{expectedCartons}) — พร้อมปริ้น Tracking"
                : $"วางกล่องช่อง {slot.SlotId} ({cartonsInSlot}/{expectedCartons})"
        ));
    }

    // ── 2) ดูรายละเอียด Slot ──────────────────────────────────
    public async Task<ServiceResult> GetSlotAsync(string slotId)
    {
        var slot = await db.CheckInSlots
            .FirstOrDefaultAsync(s => s.SlotId == slotId);

        if (slot is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบช่อง '{slotId}'"));

        var entries = await db.CheckInEntries
            .Include(e => e.Packing)
            .Where(e => e.SlotId == slotId)
            .OrderBy(e => e.ScannedAt)
            .ToListAsync();

        var cartons = entries.Select(e => new CheckInCartonItem(
            PackingId: e.PackingId,
            PalletId: e.Packing?.PalletId ?? string.Empty,
            Status: e.Status,
            ScannedAt: e.ScannedAt
        )).ToList();

        var (inSlot, expected) = await ComputeSlotProgressAsync(slotId, slot.Owner);
        var isReady = inSlot >= expected && expected > 0;

        return ServiceResult.Ok(new CheckInSlotDetail(
            SlotId: slot.SlotId,
            Owner: slot.Owner,
            Status: slot.Status,
            TrackingId: slot.TrackingId,
            CreatedAt: slot.CreatedAt,
            CompletedAt: slot.CompletedAt,
            CartonsInSlot: inSlot,
            ExpectedCartons: expected,
            IsReadyToComplete: isReady,
            Cartons: cartons
        ));
    }

    // ── 3) List Slot ที่ active อยู่ (OPEN/READY) ─────────────
    public async Task<ServiceResult> GetActiveSlotsAsync()
    {
        var slots = await db.CheckInSlots
            .Where(s => s.Status != "SHIPPED")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        var result = new List<CheckInSlotSummary>();
        foreach (var s in slots)
        {
            var (inSlot, expected) = await ComputeSlotProgressAsync(s.SlotId, s.Owner);
            result.Add(new CheckInSlotSummary(
                SlotId: s.SlotId,
                Owner: s.Owner,
                Status: s.Status,
                CartonsInSlot: inSlot,
                ExpectedCartons: expected,
                CreatedAt: s.CreatedAt
            ));
        }

        return ServiceResult.Ok(result);
    }

    // ── 4) Complete: generate Tracking → Status=READY ────────
    public async Task<ServiceResult> CompleteSlotAsync(CompleteCheckInRequest req)
    {
        var slot = await db.CheckInSlots
            .FirstOrDefaultAsync(s => s.SlotId == req.SlotId);

        if (slot is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบช่อง '{req.SlotId}'"));

        if (slot.Status != "OPEN")
            return ServiceResult.BadRequest(new ApiError(
                $"ช่อง '{req.SlotId}' ไม่อยู่ในสถานะ OPEN (สถานะ: {slot.Status})"));

        var (inSlot, expected) = await ComputeSlotProgressAsync(slot.SlotId, slot.Owner);
        if (inSlot < expected)
            return ServiceResult.BadRequest(new ApiError(
                $"ยังวางกล่องไม่ครบ ({inSlot}/{expected})"));

        var trackingId = $"TRK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";

        slot.Status = "READY";
        slot.TrackingId = trackingId;
        slot.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new CompleteCheckInResponse(
            SlotId: slot.SlotId,
            Owner: slot.Owner,
            TrackingId: trackingId,
            CompletedAt: slot.CompletedAt.Value,
            CartonsCount: inSlot,
            Message: $"ของลูกค้า {slot.Owner} พร้อมจัดส่ง"
        ));
    }

    // ── 5) Dispatch: ย้ายขึ้นรถแล้ว → Status=SHIPPED ──────────
    public async Task<ServiceResult> DispatchSlotAsync(DispatchCheckInRequest req)
    {
        var slot = await db.CheckInSlots
            .FirstOrDefaultAsync(s => s.SlotId == req.SlotId);

        if (slot is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบช่อง '{req.SlotId}'"));

        if (slot.Status != "READY")
            return ServiceResult.BadRequest(new ApiError(
                $"ช่อง '{req.SlotId}' ต้องเป็น READY ก่อน (สถานะ: {slot.Status})"));

        var shippedAt = DateTime.UtcNow;

        slot.Status = "SHIPPED";
        slot.ShippedAt = shippedAt;

        var entries = await db.CheckInEntries
            .Where(e => e.SlotId == slot.SlotId)
            .ToListAsync();

        // อัปเดต CheckInEntry + Packing → SHIPPED
        var packingIds = entries.Select(e => e.PackingId).ToList();
        var packings = await db.Packings
            .Where(p => packingIds.Contains(p.PackingId))
            .ToListAsync();

        foreach (var e in entries)
        {
            e.Status = "SHIPPED";
            e.ShippedAt = shippedAt;
        }

        foreach (var p in packings)
        {
            p.Status = "SHIPPED";
        }

        // Pallet ถูก reset เป็น AVAILABLE ตั้งแต่ตอน Packing confirm แล้ว
        // ไม่ต้องยุ่งกับ Pallet ตรงนี้

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new DispatchCheckInResponse(
            SlotId: slot.SlotId,
            Owner: slot.Owner,
            ShippedAt: shippedAt,
            CartonsCount: entries.Count,
            Message: $"ช่อง {slot.SlotId} ย้ายขึ้นรถแล้ว ({entries.Count} กล่อง)"
        ));
    }

    // ── Helpers ──────────────────────────────────
    /// <summary>
    /// นับจำนวน carton ใน slot + จำนวนที่ควรมีทั้งหมดของ Owner
    /// Expected = จำนวน Pack ที่ DONE และมี Part ของ Owner นี้
    /// </summary>
    private async Task<(int inSlot, int expected)> ComputeSlotProgressAsync(string slotId, string owner)
    {
        var inSlot = await db.CheckInEntries
            .CountAsync(e => e.SlotId == slotId);

        // Expected = Pack DONE ของ Owner นี้ (SHIPPED จะไม่นับ เพราะ Status เปลี่ยนแล้ว)
        var expected = await db.Packings
            .Where(p => p.Status == "DONE")
            .Where(p => db.PackingDetails
                .Where(d => d.PackingId == p.PackingId)
                .Join(db.PickOrderDetails,
                    d => d.PickOrderId,
                    pod => pod.PickOrderId,
                    (d, pod) => pod.PartId)
                .Join(db.Parts, partId => partId, pa => pa.PartId, (partId, pa) => pa.Owner)
                .Any(o => o == owner))
            .CountAsync();

        return (inSlot, expected);
    }

    private async Task<int> GetNextSlotNumberAsync()
    {
        // รวม slot ทั้งหมด → หาตัวถัดไป (SLOT-01, 02, ...)
        var existing = await db.CheckInSlots
            .Select(s => s.SlotId)
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
