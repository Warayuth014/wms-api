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

        if (pallet.Status == "AVAILABLE")
            return BadRequest(new ApiError($"Pallet '{palletId}' is empty."));

        if (pallet.Status == "UNLOADING")
            return BadRequest(new ApiError($"Pallet '{palletId}' is currently being unloaded."));

        if (pallet.Status == "DONE")
            return BadRequest(new ApiError($"Pallet '{palletId}' already completed."));

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
            LotNumber: l.LotNumber,
            ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
            Qty: l.QtyReceived,
            Condition: l.Condition
        )).ToList();

        bool needsLabeling = pallet.Status == "PW";

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

        if (pallet.Status != "FG")
            return BadRequest(new ApiError(
                $"Pallet must be FG to unload (current: {pallet.Status})."));

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

        // สร้าง UnloadLines ล่วงหน้าทุก Part
        foreach (var rl in receiptLines)
        {
            db.UnloadLines.Add(new UnloadLine
            {
                SessionId = session.SessionId,
                PalletId = req.PalletId,
                PartId = rl.PartId,
                LotNumber = rl.LotNumber,
                ExpiredDate = rl.ExpiredDate,
                QtyUnloaded = rl.QtyReceived,
                Status = "PENDING",
                OperatorId = req.OperatorId
            });
        }

        pallet.Status = "UNLOADING";
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var items = receiptLines.Select(l => new UnloadItemResponse(
            PartId: l.PartId,
            Owner: l.Part!.Owner,
            Brand: l.Part!.Brand,
            ItemDesc: l.Part!.ItemDesc,
            LotNumber: l.LotNumber,
            ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
            Qty: l.QtyReceived,
            Condition: l.Condition
        )).ToList();

        return Ok(new OpenUnloadResponse(
            SessionId: session.SessionId,
            PalletId: req.PalletId,
            Status: session.Status,
            Items: items
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

        var line = await db.UnloadLines
            .FirstOrDefaultAsync(l => l.SessionId == req.SessionId
                                   && l.PartId == req.PartId);

        if (line is null)
            return NotFound(new ApiError($"Part '{req.PartId}' not found in session."));

        if (line.Status == "CONFIRMED")
            return BadRequest(new ApiError($"Part '{req.PartId}' already confirmed."));

        line.Status = "CONFIRMED";
        line.ConfirmedAt = DateTime.UtcNow;
        line.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // นับว่าครบไหม
        var allLines = await db.UnloadLines
            .Where(l => l.SessionId == req.SessionId).ToListAsync();
        var confirmedCount = allLines.Count(l => l.Status == "CONFIRMED");
        var totalCount = allLines.Count;
        var allConfirmed = confirmedCount == totalCount;

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
    // GET /api/unload/scan-basket/{basketId}
    // สแกน Basket
    // =============================================
    [HttpGet("scan-basket/{basketId}")]
    public async Task<IActionResult> ScanBasket(string basketId)
    {
        var basket = await db.Baskets.FindAsync(basketId);
        if (basket is null)
            return NotFound(new ApiError($"Basket '{basketId}' not found."));

        if (basket.Status == "DONE")
            return BadRequest(new ApiError($"Basket '{basketId}' is already done."));

        return Ok(new ScanBasketResponse(
            BasketId: basket.BasketId,
            Label: basket.Label,
            Zone: basket.Zone,
            Destination: basket.Destination,
            Status: basket.Status,
            Message: $"✅ Basket ready → {basket.Destination ?? "No destination"}"
        ));
    }

    // =============================================
    // POST /api/unload/load-to-basket
    // Step 2: Map สินค้าเข้าตะกร้า
    // =============================================
    [HttpPost("load-to-basket")]
    public async Task<IActionResult> LoadToBasket([FromBody] LoadToBasketRequest req)
    {
        var session = await db.UnloadSessions
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId
                                   && s.Status == "STEP2");

        if (session is null)
            return BadRequest(new ApiError("Invalid session or not in STEP2."));

        var basket = await db.Baskets.FindAsync(req.BasketId);
        if (basket is null)
            return NotFound(new ApiError($"Basket '{req.BasketId}' not found."));

        // ตรวจว่า Part นี้ confirm unload แล้ว
        var unloadLine = await db.UnloadLines
            .FirstOrDefaultAsync(l => l.SessionId == req.SessionId
                                   && l.PartId == req.PartId
                                   && l.Status == "CONFIRMED");

        if (unloadLine is null)
            return BadRequest(new ApiError(
                $"Part '{req.PartId}' not confirmed from pallet yet."));

        // ตรวจว่า load ไปแล้วหรือยัง
        var alreadyLoaded = await db.BasketLines
            .AnyAsync(b => b.SessionId == req.SessionId
                        && b.PartId == req.PartId
                        && b.Status == "LOADED");

        if (alreadyLoaded)
            return BadRequest(new ApiError($"Part '{req.PartId}' already loaded."));

        // สร้าง BasketLine
        db.BasketLines.Add(new BasketLine
        {
            SessionId = req.SessionId,
            BasketId = req.BasketId,
            PartId = req.PartId,
            PalletId = req.PalletId,
            LotNumber = unloadLine.LotNumber,
            ExpiredDate = unloadLine.ExpiredDate,
            QtyLoaded = unloadLine.QtyUnloaded,
            Status = "LOADED",
            OperatorId = req.OperatorId
        });

        if (basket.Status == "AVAILABLE")
        {
            basket.Status = "IN_USE";
            basket.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // นับว่าครบไหม
        var totalUnload = await db.UnloadLines
            .CountAsync(l => l.SessionId == req.SessionId
                          && l.Status == "CONFIRMED");
        var totalLoaded = await db.BasketLines
            .CountAsync(b => b.SessionId == req.SessionId
                          && b.Status == "LOADED");
        var allLoaded = totalLoaded >= totalUnload;

        // ครบ → COMPLETED
        if (allLoaded)
        {
            session.Status = "COMPLETED";
            session.CompletedAt = DateTime.UtcNow;

            // Pallet → AVAILABLE พร้อมใช้ใหม่
            var pallet = await db.Pallets.FindAsync(req.PalletId);
            if (pallet is not null)
            {
                pallet.Type = null;
                pallet.Status = "AVAILABLE";
                pallet.UpdatedAt = DateTime.UtcNow;
            }

            basket.Status = "DONE";
            basket.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
        }

        return Ok(new LoadToBasketResponse(
            Success: true,
            Message: allLoaded
                        ? "✅ All loaded. Pallet is now AVAILABLE."
                        : $"Loaded {totalLoaded}/{totalUnload}.",
            LoadedCount: totalLoaded,
            TotalCount: totalUnload,
            AllLoaded: allLoaded
        ));
    }
}