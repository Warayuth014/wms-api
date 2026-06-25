using WmsApi.DTOs;
using WmsApi.Services.Common;

namespace WmsApi.Services.Basket;

public interface IBasketService
{
    Task<ServiceResult> GetUnloadedItemsAsync();
    Task<ServiceResult> LoadToBasketAsync(LoadToBasketRequest req);
}
