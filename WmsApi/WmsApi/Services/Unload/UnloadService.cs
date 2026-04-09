using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Unload;

public class UnloadService(WmsDbContext db) : IUnloadService
{
    public async Task<ServiceResult> ScanPalletAsync(string palletId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"Pallet '{palletId}' not found."));

        if (pallet.Status is not ("REPLENISH" or "UNLOADING"))
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{palletId}' ไม่พร้อม Unload (สถานะ: {pallet.Status}) — ต้องเป็น REPLENISH เท่านั้น"));
        }

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

        var needsLabeling = pallet.Type == "PW";

        return ServiceResult.Ok(new ScanPalletForUnloadResponse(
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

    public async Task<ServiceResult> ConfirmLabelingAsync(ConfirmLabelingRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status != "PW")
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet is not PW status (current: {pallet.Status})."));
        }

        pallet.Type = "FG";
        pallet.Status = "FG";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new ApiSuccess(true,
            $"Pallet '{req.PalletId}' changed to FG ✅ Ready to unload."));
    }

    public async Task<ServiceResult> OpenSessionAsync(OpenUnloadRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status == "UNLOADING")
        {
            var existing = await db.UnloadSessions
                .Include(s => s.UnloadLines)
                    .ThenInclude(l => l.Part)
                .FirstOrDefaultAsync(s => s.PalletId == req.PalletId
                                       && (s.Status == "STEP1" || s.Status == "STEP2"));

            if (existing is null)
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{req.PalletId}' is UNLOADING but no active session found."));
            }

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

            return ServiceResult.Ok(new OpenUnloadResponse(
                SessionId: existing.SessionId,
                PalletId: req.PalletId,
                Status: existing.Status,
                Items: existingItems,
                ConfirmedPartIds: confirmedPartIds
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

        var session = new UnloadSession
        {
            PalletId = req.PalletId,
            OperatorId = req.OperatorId,
            Status = "STEP1",
            CreatedAt = DateTime.UtcNow
        };

        db.UnloadSessions.Add(session);
        await db.SaveChangesAsync();

        var grouped = receiptLines
            .GroupBy(rl => rl.PartId)
            .ToList();

        var itemsList = new List<UnloadItemResponse>();

        foreach (var g in grouped)
        {
            var partId = g.Key;
            var firstLine = g.First();
            var totalOnPallet = g.Sum(rl => rl.QtyReceived);

            // นับเฉพาะ UnloadLines จาก Session ที่ยังไม่ COMPLETED (รอบปัจจุบัน)
            // เพื่อไม่ให้หัก UnloadLines จากรอบเก่าที่ Pallet เดิมเคย Unload ไปแล้ว
            var alreadyUnloaded = await db.UnloadLines
                .Include(l => l.Session)
                .Where(l => l.PalletId == req.PalletId
                          && l.PartId == partId
                          && l.Session!.Status != "COMPLETED"
                          && (l.Status == "CONFIRMED" || l.Status == "LOADED" || l.Status == "RETURNED"))
                .SumAsync(l => (int?)l.QtyUnloaded) ?? 0;

            var remaining = totalOnPallet - alreadyUnloaded;
            if (remaining <= 0)
                continue;

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
            return ServiceResult.BadRequest(new ApiError($"No remaining items to unload on pallet '{req.PalletId}'."));

        pallet.Status = "UNLOADING";
        pallet.Location = "UNLOAD";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ServiceResult.Ok(new OpenUnloadResponse(
            SessionId: session.SessionId,
            PalletId: req.PalletId,
            Status: session.Status,
            Items: itemsList,
            ConfirmedPartIds: []
        ));
    }

    public async Task<ServiceResult> ConfirmUnloadAsync(ConfirmUnloadRequest req)
    {
        var session = await db.UnloadSessions
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId && s.Status == "STEP1");

        if (session is null)
            return ServiceResult.BadRequest(new ApiError("Invalid session or not in STEP1."));

        var line = await db.UnloadLines
            .FirstOrDefaultAsync(l => l.SessionId == req.SessionId
                                   && l.PartId == req.PartId
                                   && l.Status == "PENDING");

        if (line is null)
        {
            var hasConfirmed = await db.UnloadLines
                .AnyAsync(l => l.SessionId == req.SessionId
                            && l.PartId == req.PartId
                            && l.Status == "CONFIRMED");

            return hasConfirmed
                ? ServiceResult.BadRequest(new ApiError($"Part '{req.PartId}' ไม่มีของเหลือให้ unload แล้ว"))
                : ServiceResult.NotFound(new ApiError($"Part '{req.PartId}' not found in session."));
        }

        var originalQty = line.QtyUnloaded;
        if (req.QtyUnloaded.HasValue)
        {
            if (req.QtyUnloaded.Value <= 0)
                return ServiceResult.BadRequest(new ApiError("จำนวนต้องมากกว่า 0"));

            if (req.QtyUnloaded.Value > line.QtyUnloaded)
                return ServiceResult.BadRequest(new ApiError($"จำนวนเกินที่มีบน Pallet ({line.QtyUnloaded})"));

            line.QtyUnloaded = req.QtyUnloaded.Value;
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
                OperatorId = line.OperatorId
            });
        }

        var receiptLines = await db.ReceiptLines
            .Where(r => r.PalletId == line.PalletId
                     && r.PartId == req.PartId
                     && r.Status == "PALLETIZED")
            .ToListAsync();

        var totalQtyOnPallet = receiptLines.Sum(r => r.QtyReceived);

        if (receiptLines.Count > 0)
        {
            // นับเฉพาะ UnloadLines จาก Session ที่ยังไม่ COMPLETED (รอบปัจจุบัน)
            var previouslyUnloaded = await db.UnloadLines
                .Include(l => l.Session)
                .Where(l => l.PalletId == line.PalletId
                          && l.PartId == req.PartId
                          && l.LineId != line.LineId
                          && l.Session!.Status != "COMPLETED"
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
