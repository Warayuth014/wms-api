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

// ── Preview ก่อน Check-IN (ไม่เขียน DB) ──────────────────────
public record PreviewCheckInRequest(
    string PackingId
);

public record PreviewCheckInResponse(
    string PackingId,
    string Owner,
    string? CustomerOrderId,
    string PackStatus,
    int ItemCount,
    int OrderCount,
    string SlotId,        // slot ที่จะถูก assign (ของเดิมหรือชื่อใหม่)
    bool IsNewSlot,       // true = จะสร้าง slot ใหม่
    bool IsAlreadyCheckedIn,
    string? DispatchDestination,
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
    int PipelineTotal = 0,
    int PickDone = 0,
    int PackDone = 0,
    int CheckInDone = 0
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
