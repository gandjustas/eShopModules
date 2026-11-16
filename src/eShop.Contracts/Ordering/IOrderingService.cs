namespace eShop.Contracts.Ordering;

// The orders of a buyer. The buyer is an argument: over the network the token said who it was,
// in the process the storefront already knows
public interface IOrderingService
{
    Task<OrderRecord[]> GetOrdersAsync(string buyerId);
    Task CreateOrderAsync(CreateOrderRequest request, Guid requestId);
}

public record OrderRecord(int OrderNumber, DateTime Date, string Status, decimal Total);

public record CreateOrderRequest(
    string UserId,
    string UserName,
    string City,
    string Street,
    string State,
    string Country,
    string ZipCode,
    string CardNumber,
    string CardHolderName,
    DateTime CardExpiration,
    string CardSecurityNumber,
    int CardTypeId,
    string Buyer,
    IReadOnlyList<OrderItem> Items);

public record OrderItem(int ProductId, string ProductName, decimal UnitPrice, int Quantity);
