namespace WmsApi.DTOs;

// ── Pallet level ──────────────────────────────────
public record PackingPalletResponse(
    string PalletId,
    string Status,
    string? Location,
    List<PackingSummary> Packs
);

public record PackingSummary(
    string PackingId,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int OrderCount,
    int OrderDoneCount
);

// ── Pack level ──────────────────────────────────
public record PackingDetailResponse(
    string PackingId,
    string PalletId,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? TrackingId,
    List<PackingOrderSummary> Orders
);

public record PackingOrderSummary(
    string PickOrderId,
    string Status,
    int PartCount,
    int PartDoneCount
);

// ── Order level ──────────────────────────────────
public record PackingOrderResponse(
    string PackingId,
    string PickOrderId,
    string Status,
    List<PackingPartItem> Parts,
    bool PackFinalized = false,
    string? TrackingId = null,
    bool PalletReleased = false
);

public record PackingPartItem(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int RequiredQty,
    int ScannedQty,
    List<string> AvailableSerials
);

// ── Scan / Confirm ──────────────────────────────────
public record ScanPackPartRequest(
    string PackingId,
    string PickOrderId,
    string PartId,
    int Qty,
    string OperatorId,
    List<string>? SerialNumbers = null
);

