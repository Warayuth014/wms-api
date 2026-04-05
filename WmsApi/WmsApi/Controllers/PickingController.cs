using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Common;
using WmsApi.Services.Picking;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/picking")]
public class PickingController(IPickingService service) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<IActionResult> GetPickOrders() =>
        this.ToActionResult(await service.GetPickOrdersAsync());

    [HttpGet("order/{pickOrderId}")]
    public async Task<IActionResult> GetPickOrder(string pickOrderId) =>
        this.ToActionResult(await service.GetPickOrderAsync(pickOrderId));

    [HttpPost("assign-station")]
    public async Task<IActionResult> AssignStation([FromBody] AssignPickStationRequest req) =>
        this.ToActionResult(await service.AssignStationAsync(req));

    [HttpPost("confirm-pick")]
    public async Task<IActionResult> ConfirmPick([FromBody] ConfirmPickRequest req) =>
        this.ToActionResult(await service.ConfirmPickAsync(req));

    [HttpPost("return-pallet")]
    public async Task<IActionResult> ReturnPallet([FromBody] ReturnPalletRequest req) =>
        this.ToActionResult(await service.ReturnPalletAsync(req));

    [HttpGet("available-lines")]
    public async Task<IActionResult> GetAvailableLines() =>
        this.ToActionResult(await service.GetAvailableLinesAsync());

    [HttpPost("create-test-order")]
    public async Task<IActionResult> CreateTestOrder([FromBody] CreateTestOrderRequest req) =>
        this.ToActionResult(await service.CreateTestOrderAsync(req));
}
