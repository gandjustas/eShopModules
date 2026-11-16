using eShop.ServiceDefaults;
using FusionModules;

[assembly: HostingStartup(typeof(eShop.Ordering.API.Module))]

namespace eShop.Ordering.API;

internal sealed class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddApplicationServices();
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
