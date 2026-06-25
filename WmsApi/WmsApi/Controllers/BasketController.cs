using Microsoft.AspNetCore.Mvc;
using WmsApi.DTOs;
using WmsApi.Services.Basket;
using WmsApi.Services.Common;

namespace WmsApi.Controllers;

[ApiController]
[Route("api/basket")]
public class BasketController(IBasketService service) : ControllerBase
{
    [HttpGet("unloaded-items")]
    public async Task<IActionResult> GetUnloadedItems() =>
        this.ToActionResult(await service.GetUnloadedItemsAsync());

    [HttpPost("load")]
    public async Task<IActionResult> LoadToBasket([FromBody] LoadToBasketRequest req) =>
        this.ToActionResult(await service.LoadToBasketAsync(req));
}
