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
    string? ImageUrl,
    int QtyOrdered,
    int QtyReceived,
    int QtyRemaining,
    string Status,
    string Condition,          // FG | PW (จาก Parts)
    string? LotNumber,         // จาก Parts
    string? ExpiredDate        // จาก Parts (yyyy-MM-dd)
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
    string OperatorId
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
    string Condition,          // FG | PW (จาก Parts)
    string? LotNumber,         // จาก Parts
    string POItemStatus,       // PENDING | PARTIAL | RECEIVED | OVER
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
    string Message,
    bool AutoClosed = false,
    string? POStatus = null,
    string? CloseMessage = null
);

// ── Active Session ────────────────────────────
public record ActiveReceivingSessionResponse(
    int SessionId,
    string POId,
    string SupplierName,
    string Status,
    List<POItemResponse> PendingItems,
    List<ScanReceiptPartResponse> PendingLines
);

// ── Close Session ─────────────────────────────
public record PartialItemSummary(
    string PartId,
    string ItemDesc,
    int QtyOrdered,
    int QtyReceived,
    int QtyRemaining
);

public record CloseReceivingResponse(
    bool Success,
    string POStatus,       // RECEIVED | PARTIAL
    string Message,
    int TotalParts,
    int ReceivedParts,
    List<PartialItemSummary> PartialItems   // รายการที่รับไม่ครบ
);

// ── Pending Pallet Lines ───────────────────────
public record PendingPalletLineResponse(
    int LineId,
    int SessionId,
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
    string? ImageUrl,
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
    List<UnloadItemResponse> Items,
    List<string> ConfirmedPartIds  // สำหรับ resume — parts ที่ confirm แล้ว
);

