using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Common;
using WmsApi.Services.Packing;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/packing")]
public class PackingController(IPackingService service) : ControllerBase
{
    [HttpGet("pallet/{palletId}")]
    public async Task<IActionResult> ScanPallet(string palletId) =>
        this.ToActionResult(await service.ScanPalletAsync(palletId));

    [HttpGet("pack/{packingId}")]
    public async Task<IActionResult> GetPack(string packingId) =>
        this.ToActionResult(await service.GetPackAsync(packingId));

    [HttpGet("pack/{packingId}/order/{pickOrderId}")]
    public async Task<IActionResult> GetOrder(string packingId, string pickOrderId) =>
        this.ToActionResult(await service.GetOrderAsync(packingId, pickOrderId));

    [HttpPost("scan-part")]
    public async Task<IActionResult> ScanPart([FromBody] ScanPackPartRequest req) =>
        this.ToActionResult(await service.ScanPartAsync(req));

    [HttpPost("confirm-pack")]
    public async Task<IActionResult> ConfirmPack([FromBody] ConfirmPackRequest req) =>
        this.ToActionResult(await service.ConfirmPackAsync(req));
}
