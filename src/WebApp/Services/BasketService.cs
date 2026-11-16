using Microsoft.AspNetCore.Components.Authorization;

namespace eShop.WebApp.Services;

// The basket of the signed-in buyer. It used to be a gRPC client of Basket.API with the user's token;
// now the buyer comes from the storefront's authentication state and the basket from the module in the process
public class BasketService(IBasketService basket, AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<IReadOnlyCollection<BasketQuantity>> GetBasketAsync()
        => await authenticationStateProvider.GetBuyerIdAsync() is { } buyerId
            ? await basket.GetBasketAsync(buyerId)
            : [];

    public async Task DeleteBasketAsync()
        => await basket.DeleteBasketAsync(await BuyerIdAsync());

    public async Task UpdateBasketAsync(IReadOnlyCollection<BasketQuantity> items)
        => await basket.UpdateBasketAsync(await BuyerIdAsync(), items);

    private async Task<string> BuyerIdAsync()
        => await authenticationStateProvider.GetBuyerIdAsync()
           ?? throw new InvalidOperationException("The caller is not authenticated.");
}
