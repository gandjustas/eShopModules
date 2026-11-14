using FusionModules;

[assembly: HostingStartup(typeof(PaymentProcessor.Module))]

namespace PaymentProcessor;

internal sealed class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddRabbitMqEventBus("EventBus")
            .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();
        builder.Services.AddOptions<PaymentOptions>()
            .BindConfiguration(nameof(PaymentOptions));
    }
}
