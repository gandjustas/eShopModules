using System.Text.Json.Serialization;
using eShop.Basket.API.IntegrationEvents.EventHandling;
using eShop.Basket.API.IntegrationEvents.EventHandling.Events;
using eShop.Basket.API.Repositories;
using FusionModules;

[assembly: HostingStartup(typeof(Basket.API.Module))]

namespace Basket.API;

internal sealed class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddRedisClient("redis");
        builder.Services.AddSingleton<IBasketRepository, RedisBasketRepository>();

        builder.AddRabbitMqEventBus("eventbus")
               .AddSubscription<OrderStartedIntegrationEvent, OrderStartedIntegrationEventHandler>()
               .ConfigureJsonOptions(options => options.TypeInfoResolverChain.Add(IntegrationEventContext.Default));

        builder.Services.AddGrpc();

    }
    protected override void Configure(IApplicationBuilder app)
    {

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<BasketService>();
        });
    }
}

[JsonSerializable(typeof(OrderStartedIntegrationEvent))]
partial class IntegrationEventContext : JsonSerializerContext
{

}
