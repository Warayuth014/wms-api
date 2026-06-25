using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Hubs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Putaway;

public class PutawayService(WmsDbContext db, IHubContext<PutawayHub> hub) : IPutawayService
{
    private static readonly HashSet<string> ValidDestinations =
        new(StringComparer.OrdinalIgnoreCase) { "ASRS", "PREWORK", "REPLENISH" };

    public async Task<ServiceResult> ScanPalletAsync(string palletId, string? stationId)
    {
        var pallet = await db.Pallets.FindAsync(palletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"Pallet '{palletId}' not found."));

        var isPWStation = stationId?.StartsWith("PW-STN", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isPWStation)
        {
            if (pallet.Status != "PREWORK")
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{palletId}' ยังไม่ได้อยู่ที่ Prework (สถานะ: {pallet.Status})",
                    "ต้องนำ Pallet ไปที่ Prework ก่อน โดย Putaway ที่ STN-1/2/3 → เลือก Prework"));
            }
        }
        else
        {
            if (pallet.Status == "AVAILABLE" && pallet.Location == "ASRS")
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{palletId}' อยู่ใน ASRS แล้ว",
                    "Pallet นี้ถูกจัดเก็บเข้า ASRS เรียบร้อยแล้ว"));
            }

            if (pallet.Status is not ("FG" or "PW" or "AVAILABLE"))
            {
                return ServiceResult.BadRequest(new ApiError(
                    $"Pallet '{palletId}' ไม่พร้อม Putaway (สถานะปัจจุบัน: {pallet.Status})",
                    "Pallet ต้องเป็นสถานะ FG, PW หรือ AVAILABLE เท่านั้น"));
            }
        }

        var lines = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId == palletId && l.Status == "PALLETIZED")
            .ToListAsync();

        var items = lines.Select(l => new UnloadItemResponse(
            PartId: l.PartId,
            Owner: l.Part!.Owner,
            Brand: l.Part!.Brand,
            ItemDesc: l.Part!.ItemDesc,
            ImageUrl: l.Part!.ImageUrl,
            LotNumber: l.LotNumber,
            ExpiredDate: l.ExpiredDate?.ToString("yyyy-MM-dd"),
            Qty: l.QtyReceived,
            Condition: l.Condition
        )).ToList();

        var suggested = pallet.Status == "AVAILABLE" ? "ASRS"
            : pallet.Type == "FG" ? "ASRS" : "PREWORK";

        var message = pallet.Status == "AVAILABLE"
            ? "Pallet ว่าง → ส่งเข้า ASRS"
            : pallet.Type == "FG"
                ? "Pallet FG → เก็บเข้า ASRS"
                : "Pallet PW → แนะนำ Prework (เลือก ASRS ได้)";

        return ServiceResult.Ok(new ScanPalletForPutawayResponse(
            PalletId: pallet.PalletId,
            Type: pallet.Type ?? "-",
            Status: pallet.Status,
            SuggestedDestination: suggested,
            Items: items,
            Message: message
        ));
    }

    public async Task<ServiceResult> ConfirmPutawayAsync(ConfirmPutawayRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status is not ("FG" or "PW" or "PREWORK" or "AVAILABLE"))
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' ไม่พร้อม Putaway (สถานะ: {pallet.Status})"));
        }

        if (pallet.Status == "AVAILABLE" && pallet.Location == "ASRS")
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' อยู่ใน ASRS แล้ว"));
        }

        var operator_ = await db.Users.FindAsync(req.OperatorId);
        if (operator_ is null)
            return ServiceResult.NotFound(new ApiError($"User '{req.OperatorId}' not found."));

        var dest = req.Destination.ToUpper();
        if (!ValidDestinations.Contains(dest))
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Destination ไม่ถูกต้อง: '{req.Destination}'",
                "ค่าที่รองรับ: ASRS, PREWORK, REPLENISH"));
        }

        if (pallet.Type == "FG" && dest == "PREWORK")
        {
            return ServiceResult.BadRequest(new ApiError(
                "Pallet ประเภท FG ไม่สามารถส่งไป PREWORK ได้",
                "FG รองรับ ASRS หรือ REPLENISH เท่านั้น"));
        }

        if (pallet.Type == "PW" && dest == "REPLENISH")
        {
            return ServiceResult.BadRequest(new ApiError(
                "Pallet ประเภท PW ไม่สามารถส่งไป REPLENISH ได้",
                "PW รองรับ ASRS หรือ PREWORK เท่านั้น"));
        }

        if (req.WrappingRequired && dest != "ASRS")
        {
            return ServiceResult.BadRequest(new ApiError(
                "WrappingRequired ใช้ได้กับ Destination ASRS เท่านั้น"));
        }

        var isPWStation = req.StationId.StartsWith("PW-STN", StringComparison.OrdinalIgnoreCase);
        var convertedPWtoFG = isPWStation && pallet.Type == "PW";
        if (convertedPWtoFG)
        {
            // ส่ง PW pallet ออกจาก PW-Station = prework เสร็จแล้ว → convert PW→FG อัตโนมัติ
            pallet.Type = "FG";

            var linesOnPallet = await db.ReceiptLines
                .Where(l => l.PalletId == req.PalletId && l.Condition == "PW")
                .ToListAsync();
            foreach (var l in linesOnPallet)
                l.Condition = "FG";
        }

        var busySession = await db.PutawaySessions
            .FirstOrDefaultAsync(s => s.StationId == req.StationId.ToUpper()
                                   && s.Status == "AGV_DISPATCHED");
        if (busySession is not null)
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Station '{req.StationId}' ไม่ว่าง",
                $"มี Pallet '{busySession.PalletId}' อยู่ที่ Station นี้แล้ว กรุณารอ AGV มารับก่อน"));
        }

        string? mappedPWStation = null;
        if (dest == "PREWORK")
        {
            var receiveStations = new[] { "PW-STN-1", "PW-STN-3", "PW-STN-5" };
            var occupiedStations = await db.Pallets
                .Where(p => p.Location != null && receiveStations.Contains(p.Location))
                .Select(p => p.Location!)
                .ToListAsync();

            mappedPWStation = receiveStations.FirstOrDefault(s => !occupiedStations.Contains(s));
            if (mappedPWStation is null)
            {
                return ServiceResult.BadRequest(new ApiError(
                    "Prework Station เต็มทั้งหมด (PW-STN-1, PW-STN-3, PW-STN-5)",
                    "กรุณาคืน Pallet เปล่าที่ Station ใดก่อน แล้วส่ง Pallet ใหม่อีกครั้ง"));
            }
        }

        var session = new PutawaySession
        {
            PalletId = req.PalletId,
            StationId = req.StationId.ToUpper(),
            Destination = dest,
            Status = "AGV_DISPATCHED",
            WrappingRequired = req.WrappingRequired,
            OperatorId = req.OperatorId,
            CreatedAt = DateTime.UtcNow
        };
        db.PutawaySessions.Add(session);
        await db.SaveChangesAsync();

        if (req.WrappingRequired)
        {
            db.WrappingSessions.Add(new WrappingSession
            {
                PutawayId = session.PutawayId,
                PalletId = req.PalletId,
                Status = "COMPLETED",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        }

        if (dest == "PREWORK")
        {
            var lines = await db.ReceiptLines
                .Include(l => l.Part)
                .Where(l => l.PalletId == req.PalletId && l.Status == "PALLETIZED")
                .ToListAsync();

            var payloadObj = new
            {
                putawayId = session.PutawayId,
                palletId = req.PalletId,
                stationId = mappedPWStation,
                operatorId = req.OperatorId,
                items = lines.Select(l => new
                {
                    partId = l.PartId,
                    lotNumber = l.LotNumber,
                    expiredDate = l.ExpiredDate?.ToString("yyyy-MM-dd"),
                    qty = l.QtyReceived,
                    condition = l.Condition
                })
            };

            db.ShipXQueues.Add(new ShipXQueue
            {
                PutawayId = session.PutawayId,
                PalletId = req.PalletId,
                Payload = JsonSerializer.Serialize(payloadObj),
                Status = "QUEUED",
                CreatedAt = DateTime.UtcNow
            });

            pallet.Status = "IN_TRANSIT";
            pallet.Location = mappedPWStation;
        }
        else
        {
            pallet.Status = "IN_TRANSIT";
            pallet.Location = dest;
        }

        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("StationDispatched", new
        {
            stationId = req.StationId.ToUpper(),
            palletId = req.PalletId,
            destination = dest,
        });

        var convertMsg = convertedPWtoFG ? " (PW→FG converted)" : "";
        var wrappingMsg = req.WrappingRequired ? " (ผ่าน Wrapping Machine)" : "";
        var destLabel = dest switch
        {
            "ASRS" => "ASRS",
            "PREWORK" => "Pre Work Station",
            "REPLENISH" => "Replenish Station",
            _ => dest
        };

        return ServiceResult.Ok(new ConfirmPutawayResponse(
            Success: true,
            PalletId: req.PalletId,
            StationId: req.StationId.ToUpper(),
            Destination: dest,
            Message: $"✅ Pallet '{req.PalletId}' → {destLabel}{convertMsg}{wrappingMsg}"
        ));
    }

    public async Task<ServiceResult> GetStationStatusAsync()
    {
        var activeSessions = await db.PutawaySessions
            .Where(s => s.Status == "AGV_DISPATCHED")
            .Select(s => new
            {
                s.StationId,
                s.PalletId,
                s.Destination,
                s.CreatedAt
            })
            .ToListAsync();

        var palletIds = activeSessions.Select(s => s.PalletId).Distinct().ToList();
        var palletItems = await db.ReceiptLines
            .Include(l => l.Part)
            .Where(l => l.PalletId != null && palletIds.Contains(l.PalletId) && l.Status == "PALLETIZED")
            .GroupBy(l => l.PalletId)
            .Select(g => new
            {
                PalletId = g.Key,
                Items = g.Select(l => new
                {
                    l.PartId,
                    l.Part!.ItemDesc,
                    Qty = l.QtyReceived
                }).ToList()
            })
            .ToListAsync();

        var palletItemsDict = palletItems.ToDictionary(p => p.PalletId!, p => p.Items);

        var result = activeSessions.Select(s => new
        {
            s.StationId,
            s.PalletId,
            s.Destination,
            s.CreatedAt,
            Items = palletItemsDict.GetValueOrDefault(s.PalletId, [])
        }).ToList();

        return ServiceResult.Ok(new { items = result });
    }

    public async Task<ServiceResult> GetPreworkStationStatusAsync()
    {
        var receiveStations = new[] { "PW-STN-1", "PW-STN-3", "PW-STN-5" };

        var pallets = await db.Pallets
            .Where(p => receiveStations.Contains(p.Location!)
                    && (p.Status == "IN_TRANSIT" || p.Status == "PREWORK" || p.Status == "AVAILABLE"))
            .ToListAsync();

        var palletStationPairs = pallets
            .Where(p => p.Location != null)
            .Select(p => new { p.PalletId, Station = p.Location! })
            .ToList();
        var palletIds = palletStationPairs.Select(p => p.PalletId).ToList();

        var cutLogs = await db.PreworkCutLogs
            .Where(c => palletIds.Contains(c.PalletId))
            .OrderByDescending(c => c.CutAt)
            .ToListAsync();

        var result = receiveStations.Select(stationId =>
        {
            var pallet = pallets.FirstOrDefault(p => p.Location == stationId);
            if (pallet is null)
            {
                return new
                {
                    StationId = stationId,
                    PalletId = (string?)null,
                    PalletStatus = (string?)null,
                    CutItems = Array.Empty<object>()
                };
            }

            var allLogs = cutLogs
                .Where(c => c.PalletId == pallet.PalletId && c.StationId == stationId)
                .ToList();
            var latestCutAt = allLogs.FirstOrDefault()?.CutAt;
            var logs = latestCutAt is null
                ? allLogs
                : allLogs.Where(c => (latestCutAt.Value - c.CutAt).TotalSeconds < 5).ToList();

            return new
            {
                StationId = stationId,
                PalletId = (string?)pallet.PalletId,
                PalletStatus = (string?)pallet.Status,
                CutItems = logs.Select(l => (object)new
                {
                    l.PartId,
                    l.Owner,
                    l.Brand,
                    l.ItemDesc,
                    l.ImageUrl,
                    l.Qty,
                    l.LotNumber,
                    ExpiredDate = l.ExpiredDate?.ToString("yyyy-MM-dd"),
                    l.Condition,
                    CutAt = l.CutAt.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToArray()
            };
        }).ToList();

        return ServiceResult.Ok(new { stations = result });
    }

    public async Task<ServiceResult> PreworkReturnPalletAsync(PreworkReturnPalletRequest req)
    {
        var pallet = await db.Pallets.FindAsync(req.PalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"Pallet '{req.PalletId}' not found."));

        if (pallet.Status != "AVAILABLE")
        {
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{req.PalletId}' สถานะ '{pallet.Status}' — ต้องเป็น AVAILABLE (ตัดยอดแล้ว)"));
        }

        pallet.Type = null;
        pallet.Status = "AVAILABLE";
        pallet.Location = null;
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("PalletReturned", new
        {
            stationId = req.StationId,
            palletId = req.PalletId,
        });

        return ServiceResult.Ok(new ApiSuccess(
            true,
            $"✅ Pallet '{req.PalletId}' คืนเรียบร้อย (AVAILABLE)"));
    }
}
