namespace WmsApi.DTOs;

public record ScanPalletForPutawayResponse(
    string PalletId,
    string Type,
    string Status,
    string SuggestedDestination,
    List<UnloadItemResponse> Items,
    string Message
);

public record ConfirmPutawayRequest(
    string StationId,
    string PalletId,
    string Destination,
    string OperatorId,
    bool WrappingRequired = false,
    bool ConvertToFG = true
);

public record ConfirmPutawayResponse(
    bool Success,
    string PalletId,
    string StationId,
    string Destination,
    string Message
);

public record RecallToPreworkRequest(
    string PalletId,
    string StationId,
    string OperatorId
);

public record PreworkReceiveRequest(
    string PalletId,
    string StationId,
    string OperatorId
);

public record PreworkReceiveItemResponse(
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

public record PreworkReceiveResponse(
    bool Success,
    string PalletId,
    string StationId,
    List<PreworkReceiveItemResponse> Items,
    string Message
);

public record PreworkReturnPalletRequest(
    string PalletId,
    string StationId,
    string OperatorId
);

public record ReturnPalletToAsisRequest(
    string PalletId,
    int? SessionId,
    string OperatorId
);

public record AsisDispatchRequest(
    string PalletId
);
