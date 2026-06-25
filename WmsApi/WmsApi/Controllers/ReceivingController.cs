using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Common;
using WmsApi.Services.Receiving;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/receiving")]
public class ReceivingController(IReceivingService service) : ControllerBase
{
    [HttpGet("po/{poId}")]
    public async Task<IActionResult> GetPO(string poId) =>
        this.ToActionResult(await service.GetPOAsync(poId));

    [HttpGet("validate-serial")]
    public async Task<IActionResult> ValidateSerial([FromQuery] string partId, [FromQuery] string serialNo) =>
        this.ToActionResult(await service.ValidateSerialAsync(partId, serialNo));

    [HttpPost("scan-part")]
    public async Task<IActionResult> ScanPart([FromBody] ScanReceiptPartRequest req) =>
        this.ToActionResult(await service.ScanPartAsync(req));

    [HttpPost("assign-pallet")]
    public async Task<IActionResult> AssignPallet([FromBody] AssignPalletRequest req) =>
        this.ToActionResult(await service.AssignPalletAsync(req));

    [HttpGet("pending-pallet-lines")]
    public async Task<IActionResult> GetPendingPalletLines() =>
        this.ToActionResult(await service.GetPendingPalletLinesAsync());
}
