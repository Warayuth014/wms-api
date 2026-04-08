using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Packing;

public interface IPackingService
{
    Task<ServiceResult> ScanPalletAsync(string palletId);
    Task<ServiceResult> GetPackAsync(string packingId);
    Task<ServiceResult> GetOrderAsync(string packingId, string pickOrderId);
    Task<ServiceResult> ScanPartAsync(ScanPackPartRequest req);
    Task<ServiceResult> ConfirmPackAsync(ConfirmPackRequest req);
}
