namespace Webhooks.API.IntegrationEvents;

internal record OrderStatusChangedToShippedIntegrationEvent(int OrderId, string OrderStatus, string BuyerName) : IntegrationEvent;
