using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Unload;

public interface IUnloadService
{
    Task<ServiceResult> OpenSessionAsync(OpenUnloadRequest req);
    Task<ServiceResult> ConfirmUnloadAsync(ConfirmUnloadRequest req);
    Task<ServiceResult> ReturnPalletToAsisAsync(ReturnPalletToAsisRequest req);
}
