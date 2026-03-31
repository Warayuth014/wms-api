using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/replenish")]
public class ReplenishController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // GET /api/replenish/check-trigger
    // ตรวจว่า Part ไหนต้องเติม (OnHand < MinStock)
    // =============================================
    [HttpGet("check-trigger")]
    public async Task<IActionResult> CheckTrigger()
    {
        // รวม on-hand ต่อ Part จาก ToteInventory
        var onHandByPart = await db.ToteInventory
            .GroupBy(x => x.PartId)
            .Select(g => new { PartId = g.Key, QtyOnHand = g.Sum(x => x.QtyOnHand) })
            .ToListAsync();

        var onHandDict = onHandByPart.ToDictionary(x => x.PartId, x => x.QtyOnHand);

        // Part ที่มี MinStock/MaxStock กำหนดไว้
        var parts = await db.Parts
            .Where(p => p.MinStock != null && p.MaxStock != null)
            .ToListAsync();

        // Part ที่อยู่ใน active replenish order แล้ว (ไม่ต้องสร้างซ้ำ)
        var activePartIds = await db.ReplenishOrderLines
            .Where(l => l.Status != "COMPLETED" &&
                        l.ReplenishOrder!.Status != "COMPLETED")
            .Select(l => l.PartId)
            .Distinct()
            .ToListAsync();

        var needReplenish = new List<ReplenishTriggerItem>();

        foreach (var part in parts)
        {
            if (activePartIds.Contains(part.PartId)) continue;

            var onHand = onHandDict.GetValueOrDefault(part.PartId, 0);
            if (onHand < part.MinStock!.Value)
            {
                needReplenish.Add(new ReplenishTriggerItem(
                    PartId: part.PartId,
                    Owner: part.Owner,
                    Brand: part.Brand,
                    ItemDesc: part.ItemDesc,
                    ImageUrl: part.ImageUrl,
                    QtyOnHand: onHand,
                    MinStock: part.MinStock.Value,
                    MaxStock: part.MaxStock!.Value,
                    QtyRequired: part.MaxStock.Value - onHand
                ));
            }
        }

        return Ok(new CheckTriggerResponse(needReplenish.Count, needReplenish));
    }

    // =============================================
    // POST /api/replenish/create-order
    // สร้าง Replenish Order
    // =============================================
    [HttpPost("create-order")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateReplenishOrderRequest req)
    {
        if (req.Lines.Count == 0)
            return BadRequest(new ApiError("ต้องระบุอย่างน้อย 1 Part"));

        var order = new ReplenishOrder
        {
            Status = "PENDING",
            TriggeredBy = req.TriggeredBy.ToUpper() == "MANUAL" ? "MANUAL" : "SYSTEM",
            CreatedAt = DateTime.UtcNow
        };
        db.ReplenishOrders.Add(order);
        await db.SaveChangesAsync();

        var lineDtos = new List<ReplenishOrderLineDto>();

        foreach (var lineReq in req.Lines)
        {
            var part = await db.Parts.FindAsync(lineReq.PartId);
            if (part is null) continue;

            var line = new ReplenishOrderLine
            {
                OrderId = order.OrderId,
                PartId = lineReq.PartId,
                QtyRequired = lineReq.QtyRequired,
                QtyFilled = 0,
                Status = "PENDING"
            };
            db.ReplenishOrderLines.Add(line);
            await db.SaveChangesAsync();

            lineDtos.Add(new ReplenishOrderLineDto(
                LineId: line.LineId,
                PartId: part.PartId,
                Owner: part.Owner,
                Brand: part.Brand,
                ItemDesc: part.ItemDesc,
                ImageUrl: part.ImageUrl,
                QtyRequired: line.QtyRequired,
                QtyFilled: 0,
                Status: "PENDING"
            ));
        }

        return Ok(new ReplenishOrderResponse(
            OrderId: order.OrderId,
            Status: order.Status,
            TriggeredBy: order.TriggeredBy,
            CreatedAt: order.CreatedAt,
            Lines: lineDtos
        ));
    }

    // =============================================
    // GET /api/replenish/orders
    // รายการ Order ที่ยังทำไม่เสร็จ
    // =============================================
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await db.ReplenishOrders
            .Include(o => o.Lines)
                .ThenInclude(l => l.Part)
            .Where(o => o.Status != "COMPLETED")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(o => new ReplenishOrderResponse(
            OrderId: o.OrderId,
            Status: o.Status,
            TriggeredBy: o.TriggeredBy,
            CreatedAt: o.CreatedAt,
            Lines: o.Lines.Select(l => new ReplenishOrderLineDto(
                LineId: l.LineId,
                PartId: l.PartId,
                Owner: l.Part!.Owner,
                Brand: l.Part.Brand,
                ItemDesc: l.Part.ItemDesc,
                ImageUrl: l.Part.ImageUrl,
                QtyRequired: l.QtyRequired,
                QtyFilled: l.QtyFilled,
                Status: l.Status
            )).ToList()
        )).ToList();

        return Ok(new { items = result });
    }

    // =============================================
    // GET /api/replenish/orders/{orderId}
    // รายละเอียด Order
    // =============================================
    [HttpGet("orders/{orderId:int}")]
    public async Task<IActionResult> GetOrder(int orderId)
    {
        var order = await db.ReplenishOrders
            .Include(o => o.Lines)
                .ThenInclude(l => l.Part)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order is null)
            return NotFound(new ApiError($"Order #{orderId} not found."));

        return Ok(new ReplenishOrderResponse(
            OrderId: order.OrderId,
            Status: order.Status,
            TriggeredBy: order.TriggeredBy,
            CreatedAt: order.CreatedAt,
            Lines: order.Lines.Select(l => new ReplenishOrderLineDto(
                LineId: l.LineId,
                PartId: l.PartId,
                Owner: l.Part!.Owner,
                Brand: l.Part.Brand,
                ItemDesc: l.Part.ItemDesc,
                ImageUrl: l.Part.ImageUrl,
                QtyRequired: l.QtyRequired,
                QtyFilled: l.QtyFilled,
                Status: l.Status
            )).ToList()
        ));
    }

    // =============================================
    // GET /api/replenish/scan-tote/{toteId}
    // สแกน Tote เปล่า → ดูว่า Tote มีอะไรอยู่
    // =============================================
    [HttpGet("scan-tote/{toteId}")]
    public async Task<IActionResult> ScanTote(string toteId)
    {
        var tote = await db.Totes
            .Include(t => t.Inventory)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(t => t.ToteId == toteId.ToUpper());

        if (tote is null)
            return NotFound(new ApiError($"Tote '{toteId}' not found."));

        // ถ้า Tote กำลัง REPLENISHING → คืน session ปัจจุบันด้วย
        if (tote.Status == "REPLENISHING")
        {
            var existing = await db.ReplenishSessions
                .Where(s => s.ToteId == tote.ToteId && s.Status == "OPEN")
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            return Conflict(new
            {
                error = $"Tote '{toteId}' กำลังใช้งานอยู่ (REPLENISHING)",
                existingSessionId = existing?.SessionId,
                hint = "Resume session เดิมหรือรอให้เสร็จก่อน"
            });
        }

        var inventory = tote.Inventory
            .Select(i => new ToteInventoryItemDto(
                PartId: i.PartId,
                ItemDesc: i.Part!.ItemDesc,
                QtyOnHand: i.QtyOnHand
            )).ToList();

        return Ok(new ToteScanResponse(
            ToteId: tote.ToteId,
            Label: tote.Label,
            Status: tote.Status,
            Location: tote.Location,
            CurrentInventory: inventory
        ));
    }

    // =============================================
    // POST /api/replenish/open-session
    // เปิด Session: ผูก Tote + Pallet + Order
    // =============================================
    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenReplenishSessionRequest req)
    {
        var order = await db.ReplenishOrders
            .Include(o => o.Lines)
                .ThenInclude(l => l.Part)
            .FirstOrDefaultAsync(o => o.OrderId == req.OrderId);

        if (order is null)
            return NotFound(new ApiError($"Order #{req.OrderId} not found."));

        if (order.Status == "COMPLETED")
            return BadRequest(new ApiError($"Order #{req.OrderId} เสร็จสิ้นแล้ว"));

        var tote = await db.Totes.FindAsync(req.ToteId.ToUpper());
        if (tote is null)
            return NotFound(new ApiError($"Tote '{req.ToteId}' not found."));

        if (tote.Status == "REPLENISHING")
            return BadRequest(new ApiError(
                $"Tote '{req.ToteId}' กำลังใช้งานอยู่",
                "รอให้ Session ปัจจุบันเสร็จก่อน"));

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status != "REPLENISH")
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่ได้อยู่ที่ Replenish Station (สถานะ: {pallet.Status})",
                "Pallet ต้องมีสถานะ REPLENISH"));

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        // สร้าง Session
        var session = new ReplenishSession
        {
            OrderId = req.OrderId,
            ToteId = req.ToteId.ToUpper(),
            PalletId = req.PalletId,
            OperatorId = req.OperatorId,
            Status = "OPEN",
            CreatedAt = DateTime.UtcNow
        };
        db.ReplenishSessions.Add(session);
        await db.SaveChangesAsync();

        // สร้าง SessionLine สำหรับแต่ละ OrderLine ที่ยังไม่เสร็จ
        var pendingLines = order.Lines
            .Where(l => l.Status != "COMPLETED")
            .ToList();

        var lineDtos = new List<ReplenishSessionLineDto>();

        foreach (var ol in pendingLines)
        {
            var sl = new ReplenishSessionLine
            {
                SessionId = session.SessionId,
                PartId = ol.PartId,
                OrderLineId = ol.LineId,
                QtyFilled = 0,
                Status = "PENDING"
            };
            db.ReplenishSessionLines.Add(sl);
            await db.SaveChangesAsync();

            lineDtos.Add(new ReplenishSessionLineDto(
                LineId: sl.LineId,
                PartId: ol.PartId,
                Owner: ol.Part!.Owner,
                Brand: ol.Part.Brand,
                ItemDesc: ol.Part.ItemDesc,
                ImageUrl: ol.Part.ImageUrl,
                OrderLineId: ol.LineId,
                QtyRequired: ol.QtyRequired - ol.QtyFilled,
                QtyFilled: 0,
                SessionLineStatus: "PENDING",
                OrderLineStatus: ol.Status
            ));
        }

        // อัปเดต Tote และ Order
        tote.Status = "REPLENISHING";
        tote.Location = "REPLENISH_STATION";
        tote.UpdatedAt = DateTime.UtcNow;

        order.Status = "IN_PROGRESS";

        await db.SaveChangesAsync();

        return Ok(new ReplenishSessionResponse(
            SessionId: session.SessionId,
            OrderId: req.OrderId,
            ToteId: req.ToteId.ToUpper(),
            PalletId: req.PalletId,
            Status: "OPEN",
            Lines: lineDtos
        ));
    }

    // =============================================
    // POST /api/replenish/confirm-line
    // ยืนยันของที่ใส่ลง Tote แต่ละ Item
    // =============================================
    [HttpPost("confirm-line")]
    public async Task<IActionResult> ConfirmLine([FromBody] ConfirmReplenishLineRequest req)
    {
        var sl = await db.ReplenishSessionLines
            .Include(x => x.Session)
            .Include(x => x.OrderLine)
            .FirstOrDefaultAsync(x => x.LineId == req.SessionLineId);

        if (sl is null)
            return NotFound(new ApiError($"SessionLine #{req.SessionLineId} not found."));

        if (sl.Session!.SessionId != req.SessionId)
            return BadRequest(new ApiError("SessionLine ไม่ตรงกับ Session ที่ระบุ"));

        if (sl.Session.Status != "OPEN")
            return BadRequest(new ApiError("Session ปิดแล้ว ไม่สามารถยืนยันได้"));

        if (req.QtyFilled <= 0)
            return BadRequest(new ApiError("QtyFilled ต้องมากกว่า 0"));

        // อัปเดต SessionLine
        sl.QtyFilled = req.QtyFilled;
        sl.Status = "CONFIRMED";

        // อัปเดต OrderLine
        var ol = sl.OrderLine!;
        ol.QtyFilled += req.QtyFilled;

        if (ol.QtyFilled >= ol.QtyRequired)
            ol.Status = "COMPLETED";
        else
            ol.Status = "PARTIAL";

        await db.SaveChangesAsync();

        return Ok(new ConfirmReplenishLineResponse(
            SessionLineId: sl.LineId,
            PartId: sl.PartId,
            QtyFilled: sl.QtyFilled,
            SessionLineStatus: sl.Status,
            OrderLineStatus: ol.Status,
            OrderLineQtyFilled: ol.QtyFilled,
            OrderLineQtyRequired: ol.QtyRequired
        ));
    }

    // =============================================
    // POST /api/replenish/complete-session
    // ปิด Session → อัปเดต ToteInventory
    // =============================================
    [HttpPost("complete-session")]
    public async Task<IActionResult> CompleteSession([FromBody] CompleteReplenishSessionRequest req)
    {
        var session = await db.ReplenishSessions
            .Include(s => s.Lines)
                .ThenInclude(l => l.Part)
            .Include(s => s.ReplenishOrder)
                .ThenInclude(o => o!.Lines)
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId);

        if (session is null)
            return NotFound(new ApiError($"Session #{req.SessionId} not found."));

        if (session.Status != "OPEN")
            return BadRequest(new ApiError($"Session #{req.SessionId} ปิดแล้ว"));

        var unconfirmed = session.Lines.Count(l => l.Status != "CONFIRMED");
        if (unconfirmed > 0)
            return BadRequest(new ApiError(
                $"ยังมี {unconfirmed} รายการที่ยังไม่ยืนยัน",
                "กรุณายืนยันทุกรายการก่อน Complete"));

        // อัปเดต ToteInventory (upsert)
        var updatedInventory = new List<ToteInventoryItemDto>();

        foreach (var sl in session.Lines.Where(l => l.QtyFilled > 0))
        {
            var inv = await db.ToteInventory
                .FirstOrDefaultAsync(x => x.ToteId == session.ToteId && x.PartId == sl.PartId);

            if (inv is null)
            {
                inv = new ToteInventory
                {
                    ToteId = session.ToteId,
                    PartId = sl.PartId,
                    QtyOnHand = sl.QtyFilled,
                    UpdatedAt = DateTime.UtcNow
                };
                db.ToteInventory.Add(inv);
            }
            else
            {
                inv.QtyOnHand += sl.QtyFilled;
                inv.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            updatedInventory.Add(new ToteInventoryItemDto(sl.PartId, sl.Part!.ItemDesc, inv.QtyOnHand));
        }

        // ปิด Session
        session.Status = "COMPLETED";
        session.CompletedAt = DateTime.UtcNow;

        // คืน Tote → STORAGE
        var tote = await db.Totes.FindAsync(session.ToteId);
        if (tote is not null)
        {
            tote.Status = "AVAILABLE";
            tote.Location = "STORAGE";
            tote.UpdatedAt = DateTime.UtcNow;
        }

        // ตรวจว่า Order เสร็จทั้งหมดไหม
        var order = session.ReplenishOrder!;
        var allDone = order.Lines.All(l => l.Status == "COMPLETED");
        if (allDone)
        {
            order.Status = "COMPLETED";
            order.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return Ok(new CompleteReplenishSessionResponse(
            Success: true,
            OrderStatus: order.Status,
            TotalLinesCompleted: session.Lines.Count(l => l.Status == "CONFIRMED"),
            UpdatedInventory: updatedInventory,
            Message: allDone
                ? $"✅ Order #{order.OrderId} เสร็จสิ้นแล้ว — Tote '{session.ToteId}' ส่งกลับ Storage"
                : $"✅ Session เสร็จสิ้น — Order #{order.OrderId} ยังมีรายการค้างอยู่"
        ));
    }
}
