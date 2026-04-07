using Microsoft.EntityFrameworkCore;
using WmsApi.Data;
using WmsApi.DTOs;
using WmsApi.Models;
using WmsApi.Services.Common;

namespace WmsApi.Services.Sorting;

public class SortingService(WmsDbContext db) : ISortingService
{
    public async Task<ServiceResult> GetStationsAsync()
    {
        var stations = await db.SortStations
            .OrderBy(s => s.StationId)
            .Select(s => new SortStationResponse(s.StationId, s.Name, s.Status))
            .ToListAsync();

        return ServiceResult.Ok(stations);
    }

    public async Task<ServiceResult> OpenSessionAsync(OpenSortSessionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.StationId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Station"));
        if (string.IsNullOrWhiteSpace(req.SortPalletId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Pallet ID"));
        if (string.IsNullOrWhiteSpace(req.OperatorId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Operator ID"));

        var station = await db.SortStations.FindAsync(req.StationId);
        if (station is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Station '{req.StationId}'"));

        if (station.Status == "BUSY")
            return ServiceResult.BadRequest(new ApiError(
                $"Station '{req.StationId}' กำลังใช้งานอยู่"));

        var pallet = await db.Pallets.FindAsync(req.SortPalletId);
        if (pallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Pallet '{req.SortPalletId}'"));

        if (pallet.Status != "AVAILABLE")
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{req.SortPalletId}' ไม่ว่าง (สถานะ: {pallet.Status})"));

        var session = new SortSession
        {
            StationId = req.StationId,
            SortPalletId = req.SortPalletId,
            Status = "OPEN",
            OperatorId = req.OperatorId,
            CreatedAt = DateTime.UtcNow,
        };

        db.SortSessions.Add(session);

        station.Status = "BUSY";
        pallet.Status = "SORTING";
        pallet.Location = req.StationId;
        pallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ServiceResult.Ok(BuildSessionResponse(session));
    }

    public async Task<ServiceResult> GetSessionAsync(int sessionId)
    {
        var session = await db.SortSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Session {sessionId}"));

        return ServiceResult.Ok(BuildSessionResponse(session));
    }

    public async Task<ServiceResult> ScanCartonAsync(ScanSortCartonRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CartonId))
            return ServiceResult.BadRequest(new ApiError("กรุณาระบุ Carton/Tracking"));

        var session = await db.SortSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId);

        if (session is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Session {req.SessionId}"));

        if (session.Status != "OPEN")
            return ServiceResult.BadRequest(new ApiError("Session นี้ปิดแล้ว"));

        // หา source pallet จาก TrackingId หรือ PalletId
        var carton = req.CartonId.Trim().ToUpper();
        var sourcePallet = await db.Pallets
            .FirstOrDefaultAsync(p => p.PalletId == carton || p.TrackingId == carton);

        if (sourcePallet is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Carton/Pallet '{carton}'"));

        if (sourcePallet.Status != "SHIPPED")
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{sourcePallet.PalletId}' ไม่พร้อม Sort (สถานะ: {sourcePallet.Status})"));

        if (session.Items.Any(i => i.SourcePalletId == sourcePallet.PalletId))
            return ServiceResult.BadRequest(new ApiError(
                $"Pallet '{sourcePallet.PalletId}' ถูกสแกนไปแล้ว"));

        var item = new SortSessionItem
        {
            SessionId = session.SessionId,
            SourcePalletId = sourcePallet.PalletId,
            TrackingId = sourcePallet.TrackingId,
            ScannedAt = DateTime.UtcNow,
        };

        db.SortSessionItems.Add(item);

        sourcePallet.Status = "SORTED";
        sourcePallet.Location = session.StationId;
        sourcePallet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // refresh
        session = await db.SortSessions
            .Include(s => s.Items)
            .FirstAsync(s => s.SessionId == req.SessionId);

        return ServiceResult.Ok(BuildSessionResponse(session));
    }

    public async Task<ServiceResult> CloseSessionAsync(CloseSortSessionRequest req)
    {
        var session = await db.SortSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SessionId == req.SessionId);

        if (session is null)
            return ServiceResult.NotFound(new ApiError($"ไม่พบ Session {req.SessionId}"));

        if (session.Status != "OPEN")
            return ServiceResult.BadRequest(new ApiError("Session นี้ปิดแล้ว"));

        if (session.Items.Count == 0)
            return ServiceResult.BadRequest(new ApiError(
                "ยังไม่มี Carton ใน Session นี้ ปิดไม่ได้"));

        session.Status = "CLOSED";
        session.ClosedAt = DateTime.UtcNow;

        // ปลดล็อค station
        var station = await db.SortStations.FindAsync(session.StationId);
        if (station != null) station.Status = "AVAILABLE";

        // ส่ง sort pallet ไป Docking
        var sortPallet = await db.Pallets.FindAsync(session.SortPalletId);
        if (sortPallet != null)
        {
            sortPallet.Status = "AT_DOCK";
            sortPallet.Location = "DOCKING";
            sortPallet.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        return ServiceResult.Ok(BuildSessionResponse(session));
    }

    private static SortSessionResponse BuildSessionResponse(SortSession s) =>
        new(
            SessionId: s.SessionId,
            StationId: s.StationId,
            SortPalletId: s.SortPalletId,
            Status: s.Status,
            CreatedAt: s.CreatedAt,
            ClosedAt: s.ClosedAt,
            Items: s.Items
                .OrderByDescending(i => i.ScannedAt)
                .Select(i => new SortSessionItemResponse(
                    Id: i.Id,
                    SourcePalletId: i.SourcePalletId,
                    TrackingId: i.TrackingId,
                    ScannedAt: i.ScannedAt
                )).ToList()
        );

    public Task<ServiceResult> GetOpenSessionByStationAsync(string stationId)
    {
        throw new NotImplementedException();
    }

    //public Task<ServiceResult> CancelSessionAsync(CancelSortSessionRequest req)
    //{
    //    throw new NotImplementedException();
    //}
}
