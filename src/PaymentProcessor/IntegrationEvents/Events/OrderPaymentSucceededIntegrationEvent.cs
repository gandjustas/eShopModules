namespace eShop.PaymentProcessor.IntegrationEvents.Events;

internal record OrderPaymentSucceededIntegrationEvent(int OrderId) : IntegrationEvent;
