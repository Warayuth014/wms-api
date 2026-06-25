using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Putaway;

public interface IPutawayService
{
    Task<ServiceResult> ScanPalletAsync(string palletId, string? stationId);
    Task<ServiceResult> ConfirmPutawayAsync(ConfirmPutawayRequest req);
    Task<ServiceResult> GetStationStatusAsync();
    Task<ServiceResult> GetPreworkStationStatusAsync();
    Task<ServiceResult> PreworkReturnPalletAsync(PreworkReturnPalletRequest req);
}
