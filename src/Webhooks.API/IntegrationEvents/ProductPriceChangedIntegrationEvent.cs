namespace Webhooks.API.IntegrationEvents;

internal record ProductPriceChangedIntegrationEvent(int ProductId, decimal NewPrice, decimal OldPrice) : IntegrationEvent;
