using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Hubs;
using WmsApi.Models;
using WmsApi.Services.Picking;

namespace WmsApi.Controllers;

/// <summary>
/// Simulation Controller — รวม endpoint จำลองระบบอัตโนมัติทั้งหมด
/// ระบบจริงจะมี AGV/ASRS/Robot ทำ — mockup ใช้ยิง API แทน
/// </summary>
[ApiController]
[Route("api/simulate")]
public class SimulationController(
    WmsDbContext db,
    IHubContext<PutawayHub> hub,
    IPickingService pickingService) : ControllerBase
{
    // ─────────────────────────────────────────────
    //  AGV — จำลองรถขนอัตโนมัติ
    // ─────────────────────────────────────────────

    /// <summary>
    /// AGV รับ Pallet จาก Station แล้ว
    /// RETURNING/FG/PW → IN_TRANSIT
    /// </summary>
    [HttpPost("agv/pickup-pallet/{palletId}")]
    public async Task<IActionResult> AgvPickupPallet(string palletId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{palletId}' not found."));

        var oldStatus = pallet.Status;
        pallet.Status = "IN_TRANSIT";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            $"🤖 AGV รับ Pallet '{palletId}' แล้ว ({oldStatus} → IN_TRANSIT)"));
    }

    /// <summary>
    /// AGV ส่ง Pallet ถึงปลายทางแล้ว
    /// IN_TRANSIT → AVAILABLE (ถ้าปลายทางพร้อมใช้)
    /// </summary>
    [HttpPost("agv/deliver-pallet")]
    public async Task<IActionResult> AgvDeliverPallet([FromBody] AgvDeliverRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        var dest = req.Destination.ToUpper();

        // กำหนด status ตามปลายทาง
        if (dest == "ASRS")
        {
            // ถึง ASRS → ถ้ามีของ = STORED, ว่าง = AVAILABLE
            var hasItems = await db.ReceiptLines.AnyAsync(l => l.PalletId == req.PalletId);
            pallet.Status = hasItems ? "STORED" : "AVAILABLE";
        }
        else
        {
            pallet.Status = dest switch
            {
                "PREWORK" => "PREWORK",        // ถึง Prework → รอติดฉลาก
                "REPLENISH" => "REPLENISH",     // ถึง Replenish Rack → รอ Unload
                "PICK_STATION" => "AVAILABLE",  // ถึง Pick Station → พร้อม pick
                "UNLOAD_STATION" => "FG",       // ถึง Unload Station → พร้อม unload
                _ => "AVAILABLE",
            };
        }
        pallet.Location = dest;
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── Auto-allocate เมื่อ pallet เข้า ASRS (STORED) ──
        var allocatedInfo = new List<string>();
        if (dest == "ASRS" && pallet.Status == "STORED")
        {
            var partIds = await db.ReceiptLines
                .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
                .Select(l => l.PartId)
                .Distinct()
                .ToListAsync();

            foreach (var pid in partIds)
            {
                var (_, qty) = await pickingService.AllocatePendingForPartAsync(pid);
                if (qty > 0) allocatedInfo.Add($"{pid}×{qty}");
            }
        }

        var allocMsg = allocatedInfo.Count > 0
            ? $" | Auto-allocated: {string.Join(", ", allocatedInfo)}"
            : "";

        return Ok(new ApiSuccess(true,
            $"🤖 AGV ส่ง Pallet '{req.PalletId}' ถึง {dest} แล้ว (สถานะ: {pallet.Status}){allocMsg}"));
    }

    // ─────────────────────────────────────────────
    //  ASRS — จำลองคลังอัตโนมัติ
    // ─────────────────────────────────────────────

    /// <summary>
    /// ASRS รับ Pallet เข้าเก็บแล้ว
    /// IN_TRANSIT → AVAILABLE, Location = ASRS
    /// </summary>
    [HttpPost("asrs/receive-pallet/{palletId}")]
    public async Task<IActionResult> AsrsReceivePallet(string palletId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{palletId}' not found."));

        // อัพเดท PutawaySession ที่เกี่ยวข้อง (ถ้ามี)
        var putaway = await db.PutawaySessions
            .Where(p => p.PalletId == palletId && p.Status == "AGV_DISPATCHED")
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (putaway is not null)
        {
            putaway.Status = "COMPLETED";
            putaway.CompletedAt = DateTime.UtcNow;
        }

        // กำหนด location ตาม destination จริง
        var dest = putaway?.Destination ?? "ASRS";
        var hasItems = await db.ReceiptLines.AnyAsync(l => l.PalletId == palletId);

        if (dest == "REPLENISH")
        {
            // Replenish → Status = "REPLENISH" (ป้องกันไม่ให้ scan Putaway ซ้ำ, รอ Unload)
            pallet.Status = hasItems ? "REPLENISH" : "AVAILABLE";
            pallet.Location = "REPLENISH";
        }
        else if (dest == "PREWORK")
        {
            // Location ถูกแมพไว้แล้วตอน confirm (PW-STN-x) — ใช้ต่อเลย
            var stationId = pallet.Location ?? "PREWORK";

            if (!hasItems)
            {
                pallet.Status = "AVAILABLE";
                pallet.Location = null;
            }
            else
            {
                // ── Auto-cut: ตัดยอด ReceiptLines ออกจาก Pallet ทันที ──
                var lines = await db.ReceiptLines
                    .Include(l => l.Part)
                    .Where(l => l.PalletId == palletId && l.Status == "PALLETIZED")
                    .ToListAsync();

                foreach (var line in lines)
                {
                    // บันทึก PreworkCutLog
                    db.PreworkCutLogs.Add(new PreworkCutLog
                    {
                        PalletId = palletId,
                        StationId = stationId,
                        PartId = line.PartId,
                        Owner = line.Part?.Owner,
                        Brand = line.Part?.Brand,
                        ItemDesc = line.Part?.ItemDesc,
                        ImageUrl = line.Part?.ImageUrl,
                        Qty = line.QtyReceived,
                        LotNumber = line.LotNumber,
                        ExpiredDate = line.ExpiredDate,
                        Condition = line.Condition,
                        OperatorId = putaway?.OperatorId ?? "SYSTEM",
                        CutAt = DateTime.UtcNow,
                    });

                    // ตัดยอด — เปลี่ยน status + ปลด PalletId
                    line.Status = "PREWORK_RECEIVED";
                    line.PalletId = null;
                    line.UpdatedAt = DateTime.UtcNow;
                }

                // Pallet ว่างแล้ว รอคืน (Location ยังเป็น PW-STN-x เดิม)
                pallet.Status = "AVAILABLE";
            }
        }
        else
        {
            // ASRS — ถ้ามีของ = STORED, ว่าง = AVAILABLE
            pallet.Status = hasItems ? "STORED" : "AVAILABLE";
            pallet.Location = "ASRS";
        }
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── Auto-allocate: pallet นี้เข้า ASRS (STORED) แล้ว ──
        // ให้เติม PickOrderSub ให้ PickOrder ที่ยัง allocate ไม่ครบของ part เหล่านี้
        var allocatedInfo = new List<string>();
        if (dest == "ASRS" && pallet.Status == "STORED")
        {
            var partIds = await db.ReceiptLines
                .Where(l => l.PalletId == palletId && l.Status == "PALLETIZED")
                .Select(l => l.PartId)
                .Distinct()
                .ToListAsync();

            foreach (var pid in partIds)
            {
                var (subs, qty) = await pickingService.AllocatePendingForPartAsync(pid);
                if (qty > 0) allocatedInfo.Add($"{pid}×{qty}");
            }
        }

        // ── SignalR: broadcast pallet arrived ──
        await hub.Clients.All.SendAsync("PalletArrived", new
        {
            stationId = pallet.Location,
            palletId,
            destination = dest,
        });

        var allocMsg = allocatedInfo.Count > 0
            ? $" | Auto-allocated: {string.Join(", ", allocatedInfo)}"
            : "";

        return Ok(new ApiSuccess(true,
            $"📦 Pallet '{palletId}' ถึงปลายทาง {pallet.Location} แล้ว (Status: {pallet.Status}){allocMsg}"));
    }

    /// <summary>
    /// ASRS ดึง Pallet ออกมา → จำลอง AGV นำไปยังปลายทาง
    /// AVAILABLE → IN_TRANSIT → AVAILABLE (ถึงปลายทาง)
    /// ทำ 2 ขั้นในครั้งเดียว (shortcut สำหรับ mockup)
    /// </summary>
    [HttpPost("asrs/retrieve-pallet")]
    public async Task<IActionResult> AsrsRetrievePallet([FromBody] AsrsRetrieveRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Location != "ASRS" || (pallet.Status != "AVAILABLE" && pallet.Status != "STORED"))
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่ได้อยู่ใน ASRS (Location: {pallet.Location}, Status: {pallet.Status})"));

        var dest = req.Destination.ToUpper();

        // จำลอง: ASRS ดึงออก → IN_TRANSIT → ถึงปลายทาง (ทำทีเดียว)
        pallet.Status = dest switch
        {
            "PICK_STATION" => "AVAILABLE",
            "UNLOAD_STATION" => pallet.Type ?? "FG",
            "PREWORK" => "PREWORK",
            _ => "AVAILABLE",
        };
        pallet.Location = dest;
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            $"📦 ASRS ดึง Pallet '{req.PalletId}' → {dest} (สถานะ: {pallet.Status})"));
    }

    // ─────────────────────────────────────────────
    //  Pallet Return — จำลอง pallet กลับจาก Pick/Unload
    // ─────────────────────────────────────────────

    /// <summary>
    /// Pallet ที่ RETURNING ถูก AGV นำกลับ ASRS/Zone Pick เรียบร้อย
    /// RETURNING → AVAILABLE, Location = destination
    /// </summary>
    [HttpPost("pallet/return-complete")]
    public async Task<IActionResult> PalletReturnComplete([FromBody] PalletReturnCompleteRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        var dest = req.Destination?.ToUpper() ?? "ASRS";

        // ถ้ากลับ ASRS → เช็คว่ามีของหรือเปล่า
        if (dest == "ASRS")
        {
            var hasItems = await db.ReceiptLines.AnyAsync(l => l.PalletId == req.PalletId);
            pallet.Status = hasItems ? "STORED" : "AVAILABLE";
        }
        else
        {
            pallet.Status = "AVAILABLE";
        }
        pallet.Location = dest;
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── Auto-allocate เมื่อ pallet กลับ ASRS แล้วยังมีของ (STORED) ──
        var allocatedInfo = new List<string>();
        if (dest == "ASRS" && pallet.Status == "STORED")
        {
            var partIds = await db.ReceiptLines
                .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
                .Select(l => l.PartId)
                .Distinct()
                .ToListAsync();

            foreach (var pid in partIds)
            {
                var (_, qty) = await pickingService.AllocatePendingForPartAsync(pid);
                if (qty > 0) allocatedInfo.Add($"{pid}×{qty}");
            }
        }

        var allocMsg = allocatedInfo.Count > 0
            ? $" | Auto-allocated: {string.Join(", ", allocatedInfo)}"
            : "";

        return Ok(new ApiSuccess(true,
            $"📦 Pallet '{req.PalletId}' ส่งกลับ {dest} เรียบร้อย (Status: {pallet.Status}){allocMsg}"));
    }

    // ─────────────────────────────────────────────
    //  Pick Zone — ส่ง pallet เปล่าไปรอที่จุด Pick
    //  (สำหรับเป็น dest pallet รับของจาก source pallet)
    // ─────────────────────────────────────────────

    /// <summary>
    /// ส่ง Pallet เปล่าไปรอที่ Pick Zone (Location=PICK)
    /// จะถูกเสนอใน suggest-dest-pallets ให้เป็นปลายทาง
    /// </summary>
    [HttpPost("pallet/send-to-pick/{palletId}")]
    public async Task<IActionResult> SendPalletToPick(string palletId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return NotFound(new ApiError($"ไม่พบ Pallet '{palletId}'"));

        // ต้องเป็น pallet เปล่าจริงๆ — ไม่มี ReceiptLines ค้างอยู่
        var hasItems = await db.ReceiptLines.AnyAsync(l =>
            l.PalletId == palletId &&
            (l.Status == "PALLETIZED" || l.Status == "PICKING"));
        if (hasItems)
            return BadRequest(new ApiError(
                $"Pallet '{palletId}' ยังมีของอยู่ — ใช้ pallet เปล่าเท่านั้น"));

        // ห้ามดึง pallet ที่กำลัง PICKING (อยู่ที่ source station) มาเป็น dest
        if (pallet.Status == "PICKING")
            return BadRequest(new ApiError(
                $"Pallet '{palletId}' กำลังใช้งาน (Status: PICKING)"));

        pallet.Status = "AVAILABLE";
        pallet.Type = null;
        pallet.Location = "PICK";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            $"📦 Pallet '{palletId}' พร้อมรอที่ Pick Zone (Location=PICK)"));
    }

    // ─────────────────────────────────────────────
    //  Prework — จำลองติดสติ๊กเกอร์ + แมพลง Pallet เปล่า
    // ─────────────────────────────────────────────

    /// <summary>
    /// จำลองขั้นตอน: คนงานเอาสินค้าที่ตัดยอด (PW) ไปติดสติ๊กเกอร์
    /// แล้วแมพลง Pallet เปล่า → สินค้าเปลี่ยนเป็น FG พร้อมส่ง ASRS
    /// </summary>
    [HttpPost("prework/label-and-repalletize")]
    public async Task<IActionResult> PreworkLabelAndRepalletize([FromBody] PreworkRepalletizeRequest req)
    {
        // 1. หา Pallet เปล่า
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status != "AVAILABLE")
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่ว่าง (สถานะ: {pallet.Status})"));

        // 2. หา ReceiptLines ที่ตัดยอดแล้ว (PREWORK_RECEIVED, PalletId=null)
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.Status == "PREWORK_RECEIVED" && l.PalletId == null)
            .ToListAsync();

        if (lines.Count == 0)
            return BadRequest(new ApiError("ไม่มีสินค้าที่ตัดยอดรอแมพ (PREWORK_RECEIVED)"));

        // 3. เลือกเฉพาะ lines ที่ compatible (Owner + Batch) กับ Pallet
        var existingLines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
            .ToListAsync();

        // หา Owner ที่ lock ไว้แล้ว (ถ้า Pallet มีของอยู่)
        var lockedOwner = existingLines
            .Where(l => l.Part != null)
            .Select(l => l.Part!.Owner)
            .FirstOrDefault();

        // หา Part→Batch ที่ lock ไว้แล้ว
        var lockedBatches = existingLines
            .Where(l => l.PartId != null)
            .GroupBy(l => l.PartId!)
            .ToDictionary(g => g.Key, g => g.First().LotNumber);

        // ถ้ายังไม่มี lock → ใช้ Owner ของ line แรก
        if (lockedOwner == null)
            lockedOwner = lines.FirstOrDefault(l => l.Part != null)?.Part!.Owner;

        // กรอง lines ที่ Owner ตรง
        var compatible = lines
            .Where(l => l.Part == null || l.Part.Owner == lockedOwner)
            .ToList();

        // กรอง Batch — Part เดียวกันต้อง Batch เดียวกัน
        var selected = new List<WmsApi.Models.ReceiptLine>();
        var batchMap = new Dictionary<string, string?>(lockedBatches!);

        foreach (var line in compatible)
        {
            if (line.PartId != null && batchMap.TryGetValue(line.PartId, out var existingBatch))
            {
                if (line.LotNumber != existingBatch)
                    continue; // Batch ไม่ตรง → ข้าม
            }

            selected.Add(line);
            if (line.PartId != null && !batchMap.ContainsKey(line.PartId))
                batchMap[line.PartId] = line.LotNumber;
        }

        if (selected.Count == 0)
            return BadRequest(new ApiError("ไม่มีสินค้าที่ compatible กับ Pallet นี้ได้"));

        var skipped = lines.Count - selected.Count;

        // 4. แมพสินค้าลง Pallet + เปลี่ยน PW → FG
        foreach (var line in selected)
        {
            line.PalletId = req.PalletId;
            line.Condition = "FG";
            line.Status = "PALLETIZED";
            line.UpdatedAt = DateTime.UtcNow;
        }

        // 4. Update Pallet → PW + PREWORK (เพื่อให้ PW-STN-2,4,6 scan ได้)
        pallet.Type = "PW";
        pallet.Status = "PREWORK";
        pallet.Location = "PREWORK";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── SignalR: broadcast labeling completed ──
        await hub.Clients.All.SendAsync("LabelingCompleted", new
        {
            palletId = req.PalletId,
            palletType = pallet.Type,
            palletStatus = pallet.Status,
        });

        var msg = $"🏷️ ติดสติ๊กเกอร์เสร็จ — แมพ {selected.Count} รายการลง Pallet '{req.PalletId}' (PW→FG) พร้อมส่ง ASRS จาก PW-STN-2/4/6";
        if (skipped > 0)
            msg += $"\n⚠️ ข้าม {skipped} รายการ (คนละ Owner/Batch) — รอแมพ Pallet อื่น";

        return Ok(new ApiSuccess(true, msg));
    }

}


// ── Simulation DTOs ────────────────────────────
public record AgvDeliverRequest(
    string PalletId,
    string Destination   // ASRS | PREWORK | PICK_STATION | UNLOAD_STATION
);


public record AsrsRetrieveRequest(
    string PalletId,
    string Destination   // PICK_STATION | UNLOAD_STATION | PREWORK
);

public record PalletReturnCompleteRequest(
    string PalletId,
    string? Destination  // ASRS | ZONE_PICK
);

public record PreworkRepalletizeRequest(
    string PalletId      // Pallet เปล่าที่จะแมพสินค้าลง
);
