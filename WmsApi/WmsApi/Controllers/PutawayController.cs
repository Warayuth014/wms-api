using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/putaway")]
public class PutawayController(WmsDbContext db) : ControllerBase
{
    private static readonly HashSet<string> ValidDestinations =
        new(StringComparer.OrdinalIgnoreCase) { "ASRS", "PREWORK", "REPLENISH" };

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
                "ค่าที่รองรับ: ASRS, PREWORK, REPLENISH"));

        // FG → ไปได้แค่ ASRS หรือ REPLENISH
        if (pallet.Type == "FG" && dest == "PREWORK")
            return BadRequest(new ApiError(
                "Pallet ประเภท FG ไม่สามารถส่งไป PREWORK ได้",
                "FG รองรับ ASRS หรือ REPLENISH เท่านั้น"));

        // PW → ไปได้แค่ ASRS หรือ PREWORK
        if (pallet.Type == "PW" && dest == "REPLENISH")
            return BadRequest(new ApiError(
                "Pallet ประเภท PW ไม่สามารถส่งไป REPLENISH ได้",
                "PW รองรับ ASRS หรือ PREWORK เท่านั้น"));

        // Wrapping ใช้ได้กับ ASRS เท่านั้น
        if (req.WrappingRequired && dest != "ASRS")
            return BadRequest(new ApiError(
                "WrappingRequired ใช้ได้กับ Destination ASRS เท่านั้น"));

        // ── PW-STN: convert PW → FG ────────────
        var isPWStation = req.StationId.StartsWith("PW-STN", StringComparison.OrdinalIgnoreCase);
        if (isPWStation && pallet.Type == "PW" && req.ConvertToFG)
        {
            pallet.Type = "FG";

            var linesOnPallet = await db.ReceiptLines
                .Where(l => l.PalletId == req.PalletId && l.Condition == "PW")
                .ToListAsync();
            foreach (var l in linesOnPallet)
                l.Condition = "FG";
        }

        // ── เช็คว่า Station ว่างหรือไม่ ─────────
        var busySession = await db.PutawaySessions
            .FirstOrDefaultAsync(s => s.StationId == req.StationId.ToUpper()
                                   && s.Status == "AGV_DISPATCHED");
        if (busySession is not null)
            return BadRequest(new ApiError(
                $"Station '{req.StationId}' ไม่ว่าง",
                $"มี Pallet '{busySession.PalletId}' อยู่ที่ Station นี้แล้ว กรุณารอ AGV มารับก่อน"));

        // ── สร้าง PutawaySession ────────────────
        var session = new PutawaySession
        {
            PalletId = req.PalletId,
            StationId = req.StationId.ToUpper(),
            Destination = dest,
            Status = "AGV_DISPATCHED",
            WrappingRequired = req.WrappingRequired,
            OperatorId = req.OperatorId,
            CreatedAt = DateTime.UtcNow
        };
        db.PutawaySessions.Add(session);
        await db.SaveChangesAsync(); // SaveChanges เพื่อให้ session.PutawayId ถูก generate

        // ── Wrapping Machine — บันทึกว่าผ่าน Wrapping แล้ว dispatch ทันที ──
        if (req.WrappingRequired)
        {
            db.WrappingSessions.Add(new WrappingSession
            {
                PutawayId = session.PutawayId,
                PalletId = req.PalletId,
                Status = "COMPLETED",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        }

        if (dest == "PREWORK")
        {
            // ── ShipX Queue (Pre Work → AMR path) ─
            var lines = await db.ReceiptLines
                .Include(l => l.Part)
                .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
                .ToListAsync();

            var payloadObj = new
            {
                putawayId = session.PutawayId,
                palletId = req.PalletId,
                stationId = req.StationId.ToUpper(),
                operatorId = req.OperatorId,
                items = lines.Select(l => new
                {
                    partId = l.PartId,
                    lotNumber = l.LotNumber,
                    expiredDate = l.ExpiredDate?.ToString("yyyy-MM-dd"),
                    qty = l.QtyReceived,
                    condition = l.Condition
                })
            };

            db.ShipXQueues.Add(new ShipXQueue
            {
                PutawayId = session.PutawayId,
                PalletId = req.PalletId,
                Payload = JsonSerializer.Serialize(payloadObj),
                Status = "QUEUED",
                CreatedAt = DateTime.UtcNow
            });

            pallet.Status = "PREWORK";
        }
        else
        {
            // ASRS (no wrapping) หรือ REPLENISH → AMR dispatched ทันที
            pallet.Status = "IN_TRANSIT";
        }

        pallet.Location = dest;
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var convertMsg = isPWStation && req.ConvertToFG ? " (PW→FG converted)" : "";
        var wrappingMsg = req.WrappingRequired ? " (ผ่าน Wrapping Machine)" : "";
        var destLabel = dest switch
        {
            "ASRS" => "ASRS",
            "PREWORK" => "Pre Work Station",
            "REPLENISH" => "Replenish Station",
            _ => dest
        };

        return Ok(new ConfirmPutawayResponse(
            Success: true,
            PalletId: req.PalletId,
            StationId: req.StationId.ToUpper(),
            Destination: dest,
            Message: $"✅ Pallet '{req.PalletId}' → {destLabel}{convertMsg}{wrappingMsg}"
        ));
    }

    // =============================================
    // GET /api/putaway/station-status
    // ดึงสถานะ Station ทั้งหมด (pallet ที่ AGV_DISPATCHED อยู่)
    // =============================================
    [HttpGet("station-status")]
    public async Task<IActionResult> GetStationStatus()
    {
        var activeSessions = await db.PutawaySessions
            .Where(s => s.Status == "AGV_DISPATCHED")
            .Select(s => new
            {
                s.StationId,
                s.PalletId,
                s.Destination,
                s.CreatedAt
            })
            .ToListAsync();

        // ดึงข้อมูลสินค้าบน pallet ที่ยังอยู่ใน station
        var palletIds = activeSessions.Select(s => s.PalletId).Distinct().ToList();
        var palletItems = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => palletIds.Contains(l.PalletId) && l.Status == "PALLETIZED")
            .GroupBy(l => l.PalletId)
            .Select(g => new
            {
                PalletId = g.Key,
                Items = g.Select(l => new
                {
                    l.PartId,
                    l.Part!.ItemDesc,
                    Qty = l.QtyReceived
                }).ToList()
            })
            .ToListAsync();

        var palletItemsDict = palletItems.ToDictionary(p => p.PalletId, p => p.Items);

        var result = activeSessions.Select(s => new
        {
            s.StationId,
            s.PalletId,
            s.Destination,
            s.CreatedAt,
            Items = palletItemsDict.GetValueOrDefault(s.PalletId, [])
        });

        return Ok(result);
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

        if (pallet.Status is not ("IN_TRANSIT" or "RETURNING" or "STORED"))
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่พร้อมเรียกกลับ (สถานะ: {pallet.Status})"));

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        // เช็คว่า Station ว่างหรือไม่
        var busySession = await db.PutawaySessions
            .FirstOrDefaultAsync(s => s.StationId == req.StationId.ToUpper()
                                   && s.Status == "AGV_DISPATCHED");
        if (busySession is not null)
            return BadRequest(new ApiError(
                $"Station '{req.StationId}' ไม่ว่าง",
                $"มี Pallet '{busySession.PalletId}' อยู่ที่ Station นี้แล้ว กรุณารอ AGV มารับก่อน"));

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
