using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Sorting;

public interface ISortingService
{
    Task<ServiceResult> GetStationsAsync();
    Task<ServiceResult> GetStationDetailAsync(int stationId);
    Task<ServiceResult> ToggleStationAsync(ToggleStationRequest req);
    Task<ServiceResult> ClearStationAsync(ClearStationRequest req);

    // Test / dev harness — สร้าง batch ใหม่
    Task<ServiceResult> GetAvailablePacksAsync();
    Task<ServiceResult> CreateTestBatchAsync(CreateSortingBatchRequest req);

    // ใช้โดย SortingFlowSimulator hosted service
    Task<WmsApi.Models.SortingPallet> CreatePalletForBatchAsync(
        int stationId, List<string> packingIds, string operatorId);
}
