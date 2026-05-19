using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Receiving;

public interface IReceivingService
{
    Task<ServiceResult> GetPOAsync(string poId);
    Task<ServiceResult> GetActiveSessionAsync(string poId);
    Task<ServiceResult> ValidateSerialAsync(string partId, string serialNo);
    Task<ServiceResult> OpenSessionAsync(OpenReceivingRequest req);
    Task<ServiceResult> ScanPartAsync(ScanReceiptPartRequest req);
    Task<ServiceResult> AssignPalletAsync(AssignPalletRequest req);
    Task<ServiceResult> GetPendingPalletLinesAsync();
    Task<ServiceResult> CloseSessionAsync(int sessionId);
}
