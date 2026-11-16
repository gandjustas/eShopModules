namespace eShop.Catalog.API.IntegrationEvents.Events;

internal record OrderStockConfirmedIntegrationEvent(int OrderId) : IntegrationEvent;
