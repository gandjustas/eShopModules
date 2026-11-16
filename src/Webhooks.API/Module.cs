using FusionModules;

[assembly: HostingStartup(typeof(Webhooks.API.Module))]

namespace Webhooks.API;

internal sealed class Module : ModuleBase
{

    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddRabbitMqEventBus("eventbus")
               .AddSubscription<ProductPriceChangedIntegrationEvent, ProductPriceChangedIntegrationEventHandler>()
               .AddSubscription<OrderStatusChangedToShippedIntegrationEvent, OrderStatusChangedToShippedIntegrationEventHandler>()
               .AddSubscription<OrderStatusChangedToPaidIntegrationEvent, OrderStatusChangedToPaidIntegrationEventHandler>();

        builder.AddNpgsqlDbContext<WebhooksContext>("webhooksdb");

        builder.Services.AddMigration<WebhooksContext>();

        builder.Services.AddTransient<IGrantUrlTesterService, GrantUrlTesterService>();
        builder.Services.AddTransient<IWebhooksRetriever, WebhooksRetriever>();
        builder.Services.AddTransient<IWebhooksSender, WebhooksSender>();
    }
    protected override void Configure(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var webHooks = endpoints.NewVersionedApi("Web Hooks");

            webHooks.MapWebHooksApiV1()
                    .RequireAuthorization(AuthenticationExtensions.BearerPolicy);
        });
    }
}
