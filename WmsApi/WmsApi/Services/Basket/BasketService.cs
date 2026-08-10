using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Basket;

public class BasketService(WmsDbContext db) : IBasketService
{
    // ── รายการสินค้าที่ Unload แล้ว (group by Part+Lot) ──────────
    public async Task<ServiceResult> GetUnloadedItemsAsync()
    {
        var lines = await db.UnloadLines
            .Include(l => l.Part)
            .Where(l => l.Status == "CONFIRMED" || l.Status == "LOADED")
            .ToListAsync();

        var lineIds = lines.Select(l => l.LineId).ToList();

        // คำนวณ QtyLoaded จาก BasketLines
        var loadedMap = await db.BasketLines
            .Where(bl => lineIds.Contains(bl.UnloadLineId) && bl.Status == "LOADED")
            .GroupBy(bl => bl.UnloadLineId)
            .Select(g => new { UnloadLineId = g.Key, TotalLoaded = g.Sum(x => x.QtyLoaded) })
            .ToDictionaryAsync(x => x.UnloadLineId, x => x.TotalLoaded);

        // หา BasketId ล่าสุดต่อ UnloadLine
        var basketMap = await db.BasketLines
            .Where(bl => lineIds.Contains(bl.UnloadLineId) && bl.Status == "LOADED")
            .GroupBy(bl => bl.UnloadLineId)
            .Select(g => new { UnloadLineId = g.Key, BasketId = g.OrderByDescending(x => x.LoadedAt).First().BasketId })
            .ToDictionaryAsync(x => x.UnloadLineId, x => x.BasketId);

        // S/N ที่ยัง STORED (ยังไม่ได้ load) ของ Part เหล่านี้ — ใช้เช็คว่า Part ไหนต้องสแกน S/N ก่อน load เข้า basket
        var partIdsInScope = lines.Select(l => l.PartId).Distinct().ToList();
        var availableSerials = await db.PartSerials
            .Include(s => s.ReceiptLine)
            .Where(s => s.Status == "STORED"
                     && s.PalletId != null
                     && partIdsInScope.Contains(s.PartId))
            .Select(s => new { s.PartId, s.PalletId, LotNumber = s.ReceiptLine!.LotNumber, s.SerialNo })
            .ToListAsync();

        // Group by Owner+Part+Lot
        var grouped = lines
            .GroupBy(l => new { Owner = l.Part?.Owner ?? string.Empty, l.PartId, l.LotNumber })
            .Select(g =>
            {
                var first = g.First();
                var ids = g.Select(l => l.LineId).ToList();
                var totalUnloaded = g.Sum(l => l.QtyUnloaded);
                var totalLoaded = ids.Sum(id => loadedMap.GetValueOrDefault(id, 0));
                var lastBasket = ids
                    .Select(id => basketMap.GetValueOrDefault(id))
                    .Where(b => b != null)
                    .LastOrDefault();

                // S/N ต้องมาจาก Pallet เดียวกับที่ contribute เข้ากลุ่มนี้เท่านั้น (lot เดียวกันอาจมาจากหลาย pallet)
                var palletIdsInGroup = g.Select(l => l.PalletId).ToHashSet();
                var serialNumbers = availableSerials
                    .Where(s => s.PartId == first.PartId
                             && s.LotNumber == first.LotNumber
                             && palletIdsInGroup.Contains(s.PalletId!))
                    .Select(s => s.SerialNo)
                    .ToList();

                return new UnloadedItemResponse(
                    PartId: first.PartId,
                    Owner: first.Part?.Owner ?? string.Empty,
                    Brand: first.Part?.Brand ?? string.Empty,
                    ItemDesc: first.Part?.ItemDesc ?? string.Empty,
                    ImageUrl: first.Part?.ImageUrl,
                    LotNumber: first.LotNumber,
                    ExpiredDate: first.ExpiredDate?.ToString("yyyy-MM-dd"),
                    QtyUnloaded: totalUnloaded,
                    QtyLoaded: totalLoaded,
                    QtyRemaining: totalUnloaded - totalLoaded,
                    BasketId: lastBasket,
                    UnloadLineIds: ids,
                    SerialNumbers: serialNumbers
                );
            })
            .OrderByDescending(i => i.QtyRemaining > 0)
            .ThenBy(i => i.PartId)
            .ToList();

        var totalItems = grouped.Count;
        var totalLoaded2 = grouped.Count(i => i.QtyRemaining <= 0);

        return ServiceResult.Ok(new UnloadedItemsResponse(
            Items: grouped,
            TotalItems: totalItems,
            TotalLoaded: totalLoaded2,
            Message: $"พบ {totalItems} รายการ (load แล้ว {totalLoaded2})"
        ));
    }

