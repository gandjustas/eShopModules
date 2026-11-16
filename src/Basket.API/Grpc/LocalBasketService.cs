using eShop.Basket.API.Model;
using eShop.Basket.API.Repositories;
using eShop.Contracts.Basket;

namespace eShop.Basket.API.Grpc;

// The basket for the storefront in the same process: the same store the gRPC service uses, without the network
internal sealed class LocalBasketService(IBasketRepository repository) : IBasketService
{
    public async Task<IReadOnlyCollection<BasketQuantity>> GetBasketAsync(string buyerId)
        => await repository.GetBasketAsync(buyerId) is { } basket
            ? [.. basket.Items.Select(item => new BasketQuantity(item.ProductId, item.Quantity))]
            : [];

    public async Task UpdateBasketAsync(string buyerId, IReadOnlyCollection<BasketQuantity> items)
    {
        var basket = new CustomerBasket { BuyerId = buyerId };
        basket.Items.AddRange(items.Select(item => new Model.BasketItem { ProductId = item.ProductId, Quantity = item.Quantity }));

        _ = await repository.UpdateBasketAsync(basket)
            ?? throw new InvalidOperationException($"Basket with buyer id {buyerId} does not exist");
    }

    public Task DeleteBasketAsync(string buyerId) => repository.DeleteBasketAsync(buyerId);
}
