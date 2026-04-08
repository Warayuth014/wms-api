namespace WmsApi.DTOs;

// ── Pallet level ──────────────────────────────────
public record PackingPalletResponse(
    string PalletId,
    string Status,
    string? Location,
    List<PackingSummary> Packs,
    string Message
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
    List<PackingPartItem> Parts
);

public record PackingPartItem(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    int RequiredQty,
    int ScannedQty
);

// ── Scan / Confirm ──────────────────────────────────
public record ScanPackPartRequest(
    string PackingId,
    string PickOrderId,
    string PartId,
    int Qty,
    string OperatorId
);

public record ConfirmPackRequest(
    string PackingId,
    string OperatorId
);

public record ConfirmPackResponse(
    string PackingId,
    string Status,
    string? TrackingId,
    bool PalletShipped,
    DateTime CompletedAt,
    string Message
);
