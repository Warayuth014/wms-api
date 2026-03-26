using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/cancel")]
public class CancelController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // POST /api/cancel/request
    // Operator ขอ cancel
    // =============================================
    [HttpPost("request")]
    public async Task<IActionResult> RequestCancel([FromBody] CancelRequest req)
    {
        // ตรวจ user
        var user = await db.Users.FindAsync(req.RequestBy);
        if (user is null)
            return NotFound(new ApiError($"User '{req.RequestBy}' not found."));

        // ตรวจว่า RefType ถูกและ RefId มีอยู่จริง
        bool refExists = req.RefType switch
        {
            "ReceiptLine" => await db.ReceiptLines
                                .AnyAsync(l => l.LineId == req.RefId),
            "UnloadLine" => await db.UnloadLines
                                .AnyAsync(l => l.LineId == req.RefId),
            "BasketLine" => await db.BasketLines
                                .AnyAsync(l => l.LineId == req.RefId),
            _ => false
        };

        if (!refExists)
            return NotFound(new ApiError(
                $"{req.RefType} id '{req.RefId}' not found."));

        // ตรวจว่ามี pending cancel อยู่แล้วไหม
        var existing = await db.CancelLogs
            .FirstOrDefaultAsync(c => c.RefType == req.RefType
                                   && c.RefId == req.RefId
                                   && c.Status == "PENDING");

        if (existing is not null)
            return BadRequest(new ApiError(
                "Cancel request already pending for this item."));

        var log = new CancelLog
        {
            RefType = req.RefType,
            RefId = req.RefId,
            Reason = req.Reason,
            RequestBy = req.RequestBy,
            Status = "PENDING"
        };

        db.CancelLogs.Add(log);
        await db.SaveChangesAsync();

        return Ok(new CancelLogResponse(
            CancelId: log.CancelId,
            RefType: log.RefType,
            RefId: log.RefId,
            Reason: log.Reason,
            RequestBy: log.RequestBy,
            ApprovedBy: null,
            Status: log.Status,
            CancelledAt: null
        ));
    }

    // =============================================
    // POST /api/cancel/approve
    // Supervisor approve → mark CANCELLED
    // =============================================
    [HttpPost("approve")]
    public async Task<IActionResult> ApproveCancel([FromBody] ApproveCancelRequest req)
    {
        // ตรวจว่าเป็น SUPERVISOR
        var supervisor = await db.Users.FindAsync(req.ApprovedBy);
        if (supervisor is null)
            return NotFound(new ApiError($"User '{req.ApprovedBy}' not found."));

        if (supervisor.Role != "SUPERVISOR")
            return BadRequest(new ApiError(
                "Only SUPERVISOR can approve cancellations."));

        var log = await db.CancelLogs.FindAsync(req.CancelId);
        if (log is null)
            return NotFound(new ApiError(
                $"Cancel request '{req.CancelId}' not found."));

        if (log.Status != "PENDING")
            return BadRequest(new ApiError(
                $"Cancel request already '{log.Status}'."));

        log.ApprovedBy = req.ApprovedBy;
        log.Status = "APPROVED";
        log.CancelledAt = DateTime.UtcNow;

        // mark record จริงว่า CANCELLED
        switch (log.RefType)
        {
            case "ReceiptLine":
                var rl = await db.ReceiptLines.FindAsync(log.RefId);
                if (rl is not null)
                {
                    // reverse QtyReceived ใน POItem
                    var poItem = await db.POItems
                        .FirstOrDefaultAsync(i => i.POId == rl.POId
                                               && i.PartId == rl.PartId);
                    if (poItem is not null)
                    {
                        poItem.QtyReceived = Math.Max(0,
                            poItem.QtyReceived - rl.QtyReceived);
                        poItem.Status = poItem.QtyReceived == 0
                                        ? "PENDING"
                                        : poItem.QtyReceived >= poItem.QtyOrdered
                                        ? "RECEIVED"
                                        : "PARTIAL";
                    }
                    rl.Status = "CANCELLED";
                    rl.UpdatedAt = DateTime.UtcNow;
                }
                break;

            case "UnloadLine":
                var ul = await db.UnloadLines.FindAsync(log.RefId);
                if (ul is not null)
                {
                    ul.Status = "CANCELLED";
                    ul.UpdatedAt = DateTime.UtcNow;
                }
                break;

            case "BasketLine":
                var bl = await db.BasketLines.FindAsync(log.RefId);
                if (bl is not null)
                    bl.Status = "CANCELLED";
                break;
        }

        await db.SaveChangesAsync();

        return Ok(new CancelLogResponse(
            CancelId: log.CancelId,
            RefType: log.RefType,
            RefId: log.RefId,
            Reason: log.Reason,
            RequestBy: log.RequestBy,
            ApprovedBy: log.ApprovedBy,
            Status: log.Status,
            CancelledAt: log.CancelledAt
        ));
    }

    // =============================================
    // POST /api/cancel/reject/{cancelId}
    // Supervisor reject
    // =============================================
    [HttpPost("reject/{cancelId}")]
    public async Task<IActionResult> RejectCancel(
        int cancelId, [FromQuery] string supervisorId)
    {
        var supervisor = await db.Users.FindAsync(supervisorId);
        if (supervisor is null)
            return NotFound(new ApiError($"User '{supervisorId}' not found."));

        if (supervisor.Role != "SUPERVISOR")
            return BadRequest(new ApiError(
                "Only SUPERVISOR can reject cancellations."));

        var log = await db.CancelLogs.FindAsync(cancelId);
        if (log is null)
            return NotFound(new ApiError(
                $"Cancel request '{cancelId}' not found."));

        if (log.Status != "PENDING")
            return BadRequest(new ApiError(
                $"Cancel request already '{log.Status}'."));

        log.ApprovedBy = supervisorId;
        log.Status = "REJECTED";

        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true,
            $"Cancel request '{cancelId}' rejected."));
    }

    // =============================================
    // GET /api/cancel/pending
    // ดู list ที่รอ approve
    // =============================================
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var logs = await db.CancelLogs
            .Where(l => l.Status == "PENDING")
            .OrderBy(l => l.CancelId)
            .Select(l => new CancelLogResponse(
                l.CancelId,
                l.RefType,
                l.RefId,
                l.Reason,
                l.RequestBy,
                l.ApprovedBy,
                l.Status,
                l.CancelledAt))
            .ToListAsync();

        return Ok(logs);
    }
}