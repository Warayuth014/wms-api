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

public record POItemResponse(
    int Id,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int QtyOrdered,
    int QtyReceived,
    int QtyRemaining,
    string Status,
    string Condition,
    string? LotNumber,
    string? ExpiredDate
);

public record ScanReceiptPartRequest(
    string POId,
    string PartId,
    int QtyReceived,
    string OperatorId,
    List<string>? SerialNumbers = null
);

public record ValidateReceivingSerialResponse(
    string PartId,
    string SerialNo,
    string Status
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
    string Message
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
