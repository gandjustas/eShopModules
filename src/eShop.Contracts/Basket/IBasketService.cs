namespace eShop.Contracts.Basket;

// The basket of a buyer. The buyer is an argument: over the network the token said who it was,
// in the process the storefront already knows
public interface IBasketService
{
    Task<IReadOnlyCollection<BasketQuantity>> GetBasketAsync(string buyerId);
    Task UpdateBasketAsync(string buyerId, IReadOnlyCollection<BasketQuantity> items);
    Task DeleteBasketAsync(string buyerId);
}

public record BasketQuantity(int ProductId, int Quantity);
