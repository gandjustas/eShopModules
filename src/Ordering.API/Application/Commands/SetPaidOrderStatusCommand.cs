namespace eShop.Ordering.API.Application.Commands;

internal record SetPaidOrderStatusCommand(int OrderNumber) : IRequest<bool>;
