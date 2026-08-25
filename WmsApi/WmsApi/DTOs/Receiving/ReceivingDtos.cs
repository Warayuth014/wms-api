namespace WmsApi.DTOs;

public record POResponse(
    string POId,
    string SupplierId,
    string SupplierName,
    string Status,
    DateTime CreatedAt,
    List<POItemResponse> Items,
    List<ScanReceiptPartResponse> PendingLines
);

// 1 lot = 1 line — ถ้า Part มีของมาจากหลาย lot จะมีหลาย entry ใน items[] (Id คือ POItemLot.Id)
public record POItemResponse(
    int Id,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    bool SerialRequire,
    int QtyOrdered,
    int QtyReceived,
    int QtyRemaining,
    string Status,
    string Condition,
    string LotNumber,
    string? ExpiredDate
);

public record POItemLotResponse(
    int Id,
    string LotNumber,
    int QtyOrdered,
    int QtyReceived,
    List<string> SerialNumbers
);

public record ScanReceiptPartRequest(
    string POId,
    string PartId,
    int LineId,
    int QtyReceived,
    string OperatorId,
    List<string>? SerialNumbers = null,
    string? PalletId = null
);

public record ValidateReceivingSerialResponse(
    string PartId,
    string SerialNo,
    string Status,
    int? LineId,
    string? POId,
    string? Condition,
    string? LotNumber
);

// เรียกด้วย partId อย่างเดียว (ไม่มี serialNo) — ตอนสแกน Part ID ครั้งแรก
// ยังไม่รู้ condition/lot ที่จะรับ ให้ frontend เอาไป popup เลือก
public record PartLinesResponse(
    string PartId,
    bool SerialRequire,
    List<PartLineResponse> Lines
);

public record PartLineResponse(
    int LineId,
    string POId,
    string Condition,
    List<POItemLotResponse> Lots
);

public record ScanReceiptPartResponse(
    int LineId,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int QtyOrdered,
    int QtyReceived,
    int QtyRemaining,
    string Condition,
    string? LotNumber,
    string POItemStatus,
    string Message,
    // ── ผลของการผูก Pallet (ถ้าส่ง PalletId มาพร้อม scan-part) ──
    string? PalletId = null,
    bool AutoClosed = false,
    string? POStatus = null,
    string? CloseMessage = null,
    string? PalletError = null
);

public record AssignPalletRequest(
    string PalletId,
    string PalletType,
    string OperatorId,
    List<int> LineIds
);

public record AssignPalletResponse(
    bool Success,
    string PalletId,
    string PalletType,
    int LinesAssigned,
    List<string> PartsAssigned,
    string Message,
    bool AutoClosed = false,
    string? POStatus = null,
    string? CloseMessage = null
);

public record PendingPalletLineResponse(
    int LineId,
    string POId,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int QtyReceived,
    string Condition,
    string? LotNumber,
    DateTime ReceivedAt
);

public record PendingPalletLinesResponse(
    int Count,
    List<PendingPalletLineResponse> Lines
);
