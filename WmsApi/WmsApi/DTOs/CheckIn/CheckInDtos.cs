namespace WmsApi.DTOs;

// ── Scan Carton (PackingId) ──────────────────────────────────
public record ScanCheckInRequest(
    string PackingId,
    string OperatorId
);

public record ScanCheckInResponse(
    string SlotId,
    string Owner,
    string PackingId,
    int CartonsInSlot,
    int ExpectedCartons,
    bool IsReadyToComplete,
    string Message
);

// ── Slot detail ──────────────────────────────────
public record CheckInSlotDetail(
    string SlotId,
    string Owner,
    string Status,
    string? TrackingId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int CartonsInSlot,
    int ExpectedCartons,
    bool IsReadyToComplete,
    List<CheckInCartonItem> Cartons
);

public record CheckInCartonItem(
    string PackingId,
    string PalletId,
    string Status,
    DateTime ScannedAt
);

// ── List active slots ──────────────────────────────────
public record CheckInSlotSummary(
    string SlotId,
    string Owner,
    string Status,
    int CartonsInSlot,
    int ExpectedCartons,
    DateTime CreatedAt
);

// ── Complete (all cartons gathered → generate tracking) ──────
public record CompleteCheckInRequest(
    string SlotId,
    string OperatorId
);

public record CompleteCheckInResponse(
    string SlotId,
    string Owner,
    string TrackingId,
    DateTime CompletedAt,
    int CartonsCount,
    string Message
);

// ── Dispatch (move to shipping) ──────────────────────────────
public record DispatchCheckInRequest(
    string SlotId,
    string OperatorId
);

public record DispatchCheckInResponse(
    string SlotId,
    string Owner,
    DateTime ShippedAt,
    int CartonsCount,
    string Message
);
