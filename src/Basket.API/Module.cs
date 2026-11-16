using System.Text.Json.Serialization;
using eShop.Basket.API.IntegrationEvents.EventHandling;
using eShop.Basket.API.IntegrationEvents.EventHandling.Events;
using eShop.Basket.API.Repositories;
using eShop.ServiceDefaults;
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

        // The storefront in the same process uses the basket through the contract, not through gRPC
        builder.Services.AddSingleton<eShop.Contracts.Basket.IBasketService, LocalBasketService>();
    }

    protected override void Configure(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            // Токен проверяет политика Bearer: в общем хосте схема по умолчанию - cookie витрины.
            // GetBasket помечен AllowAnonymous, пользователя для него тоже ставит эта политика
            endpoints.MapGrpcService<BasketService>()
                     .RequireAuthorization(AuthenticationExtensions.BearerPolicy);
        });
    }
}

[JsonSerializable(typeof(OrderStartedIntegrationEvent))]
partial class IntegrationEventContext : JsonSerializerContext
{

}