// ── Confirm Unload (Step 1) ───────────────────
public record ConfirmUnloadRequest(
    int SessionId,
    string PalletId,
    string PartId,
    string OperatorId,
    int? QtyUnloaded = null   // ถ้าไม่ส่ง → ใช้ค่าเดิม (QtyReceived ทั้งหมด)
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
// 4.X Putaway
// =============================================

// ── Scan Pallet for Putaway ───────────────────
public record ScanPalletForPutawayResponse(
    string PalletId,
    string Type,              // FG | PW
    string Status,
    string SuggestedDestination, // ASRS | PREWORK
    List<UnloadItemResponse> Items,
    string Message
);

// ── Confirm Putaway ───────────────────────────
public record ConfirmPutawayRequest(
    string StationId,
    string PalletId,
    string Destination,       // ASRS | PREWORK
    string OperatorId,
    bool ConvertToFG = true   // PW-STN: true = convert PW→FG ก่อนส่ง, false = ส่ง ASRS แบบยังเป็น PW
);

public record ConfirmPutawayResponse(
    bool Success,
    string PalletId,
    string StationId,
    string Destination,
    string Message
);

// ── Recall PW from ASRS ─────────────────────
public record RecallToPreworkRequest(
    string PalletId,
    string StationId,       // PW-STN ที่จะรับ
    string OperatorId
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

// =============================================
// 4.5 Return — Flow 1
// =============================================

// ── GET Order ────────────────────────────────
public record OrderResponse(
    string OrderId,
    string CustomerId,
    string CustomerName,
    string Status,
    DateTime CreatedAt,
    List<OrderItemResponse> Items
);

public record OrderItemResponse(
    int Id,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int QtySold,
    string Status
);

// ── Open Return Session ───────────────────────
public record OpenReturnRequest(
    string OrderId,
    string OperatorId
);

public record OpenReturnResponse(
    int ReturnId,
    string OrderId,
    string CustomerName,
    string Status,
    List<OrderItemResponse> Items
);

// ── Receive Item ──────────────────────────────
public record ReceiveReturnItemRequest(
    int ReturnId,
    string OrderId,
    string PartId,
    int QtyReturned,
    string? Note,
    string OperatorId
);

public record ReceiveReturnItemResponse(
    int LineId,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int QtySold,
    int QtyReturned,
    string Status,
    string Message
);

// ── Close Return Session ──────────────────────
public record CloseReturnResponse(
    bool Success,
    string OrderStatus,
    string Message,
    int TotalParts,
    int ReturnedParts
);

// ── Confirmed Unload Items (grouped by PartId) ─
public record GroupedConfirmedItemResponse(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    string? LotNumber,
    int TotalQty
);

// ── Loaded Basket Items ────────────────────
public record LoadedBasketItemResponse(
    int LineId,
    string PartId,
    string PalletId,
    string Owner,
    string ItemDesc,
    string? ImageUrl,
    int QtyLoaded,
    string? LotNumber,
    string BasketId,
    string BasketLabel,
    string? BasketDestination
);

// ── Return Basket ──────────────────────────
public record ReturnBasketRequest(
    string BasketId,
    string OperatorId
);

// ── Return Pallet to ASRS ──────────────────
public record ReturnPalletToAsisRequest(
    string PalletId,
    int? SessionId,
    string OperatorId
);

// ── ASRS Dispatch (RETURNING → IN_TRANSIT) ─
public record AsisDispatchRequest(
    string PalletId
);

// ── Load Basket (by PartId + Qty) ──────────
public record LoadBasketRequest(
    string PartId,
    string BasketId,
    int Qty,
    string OperatorId
);

public record LoadBasketResponse2(
    bool Success,
    string BasketId,
    string PartId,
    int QtyLoaded,
    int QtyRemaining,
    string Message
);

// =============================================
// 4.6 Picking
// =============================================

// ── Open Picking Session ─────────────────────
public record OpenPickingRequest(
    string PackPalletId,
    string OperatorId
);

public record OpenPickingResponse(
    int SessionId,
    string PackPalletId,
    string Status,
    List<PickingLineResponse> PickedLines
);

// ── Scan Source (Pallet หรือ Basket) ─────────
public record ScanSourceResponse(
    string SourceId,
    string SourceType,     // PALLET | BASKET
    string Type,           // FG | PW | - (pallet type) หรือ basket label
    string Status,
    List<SourceItemResponse> Items,
    string Message
);

public record SourceItemResponse(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    string? LotNumber,
    string? ExpiredDate,
    int Qty,
    string Condition
);

// ── Pick Item ────────────────────────────────
public record PickItemRequest(
    int SessionId,
    string SourceId,       // Pallet ID or Basket ID
    string SourceType,     // PALLET | BASKET
    string PartId,
    int QtyPicked,
    string OperatorId
);

public record PickItemResponse(
    bool Success,
    int LineId,
    string PartId,
    int QtyPicked,
    int RemainingOnSource,
    string Message
);

// ── Return Source (Pallet หรือ Basket) ───────
public record ReturnSourceRequest(
    string SourceId,
    string SourceType,     // PALLET | BASKET
    string OperatorId,
    string Destination = "ASRS"  // ASRS | ZONE_PICK
);

// ── Request Pallet from ASRS (simulation) ───
public record RequestFromAsrsRequest(
    string PalletId,
    string OperatorId
);

// ── Complete Picking Session ─────────────────
public record CompletePickingResponse(
    bool Success,
    int TotalItemsPicked,
    string PackPalletId,
    string Message
);

// ── Picking Line Response ────────────────────
public record PickingLineResponse(
    int LineId,
    string SourceId,       // Pallet ID or Basket ID
    string SourceType,     // PALLET | BASKET
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    string? LotNumber,
    string? ExpiredDate,
    int QtyPicked,
    string Status
);

// =============================================
// 4.7 Picking v2 — Pick Order flow
// =============================================

// ── Pick Order ───────────────────────────────
public record PickOrderResponse(
    string PickOrderId,
    string Status,
    DateTime CreatedAt,
    List<PickOrderDetailResponse> Details
);

public record PickOrderDetailResponse(
    int Id,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int RequiredQty,
    int ReservedQty,
    int RemainingQty,
    string Status,  // PENDING | PARTIAL | COMPLETED
    List<PickOrderSubResponse> Subs
);

public record PickOrderSubResponse(
    int Id,
    int PickOrderDetailId,
    int ReceiptLineId,
    string? PalletId,
    int AllocatedQty,
    int PickedQty,
    string Status  // PENDING | PICKED
);

// ── Assign Pick Station ───────────────────────
public record AssignPickStationRequest(
    string PalletId,
    string OperatorId,
    string? PickOrderId = null   // optional — ถ้าไม่ระบุจะ auto-detect จาก pallet
);

public record PickItemOnPallet(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int QtyOnPallet,
    int QtyToPickSuggested,  // min(QtyOnPallet, RemainingNeeded)
    string Condition
);

public record AssignPickStationResponse(
    string StationId,
    string StationName,
    string PalletId,
    string PickOrderId,          // pick order ที่ auto-detect หรือระบุมา
    List<PickItemOnPallet> PalletItems,
    List<PickOrderDetailResponse> PickOrderItems,  // remaining needs
    string Message
);

// ── Confirm Pick ──────────────────────────────
public record PickConfirmItem(
    string PartId,
    int Qty
);

public record ConfirmPickRequest(
    string PickOrderId,
    string SourcePalletId,
    string DestPalletId,
    List<PickConfirmItem> Items,
    string OperatorId
);

public record PickRemainingItem(
    string PartId,
    string ItemDesc,
    int RequiredQty,
    int PickedQty,
    int RemainingQty
);

public record ConfirmPickResponse(
    bool IsPickOrderComplete,
    bool SourcePalletEmpty,      // pallet ว่างเลย (ไม่มีของเลย)
    bool SourcePickDone,         // pick order นี้บน pallet นี้เสร็จแล้ว (แต่อาจยังมีของอื่นเหลือ)
    string PickOrderStatus,
    List<PickRemainingItem> RemainingItems,
    string Message
);

// ── Return Pallet ────────────────────────────
public record ReturnPalletRequest(
    string PalletId,
    string Destination   // ASRS | ZONE_PACK
);

// ── TEST: Create Test Pick Order ─────────────
public record CreateTestOrderRequest(
    string OperatorId,
    List<TestOrderItem> Items
);

public record TestOrderItem(
    int LineId,        // ReceiptLine.LineId
    string PartId,
    int Qty            // จำนวนที่ต้องการ pick
);