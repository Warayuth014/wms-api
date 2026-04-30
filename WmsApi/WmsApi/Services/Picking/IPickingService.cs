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
    Task<ServiceResult> CreatePickOrderAsync(CreatePickOrderRequest req);
    Task<ServiceResult> SendToPackAsync(string palletId);

    // Auto-allocate — เรียกจาก ASRS/PO receipt flow เพื่อเติม Sub ให้ PickOrder ที่ขาด
    Task<(int allocationsCreated, int qtyAllocated)> AllocatePendingForPartAsync(string partId);
}
