using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Unload;

public class UnloadService(WmsDbContext db) : IUnloadService
{
    public async Task<ServiceResult> OpenSessionAsync(OpenUnloadRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        // S/N ที่ยัง STORED อยู่บน pallet นี้ — ใช้แปะเข้า item แต่ละ Part+Lot ด้านล่าง
        var availableSerials = await db.PartSerials
            .Include(s => s.ReceiptLine)
            .Where(s => s.PalletId == req.PalletId && s.Status == "STORED")
            .Select(s => new { s.PartId, LotNumber = s.ReceiptLine!.LotNumber, s.SerialNo })
            .ToListAsync();

        List<string> SerialsFor(string partId, string? lotNumber) => availableSerials
            .Where(s => s.PartId == partId && s.LotNumber == lotNumber)
            .Select(s => s.SerialNo)
            .ToList();

        if (pallet.Status == "UNLOADING")
        {
            // เรียงเอา session ล่าสุดก่อนเสมอ — pallet ควรมี session ค้างแค่ตัวเดียว แต่ถ้ามีหลายตัว (เช่น session เก่าตกค้าง)
            // ต้องไม่สุ่มหยิบ เพราะ EF/SQL ไม่รับประกันลำดับถ้าไม่ใส่ OrderBy
            var existing = await db.UnloadSessions
                .Include(s => s.UnloadLines)
                    .ThenInclude(l => l.Part)
                .Where(s => s.PalletId == req.PalletId
                         && (s.Status == "STEP1" || s.Status == "STEP2"))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (existing is null)
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' is UNLOADING but no active session found."));
            }

            // ดึง Condition จริงต่อ Part จาก ReceiptLines บน pallet เดียวกัน
            // (UnloadLines ไม่เก็บ Condition — เคย hardcode "NORMAL" ทำให้ StatusBadge ฝั่ง Flutter โชว์ผิด)
            var receiptConditions = await db.ReceiptLines
                .Where(l => l.PalletId == req.PalletId)
                .Select(l => new { l.PartId, l.Condition })
                .ToListAsync();
            var conditionMap = receiptConditions
                .GroupBy(x => x.PartId)
                .ToDictionary(g => g.Key, g => g.First().Condition);

            var existingItems = existing.UnloadLines.Select(l => new UnloadItemResponse(
                LineId: l.LineId,
                PartId: l.PartId,
                Owner: l.Part!.Owner,
                Brand: l.Part!.Brand,
                ItemDesc: l.Part!.ItemDesc,
                ImageUrl: l.Part!.ImageUrl,
                LotNumber: l.LotNumber,
                ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
                Qty: l.QtyUnloaded,
                Condition: conditionMap.GetValueOrDefault(l.PartId, "FG"),
                SerialNumbers: SerialsFor(l.PartId, l.LotNumber)
            )).ToList();

            var confirmedLineIds = existing.UnloadLines
                .Where(l => l.Status == "CONFIRMED")
                .Select(l => l.LineId)
                .ToList();

            return ServiceResult.Ok(new OpenUnloadResponse(
                SessionId: existing.SessionId,
                PalletId: req.PalletId,
                Status: existing.Status,
                Items: existingItems,
                ConfirmedLineIds: confirmedLineIds
            ));
        }

