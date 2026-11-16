using eShop.Contracts.Ordering;
using Contract = eShop.Contracts.Ordering;

namespace eShop.Ordering.API.Apis;

// Orders for the storefront in the same process: the same method that answers POST /api/orders
// (card number mask, idempotency by requestId) and the same query for the list, without the network.
// A scope per call, like an HTTP request
internal sealed class LocalOrderingService(IServiceScopeFactory scopes) : IOrderingService
{
    public async Task<OrderRecord[]> GetOrdersAsync(string buyerId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var orders = await scope.ServiceProvider.GetRequiredService<IOrderQueries>().GetOrdersFromUserAsync(buyerId);
        return [.. orders.Select(order => new OrderRecord(order.OrderNumber, order.Date, order.Status, (decimal)order.Total))];
    }

    public async Task CreateOrderAsync(Contract.CreateOrderRequest request, Guid requestId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = ActivatorUtilities.CreateInstance<OrderServices>(scope.ServiceProvider);

        await OrdersApi.CreateOrderAsync(requestId, new CreateOrderRequest(
            request.UserId, request.UserName, request.City, request.Street, request.State, request.Country,
            request.ZipCode, request.CardNumber, request.CardHolderName, request.CardExpiration,
            request.CardSecurityNumber, request.CardTypeId, request.Buyer,
            [.. request.Items.Select(item => new BasketItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
            })]), services);
    }
}
