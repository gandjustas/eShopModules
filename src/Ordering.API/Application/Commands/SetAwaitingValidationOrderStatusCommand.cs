namespace eShop.Ordering.API.Application.Commands;

internal record SetAwaitingValidationOrderStatusCommand(int OrderNumber) : IRequest<bool>;
