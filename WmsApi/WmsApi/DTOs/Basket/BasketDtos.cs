namespace WmsApi.DTOs;

// ── รายการสินค้าที่ Unload แล้ว (group by Part+Lot) ──────────
public record UnloadedItemResponse(
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    string? LotNumber,
    string? ExpiredDate,
    int QtyUnloaded,        // รวมทุก UnloadLine ของ Part+Lot นี้
    int QtyLoaded,          // จำนวนที่ load เข้า basket แล้ว
    int QtyRemaining,       // QtyUnloaded - QtyLoaded
    string? BasketId,       // basket ล่าสุดที่ load เข้าไป (ถ้ามี)
    List<int> UnloadLineIds // UnloadLine IDs ทั้งหมดใน group นี้
);

public record UnloadedItemsResponse(
    List<UnloadedItemResponse> Items,
    int TotalItems,
    int TotalLoaded,
    string Message
);

// ── Load Part เข้า Basket ──────────
public record LoadToBasketRequest(
    string PartId,
    string? LotNumber,
    string BasketId,
    int Qty,
    string OperatorId
);

public record LoadToBasketResponse(
    string BasketId,
    string PartId,
    int QtyLoaded,
    string BasketLabel,
    string Message
);
