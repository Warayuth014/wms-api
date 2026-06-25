using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Common;
using WmsApi.Services.Unload;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/unload")]
public class UnloadController(IUnloadService service) : ControllerBase
{
    [HttpPost("open-session")]
    public async Task<IActionResult> OpenSession([FromBody] OpenUnloadRequest req) =>
        this.ToActionResult(await service.OpenSessionAsync(req));

    [HttpPost("confirm-unload")]
    public async Task<IActionResult> ConfirmUnload([FromBody] ConfirmUnloadRequest req) =>
        this.ToActionResult(await service.ConfirmUnloadAsync(req));

    [HttpPost("return-pallet-to-asis")]
    public async Task<IActionResult> ReturnPalletToAsis([FromBody] ReturnPalletToAsisRequest req) =>
        this.ToActionResult(await service.ReturnPalletToAsisAsync(req));
}
