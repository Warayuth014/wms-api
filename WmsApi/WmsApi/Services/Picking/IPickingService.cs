using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Picking;

public interface IPickingService
{
    Task<ServiceResult> GetPickOrdersAsync();
    Task<ServiceResult> GetPickOrderAsync(string pickOrderId);

    // New flow: 2 pages before scan-pallet
    Task<ServiceResult> ListOrdersAsync();              // หน้า 1
    Task<ServiceResult> GetOrderDetailAsync(string pickOrderId);   // หน้า 2
    Task<ServiceResult> NotifyArrivalAsync(string pickOrderId);    // robot simulator
    Task<ServiceResult> SuggestDestPalletsAsync(string? pickOrderId);  // pallet แนะนำเป็นปลายทาง (ต่อจากของเดิมถ้าเป็น order เดียวกัน)
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
