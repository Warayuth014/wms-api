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

    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenSortSessionRequest req) =>
        this.ToActionResult(await service.OpenSessionAsync(req));

    [HttpGet("session/{sessionId:int}")]
    public async Task<IActionResult> GetSession(int sessionId) =>
        this.ToActionResult(await service.GetSessionAsync(sessionId));

    [HttpPost("scan-carton")]
    public async Task<IActionResult> ScanCarton([FromBody] ScanSortCartonRequest req) =>
        this.ToActionResult(await service.ScanCartonAsync(req));

    [HttpPost("close-session")]
    public async Task<IActionResult> CloseSession([FromBody] CloseSortSessionRequest req) =>
        this.ToActionResult(await service.CloseSessionAsync(req));
}
