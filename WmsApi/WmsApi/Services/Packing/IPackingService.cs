using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Packing;

public interface IPackingService
{
    Task<ServiceResult> ScanPalletAsync(string palletId);
    Task<ServiceResult> ConfirmPackAsync(ConfirmPackRequest req);
}
