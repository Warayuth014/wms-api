using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/receiving")]
public class ReceivingController(WmsDbContext db) : ControllerBase
{
    // =============================================
    // GET /api/receiving/po/{poId}
    // สแกน PO ดูรายการสินค้า
    // =============================================
    [HttpGet("po/{poId}")]
    public async Task<IActionResult> GetPO(string poId)
    {
        var po = await db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(p => p.POId == poId);

        if (po is null)
            return NotFound(new ApiError($"PO '{poId}' not found."));

        return Ok(new POResponse(
            POId: po.POId,
            SupplierId: po.SupplierId,
            SupplierName: po.Supplier!.FullName,
            Status: po.Status,
            CreatedAt: po.CreatedAt,
            Items: po.Items.Select(i => new POItemResponse(
                Id: i.Id,
                PartId: i.PartId,
                Owner: i.Part!.Owner,
                Brand: i.Part!.Brand,
                ItemDesc: i.Part!.ItemDesc,
                QtyOrdered: i.QtyOrdered,
                QtyReceived: i.QtyReceived,
                Status: i.Status
            )).ToList()
        ));
    }

    // =============================================
    // POST /api/receiving/open-session
    // เปิด session รับของ
    // =============================================
    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenReceivingRequest req)
    {
        var po = await db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
                .ThenInclude(i => i.Part)
            .FirstOrDefaultAsync(p => p.POId == req.POId);

        if (po is null)
            return NotFound(new ApiError($"PO '{req.POId}' not found."));

        if (po.Status == "RECEIVED")
            return BadRequest(new ApiError($"PO '{req.POId}' already fully received."));

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        // สร้าง session
        var session = new ReceivingSession
        {
            POId = req.POId,
            OperatorId = req.OperatorId,
            Status = "OPEN",
            OpenedAt = DateTime.UtcNow
        };

        db.ReceivingSessions.Add(session);

        po.Status = "RECEIVING";
        po.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // ส่งกลับเฉพาะ Part ที่ยังไม่ได้รับ
        var pendingItems = po.Items
            .Where(i => i.Status != "RECEIVED")
            .Select(i => new POItemResponse(
                Id: i.Id,
                PartId: i.PartId,
                Owner: i.Part!.Owner,
                Brand: i.Part!.Brand,
                ItemDesc: i.Part!.ItemDesc,
                QtyOrdered: i.QtyOrdered,
                QtyReceived: i.QtyReceived,
                Status: i.Status
            )).ToList();

        return Ok(new OpenReceivingResponse(
            SessionId: session.SessionId,
            POId: po.POId,
            SupplierName: po.Supplier!.FullName,
            Status: session.Status,
            PendingItems: pendingItems
        ));
    }

    // =============================================
    // POST /api/receiving/scan-part
    // สแกน Part + บันทึก qty/lot/expired
    // =============================================
    [HttpPost("scan-part")]
    public async Task<IActionResult> ScanPart([FromBody] ScanReceiptPartRequest req)
    {
        // ตรวจ session
        var session = await db.ReceivingSessions.FindAsync(req.SessionId);
        if (session is null || session.Status != "OPEN")
            return BadRequest(new ApiError("Invalid or closed session."));

        // ตรวจ Part ใน PO
        var poItem = await db.POItems
            .Include(i => i.Part)
            .FirstOrDefaultAsync(i => i.POId == req.POId
                                   && i.PartId == req.PartId);

        if (poItem is null)
            return BadRequest(new ApiError(
                $"Part '{req.PartId}' is not in PO '{req.POId}'.",
                "Wrong item received."));

        // ตรวจสแกนซ้ำใน session นี้
        var existing = await db.ReceiptLines
            .FirstOrDefaultAsync(l => l.SessionId == req.SessionId
                                   && l.PartId == req.PartId
                                   && l.Status != "CANCELLED");

        if (existing is not null)
            return BadRequest(new ApiError(
                $"Part '{req.PartId}' already scanned in this session."));

        // คำนวณ qty
        var newTotal = poItem.QtyReceived + req.QtyReceived;
        var isOver = newTotal > poItem.QtyOrdered;
        var poStatus = newTotal >= poItem.QtyOrdered ? "RECEIVED"
                      : newTotal > 0 ? "PARTIAL"
                                                      : "PENDING";
        var message = isOver
                        ? $"⚠️ Over receiving: ordered {poItem.QtyOrdered}, total {newTotal}."
                        : poStatus == "RECEIVED"
                        ? $"✅ Received completely ({newTotal}/{poItem.QtyOrdered})."
                        : $"Partial ({newTotal}/{poItem.QtyOrdered}).";

        // Parse ExpiredDate
        DateOnly? expired = null;
        if (!string.IsNullOrEmpty(req.ExpiredDate) &&
            DateOnly.TryParse(req.ExpiredDate, out var d))
            expired = d;

        // สร้าง ReceiptLine
        var line = new ReceiptLine
        {
            SessionId = req.SessionId,
            POId = req.POId,
            PartId = req.PartId,
            QtyReceived = req.QtyReceived,
            LotNumber = req.LotNumber,
            ExpiredDate = expired,
            Condition = req.Condition,
            Status = "PENDING",
            OperatorId = req.OperatorId
        };

        db.ReceiptLines.Add(line);

        // Update POItem
        poItem.QtyReceived = newTotal;
        poItem.Status = isOver ? "OVER" : poStatus;

        await db.SaveChangesAsync();

        return Ok(new ScanReceiptPartResponse(
            LineId: line.LineId,
            PartId: poItem.PartId,
            Owner: poItem.Part!.Owner,
            Brand: poItem.Part!.Brand,
            ItemDesc: poItem.Part!.ItemDesc,
            QtyOrdered: poItem.QtyOrdered,
            QtyReceived: newTotal,
            Condition: req.Condition,
            POItemStatus: poItem.Status,
            Message: message
        ));
    }

    // =============================================
    // POST /api/receiving/assign-pallet
    // สแกน Pallet ผูกสินค้า
    // =============================================
    [HttpPost("assign-pallet")]
    public async Task<IActionResult> AssignPallet([FromBody] AssignPalletRequest req)
    {
        var session = await db.ReceivingSessions.FindAsync(req.SessionId);
        if (session is null || session.Status != "OPEN")
            return BadRequest(new ApiError("Invalid or closed session."));

        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status != "AVAILABLE")
            return BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' is not available (status: {pallet.Status})."));

        // ดึง lines ที่ระบุมา
        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => req.LineIds.Contains(l.LineId)
                     && l.SessionId == req.SessionId
                     && l.Status == "PENDING")
            .ToListAsync();

        if (lines.Count == 0)
            return BadRequest(new ApiError("No pending lines found."));

        // ผูก Pallet
        foreach (var line in lines)
        {
            line.PalletId = req.PalletId;
            line.Status = "PALLETIZED";
            line.UpdatedAt = DateTime.UtcNow;
        }

        // ตั้งค่า Pallet
        pallet.Type = req.PalletType;
        pallet.Status = req.PalletType;  // FG หรือ PW
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new AssignPalletResponse(
            Success: true,
            PalletId: pallet.PalletId,
            PalletType: pallet.Type!,
            LinesAssigned: lines.Count,
            PartsAssigned: lines.Select(l => l.PartId).ToList(),
            Message: $"Pallet '{req.PalletId}' ({req.PalletType}) assigned with {lines.Count} part(s)."
        ));
    }

    // =============================================
    // POST /api/receiving/close-session/{sessionId}
    // ปิด session
    // =============================================
    [HttpPost("close-session/{sessionId}")]
    public async Task<IActionResult> CloseSession(int sessionId)
    {
        var session = await db.ReceivingSessions
            .Include(s => s.PurchaseOrder)
                .ThenInclude(p => p!.Items)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session is null)
            return NotFound(new ApiError($"Session '{sessionId}' not found."));

        if (session.Status == "CLOSED")
            return BadRequest(new ApiError("Session already closed."));

        var po = session.PurchaseOrder!;
        var items = po.Items.ToList();
        var total = items.Count;
        var received = items.Count(i => i.Status == "RECEIVED" || i.Status == "OVER");
        var hasPartial = items.Any(i => i.Status == "PARTIAL" || i.Status == "PENDING");

        po.Status = hasPartial ? "PARTIAL" : "RECEIVED";
        po.UpdatedAt = DateTime.UtcNow;

        session.Status = "CLOSED";
        session.ClosedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new CloseReceivingResponse(
            Success: true,
            POStatus: po.Status,
            Message: po.Status == "RECEIVED"
                           ? "PO fully received ✅"
                           : $"PO partially received ⚠️ ({received}/{total} parts)",
            TotalParts: total,
            ReceivedParts: received
        ));
    }
}