using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Common;
using WmsApi.Services.Packing;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/packing")]
public class PackingController(IPackingService service) : ControllerBase
{
    [HttpGet("scan-pallet/{palletId}")]
    public async Task<IActionResult> ScanPallet(string palletId) =>
        this.ToActionResult(await service.ScanPalletAsync(palletId));

    [HttpPost("confirm-pack")]
    public async Task<IActionResult> ConfirmPack([FromBody] ConfirmPackRequest req) =>
        this.ToActionResult(await service.ConfirmPackAsync(req));
}
