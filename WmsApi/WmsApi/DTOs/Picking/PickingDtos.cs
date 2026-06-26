namespace WmsApi.DTOs;

// ── Order list page (WAITING + PICKING) ───────────────
public record PickOrderListItem(
    string PickOrderId,
    string Status,             // WAITING | PICKING
    string Owner,
    string? CustomerOrderId,
    int PartCount,             // distinct parts
    int TotalRequiredQty,      // sum of all PickOrderDetail.RequiredQty
    int PalletCount,           // distinct source pallets
    DateTime CreatedAt
);

// ── Order detail page (pallets + parts) ───────────────
public record PickOrderDetailFull(
    string PickOrderId,
    string Status,
    string Owner,
    string? CustomerOrderId,
    DateTime CreatedAt,
    List<PickOrderPalletInfo> Pallets,
    List<PickOrderPartInfo> Parts
);

public record PickOrderPalletInfo(
    string PalletId,
    string PalletStatus,        // STORED | PICKING
    string? StationId,
    string? StationName,
    int PartCount,
    int TotalQty,
    List<PickOrderPalletPartInfo> Parts   // parts ที่อยู่บน pallet นี้
);

public record PickOrderPalletPartInfo(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int AllocatedQty,   // จำนวนที่จะ pick จาก pallet นี้
    int PickedQty,
    string Status
);

public record PickOrderPartInfo(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int RequiredQty,
    int ReservedQty,
    int RemainingQty,
    string Status
);

// ── Notify Arrival (Robot simulator) ──────────────────
public record NotifyArrivalResponse(
    string PickOrderId,
    string Status,              // PICKING after success
    List<NotifyArrivalAssignment> Assignments,
    string Message
);

public record NotifyArrivalAssignment(
    string PalletId,
    string? StationId,
    string Outcome              // ASSIGNED | ALREADY_AT_STATION | NO_FREE_STATION
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
    string Status
);

public record AssignPickStationRequest(
    string PalletId,
    string? PickOrderId = null
);

public record PickItemOnPallet(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int QtyOnPallet,
    int QtyToPickSuggested,
    string Condition,
    List<string> AvailableSerials
);

public record AssignPickStationResponse(
    string StationId,
    string StationName,
    string PalletId,
    string PickOrderId,
    List<PickItemOnPallet> PalletItems,
    List<PickOrderDetailResponse> PickOrderItems,
    string Message
);

public record PickConfirmItem(
    string PartId,
    int Qty,
    List<string>? SerialNumbers = null
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
    bool SourcePalletEmpty,
    bool SourcePickDone,
    string PickOrderStatus,
    List<PickRemainingItem> RemainingItems,
    string Message
);

public record ReturnPalletRequest(
    string PalletId,
    string? Destination = null   // null → backend auto-decide จาก pallet state
);

public record CreateTestOrderRequest(
    string OperatorId,
    List<TestOrderItem> Items
);

public record TestOrderItem(
    int LineId,
    string PartId,
    int Qty
);

