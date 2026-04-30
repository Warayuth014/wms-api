using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Common;
using WmsApi.Services.Sorting;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/sorting")]
public class SortingController(ISortingService service) : ControllerBase
{
    [HttpGet("stations")]
    public async Task<IActionResult> GetStations() =>
        this.ToActionResult(await service.GetStationsAsync());

    [HttpGet("stations/{stationId:int}")]
    public async Task<IActionResult> GetStation(int stationId) =>
        this.ToActionResult(await service.GetStationDetailAsync(stationId));

    [HttpPost("stations/toggle")]
    public async Task<IActionResult> Toggle([FromBody] ToggleStationRequest req) =>
        this.ToActionResult(await service.ToggleStationAsync(req));

    [HttpPost("stations/clear")]
    public async Task<IActionResult> Clear([FromBody] ClearStationRequest req) =>
        this.ToActionResult(await service.ClearStationAsync(req));

    // ── Dev harness ─────────────────────────────────
    [HttpGet("test/available-packs")]
    public async Task<IActionResult> GetAvailablePacks() =>
        this.ToActionResult(await service.GetAvailablePacksAsync());

    [HttpPost("test/create-batch")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateSortingBatchRequest req) =>
        this.ToActionResult(await service.CreateTestBatchAsync(req));
}
