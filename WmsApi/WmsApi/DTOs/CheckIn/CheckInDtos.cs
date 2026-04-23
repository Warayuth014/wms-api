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
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int CartonsInSlot,
    int ExpectedCartons,
    bool IsReadyToComplete,
    List<CheckInCartonItem> Cartons,
    string? CustomerOrderId = null,
    int PickTotal = 0,
    int PickCurrent = 0,
    int PackTotal = 0,
    int PackCurrent = 0,
    int CheckInTotal = 0,
    int CheckInCurrent = 0
);

public record CheckInCartonItem(
    string PackingId,
    string? TrackingId,
    string Status,
    DateTime ScannedAt,
    int ItemCount,
    int OrderCount
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
    List<PackTrackingItem> Trackings,
    DateTime CompletedAt,
    int CartonsCount,
    string Message
);

public record PackTrackingItem(
    string PackingId,
    string? TrackingId
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