        if (pallet.Status is not "REPLENISH")
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet ต้องเป็นสถานะ REPLENISH เท่านั้นถึงจะ Unload ได้ (ปัจจุบัน: {pallet.Status})"));
        }

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return ServiceResult.NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        var receiptLines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
            .ToListAsync();

        if (receiptLines.Count == 0)
            return ServiceResult.BadRequest(new ApiError($"No items on pallet '{req.PalletId}'."));

        // แยกกลุ่มตาม Part+Lot (ไม่ใช่ Part อย่างเดียว) — 1 Part อาจมีหลาย Lot บน pallet เดียวกัน
        var grouped = receiptLines
            .GroupBy(rl => new { rl.PartId, rl.LotNumber })
            .ToList();

        // สร้างแค่ในหน่วยความจำก่อน — ยังไม่ Add ลง db จนกว่าจะรู้ว่ามีของให้ unload จริง
        // กันไม่ให้เกิด UnloadSession ว่างเปล่าค้าง DB เวลาไม่มีอะไรเหลือให้ unload (เคยเกิด bug นี้มาแล้ว)
        var linesToAdd = new List<UnloadLine>();

        foreach (var g in grouped)
        {
            var partId = g.Key.PartId;
            var lotNumber = g.Key.LotNumber;
            var firstLine = g.First();
            var totalOnPallet = g.Sum(rl => rl.QtyReceived);

            // ใช้ ReceivedAt ของ ReceiptLines รอบปัจจุบันเป็นตัวแบ่งรอบ
            // นับ UnloadLines ที่ "สร้าง" หลัง ReceiptLine → เป็นของรอบเดียวกัน (ต้องใช้ CreatedAt ไม่ใช่ UpdatedAt
            // เพราะ UpdatedAt ขยับทุกครั้งที่ confirm ทำให้ UnloadLine รอบเก่าที่เพิ่ง confirm ดันมาทับรอบใหม่ได้)
            // ข้าม UnloadLines รอบเก่า (Pallet หมุนเวียนใหม่) ที่สร้างก่อน ReceiptLine
            var earliestReceived = g.Min(rl => rl.ReceivedAt);

            var alreadyUnloaded = await db.UnloadLines
                .Where(l => l.PalletId == req.PalletId
                          && l.PartId == partId
                          && l.LotNumber == lotNumber
                          && l.CreatedAt >= earliestReceived
                          && (l.Status == "CONFIRMED" || l.Status == "LOADED" || l.Status == "RETURNED"))
                .SumAsync(l => (int?)l.QtyUnloaded) ?? 0;

            var remaining = totalOnPallet - alreadyUnloaded;
            if (remaining <= 0)
                continue;

            linesToAdd.Add(new UnloadLine
            {
                PalletId = req.PalletId,
                PartId = partId,
                LotNumber = lotNumber,
                ExpiredDate = firstLine.ExpiredDate,
                QtyUnloaded = remaining,
                Status = "PENDING",
                OperatorId = req.OperatorId,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (linesToAdd.Count == 0)
            return ServiceResult.BadRequest(new ApiError($"No remaining items to unload on pallet '{req.PalletId}'."));

        var session = new UnloadSession
        {
            PalletId = req.PalletId,
            OperatorId = req.OperatorId,
            Status = "STEP1",
            CreatedAt = DateTime.UtcNow
        };
        db.UnloadSessions.Add(session);

        foreach (var line in linesToAdd)
        {
            line.Session = session; // EF ผูก SessionId ให้เองตอน SaveChanges (session ยังไม่มี Id จริงตอนนี้)
            db.UnloadLines.Add(line);
        }

        pallet.Status = "UNLOADING";
        pallet.Location = "UNLOAD";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(); // หลังจุดนี้ linesToAdd แต่ละตัวจะมี LineId จริงแล้ว

        var partsById = receiptLines
            .GroupBy(rl => rl.PartId)
            .ToDictionary(g => g.Key, g => g.First().Part!);

        var itemsList = linesToAdd.Select(line =>
        {
            var part = partsById[line.PartId];
            var condition = receiptLines.First(rl => rl.PartId == line.PartId && rl.LotNumber == line.LotNumber).Condition;
            return new UnloadItemResponse(
                LineId: line.LineId,
                PartId: line.PartId,
                Owner: part.Owner,
                Brand: part.Brand,
                ItemDesc: part.ItemDesc,
                ImageUrl: part.ImageUrl,
                LotNumber: line.LotNumber,
                ExpiredDate: line.ExpiredDate?.ToString("yyyy-MM-dd"),
                Qty: line.QtyUnloaded,
                Condition: condition,
                SerialNumbers: SerialsFor(line.PartId, line.LotNumber)
            );
        }).ToList();

        return ServiceResult.Ok(new OpenUnloadResponse(
            SessionId: session.SessionId,
            PalletId: req.PalletId,
            Status: session.Status,
            Items: itemsList,
            ConfirmedLineIds: []
        ));
    }

    public async Task<ServiceResult> ConfirmUnloadAsync(ConfirmUnloadRequest req)
    {
        var session = await db.UnloadSessions
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId && s.Status == "STEP1");

        if (session is null)
            return ServiceResult.BadRequest(new ApiError("Invalid session or not in STEP1."));

        // LineId ระบุ Part+Lot ที่แน่นอน — กันเคส Part เดียวกันมีหลาย Lot บน pallet เดียวกัน
        var line = await db.UnloadLines
            .FirstOrDefaultAsync(l => l.LineId == req.LineId
                                   && l.SessionId == req.SessionId
                                   && l.Status == "PENDING");

        if (line is null)
        {
            var existingLine = await db.UnloadLines
                .FirstOrDefaultAsync(l => l.LineId == req.LineId && l.SessionId == req.SessionId);

            if (existingLine is null)
                return ServiceResult.NotFound(new ApiError($"Line {req.LineId} not found in session."));

            return ServiceResult.BadRequest(new ApiError(
                $"Part '{existingLine.PartId}' (Line {req.LineId}) ไม่มีของเหลือให้ unload แล้ว (status: {existingLine.Status})"));
        }

        if (line.PartId != req.PartId)
            return ServiceResult.BadRequest(new ApiError(
                $"Line {req.LineId} เป็นของ Part '{line.PartId}' ไม่ตรงกับ '{req.PartId}'"));

        var originalQty = line.QtyUnloaded;
        if (req.QtyUnloaded.HasValue)
        {
            if (req.QtyUnloaded.Value <= 0)
                return ServiceResult.BadRequest(new ApiError("จำนวนต้องมากกว่า 0"));

            if (req.QtyUnloaded.Value > line.QtyUnloaded)
                return ServiceResult.BadRequest(new ApiError($"จำนวนเกินที่มีบน Pallet ({line.QtyUnloaded})"));

            line.QtyUnloaded = req.QtyUnloaded.Value;
        }

        // ── สินค้าที่มี S/N (ยัง STORED บน pallet นี้) ต้องสแกน S/N ให้ครบก่อน confirm unload ──
        var scannedSerials = req.SerialNumbers?
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList() ?? [];

        var availableSerials = await db.PartSerials
            .Include(s => s.ReceiptLine)
            .Where(s => s.PartId == req.PartId
                     && s.Status == "STORED"
                     && s.PalletId == line.PalletId
                     && s.ReceiptLine!.LotNumber == line.LotNumber)
            .ToListAsync();

        if (availableSerials.Count > 0 || scannedSerials.Count > 0)
        {
            if (scannedSerials.Count == 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"กรุณาสแกน S/N สำหรับ Part '{req.PartId}' ก่อน Confirm Unload"));

            if (scannedSerials.Count != line.QtyUnloaded)
                return ServiceResult.BadRequest(new ApiError(
                    $"จำนวน S/N ({scannedSerials.Count}) ไม่ตรงกับจำนวนที่ Unload ({line.QtyUnloaded})"));

            var availableSet = availableSerials.Select(s => s.SerialNo).ToHashSet();
            var missing = scannedSerials.Where(sn => !availableSet.Contains(sn)).ToList();
            if (missing.Count > 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ไม่พบหรือไม่พร้อม Unload: {string.Join(", ", missing)}"));
        }

        line.Status = "CONFIRMED";
        line.ConfirmedAt = DateTime.UtcNow;
        line.UpdatedAt = DateTime.UtcNow;

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
                OperatorId = line.OperatorId,
                CreatedAt = DateTime.UtcNow,
            });
        }

        var receiptLines = await db.ReceiptLines
            .Where(r => r.PalletId == line.PalletId
                     && r.PartId == req.PartId
                     && r.LotNumber == line.LotNumber
                     && r.Status == "PALLETIZED")
            .ToListAsync();

        var totalQtyOnPallet = receiptLines.Sum(r => r.QtyReceived);

        if (receiptLines.Count > 0)
        {
            // ใช้ ReceivedAt ของ ReceiptLines เป็นตัวแบ่งรอบ (เทียบกับ CreatedAt ไม่ใช่ UpdatedAt — เหตุผลเดียวกับ OpenSessionAsync)
            var earliestReceived = receiptLines.Min(r => r.ReceivedAt);

            var previouslyUnloaded = await db.UnloadLines
                .Where(l => l.PalletId == line.PalletId
                          && l.PartId == req.PartId
                          && l.LotNumber == line.LotNumber
                          && l.LineId != line.LineId
                          && l.CreatedAt >= earliestReceived
                          && (l.Status == "CONFIRMED" || l.Status == "LOADED" || l.Status == "RETURNED"))
                .SumAsync(l => (int?)l.QtyUnloaded) ?? 0;

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

        var allLines = await db.UnloadLines
            .Where(l => l.SessionId == req.SessionId)
            .ToListAsync();
        var pendingCount = allLines.Count(l => l.Status == "PENDING");
        var confirmedCount = allLines.Count(l => l.Status == "CONFIRMED");
        var totalCount = allLines.Count;
        var allConfirmed = pendingCount == 0;

        if (allConfirmed)
        {
            session.Status = "STEP2";
            session.Step1DoneAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return ServiceResult.Ok(new ConfirmUnloadResponse(
            Success: true,
            Message: allConfirmed
                ? "✅ All confirmed. Proceed to Step 2."
                : $"Confirmed {confirmedCount}/{totalCount}.",
            ConfirmedCount: confirmedCount,
            TotalCount: totalCount,
            AllConfirmed: allConfirmed
        ));
    }

    public async Task<ServiceResult> ReturnPalletToAsisAsync(ReturnPalletToAsisRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (req.SessionId.HasValue)
        {
            var session = await db.UnloadSessions
                .FirstOrDefaultAsync(s => s.SessionId == req.SessionId.Value
                                       && (s.Status == "STEP1" || s.Status == "STEP2"));
            if (session is not null)
            {
                var pendingLines = await db.UnloadLines
                    .Where(l => l.SessionId == req.SessionId.Value && l.Status == "PENDING")
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

        var remainingLines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
            .ToListAsync();

        if (remainingLines.Count > 0)
        {
            var condition = remainingLines.First().Condition;
            pallet.Type = condition;
            pallet.Status = "REPLENISH";
            pallet.Location = "REPLENISH";
        }
        else
        {
            pallet.Type = null;
            pallet.Status = "AVAILABLE";
            pallet.Location = null;
        }

        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new ApiSuccess(true,
            remainingLines.Count > 0
                ? $"✅ Pallet '{req.PalletId}' → AGV กำลังรับกลับ ASRS"
                : $"✅ Pallet '{req.PalletId}' ว่างแล้ว"));
    }
}
