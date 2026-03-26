using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/return")]
public class ReturnController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // GET /api/return/order/{orderId}
    // สแกนเลข Order เช็คว่ามีในระบบไหม
    // =============================================
    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetOrder(string orderId)
    {
        var order = await db.SalesOrders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order is null)
            return NotFound(new ApiError(
                $"Order '{orderId}' not found."));

        if (order.Status == "RETURNED")
            return BadRequest(new ApiError(
                $"Order '{orderId}' already fully returned."));

        return Ok(new OrderResponse(
            OrderId: order.OrderId,
            CustomerId: order.CustomerId,
            CustomerName: order.Customer!.FullName,
            Status: order.Status,
            CreatedAt: order.CreatedAt,
            Items: order.Items.Select(i => new OrderItemResponse(
                Id: i.Id,
                PartId: i.PartId,
                Owner: i.Part!.Owner,
                Brand: i.Part!.Brand,
                ItemDesc: i.Part!.ItemDesc,
                ImageUrl: i.Part!.ImageUrl,
                QtySold: i.QtySold,
                Status: i.Status
            )).ToList()
        ));
    }

    // =============================================
    // POST /api/return/open-session
    // เปิด session รับคืน
    // =============================================
    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession(
        [FromBody] OpenReturnRequest req)
    {
        var order = await db.SalesOrders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(o => o.OrderId == req.OrderId);

        if (order is null)
            return NotFound(new ApiError(
                $"Order '{req.OrderId}' not found."));

        if (order.Status == "RETURNED")
            return BadRequest(new ApiError(
                $"Order '{req.OrderId}' already fully returned."));

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError(
                $"User '{req.OperatorId}' not found."));

        // สร้าง ReturnOrder
        var returnOrder = new ReturnOrder
        {
            OrderId = req.OrderId,
            OperatorId = req.OperatorId,
            Status = "OPEN",
            CreatedAt = DateTime.UtcNow
        };

        db.ReturnOrders.Add(returnOrder);

        order.Status = "RETURNING";
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ส่งกลับเฉพาะ Part ที่ยังไม่ได้คืน
        var activeItems = order.Items
            .Where(i => i.Status == "ACTIVE")
            .Select(i => new OrderItemResponse(
                Id: i.Id,
                PartId: i.PartId,
                Owner: i.Part!.Owner,
                Brand: i.Part!.Brand,
                ItemDesc: i.Part!.ItemDesc,
                ImageUrl: i.Part!.ImageUrl,
                QtySold: i.QtySold,
                Status: i.Status
            )).ToList();

        return Ok(new OpenReturnResponse(
            ReturnId: returnOrder.ReturnId,
            OrderId: order.OrderId,
            CustomerName: order.Customer!.FullName,
            Status: returnOrder.Status,
            Items: activeItems
        ));
    }

    // =============================================
    // POST /api/return/receive-item
    // บันทึกรายการที่รับคืน
    // =============================================
    [HttpPost("receive-item")]
    public async Task<IActionResult> ReceiveItem(
        [FromBody] ReceiveReturnItemRequest req)
    {
        // ตรวจ session
        var returnOrder = await db.ReturnOrders
            .FindAsync(req.ReturnId);

        if (returnOrder is null || returnOrder.Status != "OPEN")
            return BadRequest(new ApiError(
                "Invalid or closed return session."));

        // ตรวจว่า Part อยู่ใน Order ไหม
        var orderItem = await db.SalesOrderItems
            .Include(i => i.Part)
            .FirstOrDefaultAsync(i => i.OrderId == req.OrderId
                                   && i.PartId == req.PartId);

        if (orderItem is null)
            return BadRequest(new ApiError(
                $"Part '{req.PartId}' is not in Order '{req.OrderId}'.",
                "Wrong item returned."));

        // ตรวจว่ารับคืนไปแล้วหรือยัง
        var existing = await db.ReturnLines
            .FirstOrDefaultAsync(l => l.ReturnId == req.ReturnId
                                   && l.PartId == req.PartId);

        if (existing is not null)
            return BadRequest(new ApiError(
                $"Part '{req.PartId}' already returned in this session."));

        // ตรวจจำนวนที่คืน
        var isOver = req.QtyReturned > orderItem.QtySold;
        var message = isOver
                       ? $"⚠️ Over return: sold {orderItem.QtySold}, returning {req.QtyReturned}."
                       : $"✅ รับคืน {req.QtyReturned}/{orderItem.QtySold} ชิ้น";

        // สร้าง ReturnLine
        var line = new ReturnLine
        {
            ReturnId = req.ReturnId,
            OrderId = req.OrderId,
            PartId = req.PartId,
            QtyReturned = req.QtyReturned,
            Note = req.Note,
            Status = "RETURNED",
            OperatorId = req.OperatorId,
            ReturnedAt = DateTime.UtcNow
        };

        db.ReturnLines.Add(line);

        // Update SalesOrderItem
        orderItem.Status = "RETURNED";

        await db.SaveChangesAsync();

        return Ok(new ReceiveReturnItemResponse(
            LineId: line.LineId,
            PartId: orderItem.PartId,
            Owner: orderItem.Part!.Owner,
            Brand: orderItem.Part!.Brand,
            ItemDesc: orderItem.Part!.ItemDesc,
            ImageUrl: orderItem.Part!.ImageUrl,
            QtySold: orderItem.QtySold,
            QtyReturned: req.QtyReturned,
            Status: line.Status,
            Message: message
        ));
    }

    // =============================================
    // POST /api/return/close-session/{returnId}
    // ปิด session
    // =============================================
    [HttpPost("close-session/{returnId}")]
    public async Task<IActionResult> CloseSession(int returnId)
    {
        var returnOrder = await db.ReturnOrders
            .Include(r => r.SalesOrder)
                .ThenInclude(o => o!.Items)
            .FirstOrDefaultAsync(r => r.ReturnId == returnId);

        if (returnOrder is null)
            return NotFound(new ApiError(
                $"Return session '{returnId}' not found."));

        if (returnOrder.Status == "CLOSED")
            return BadRequest(new ApiError(
                "Return session already closed."));

        var order = returnOrder.SalesOrder!;
        var items = order.Items.ToList();
        var total = items.Count;
        var returned = items.Count(i => i.Status == "RETURNED");
        var hasActive = items.Any(i => i.Status == "ACTIVE");

        // Update SalesOrder status
        order.Status = hasActive ? "PARTIAL_RETURN" : "RETURNED";
        order.UpdatedAt = DateTime.UtcNow;

        // ปิด session
        returnOrder.Status = "CLOSED";
        returnOrder.ClosedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new CloseReturnResponse(
            Success: true,
            OrderStatus: order.Status,
            Message: order.Status == "RETURNED"
                           ? "รับคืนครบทุกรายการ ✅"
                           : $"รับคืนบางส่วน ⚠️ ({returned}/{total} รายการ)",
            TotalParts: total,
            ReturnedParts: returned
        ));
    }
}