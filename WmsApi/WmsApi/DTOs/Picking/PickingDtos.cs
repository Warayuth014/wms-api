namespace WmsApi.DTOs;

public record RequestFromAsrsRequest(
    string PalletId,
    string OperatorId
);

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
    string Status,
    List<PickOrderSubResponse> Subs
);

public record PickOrderSubResponse(
    int Id,
    int PickOrderDetailId,
    int ReceiptLineId,
    string? PalletId,
    int AllocatedQty,
    int PickedQty,
    string Status
);

public record AssignPickStationRequest(
    string PalletId,
    string OperatorId,
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
    string Destination
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

public record CreatePickOrderRequest(
    string OperatorId,
    List<CreatePickOrderItem> Items
);

public record CreatePickOrderItem(
    string PartId,
    int Qty
);

public record CreatePickOrderResponse(
    string PickOrderId,
    int TotalRequired,
    int TotalAllocated,
    List<PickOrderDetailAllocation> Details,
    string Message
);

public record PickOrderDetailAllocation(
    string PartId,
    int RequiredQty,
    int AllocatedQty,
    int ShortageQty
);
