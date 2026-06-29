namespace WmsApi.DTOs;

// ── Scan Carton (PackingId) ──────────────────────────────────
public record ScanCheckInRequest(
    string PackingId,
    string OperatorId
);

public record ScanCheckInResponse(
    string SlotId,
    bool IsReadyToComplete
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
    List<string> PickOrderIds,
    string SlotId,        // slot ที่จะถูก assign (ของเดิมหรือชื่อใหม่)
    bool IsNewSlot,       // true = จะสร้าง slot ใหม่
    bool IsAlreadyCheckedIn,
    string? DispatchDestination,
    List<PreviewCheckInItem> Items,
    int PipelineTotal = 0,
    int PickDone = 0,
    int PackDone = 0,
    int SortingDone = 0,
    int CheckInDone = 0
);

public record PreviewCheckInItem(
    string PartId,
    string ItemDesc,
    string Brand,
    string? ImageUrl,
    int Qty
);

// ── Slot detail ──────────────────────────────────
public record CheckInSlotDetail(
    string Status,
    List<CheckInCartonItem> Cartons,
    string? CustomerOrderId = null,
    int PipelineTotal = 0,
    int PickDone = 0,
    int PackDone = 0,
    int SortingDone = 0,
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

// ── Complete (all cartons gathered → generate tracking) ──────
public record CompleteCheckInRequest(
    string SlotId,
    string OperatorId
);

// ── Dispatch (move to shipping) ──────────────────────────────
public record DispatchCheckInRequest(
    string SlotId,
    string OperatorId
);
