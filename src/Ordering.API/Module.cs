using eShop.ServiceDefaults;
using FusionModules;

[assembly: HostingStartup(typeof(eShop.Ordering.API.Module))]

namespace eShop.Ordering.API;

internal sealed class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddApplicationServices();
        builder.Services.AddSingleton<eShop.Contracts.Ordering.IOrderingService, eShop.Ordering.API.Apis.LocalOrderingService>();
    }

    protected override void Configure(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var orders = endpoints.NewVersionedApi("Orders");

            orders.MapOrdersApiV1()
                  .RequireAuthorization(AuthenticationExtensions.BearerPolicy);
        });
    }
}
