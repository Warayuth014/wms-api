using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/putaway")]
public class PutawayController(WmsDbContext db) : ControllerBase
{
    private static readonly HashSet<string> ValidDestinations =
        new(StringComparer.OrdinalIgnoreCase) { "ASRS", "PREWORK" };

    // =============================================
    // GET /api/putaway/scan-pallet/{palletId}
    // สแกน Pallet สำหรับ Putaway
    // =============================================
    [HttpGet("scan-pallet/{palletId}")]
    public async Task<IActionResult> ScanPallet(string palletId, [FromQuery] string? stationId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{palletId}' not found."));

        // FG/PW = รับของแล้วรอเก็บ, PREWORK = อยู่ที่ Prework รอ convert แล้วส่ง ASRS
        if (pallet.Status is not ("FG" or "PW" or "PREWORK"))
            return BadRequest(new ApiError(
                $"Pallet '{palletId}' ไม่พร้อม Putaway (สถานะปัจจุบัน: {pallet.Status})",
                "Pallet ต้องเป็นสถานะ FG, PW หรือ PREWORK เท่านั้น"));

        // PW-STN → บังคับให้ pallet ต้องผ่าน STN→PREWORK มาก่อน
        var isPWStation = stationId?.StartsWith("PW-STN", StringComparison.OrdinalIgnoreCase) ?? false;
        if (isPWStation && pallet.Status != "PREWORK")
            return BadRequest(new ApiError(
                $"Pallet '{palletId}' ยังไม่ได้อยู่ที่ Prework (สถานะ: {pallet.Status})",
                "ต้องนำ Pallet ไปที่ Prework ก่อน โดย Putaway ที่ STN-1/2/3 → เลือก Prework"));

        // ดึงสินค้าบน Pallet
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == palletId && l.Status == "PALLETIZED")
            .ToListAsync();

        var items = lines.Select(l => new UnloadItemResponse(
            PartId: l.PartId,
            Owner: l.Part!.Owner,
            Brand: l.Part!.Brand,
            ItemDesc: l.Part!.ItemDesc,
            ImageUrl: l.Part!.ImageUrl,
            LotNumber: l.LotNumber,
            ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
            Qty: l.QtyReceived,
            Condition: l.Condition
        )).ToList();

        // FG → ASRS, PW → PREWORK (แนะนำ แต่ operator เลือกได้)
        var suggested = pallet.Type == "FG" ? "ASRS" : "PREWORK";

        var message = pallet.Type == "FG"
            ? "Pallet FG → เก็บเข้า ASRS"
            : "Pallet PW → แนะนำ Prework (เลือก ASRS ได้)";

        return Ok(new ScanPalletForPutawayResponse(
            PalletId: pallet.PalletId,
            Type: pallet.Type ?? "-",
            Status: pallet.Status,
            SuggestedDestination: suggested,
            Items: items,
            Message: message
        ));
    }

    // =============================================
    // POST /api/putaway/confirm
    // ยืนยัน Putaway → AGV มารับ Pallet
    // =============================================
    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmPutaway([FromBody] ConfirmPutawayRequest req)
    {
        // ── Validate ───────────────────────────
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status is not ("FG" or "PW" or "PREWORK"))
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่พร้อม Putaway (สถานะ: {pallet.Status})"));

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        var dest = req.Destination.ToUpper();
        if (!ValidDestinations.Contains(dest))
            return BadRequest(new ApiError(
                $"Destination ไม่ถูกต้อง: '{req.Destination}'",
                "ค่าที่รองรับ: ASRS, PREWORK"));

        // FG ต้องไป ASRS เท่านั้น
        if (pallet.Type == "FG" && dest == "PREWORK")
            return BadRequest(new ApiError(
                "Pallet ประเภท FG ต้องเก็บเข้า ASRS เท่านั้น"));

        // ── สร้าง PutawaySession ────────────────
        var session = new PutawaySession
        {
            PalletId = req.PalletId,
            StationId = req.StationId.ToUpper(),
            Destination = dest,
            Status = "AGV_DISPATCHED",
            OperatorId = req.OperatorId,
            CreatedAt = DateTime.UtcNow
        };

        db.PutawaySessions.Add(session);

        // PW-STN → convert PW เป็น FG ก่อนส่ง ASRS (เฉพาะเมื่อ ConvertToFG = true)
        var isPWStation = req.StationId.StartsWith("PW-STN", StringComparison.OrdinalIgnoreCase);
        if (isPWStation && pallet.Type == "PW" && req.ConvertToFG)
        {
            pallet.Type = "FG";

            // อัปเดต ReceiptLine.Condition บน pallet นี้ให้เป็น FG ด้วย
            var linesOnPallet = await db.ReceiptLines
                .Where(l => l.PalletId == req.PalletId && l.Condition == "PW")
                .ToListAsync();
            foreach (var l in linesOnPallet)
                l.Condition = "FG";
        }

        // ส่งไป PREWORK → Status: PREWORK (รอจัดการ), ส่งไป ASRS → IN_TRANSIT (AGV กำลังนำ)
        pallet.Status = dest == "PREWORK" ? "PREWORK" : "IN_TRANSIT";
        pallet.Location = dest;
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var destLabel = dest == "ASRS" ? "ASRS" : "Prework";
        var convertMsg = isPWStation && req.ConvertToFG ? " (PW→FG converted)" : "";

        return Ok(new ConfirmPutawayResponse(
            Success: true,
            PalletId: req.PalletId,
            StationId: req.StationId.ToUpper(),
            Destination: dest,
            Message: $"✅ AGV dispatched — Pallet '{req.PalletId}' → {destLabel}{convertMsg}"
        ));
    }

    // =============================================
    // POST /api/putaway/recall-to-prework
    // เรียก PW Pallet กลับจาก ASRS → Prework Station
    // (กรณีจุด Prework เริ่มว่างแล้ว)
    // =============================================
    [HttpPost("recall-to-prework")]
    public async Task<IActionResult> RecallToPrework([FromBody] RecallToPreworkRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        // ต้องเป็น PW ที่อยู่ใน ASRS เท่านั้น
        if (pallet.Type != "PW")
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่ใช่ประเภท PW (ประเภท: {pallet.Type})",
                "เรียกกลับได้เฉพาะ Pallet ประเภท PW ที่ฝากไว้ใน ASRS"));

        if (pallet.Location != "ASRS")
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่ได้อยู่ใน ASRS (ตำแหน่ง: {pallet.Location})",
                "Pallet ต้องอยู่ใน ASRS จึงจะเรียกกลับได้"));

        if (pallet.Status is not ("IN_TRANSIT" or "RETURNING"))
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่พร้อมเรียกกลับ (สถานะ: {pallet.Status})"));

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        // สร้าง PutawaySession สำหรับ recall
        var session = new PutawaySession
        {
            PalletId = req.PalletId,
            StationId = req.StationId.ToUpper(),
            Destination = "PREWORK",
            Status = "AGV_DISPATCHED",
            OperatorId = req.OperatorId,
            CreatedAt = DateTime.UtcNow
        };

        db.PutawaySessions.Add(session);

        // เปลี่ยนสถานะ → PREWORK, ตำแหน่ง → station ที่เรียก
        pallet.Status = "PREWORK";
        pallet.Location = "PREWORK";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ConfirmPutawayResponse(
            Success: true,
            PalletId: req.PalletId,
            StationId: req.StationId.ToUpper(),
            Destination: "PREWORK",
            Message: $"✅ AGV dispatched — เรียก Pallet '{req.PalletId}' กลับจาก ASRS → Prework ({req.StationId.ToUpper()})"
        ));
    }
}
