namespace eShop.Ordering.API.Application.Commands;

internal record CancelOrderCommand(int OrderNumber) : IRequest<bool>;

