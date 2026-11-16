namespace eShop.PaymentProcessor.IntegrationEvents.Events;

internal record OrderStatusChangedToStockConfirmedIntegrationEvent(int OrderId) : IntegrationEvent;
