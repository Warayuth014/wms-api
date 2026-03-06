namespace WmsApi.DTOs;

// =============================================
// 4.1 Common
// =============================================

public record ApiError(string Error, string? Detail = null);

public record ApiSuccess(bool Success, string Message);

// =============================================
// 4.2 Receiving — Flow 1
// =============================================

// ── GET PO ───────────────────────────────────
public record POResponse(
    string POId,
    string SupplierId,
    string SupplierName,
    string Status,
    DateTime CreatedAt,
    List<POItemResponse> Items
);

public record POItemResponse(
    int Id,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    int QtyOrdered,
    int QtyReceived,
    string Status
);

// ── Open Session ─────────────────────────────
public record OpenReceivingRequest(
    string POId,
    string OperatorId
);

public record OpenReceivingResponse(
    int SessionId,
    string POId,
    string SupplierName,
    string Status,
    List<POItemResponse> PendingItems
);

// ── Scan Part ─────────────────────────────────
public record ScanReceiptPartRequest(
    int SessionId,
    string POId,
    string PartId,
    int QtyReceived,
    string? LotNumber,
    string? ExpiredDate,   // "2026-12-31"
    string Condition,      // NORMAL | DAMAGED
    string OperatorId
);

public record ScanReceiptPartResponse(
    int LineId,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    int QtyOrdered,
    int QtyReceived,
    string Condition,
    string POItemStatus,   // PENDING | PARTIAL | RECEIVED | OVER
    string Message
);

// ── Assign Pallet ─────────────────────────────
public record AssignPalletRequest(
    int SessionId,
    string PalletId,
    string PalletType,     // FG | PW
    string OperatorId,
    List<int> LineIds      // LineId ที่จะผูกกับ Pallet นี้
);

public record AssignPalletResponse(
    bool Success,
    string PalletId,
    string PalletType,
    int LinesAssigned,
    List<string> PartsAssigned,
    string Message
);

// ── Close Session ─────────────────────────────
public record CloseReceivingResponse(
    bool Success,
    string POStatus,       // RECEIVED | PARTIAL
    string Message,
    int TotalParts,
    int ReceivedParts
);

// =============================================
// 4.3 Unload — Flow 2
// =============================================

// ── Scan Pallet ───────────────────────────────
public record ScanPalletForUnloadResponse(
    string PalletId,
    string Type,
    string Status,
    bool NeedsLabeling,  // true ถ้า PW
    List<UnloadItemResponse> Items,
    string Message
);

public record UnloadItemResponse(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? LotNumber,
    string? ExpiredDate,
    int Qty,
    string Condition
);

// ── Confirm Labeling ──────────────────────────
public record ConfirmLabelingRequest(
    string PalletId,
    string OperatorId
);

// ── Open Unload Session ───────────────────────
public record OpenUnloadRequest(
    string PalletId,
    string OperatorId
);

public record OpenUnloadResponse(
    int SessionId,
    string PalletId,
    string Status,
    List<UnloadItemResponse> Items
);

// ── Confirm Unload (Step 1) ───────────────────
public record ConfirmUnloadRequest(
    int SessionId,
    string PalletId,
    string PartId,
    string OperatorId
);

public record ConfirmUnloadResponse(
    bool Success,
    string Message,
    int ConfirmedCount,
    int TotalCount,
    bool AllConfirmed    // true → ไป Step 2 ได้เลย
);

// ── Scan Basket ───────────────────────────────
public record ScanBasketResponse(
    string BasketId,
    string Label,
    string? Zone,
    string? Destination,
    string Status,
    string Message
);

// ── Load to Basket (Step 2) ───────────────────
public record LoadToBasketRequest(
    int SessionId,
    string BasketId,
    string PartId,
    string PalletId,
    string OperatorId
);

public record LoadToBasketResponse(
    bool Success,
    string Message,
    int LoadedCount,
    int TotalCount,
    bool AllLoaded       // true → Session COMPLETED
);

// =============================================
// 4.4 Cancel
// =============================================

public record CancelRequest(
    string RefType,        // ReceiptLine | UnloadLine | BasketLine
    int RefId,
    string Reason,
    string RequestBy
);

public record ApproveCancelRequest(
    int CancelId,
    string ApprovedBy
);

public record CancelLogResponse(
    int CancelId,
    string RefType,
    int RefId,
    string Reason,
    string RequestBy,
    string? ApprovedBy,
    string Status,
    DateTime? CancelledAt
);