    // ── Load Part เข้า Basket (by PartId+LotNumber) ──────────
    public async Task<ServiceResult> LoadToBasketAsync(LoadToBasketRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.BasketId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Basket ID"));
        if (string.IsNullOrWhiteSpace(req.PartId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Part ID"));
        if (req.Qty <= 0)
            return ServiceResult.BadRequest(new ApiError("จำนวนต้องมากกว่า 0"));

        var basketId = req.BasketId.Trim().ToUpper();

        // หา UnloadLines ทั้งหมดของ Part+Lot ที่ยังเหลือ
        var unloadLines = await db.UnloadLines
            .Include(l => l.Part)
            .Where(l => l.PartId == req.PartId
                     && l.LotNumber == req.LotNumber
                     && (l.Status == "CONFIRMED" || l.Status == "LOADED"))
            .OrderBy(l => l.LineId)
            .ToListAsync();

        if (unloadLines.Count == 0)
            return ServiceResult.NotFound(new ApiError(
                $"ไม่พบ Part '{req.PartId}' ที่ Unload แล้ว"));

        // คำนวณ remaining ต่อ line
        var lineIds = unloadLines.Select(l => l.LineId).ToList();
        var loadedPerLine = await db.BasketLines
            .Where(bl => lineIds.Contains(bl.UnloadLineId) && bl.Status == "LOADED")
            .GroupBy(bl => bl.UnloadLineId)
            .Select(g => new { UnloadLineId = g.Key, Total = g.Sum(x => x.QtyLoaded) })
            .ToDictionaryAsync(x => x.UnloadLineId, x => x.Total);

        var totalRemaining = unloadLines.Sum(l =>
            l.QtyUnloaded - loadedPerLine.GetValueOrDefault(l.LineId, 0));

        if (req.Qty > totalRemaining)
            return ServiceResult.BadRequest(new ApiError(
                $"จำนวนเกิน — Unload รวม {unloadLines.Sum(l => l.QtyUnloaded)}, load แล้ว {unloadLines.Sum(l => l.QtyUnloaded) - totalRemaining}, เหลือ {totalRemaining}"));

        // ── สินค้าที่มี S/N (ยัง STORED อยู่) ต้องสแกน S/N ให้ครบก่อน load เข้า basket ──
        var scannedSerials = req.SerialNumbers?
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList() ?? [];

        var palletIdsInGroup = unloadLines.Select(l => l.PalletId).ToHashSet();

        var availableSerials = await db.PartSerials
            .Include(s => s.ReceiptLine)
            .Where(s => s.PartId == req.PartId
                     && s.Status == "STORED"
                     && s.PalletId != null && palletIdsInGroup.Contains(s.PalletId!)
                     && s.ReceiptLine!.LotNumber == req.LotNumber)
            .ToListAsync();

        var serialsToConsume = new List<PartSerial>();
        if (availableSerials.Count > 0 || scannedSerials.Count > 0)
        {
            if (scannedSerials.Count == 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"กรุณาสแกน S/N สำหรับ Part '{req.PartId}' ก่อน Load เข้า Basket"));

            if (scannedSerials.Count != req.Qty)
                return ServiceResult.BadRequest(new ApiError(
                    $"จำนวน S/N ({scannedSerials.Count}) ไม่ตรงกับจำนวนที่ Load ({req.Qty})"));

            var availableMap = availableSerials.ToDictionary(s => s.SerialNo);
            var missing = scannedSerials.Where(sn => !availableMap.ContainsKey(sn)).ToList();
            if (missing.Count > 0)
                return ServiceResult.BadRequest(new ApiError(
                    $"S/N ไม่พบหรือไม่พร้อม Load: {string.Join(", ", missing)}"));

            serialsToConsume = scannedSerials.Select(sn => availableMap[sn]).ToList();
        }

        // สร้างหรือหา Basket
        var basket = await db.Baskets.FindAsync(basketId);
        if (basket is null)
        {
            basket = new Models.Basket
            {
                BasketId = basketId,
                Label = basketId,
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Baskets.Add(basket);
        }

        // กระจาย qty เข้า UnloadLines ตามลำดับ
        var qtyLeft = req.Qty;
        foreach (var line in unloadLines)
        {
            if (qtyLeft <= 0) break;

            var loaded = loadedPerLine.GetValueOrDefault(line.LineId, 0);
            var lineRemaining = line.QtyUnloaded - loaded;
            if (lineRemaining <= 0) continue;

            var take = Math.Min(qtyLeft, lineRemaining);

            db.BasketLines.Add(new BasketLine
            {
                SessionId = line.SessionId,
                UnloadLineId = line.LineId,
                BasketId = basketId,
                PartId = req.PartId,
                PalletId = line.PalletId,
                LotNumber = line.LotNumber,
                ExpiredDate = line.ExpiredDate,
                QtyLoaded = take,
                Status = "LOADED",
                LoadedAt = DateTime.UtcNow,
                OperatorId = req.OperatorId,
            });

            if (loaded + take >= line.QtyUnloaded)
            {
                line.Status = "LOADED";
                line.UpdatedAt = DateTime.UtcNow;
            }

            qtyLeft -= take;
        }

        var loadedAt = DateTime.UtcNow;
        foreach (var s in serialsToConsume)
        {
            s.Status = "LOADED";
            s.UpdatedAt = loadedAt;
        }

        basket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ServiceResult.Ok(new LoadToBasketResponse(
            BasketId: basketId,
            PartId: req.PartId,
            QtyLoaded: req.Qty,
            BasketLabel: basket.Label,
            Message: $"Load {req.Qty} ชิ้นของ '{req.PartId}' เข้า Basket '{basketId}' สำเร็จ"
        ));
    }

}
