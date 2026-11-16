using Microsoft.AspNetCore.Components.Authorization;

namespace eShop.WebApp.Services;

// The orders of the signed-in buyer. It used to be an HTTP client of Ordering.API with the user's token
public class OrderingService(IOrderingService ordering, AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<OrderRecord[]> GetOrders()
        => await authenticationStateProvider.GetBuyerIdAsync() is { } buyerId
            ? await ordering.GetOrdersAsync(buyerId)
            : [];

    public Task CreateOrder(CreateOrderRequest request, Guid requestId)
        => ordering.CreateOrderAsync(request, requestId);
}
