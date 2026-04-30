using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Common;
using WmsApi.Services.Putaway;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/putaway")]
public class PutawayController(IPutawayService service) : ControllerBase
{
    [HttpGet("scan-pallet/{palletId}")]
    public async Task<IActionResult> ScanPallet(string palletId, [FromQuery] string? stationId) =>
        this.ToActionResult(await service.ScanPalletAsync(palletId, stationId));

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmPutaway([FromBody] ConfirmPutawayRequest req) =>
        this.ToActionResult(await service.ConfirmPutawayAsync(req));

    [HttpGet("station-status")]
    public async Task<IActionResult> GetStationStatus() =>
        this.ToActionResult(await service.GetStationStatusAsync());

    [HttpGet("prework-station-status")]
    public async Task<IActionResult> GetPreworkStationStatus() =>
        this.ToActionResult(await service.GetPreworkStationStatusAsync());

    [HttpGet("prework-pallets")]
    public async Task<IActionResult> GetPreworkPallets() =>
        this.ToActionResult(await service.GetPreworkPalletsAsync());

    [HttpPost("prework-receive")]
    public async Task<IActionResult> PreworkReceive([FromBody] PreworkReceiveRequest req) =>
        this.ToActionResult(await service.PreworkReceiveAsync(req));

    [HttpPost("prework-return-pallet")]
    public async Task<IActionResult> PreworkReturnPallet([FromBody] PreworkReturnPalletRequest req) =>
        this.ToActionResult(await service.PreworkReturnPalletAsync(req));
}
