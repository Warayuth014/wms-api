using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Hubs;
using WmsApi.Models;

namespace WmsApi.Controllers;

/// <summary>
/// Simulation Controller — รวม endpoint จำลองระบบอัตโนมัติทั้งหมด
/// ระบบจริงจะมี AGV/ASRS/Robot ทำ — mockup ใช้ยิง API แทน
/// </summary>
[ApiController]
[Route("api/simulate")]
public class SimulationController(WmsDbContext db, IHubContext<PutawayHub> hub) : ControllerBase
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

        return Ok(new ApiSuccess(true,
            $"🤖 AGV ส่ง Pallet '{req.PalletId}' ถึง {dest} แล้ว (สถานะ: {pallet.Status})"));
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

        // ── SignalR: broadcast pallet arrived ──
        await hub.Clients.All.SendAsync("PalletArrived", new
        {
            stationId = pallet.Location,
            palletId,
            destination = dest,
        });

        return Ok(new ApiSuccess(true,
            $"📦 Pallet '{palletId}' ถึงปลายทาง {pallet.Location} แล้ว (Status: {pallet.Status})"));
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
    //  Labeling — จำลองการติดฉลาก PW → FG
    // ─────────────────────────────────────────────

    /// <summary>
    /// ติดฉลากเสร็จ → แมพสินค้า PREWORK_RECEIVED ลง Pallet + เปลี่ยนเป็น FG
    /// Validate เงื่อนไขเดียวกับ assign-pallet:
    ///   1) Pallet ว่าง หรือ สินค้าใน Pallet เป็นบริษัท (Owner) เดียวกัน
    ///   2) ไม่มีสินค้านี้ หรือ มีสินค้าและ Batch เดียวกัน
    /// </summary>
    [HttpPost("labeling/complete/{palletId}")]
    public async Task<IActionResult> LabelingComplete(string palletId)
    {
        // ── 1. ตรวจ Pallet ─────────────────────────
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{palletId}' not found."));

        // Pallet ต้องว่าง (AVAILABLE) หรือเป็น FG ที่มีของอยู่แล้ว
        if (pallet.Status != "AVAILABLE" && pallet.Status != "FG")
            return BadRequest(new ApiError(
                $"Pallet '{palletId}' มีสถานะ '{pallet.Status}' ไม่สามารถแมพสินค้าได้",
                "ใช้ได้เฉพาะ Pallet ที่มีสถานะ AVAILABLE หรือ FG เท่านั้น"));

        // ── 2. หาสินค้าที่ตัดยอดแล้ว (PREWORK_RECEIVED, PalletId=null) ──
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.Status == "PREWORK_RECEIVED" && l.PalletId == null)
            .ToListAsync();

        if (lines.Count == 0)
            return BadRequest(new ApiError(
                "ไม่มีสินค้าที่ตัดยอดรอแมพ (PREWORK_RECEIVED)"));

        // ── 3. ดึงสินค้าที่อยู่ใน Pallet อยู่แล้ว ──
        var existingLinesInPallet = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == palletId && l.Status == "PALLETIZED")
            .ToListAsync();

        if (existingLinesInPallet.Count > 0)
        {
            // ── 4a. ตรวจ Owner: สินค้าใน Pallet ต้องเป็นบริษัทเดียวกัน ──
            var existingOwners = existingLinesInPallet
                .Where(l => l.Part != null)
                .Select(l => l.Part!.Owner)
                .Distinct()
                .ToList();

            var newOwners = lines
                .Where(l => l.Part != null)
                .Select(l => l.Part!.Owner)
                .Distinct()
                .ToList();

            foreach (var newOwner in newOwners)
            {
                if (existingOwners.Count > 0 && !existingOwners.Contains(newOwner))
                    return BadRequest(new ApiError(
                        $"Pallet '{palletId}' มีสินค้าของ '{existingOwners[0]}' อยู่แล้ว ไม่สามารถเพิ่มสินค้าของ '{newOwner}' ได้",
                        "สินค้าใน Pallet ต้องเป็นของบริษัท (Owner) เดียวกันเท่านั้น"));
            }

            // ── 4b. ตรวจ Part ซ้ำ: ถ้ามี Part เดียวกันใน Pallet แล้ว ต้องเป็น Batch เดียวกัน ──
            foreach (var line in lines)
            {
                var duplicateInPallet = existingLinesInPallet
                    .Where(l => l.PartId == line.PartId)
                    .ToList();

                if (duplicateInPallet.Count > 0)
                {
                    var existingBatch = duplicateInPallet.First().LotNumber;
                    if (line.LotNumber != existingBatch)
                        return BadRequest(new ApiError(
                            $"Pallet '{palletId}' มีสินค้า '{line.PartId}' Batch '{existingBatch}' อยู่แล้ว ไม่สามารถเพิ่ม Batch '{line.LotNumber}' ได้",
                            "สินค้าชนิดเดียวกันใน Pallet ต้องเป็น Batch เดียวกันเท่านั้น"));
                }
            }
        }

        // ── 5. แมพสินค้าลง Pallet + เปลี่ยน Condition เป็น FG ──
        foreach (var line in lines)
        {
            line.PalletId = palletId;
            line.Condition = "FG";
            line.Status = "PALLETIZED";
            line.UpdatedAt = DateTime.UtcNow;
        }

        pallet.Type = "FG";
        pallet.Status = "FG";     // พร้อม putaway
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ── SignalR: broadcast labeling completed ──
        await hub.Clients.All.SendAsync("LabelingCompleted", new
        {
            palletId,
            palletType = pallet.Type,
            palletStatus = pallet.Status,
        });

        return Ok(new ApiSuccess(true,
            $"🏷️ Pallet '{palletId}' ติดฉลากเสร็จ — แมพ {lines.Count} รายการ (PW → FG) พร้อม Putaway เข้า ASRS"));
    }

    // ─────────────────────────────────────────────
    //  Basket Return — จำลองการคืน Basket
    // ─────────────────────────────────────────────

    /// <summary>
    /// Robot/AGV รับ Basket ที่ว่างแล้วกลับไป
    /// RETURNING → AVAILABLE
    /// </summary>
    [HttpPost("basket/return-complete/{basketId}")]
    public async Task<IActionResult> BasketReturnComplete(string basketId)
    {
        var basket = await db.Baskets.FindAsync(basketId);
        if (basket is null)
            return NotFound(new ApiError($"Basket '{basketId}' not found."));

        basket.Status = "AVAILABLE";
        basket.Destination = null;
        basket.Zone = null;
        basket.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            $"🧺 Basket '{basketId}' ถูกส่งกลับเรียบร้อย (AVAILABLE)"));
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

        return Ok(new ApiSuccess(true,
            $"📦 Pallet '{req.PalletId}' ส่งกลับ {dest} เรียบร้อย (Status: {pallet.Status})"));
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
            .Where(l => l.Status == "PREWORK_RECEIVED" && l.PalletId == null)
            .ToListAsync();

        if (lines.Count == 0)
            return BadRequest(new ApiError("ไม่มีสินค้าที่ตัดยอดรอแมพ (PREWORK_RECEIVED)"));

        // 3. แมพสินค้าลง Pallet + เปลี่ยน PW → FG
        foreach (var line in lines)
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

        return Ok(new ApiSuccess(true,
            $"🏷️ ติดสติ๊กเกอร์เสร็จ — แมพ {lines.Count} รายการลง Pallet '{req.PalletId}' (PW→FG) พร้อมส่ง ASRS จาก PW-STN-2/4/6"));
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
