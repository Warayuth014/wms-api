using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Common;
using WmsApi.Services.Picking;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/picking")]
public class PickingController(IPickingService service) : ControllerBase
{
    // ── 2-page picking entry ─────────────────
    [HttpGet("orders-list")]
    public async Task<IActionResult> ListOrders() =>
        this.ToActionResult(await service.ListOrdersAsync());

    [HttpGet("orders/{pickOrderId}/detail")]
    public async Task<IActionResult> GetOrderDetail(string pickOrderId) =>
        this.ToActionResult(await service.GetOrderDetailAsync(pickOrderId));

    [HttpPost("orders/{pickOrderId}/notify-arrival")]
    public async Task<IActionResult> NotifyArrival(string pickOrderId) =>
        this.ToActionResult(await service.NotifyArrivalAsync(pickOrderId));

    [HttpGet("suggest-dest-pallets")]
    public async Task<IActionResult> SuggestDestPallets([FromQuery] string? pickOrderId) =>
        this.ToActionResult(await service.SuggestDestPalletsAsync(pickOrderId));

    [HttpPost("assign-station")]
    public async Task<IActionResult> AssignStation([FromBody] AssignPickStationRequest req) =>
        this.ToActionResult(await service.AssignStationAsync(req));

    [HttpPost("confirm-pick")]
    public async Task<IActionResult> ConfirmPick([FromBody] ConfirmPickRequest req) =>
        this.ToActionResult(await service.ConfirmPickAsync(req));

    [HttpPost("return-pallet")]
    public async Task<IActionResult> ReturnPallet([FromBody] ReturnPalletRequest req) =>
        this.ToActionResult(await service.ReturnPalletAsync(req));

    [HttpGet("return-pallet-preview/{palletId}")]
    public async Task<IActionResult> PreviewReturnPallet(string palletId) =>
        this.ToActionResult(await service.PreviewReturnPalletAsync(palletId));

    [HttpGet("available-lines")]
    public async Task<IActionResult> GetAvailableLines() =>
        this.ToActionResult(await service.GetAvailableLinesAsync());

    [HttpPost("create-test-order")]
    public async Task<IActionResult> CreateTestOrder([FromBody] CreateTestOrderRequest req) =>
        this.ToActionResult(await service.CreateTestOrderAsync(req));

    [HttpPost("send-to-pack/{palletId}")]
    public async Task<IActionResult> SendToPack(string palletId) =>
        this.ToActionResult(await service.SendToPackAsync(palletId));
}
