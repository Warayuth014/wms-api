namespace WmsApi.DTOs;

// LineId = UnloadLine.LineId — ระบุ Part+Lot ที่แน่นอน (1 Part อาจมีหลาย Lot บน pallet เดียวกัน)
public record UnloadItemResponse(
    int LineId,
    string PartId,
    string Owner,
    string Brand,
    string ItemDesc,
    string? ImageUrl,
    string? LotNumber,
    string? ExpiredDate,
    int Qty,
    string Condition,
    List<string> SerialNumbers // S/N ที่ยัง STORED อยู่บน pallet นี้ (Part+Lot นี้) — มีรายการ = ต้องสแกน S/N ตอน unload
);

public record OpenUnloadRequest(
    string PalletId,
    string OperatorId
);

public record OpenUnloadResponse(
    int SessionId,
    string PalletId,
    string Status,
    List<UnloadItemResponse> Items,
    List<int> ConfirmedLineIds // LineId ไม่ใช่ PartId — Part เดียวกันอาจมีหลาย Lot ซึ่งบางอันยัง PENDING บางอัน CONFIRMED
);

public record ConfirmUnloadRequest(
    int SessionId,
    string PalletId,
    string PartId,
    int LineId,
    string OperatorId,
    int? QtyUnloaded = null,
    List<string>? SerialNumbers = null
);

public record ConfirmUnloadResponse(
    bool Success,
    string Message,
    int ConfirmedCount,
    int TotalCount,
    bool AllConfirmed
);
