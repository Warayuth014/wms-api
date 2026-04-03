using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/unload")]
public class UnloadController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // GET /api/unload/scan-pallet/{palletId}
    // สแกน Pallet ตรวจ FG/PW
    // =============================================
    [HttpGet("scan-pallet/{palletId}")]
    public async Task<IActionResult> ScanPallet(string palletId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{palletId}' not found."));

        // รับเฉพาะ REPLENISH หรือ UNLOADING (resume)
        if (pallet.Status is not ("REPLENISH" or "UNLOADING"))
            return BadRequest(new ApiError(
                $"Pallet '{palletId}' ไม่พร้อม Unload (สถานะ: {pallet.Status}) — ต้องเป็น REPLENISH เท่านั้น"));

        // ดึงสินค้าที่อยู่บน Pallet
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == palletId
                     && l.Status == "PALLETIZED")
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

        bool needsLabeling = pallet.Type == "PW";

        return Ok(new ScanPalletForUnloadResponse(
            PalletId: pallet.PalletId,
            Type: pallet.Type ?? "-",
            Status: pallet.Status,
            NeedsLabeling: needsLabeling,
            Items: items,
            Message: needsLabeling
                           ? "⚠️ Pallet ยังไม่ติดสติ๊กเกอร์ กรุณาส่งไปจุด Labeling ก่อน"
                           : "✅ Pallet พร้อม unload"
        ));
    }

    // =============================================
    // POST /api/unload/confirm-labeling
    // ติดสติ๊กเกอร์แล้ว PW → FG
    // =============================================
    [HttpPost("confirm-labeling")]
    public async Task<IActionResult> ConfirmLabeling([FromBody] ConfirmLabelingRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status != "PW")
            return BadRequest(new ApiError(
                $"Pallet is not PW status (current: {pallet.Status})."));

        pallet.Type = "FG";
        pallet.Status = "FG";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            $"Pallet '{req.PalletId}' changed to FG ✅ Ready to unload."));
    }

    // =============================================
    // POST /api/unload/open-session
    // เปิด session unload + สร้าง UnloadLines อัตโนมัติ
    // =============================================
    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenUnloadRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        // ── Resume: ถ้า pallet กำลัง UNLOADING → คืน session เดิม (STEP1 หรือ STEP2) ──
        if (pallet.Status == "UNLOADING")
        {
            var existing = await db.UnloadSessions
                .Include(s => s.UnloadLines)
                    .ThenInclude(l => l.Part)
                .FirstOrDefaultAsync(s => s.PalletId == req.PalletId
                                       && (s.Status == "STEP1" || s.Status == "STEP2"));

            if (existing is null)
                return BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' is UNLOADING but no active session found."));

            var existingItems = existing.UnloadLines.Select(l => new UnloadItemResponse(
                PartId: l.PartId,
                Owner: l.Part!.Owner,
                Brand: l.Part!.Brand,
                ItemDesc: l.Part!.ItemDesc,
                ImageUrl: l.Part!.ImageUrl,
                LotNumber: l.LotNumber,
                ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
                Qty: l.QtyUnloaded,
                Condition: "NORMAL"
            )).ToList();

            var confirmedPartIds = existing.UnloadLines
                .Where(l => l.Status == "CONFIRMED")
                .Select(l => l.PartId)
                .ToList();

            return Ok(new OpenUnloadResponse(
                SessionId: existing.SessionId,
                PalletId: req.PalletId,
                Status: existing.Status,
                Items: existingItems,
                ConfirmedPartIds: confirmedPartIds
            ));
        }

        // อนุญาตเฉพาะ REPLENISH เท่านั้น
        if (pallet.Status is not "REPLENISH")
            return BadRequest(new ApiError(
                $"Pallet ต้องเป็นสถานะ REPLENISH เท่านั้นถึงจะ Unload ได้ (ปัจจุบัน: {pallet.Status})"));

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        // ดึงสินค้าบน Pallet
        var receiptLines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId
                     && l.Status == "PALLETIZED")
            .ToListAsync();

        if (receiptLines.Count == 0)
            return BadRequest(new ApiError($"No items on pallet '{req.PalletId}'."));

        // สร้าง UnloadSession
        var session = new UnloadSession
        {
            PalletId = req.PalletId,
            OperatorId = req.OperatorId,
            Status = "STEP1",
            CreatedAt = DateTime.UtcNow
        };

        db.UnloadSessions.Add(session);
        await db.SaveChangesAsync();

        // Group ReceiptLines by PartId (Part เดียวกันอาจมีหลาย ReceiptLine ต่าง Lot)
        var grouped = receiptLines
            .GroupBy(rl => rl.PartId)
            .ToList();

        var itemsList = new List<UnloadItemResponse>();

        foreach (var g in grouped)
        {
            var partId = g.Key;
            var firstLine = g.First();
            var totalOnPallet = g.Sum(rl => rl.QtyReceived);

            // คำนวณจำนวนที่เคย unload ไปแล้วจาก session ก่อนหน้า
            var alreadyUnloaded = await db.UnloadLines
                .Where(l => l.PalletId == req.PalletId
                          && l.PartId == partId
                          && (l.Status == "CONFIRMED" || l.Status == "LOADED" || l.Status == "RETURNED"))
                .SumAsync(l => (int?)l.QtyUnloaded) ?? 0;

            var remaining = totalOnPallet - alreadyUnloaded;
            if (remaining <= 0) continue; // ไม่มีของเหลือให้ unload

            db.UnloadLines.Add(new UnloadLine
            {
                SessionId = session.SessionId,
                PalletId = req.PalletId,
                PartId = partId,
                LotNumber = firstLine.LotNumber,
                ExpiredDate = firstLine.ExpiredDate,
                QtyUnloaded = remaining,
                Status = "PENDING",
                OperatorId = req.OperatorId
            });

            itemsList.Add(new UnloadItemResponse(
                PartId: partId,
                Owner: firstLine.Part!.Owner,
                Brand: firstLine.Part!.Brand,
                ItemDesc: firstLine.Part!.ItemDesc,
                ImageUrl: firstLine.Part!.ImageUrl,
                LotNumber: firstLine.LotNumber,
                ExpiredDate: firstLine.ExpiredDate?.ToString("yyyy-MM-dd"),
                Qty: remaining,
                Condition: firstLine.Condition
            ));
        }

        if (itemsList.Count == 0)
            return BadRequest(new ApiError($"No remaining items to unload on pallet '{req.PalletId}'."));

        pallet.Status = "UNLOADING";
        pallet.Location = "UNLOAD";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var items = itemsList;

        return Ok(new OpenUnloadResponse(
            SessionId: session.SessionId,
            PalletId: req.PalletId,
            Status: session.Status,
            Items: items,
            ConfirmedPartIds: []
        ));
    }

    // =============================================
    // POST /api/unload/confirm-unload
    // Step 1: Confirm นำสินค้าออกจาก Pallet
    // =============================================
    [HttpPost("confirm-unload")]
    public async Task<IActionResult> ConfirmUnload([FromBody] ConfirmUnloadRequest req)
    {
        var session = await db.UnloadSessions
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId
                                   && s.Status == "STEP1");

        if (session is null)
            return BadRequest(new ApiError("Invalid session or not in STEP1."));

        // หา PENDING line ของ Part นี้ (อาจมีหลายรอบ → เอาตัวแรก)
        var line = await db.UnloadLines
            .FirstOrDefaultAsync(l => l.SessionId == req.SessionId
                                   && l.PartId == req.PartId
                                   && l.Status == "PENDING");

        if (line is null)
        {
            // เช็คว่า confirm หมดแล้วหรือเปล่า
            var hasConfirmed = await db.UnloadLines
                .AnyAsync(l => l.SessionId == req.SessionId
                            && l.PartId == req.PartId
                            && l.Status == "CONFIRMED");
            return hasConfirmed
                ? BadRequest(new ApiError($"Part '{req.PartId}' ไม่มีของเหลือให้ unload แล้ว"))
                : NotFound(new ApiError($"Part '{req.PartId}' not found in session."));
        }

        // ถ้า client ส่ง QtyUnloaded มา → ใช้ค่านั้น (ต้อง > 0 และ <= ค่าเดิม)
        var originalQty = line.QtyUnloaded;
        if (req.QtyUnloaded.HasValue)
        {
            if (req.QtyUnloaded.Value <= 0)
                return BadRequest(new ApiError("จำนวนต้องมากกว่า 0"));
            if (req.QtyUnloaded.Value > line.QtyUnloaded)
                return BadRequest(new ApiError($"จำนวนเกินที่มีบน Pallet ({line.QtyUnloaded})"));
            line.QtyUnloaded = req.QtyUnloaded.Value;
        }

        line.Status = "CONFIRMED";
        line.ConfirmedAt = DateTime.UtcNow;
        line.UpdatedAt = DateTime.UtcNow;

        // ── Partial unload → สร้าง PENDING line ใหม่สำหรับส่วนที่เหลือ ──
        var remainder = originalQty - line.QtyUnloaded;
        if (remainder > 0)
        {
            db.UnloadLines.Add(new UnloadLine
            {
                SessionId = req.SessionId,
                PalletId = line.PalletId,
                PartId = req.PartId,
                LotNumber = line.LotNumber,
                ExpiredDate = line.ExpiredDate,
                QtyUnloaded = remainder,
                Status = "PENDING",
                OperatorId = line.OperatorId
            });
        }

        // อัปเดต ReceiptLine — ไม่แก้ QtyReceived เด็ดขาด
        // เช็คจาก UnloadLines ว่า unload รวมครบ QtyReceived หรือยัง
        var receiptLines = await db.ReceiptLines
            .Where(r => r.PalletId == line.PalletId
                     && r.PartId == req.PartId
                     && r.Status == "PALLETIZED")
            .ToListAsync();

        var totalQtyOnPallet = receiptLines.Sum(r => r.QtyReceived);

        if (receiptLines.Count > 0)
        {
            // รวม qty ที่ unload ไปแล้วจาก DB (ไม่รวม line ปัจจุบันที่ยังไม่ save)
            var previouslyUnloaded = await db.UnloadLines
                .Where(l => l.PalletId == line.PalletId
                          && l.PartId == req.PartId
                          && l.LineId != line.LineId
                          && (l.Status == "CONFIRMED" || l.Status == "LOADED" || l.Status == "RETURNED"))
                .SumAsync(l => (int?)l.QtyUnloaded) ?? 0;

            // บวก line ปัจจุบันที่เพิ่ง confirm (ยังไม่ save ลง DB)
            var totalUnloaded = previouslyUnloaded + line.QtyUnloaded;

            if (totalUnloaded >= totalQtyOnPallet)
            {
                foreach (var rl in receiptLines)
                {
                    rl.Status = "UNLOADED";
                    rl.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync();

        // นับว่าครบไหม (ไม่นับ PENDING ที่เหลือ)
        var allLines = await db.UnloadLines
            .Where(l => l.SessionId == req.SessionId).ToListAsync();
        var pendingCount = allLines.Count(l => l.Status == "PENDING");
        var confirmedCount = allLines.Count(l => l.Status == "CONFIRMED");
        var totalCount = allLines.Count;
        var allConfirmed = pendingCount == 0;

        // ครบ → เปลี่ยนเป็น STEP2 อัตโนมัติ
        if (allConfirmed)
        {
            session.Status = "STEP2";
            session.Step1DoneAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok(new ConfirmUnloadResponse(
            Success: true,
            Message: allConfirmed
                            ? "✅ All confirmed. Proceed to Step 2."
                            : $"Confirmed {confirmedCount}/{totalCount}.",
            ConfirmedCount: confirmedCount,
            TotalCount: totalCount,
            AllConfirmed: allConfirmed
        ));
    }

    // =============================================
    // POST /api/unload/return-pallet-to-asis
    // คืน Pallet ให้ AGV รับกลับ ASRS
    // =============================================
    [HttpPost("return-pallet-to-asis")]
    public async Task<IActionResult> ReturnPalletToAsis(
        [FromBody] ReturnPalletToAsisRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        // ปิด UnloadSession ที่ยังเปิดอยู่
        if (req.SessionId.HasValue)
        {
            var session = await db.UnloadSessions
                .FirstOrDefaultAsync(s => s.SessionId == req.SessionId.Value
                                       && (s.Status == "STEP1" || s.Status == "STEP2"));
            if (session is not null)
            {
                // Cancel UnloadLines ที่ยัง PENDING (ยังอยู่บน pallet)
                var pendingLines = await db.UnloadLines
                    .Where(l => l.SessionId == req.SessionId.Value
                             && l.Status == "PENDING")
                    .ToListAsync();
                foreach (var l in pendingLines)
                {
                    l.Status = "CANCELLED";
                    l.UpdatedAt = DateTime.UtcNow;
                }

                session.Status = "COMPLETED";
                session.CompletedAt = DateTime.UtcNow;
            }
        }

        // นับว่ายังมีของบน pallet ไหม
        var remainingLines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
            .ToListAsync();

        if (remainingLines.Count > 0)
        {
            // ยังมีของเหลือ → set Type ตาม condition ของสินค้าที่ยังอยู่
            var condition = remainingLines.First().Condition; // FG หรือ PW
            pallet.Type = condition;
            pallet.Status = "REPLENISH";
            pallet.Location = "REPLENISH";
        }
        else
        {
            // ไม่มีของเหลือ → ว่างเปล่า พร้อมใช้ใหม่
            pallet.Type = null;
            pallet.Status = "AVAILABLE";
            pallet.Location = null;
        }
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            remainingLines.Count > 0
                ? $"✅ Pallet '{req.PalletId}' → AGV กำลังรับกลับ ASRS"
                : $"✅ Pallet '{req.PalletId}' ว่างแล้ว"));
    }

    // asis-dispatch ย้ายไป SimulationController แล้ว
    // ใช้ POST /api/simulate/asrs/retrieve-pallet แทน
}