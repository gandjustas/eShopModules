namespace eShop.Catalog.API.IntegrationEvents.Events;

internal record ConfirmedOrderStockItem(int ProductId, bool HasStock);
