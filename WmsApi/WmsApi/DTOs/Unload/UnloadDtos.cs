namespace WmsApi.DTOs;

public record ScanPalletForUnloadResponse(
    string PalletId,
    string Type,
    string Status,
    bool NeedsLabeling,
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

public record ConfirmLabelingRequest(
    string PalletId,
    string OperatorId
);

public record OpenUnloadRequest(
    string PalletId,
    string OperatorId
);

public record OpenUnloadResponse(
    int SessionId,
    string PalletId,
    string Status,
    List<UnloadItemResponse> Items,
    List<string> ConfirmedPartIds
);

public record ConfirmUnloadRequest(
    int SessionId,
    string PalletId,
    string PartId,
    string OperatorId,
    int? QtyUnloaded = null
);

public record ConfirmUnloadResponse(
    bool Success,
    string Message,
    int ConfirmedCount,
    int TotalCount,
    bool AllConfirmed
);
