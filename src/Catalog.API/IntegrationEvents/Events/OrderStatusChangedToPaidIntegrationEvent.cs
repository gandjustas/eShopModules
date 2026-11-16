namespace eShop.Catalog.API.IntegrationEvents.Events;

internal record OrderStatusChangedToPaidIntegrationEvent(int OrderId, IEnumerable<OrderStockItem> OrderStockItems) : IntegrationEvent;
