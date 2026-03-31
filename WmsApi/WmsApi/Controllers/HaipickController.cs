using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

/// <summary>
/// Haipick Controller — Internal API แทน external Haipick system
/// จัดการ Tote inventory (on-hand stock ใน Tote storage)
/// </summary>
[ApiController]
[Route("api/haipick")]
public class HaipickController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // GET /api/haipick/inventory
    // ดู on-hand stock ทั้งหมด จัดกลุ่มตาม Part
    // =============================================
    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory()
    {
        var rows = await db.ToteInventory
            .Include(x => x.Tote)
            .Include(x => x.Part)
            .Where(x => x.QtyOnHand > 0)
            .OrderBy(x => x.PartId)
            .ToListAsync();

        var grouped = rows
            .GroupBy(x => x.PartId)
            .Select(g => new HaipickInventoryItem(
                PartId: g.Key,
                Owner: g.First().Part!.Owner,
                Brand: g.First().Part!.Brand,
                ItemDesc: g.First().Part!.ItemDesc,
                TotalQtyOnHand: g.Sum(x => x.QtyOnHand),
                ToteBreakdown: g.Select(x => new ToteBreakdownItem(
                    ToteId: x.ToteId,
                    Label: x.Tote!.Label,
                    QtyOnHand: x.QtyOnHand
                )).ToList()
            )).ToList();

        return Ok(new HaipickInventoryResponse(grouped));
    }

    // =============================================
    // GET /api/haipick/inventory/{partId}
    // ดู on-hand ของ Part นั้น (ทุก Tote)
    // =============================================
    [HttpGet("inventory/{partId}")]
    public async Task<IActionResult> GetInventoryForPart(string partId)
    {
        var part = await db.Parts.FindAsync(partId);
        if (part is null)
            return NotFound(new ApiError($"Part '{partId}' not found."));

        var rows = await db.ToteInventory
            .Include(x => x.Tote)
            .Where(x => x.PartId == partId)
            .OrderByDescending(x => x.QtyOnHand)
            .ToListAsync();

        var total = rows.Sum(x => x.QtyOnHand);
        var breakdown = rows.Select(x => new ToteBreakdownItem(
            ToteId: x.ToteId,
            Label: x.Tote!.Label,
            QtyOnHand: x.QtyOnHand
        )).ToList();

        return Ok(new HaipickInventoryItem(
            PartId: partId,
            Owner: part.Owner,
            Brand: part.Brand,
            ItemDesc: part.ItemDesc,
            TotalQtyOnHand: total,
            ToteBreakdown: breakdown
        ));
    }

    // =============================================
    // GET /api/haipick/totes
    // รายการ Tote ทั้งหมด
    // =============================================
    [HttpGet("totes")]
    public async Task<IActionResult> GetTotes()
    {
        var totes = await db.Totes
            .OrderBy(x => x.ToteId)
            .Select(x => new
            {
                x.ToteId,
                x.Label,
                x.Status,
                x.Location,
                x.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { items = totes });
    }

    // =============================================
    // POST /api/haipick/totes
    // สร้าง Tote ใหม่ (seed data / admin)
    // =============================================
    [HttpPost("totes")]
    public async Task<IActionResult> CreateTote([FromBody] CreateToteRequest req)
    {
        if (await db.Totes.AnyAsync(x => x.ToteId == req.ToteId))
            return BadRequest(new ApiError($"Tote '{req.ToteId}' มีอยู่แล้ว"));

        db.Totes.Add(new Tote
        {
            ToteId = req.ToteId.ToUpper(),
            Label = req.Label,
            Status = "AVAILABLE",
            Location = "STORAGE",
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return Ok(new ApiSuccess(true, $"✅ สร้าง Tote '{req.ToteId}' เรียบร้อย"));
    }

    // =============================================
    // POST /api/haipick/receive-tote
    // Haipick รับ Tote เข้า Storage (update on-hand)
    // เรียกหลัง complete-session เพื่อ sync กับ Haipick
    // =============================================
    [HttpPost("receive-tote")]
    public async Task<IActionResult> ReceiveTote([FromBody] ReceiveToteRequest req)
    {
        var tote = await db.Totes.FindAsync(req.ToteId);
        if (tote is null)
            return NotFound(new ApiError($"Tote '{req.ToteId}' not found."));

        var updatedItems = new List<ToteInventoryItemDto>();

        foreach (var item in req.Items)
        {
            var part = await db.Parts.FindAsync(item.PartId);
            if (part is null) continue;

            var inv = await db.ToteInventory
                .FirstOrDefaultAsync(x => x.ToteId == req.ToteId && x.PartId == item.PartId);

            if (inv is null)
            {
                inv = new ToteInventory
                {
                    ToteId = req.ToteId,
                    PartId = item.PartId,
                    QtyOnHand = item.QtyOnHand,
                    UpdatedAt = DateTime.UtcNow
                };
                db.ToteInventory.Add(inv);
            }
            else
            {
                inv.QtyOnHand = item.QtyOnHand;
                inv.UpdatedAt = DateTime.UtcNow;
            }

            updatedItems.Add(new ToteInventoryItemDto(item.PartId, part.ItemDesc, item.QtyOnHand));
        }

        tote.Status = "AVAILABLE";
        tote.Location = "STORAGE";
        tote.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            toteId = req.ToteId,
            updatedItems,
            message = $"✅ Tote '{req.ToteId}' รับเข้า Storage เรียบร้อย"
        });
    }
}

public record CreateToteRequest(string ToteId, string Label);
