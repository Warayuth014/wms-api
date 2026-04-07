namespace WmsApi.DTOs;

public record SortStationResponse(
    string StationId,
    string Name,
    string Status
);

public record OpenSortSessionRequest(
    string StationId,
    string SortPalletId,
    string OperatorId
);

public record SortSessionItemResponse(
    int Id,
    string SourcePalletId,
    string? TrackingId,
    DateTime ScannedAt
);

public record SortSessionResponse(
    int SessionId,
    string StationId,
    string SortPalletId,
    string Status,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    List<SortSessionItemResponse> Items
);

public record ScanSortCartonRequest(
    int SessionId,
    string CartonId,        // จะเป็น TrackingId หรือ PalletId ก็ได้
    string OperatorId
);

public record CloseSortSessionRequest(
    int SessionId,
    string OperatorId
);
