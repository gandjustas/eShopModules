namespace eShop.Ordering.API.Application.Commands;

internal record SetStockRejectedOrderStatusCommand(int OrderNumber, List<int> OrderStockItems) : IRequest<bool>;
