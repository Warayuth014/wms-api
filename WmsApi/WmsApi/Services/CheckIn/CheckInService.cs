using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.CheckIn;

public class CheckInService(WmsDbContext db) : ICheckInService
{
    // สถานที่ปลายทางให้สุ่มแนะนำ operator ตอน preview (mock)
    private static readonly string[] _dispatchDestinations =
        ["ประตู 1", "ประตู 2", "ประตู 3", "ประตู VIP", "ท่า A", "ท่า B", "ท่า C", "Dock 1", "Dock 2"];

    // ── 0) Preview Pack ก่อน Check-IN (ไม่เขียน DB) ──────────────
    public async Task<ServiceResult> PreviewCartonAsync(PreviewCheckInRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PackingId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Packing ID"));

        var packingId = req.PackingId.Trim().ToUpper();

        var pack = await db.Packings
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.PackingId == packingId);

        if (pack is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pack '{packingId}'"));

        if (pack.Status == "OPEN")
            return ServiceResult.BadRequest(new ApiError(
                $"Pack '{packingId}' ยัง Pack ไม่เสร็จ (สถานะ: {pack.Status})"));

        if (pack.Status == "DONE")
            return ServiceResult.BadRequest(new ApiError(
                $"Pack '{packingId}' ยังไม่ผ่าน Sorting — ต้องสแกนที่ Sorting Station ก่อน"));

        // Owner + CustomerOrderId จาก PickOrder ของ Pack
        var orderIds = pack.Details.Select(d => d.PickOrderId).ToList();
        var pickOrderIds = orderIds.Distinct().OrderBy(id => id).ToList();
        var owner = await db.PickOrderDetails
            .Where(d => orderIds.Contains(d.PickOrderId))
            .Join(db.Parts, d => d.PartId, p => p.PartId, (d, p) => p.Owner)
            .Distinct()
            .FirstOrDefaultAsync() ?? string.Empty;

        var customerOrderId = await db.PickOrders
            .Where(p => orderIds.Contains(p.PickOrderId) && p.CustomerOrderId != null)
            .Select(p => p.CustomerOrderId)
            .FirstOrDefaultAsync();

        var scannedParts = await db.PackingPartScans
            .Where(s => s.PackingId == packingId)
            .GroupBy(s => s.PartId)
            .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.ScannedQty) })
            .OrderBy(x => x.PartId)
            .ToListAsync();

        var partIds = scannedParts.Select(x => x.PartId).ToList();
        var partsById = await db.Parts
            .Where(p => partIds.Contains(p.PartId))
            .ToDictionaryAsync(p => p.PartId);

        var items = scannedParts.Select(x =>
        {
            partsById.TryGetValue(x.PartId, out var part);
            return new PreviewCheckInItem(
                PartId: x.PartId,
                ItemDesc: part?.ItemDesc ?? string.Empty,
                Brand: part?.Brand ?? string.Empty,
                ImageUrl: part?.ImageUrl,
                Qty: x.Qty
            );
        }).ToList();

        var itemCount = items.Sum(i => i.Qty);
        var orderCount = pickOrderIds.Count;

        // ตรวจว่า Pack นี้ถูก check-in ไปแล้วหรือยัง
        var existingEntry = await db.CheckInEntries
            .FirstOrDefaultAsync(e => e.PackingId == packingId);

        // หา slot ที่จะใช้: ของ customer order เดียวกันที่ยัง OPEN > fallback owner + no-CO
        string slotId;
        bool isNewSlot;
        if (existingEntry != null)
        {
            slotId = existingEntry.SlotId;
            isNewSlot = false;
        }
        else
        {
            CheckInSlot? slot = null;
            if (customerOrderId != null)
            {
                slot = await db.CheckInSlots
                    .FirstOrDefaultAsync(s => s.CustomerOrderId == customerOrderId && s.Status == "OPEN");
            }
            slot ??= await db.CheckInSlots
                .FirstOrDefaultAsync(s => s.Owner == owner && s.Status == "OPEN" && s.CustomerOrderId == null);

            if (slot != null)
            {
                slotId = slot.SlotId;
                isNewSlot = false;
            }
            else
            {
                var nextNo = await GetNextSlotNumberAsync();
                slotId = $"SLOT-{nextNo:D2}";
                isNewSlot = true;
            }
        }

        // สุ่มปลายทาง — seed ด้วย packingId เพื่อให้ Pack เดียวเห็นปลายทางเดียวกันเสมอ
        var destination = _dispatchDestinations[Math.Abs(packingId.GetHashCode()) % _dispatchDestinations.Length];

        var isAlreadyCheckedIn = existingEntry != null;
        var progress = await ComputePipelineProgressAsync(customerOrderId, owner);

        return ServiceResult.Ok(new PreviewCheckInResponse(
            PackingId: packingId,
            Owner: owner,
            CustomerOrderId: customerOrderId,
            PackStatus: pack.Status,
            ItemCount: itemCount,
            OrderCount: orderCount,
            PickOrderIds: pickOrderIds,
            SlotId: slotId,
            IsNewSlot: isNewSlot,
            IsAlreadyCheckedIn: isAlreadyCheckedIn,
            DispatchDestination: destination,
            Items: items,
            PipelineTotal: progress.PipelineTotal,
            PickDone: progress.PickDone,
            PackDone: progress.PackDone,
            SortingDone: progress.SortingDone,
            CheckInDone: progress.CheckInDone
        ));
    }

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

        if (pack.Status != "SORTED")
            return ServiceResult.BadRequest(new ApiError(
                $"Pack '{packingId}' ยังไม่พร้อม check-in (สถานะ: {pack.Status} — ต้องเป็น SORTED)"));

        // เช็คว่า Carton นี้ยัง check-in ไม่แล้ว
        var existing = await db.CheckInEntries
            .Include(e => e.Slot)
            .FirstOrDefaultAsync(e => e.PackingId == packingId);

        if (existing is not null)
            return ServiceResult.BadRequest(new ApiError(
                $"Pack '{packingId}' ถูก check-in แล้วที่ช่อง '{existing.SlotId}'"));

        // หา Owner + CustomerOrderId จาก Part/PickOrder ของ Pack นี้
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

        // หา CustomerOrderId ที่ active ของ Owner นี้ (จาก PickOrder ตัวแรก)
        var customerOrderId = await db.PickOrders
            .Where(p => orderIds.Contains(p.PickOrderId) && p.CustomerOrderId != null)
            .Select(p => p.CustomerOrderId)
            .FirstOrDefaultAsync();

        // หา Slot ที่ยังเปิดอยู่ — ใช้ CustomerOrderId ก่อน fallback ไปที่ Owner (ของเก่าที่ยังไม่มี CO)
        CheckInSlot? slot = null;
        if (customerOrderId != null)
        {
            slot = await db.CheckInSlots
                .FirstOrDefaultAsync(s => s.CustomerOrderId == customerOrderId && s.Status == "OPEN");
        }
        slot ??= await db.CheckInSlots
            .FirstOrDefaultAsync(s => s.Owner == owner && s.Status == "OPEN" && s.CustomerOrderId == null);

        if (slot is null)
        {
            // สร้าง Slot ใหม่ — หาหมายเลขถัดไป
            var nextNo = await GetNextSlotNumberAsync();
            slot = new CheckInSlot
            {
                SlotId = $"SLOT-{nextNo:D2}",
                Owner = owner,
                CustomerOrderId = customerOrderId,
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow,
            };
            db.CheckInSlots.Add(slot);
        }
        else if (slot.CustomerOrderId == null && customerOrderId != null)
        {
            // upgrade slot เก่าให้ผูกกับ CustomerOrder
            slot.CustomerOrderId = customerOrderId;
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

        // Pack: DONE → STAGED (อยู่ในช่อง รอขึ้นรถ)
        pack.Status = "STAGED";

        await db.SaveChangesAsync();

        var (cartonsInSlot, expectedCartons) = await ComputeSlotProgressAsync(
            slot.SlotId, owner, slot.CustomerOrderId);
        var isReady = cartonsInSlot >= expectedCartons && expectedCartons > 0;

        return ServiceResult.Ok(new ScanCheckInResponse(
            SlotId: slot.SlotId,
            IsReadyToComplete: isReady
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

        var packIds = entries.Select(e => e.PackingId).ToList();

        var itemCounts = await db.PackingPartScans
            .Where(s => packIds.Contains(s.PackingId))
            .GroupBy(s => s.PackingId)
            .Select(g => new { PackingId = g.Key, Count = g.Sum(x => x.ScannedQty) })
            .ToDictionaryAsync(x => x.PackingId, x => x.Count);

        var orderCounts = await db.PackingDetails
            .Where(d => packIds.Contains(d.PackingId))
            .GroupBy(d => d.PackingId)
            .Select(g => new { PackingId = g.Key, Count = g.Select(d => d.PickOrderId).Distinct().Count() })
            .ToDictionaryAsync(x => x.PackingId, x => x.Count);

        var cartons = entries.Select(e => new CheckInCartonItem(
            PackingId: e.PackingId,
            TrackingId: e.Packing?.TrackingId,
            Status: e.Status,
            ScannedAt: e.ScannedAt,
            ItemCount: itemCounts.GetValueOrDefault(e.PackingId, 0),
            OrderCount: orderCounts.GetValueOrDefault(e.PackingId, 0)
        )).ToList();

        var progress = await ComputePipelineProgressAsync(slot.CustomerOrderId, slot.Owner);

        return ServiceResult.Ok(new CheckInSlotDetail(
            Status: slot.Status,
            Cartons: cartons,
            CustomerOrderId: slot.CustomerOrderId,
            PipelineTotal: progress.PipelineTotal,
            PickDone: progress.PickDone,
            PackDone: progress.PackDone,
            SortingDone: progress.SortingDone,
            CheckInDone: progress.CheckInDone
        ));
    }

    // ── 3) Complete: Slot → READY (TrackingId อยู่ที่ Pack ไม่ gen ใหม่) ────
    public async Task<ServiceResult> CompleteSlotAsync(CompleteCheckInRequest req)
    {
        var slot = await db.CheckInSlots
            .FirstOrDefaultAsync(s => s.SlotId == req.SlotId);

        if (slot is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบช่อง '{req.SlotId}'"));

        if (slot.Status != "OPEN")
            return ServiceResult.BadRequest(new ApiError(
                $"ช่อง '{req.SlotId}' ไม่อยู่ในสถานะ OPEN (สถานะ: {slot.Status})"));

        var (inSlot, expected) = await ComputeSlotProgressAsync(
            slot.SlotId, slot.Owner, slot.CustomerOrderId);
        if (inSlot < expected)
            return ServiceResult.BadRequest(new ApiError(
                $"ยังวางกล่องไม่ครบ ({inSlot}/{expected})"));

        slot.Status = "READY";
        slot.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new ApiSuccess(true,
            $"ของลูกค้า {slot.Owner} พร้อมจัดส่ง"));
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

        // ปิด CustomerOrder เมื่อ Slot ขึ้นรถแล้ว (1 Slot = 1 CustomerOrder)
        if (slot.CustomerOrderId != null)
        {
            var customerOrder = await db.CustomerOrders
                .FirstOrDefaultAsync(c => c.CustomerOrderId == slot.CustomerOrderId);
            if (customerOrder is not null && customerOrder.Status == "ACTIVE")
            {
                customerOrder.Status = "SHIPPED";
                customerOrder.CompletedAt ??= shippedAt;
                customerOrder.ShippedAt = shippedAt;
            }
        }

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new ApiSuccess(true,
            $"ช่อง {slot.SlotId} ย้ายขึ้นรถแล้ว ({entries.Count} กล่อง)"));
    }

    // ── Helpers ──────────────────────────────────
    /// <summary>
    /// คำนวณ 3-column progress (Pick / Pack / CheckIn) สำหรับ Slot
    /// semantics: cumulative done — column คือจำนวน Pack ที่ผ่านขั้นตอนนั้น ๆ แล้ว
    ///   denominator เดียวกัน = จำนวน Pack ทั้งหมดของ CustomerOrder
    ///   Pick done    = Packs ที่ PickOrder = COMPLETED
    ///   Pack done    = Packs ที่ Status in (DONE / STAGED / SHIPPED)
    ///   CheckIn done = Packs ที่ Status in (STAGED / SHIPPED)
    /// </summary>
    private async Task<(int PipelineTotal, int PickDone, int PackDone, int SortingDone, int CheckInDone)>
        ComputePipelineProgressAsync(string? customerOrderId, string owner)
    {
        // PickOrders ของ CustomerOrder นี้ — fallback: PickOrders ที่มี Part ของ Owner
        List<string> pickOrderIds;
        if (customerOrderId != null)
        {
            pickOrderIds = await db.PickOrders
                .Where(p => p.CustomerOrderId == customerOrderId)
                .Select(p => p.PickOrderId)
                .ToListAsync();
        }
        else
        {
            pickOrderIds = await db.PickOrderDetails
                .Where(d => d.PickOrder!.Status == "WAITING"
                         || d.PickOrder!.Status == "PICKING"
                         || d.PickOrder!.Status == "COMPLETED")
                .Join(db.Parts, d => d.PartId, p => p.PartId, (d, p) => new { d.PickOrderId, p.Owner })
                .Where(x => x.Owner == owner)
                .Select(x => x.PickOrderId)
                .Distinct()
                .ToListAsync();
        }

        // Packs ของ PickOrders เหล่านี้ (denominator = จำนวน Pack ทั้งหมด)
        var packs = await db.Packings
            .Where(p => p.PickOrderId != null && pickOrderIds.Contains(p.PickOrderId))
            .Select(p => new { p.Status, p.PickOrderId })
            .ToListAsync();

        var pipelineTotal = packs.Count;

        // Pick done = Packs ที่ PickOrder = COMPLETED
        var completedPickOrderIds = await db.PickOrders
            .Where(p => pickOrderIds.Contains(p.PickOrderId) && p.Status == "COMPLETED")
            .Select(p => p.PickOrderId)
            .ToListAsync();
        var pickDone = packs.Count(p => p.PickOrderId != null && completedPickOrderIds.Contains(p.PickOrderId));

        // cumulative: ผ่าน stage นั้นแล้ว (รวม stage ถัดไปด้วย)
        var packDone = packs.Count(p => p.Status == "DONE" || p.Status == "SORTED" || p.Status == "STAGED" || p.Status == "SHIPPED");
        var sortingDone = packs.Count(p => p.Status == "SORTED" || p.Status == "STAGED" || p.Status == "SHIPPED");
        var checkInDone = packs.Count(p => p.Status == "STAGED" || p.Status == "SHIPPED");

        return (pipelineTotal, pickDone, packDone, sortingDone, checkInDone);
    }

    /// <summary>
    /// นับจำนวน carton ใน slot + จำนวนที่ควรมีทั้งหมดของ CustomerOrder/Owner
    /// Expected = Pack ที่ SORTED (รอ check-in อยู่บน Sorting Pallet) + STAGED (สแกนเข้าช่องแล้ว)
    /// scope ด้วย CustomerOrderId ก่อน (แม่นยำสุด) ถ้าไม่มีค่อย fallback ไป Owner
    /// SHIPPED ไม่นับเพราะขึ้นรถไปแล้ว
    /// DONE ไม่นับเพราะยังไม่ผ่าน Sorting (ยังไม่พร้อมเข้า slot)
    /// </summary>
    private async Task<(int inSlot, int expected)> ComputeSlotProgressAsync(
        string slotId, string owner, string? customerOrderId = null)
    {
        var inSlot = await db.CheckInEntries
            .CountAsync(e => e.SlotId == slotId);

        var readyPacks = db.Packings
            .Where(p => p.Status == "SORTED" || p.Status == "STAGED");

        if (customerOrderId != null)
        {
            // scope ด้วย CustomerOrder — แม่นยำที่สุด
            var pickOrderIds = await db.PickOrders
                .Where(p => p.CustomerOrderId == customerOrderId)
                .Select(p => p.PickOrderId)
                .ToListAsync();
            readyPacks = readyPacks
                .Where(p => p.PickOrderId != null && pickOrderIds.Contains(p.PickOrderId));
        }
        else
        {
            // fallback: scope ด้วย Owner (สำหรับ slot เก่าที่ยังไม่มี CustomerOrderId)
            readyPacks = readyPacks
                .Where(p => db.PackingDetails
                    .Where(d => d.PackingId == p.PackingId)
                    .Join(db.PickOrderDetails,
                        d => d.PickOrderId,
                        pod => pod.PickOrderId,
                        (d, pod) => pod.PartId)
                    .Join(db.Parts, partId => partId, pa => pa.PartId, (partId, pa) => pa.Owner)
                    .Any(o => o == owner));
        }

        var expected = await readyPacks.CountAsync();
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
