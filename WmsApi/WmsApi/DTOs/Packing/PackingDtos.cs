namespace WmsApi.DTOs;

public record PackingItem(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    string? LotNumber,
    int Qty,
    string Condition
);

public record PackingScanResponse(
    string PalletId,
    string Status,
    string? Location,
    string? PickOrderId,
    List<PackingItem> Items,
    string Message
);

public record ConfirmPackRequest(
    string PalletId,
    string OperatorId
);

public record ConfirmPackResponse(
    string PalletId,
    string TrackingId,
    string Status,
    DateTime PackedAt,
    string Message
);
