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
    bool WrappingRequired = false
);

public record ConfirmPutawayResponse(
    bool Success,
    string PalletId,
    string StationId,
    string Destination,
    string Message
);

public record PreworkReturnPalletRequest(
    string PalletId,
    string StationId
);

public record ReturnPalletToAsisRequest(
    string PalletId,
    int? SessionId
);
