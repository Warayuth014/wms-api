using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.CheckIn;
using WmsApi.Services.Common;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/checkin")]
public class CheckInController(ICheckInService service) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanCheckInRequest req) =>
        this.ToActionResult(await service.ScanCartonAsync(req));

    [HttpGet("slot/{slotId}")]
    public async Task<IActionResult> GetSlot(string slotId) =>
        this.ToActionResult(await service.GetSlotAsync(slotId));

    [HttpGet("slots")]
    public async Task<IActionResult> GetActiveSlots() =>
        this.ToActionResult(await service.GetActiveSlotsAsync());

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteCheckInRequest req) =>
        this.ToActionResult(await service.CompleteSlotAsync(req));

    [HttpPost("dispatch")]
    public async Task<IActionResult> Dispatch([FromBody] DispatchCheckInRequest req) =>
        this.ToActionResult(await service.DispatchSlotAsync(req));
}
