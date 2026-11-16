namespace eShop.Ordering.API.Application.Commands;

internal record ShipOrderCommand(int OrderNumber) : IRequest<bool>;
