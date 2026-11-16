namespace eShop.PaymentProcessor.IntegrationEvents.Events;

internal record OrderPaymentFailedIntegrationEvent(int OrderId) : IntegrationEvent;
