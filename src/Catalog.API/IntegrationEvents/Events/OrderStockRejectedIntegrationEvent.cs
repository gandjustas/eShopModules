namespace eShop.Catalog.API.IntegrationEvents.Events;

internal record OrderStockRejectedIntegrationEvent(int OrderId, List<ConfirmedOrderStockItem> OrderStockItems) : IntegrationEvent;
