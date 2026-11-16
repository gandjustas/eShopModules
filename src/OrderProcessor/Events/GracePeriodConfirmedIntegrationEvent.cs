namespace eShop.OrderProcessor.Events
{
    using eShop.EventBus.Events;

    internal record GracePeriodConfirmedIntegrationEvent : IntegrationEvent
    {
        public int OrderId { get; }

        public GracePeriodConfirmedIntegrationEvent(int orderId) =>
            OrderId = orderId;
    }
}
