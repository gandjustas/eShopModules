namespace eShop.Catalog.API.IntegrationEvents.Events;

internal record OrderStatusChangedToAwaitingValidationIntegrationEvent(int OrderId, IEnumerable<OrderStockItem> OrderStockItems) : IntegrationEvent;
