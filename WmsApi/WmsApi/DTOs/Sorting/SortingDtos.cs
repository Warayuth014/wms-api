namespace WmsApi.DTOs;

// ── Station list (10 cards) ──────────────────────────
public record SortingStationView(
    int StationId,
    bool Enabled,
    string Status,            // AVAILABLE | BUSY | DISABLED
    string? PalletId,
    int? CartonsCount,
    int? MaxCapacity,
    bool? IsFull,
    DateTime? StartedAt,
    string? DisabledBy,
    DateTime? DisabledAt,
    string? DisableReason
);

// ── Station detail (sheet) ───────────────────────────
public record SortingStationDetail(
    int StationId,
    bool Enabled,
    string Status,
    string? PalletId,
    int CartonsCount,
    int MaxCapacity,
    bool IsFull,
    DateTime? StartedAt,
    DateTime? FullAt,
    List<SortingStationCarton> Cartons,
    int PendingCount             // queue items ที่ยัง PENDING
);

public record SortingStationCarton(
    string PackingId,
    string Owner,
    int WeightGram,
    int ItemCount,
    DateTime SortedAt,
    int SequenceNo
);

// ── Toggle station enabled ───────────────────────────
public record ToggleStationRequest(
    int StationId,
    bool Enable,
    string OperatorId,
    string? Reason
);

// ── Clear station ────────────────────────────────────
public record ClearStationRequest(
    int StationId,
    string OperatorId
);

// ── Test: available packs (Status = DONE) ────────────
public record AvailablePackForSortingItem(
    string PackingId,
    string Owner,
    string? CustomerOrderId,
    int ItemCount,
    int OrderCount,
    DateTime CompletedAt
);

// ── Test: create batch ───────────────────────────────
public record CreateSortingBatchRequest(
    string OperatorId,
    List<string> PackingIds
);

public record CreateSortingBatchResponse(
    string Outcome,                // ASSIGNED | QUEUED
    int? StationId,                // ถ้า ASSIGNED — station ที่ได้
    string? PalletId,              // ถ้า ASSIGNED — pallet id (SP-XX)
    int BatchSize,                 // = packingIds.Count
    int? QueueId,                  // ถ้า QUEUED — id ใน queue
    string Message
);
