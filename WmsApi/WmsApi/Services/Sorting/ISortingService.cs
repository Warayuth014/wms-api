using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Sorting;

public interface ISortingService
{
    Task<ServiceResult> GetStationsAsync();
    Task<ServiceResult> OpenSessionAsync(OpenSortSessionRequest req);
    Task<ServiceResult> GetSessionAsync(int sessionId);
    Task<ServiceResult> GetOpenSessionByStationAsync(string stationId);
    Task<ServiceResult> ScanCartonAsync(ScanSortCartonRequest req);
    Task<ServiceResult> CloseSessionAsync(CloseSortSessionRequest req);
    //Task<ServiceResult> CancelSessionAsync(CancelSortSessionRequest req);
}
