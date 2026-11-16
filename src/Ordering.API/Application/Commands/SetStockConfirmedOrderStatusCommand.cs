namespace eShop.Ordering.API.Application.Commands;

internal record SetStockConfirmedOrderStatusCommand(int OrderNumber) : IRequest<bool>;
