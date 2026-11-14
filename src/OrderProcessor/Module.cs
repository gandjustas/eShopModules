using FusionModules;
using Microsoft.AspNetCore.Hosting;
using eShop.OrderProcessor;

[assembly: HostingStartup(typeof(OrderProcessor.Module))]

namespace OrderProcessor;

internal sealed class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddRabbitMqEventBus("eventbus")
               .ConfigureJsonOptions(options => options.TypeInfoResolverChain.Add(IntegrationEventContext.Default));

        builder.AddNpgsqlDataSource("orderingdb");

        builder.Services.AddOptions<BackgroundTaskOptions>()
            .BindConfiguration(nameof(BackgroundTaskOptions));

        builder.Services.AddSingleton<IGracePeriodOrdersRepository, GracePeriodOrdersRepository>();
        builder.Services.AddHostedService<GracePeriodManagerService>();
    }
}
