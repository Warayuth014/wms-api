using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.CheckIn;

public interface ICheckInService
{
    Task<ServiceResult> ScanCartonAsync(ScanCheckInRequest req);
    Task<ServiceResult> GetSlotAsync(string slotId);
    Task<ServiceResult> GetActiveSlotsAsync();
    Task<ServiceResult> CompleteSlotAsync(CompleteCheckInRequest req);
    Task<ServiceResult> DispatchSlotAsync(DispatchCheckInRequest req);
}
