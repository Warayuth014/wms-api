using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Picking;

public interface IPickingService
{
    Task<ServiceResult> GetPickOrdersAsync();
    Task<ServiceResult> GetPickOrderAsync(string pickOrderId);
    Task<ServiceResult> AssignStationAsync(AssignPickStationRequest req);
    Task<ServiceResult> ConfirmPickAsync(ConfirmPickRequest req);
    Task<ServiceResult> ReturnPalletAsync(ReturnPalletRequest req);
    Task<ServiceResult> GetAvailableLinesAsync();
    Task<ServiceResult> CreateTestOrderAsync(CreateTestOrderRequest req);
}
