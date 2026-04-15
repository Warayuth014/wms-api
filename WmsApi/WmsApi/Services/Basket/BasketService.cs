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
                    UnloadLineIds: ids
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

    // ── ดู Basket detail ──────────
    public async Task<ServiceResult> GetBasketAsync(string basketId)
    {
        var bid = basketId.Trim().ToUpper();
        var basket = await db.Baskets
            .Include(b => b.Lines).ThenInclude(l => l.Part)
            .FirstOrDefaultAsync(b => b.BasketId == bid);

        if (basket is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Basket '{bid}'"));

        var lines = basket.Lines
            .Where(l => l.Status == "LOADED")
            .Select(l => new BasketLineResponse(
                LineId: l.LineId,
                PartId: l.PartId,
                Owner: l.Part?.Owner ?? string.Empty,
                Brand: l.Part?.Brand ?? string.Empty,
                ItemDesc: l.Part?.ItemDesc ?? string.Empty,
                ImageUrl: l.Part?.ImageUrl,
                QtyLoaded: l.QtyLoaded,
                Status: l.Status
            )).ToList();

        return ServiceResult.Ok(new BasketResponse(
            BasketId: basket.BasketId,
            Label: basket.Label,
            Zone: basket.Zone,
            Destination: basket.Destination,
            Status: basket.Status,
            CreatedAt: basket.CreatedAt,
            Lines: lines
        ));
    }
}
