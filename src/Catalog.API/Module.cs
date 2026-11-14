using eShop.ServiceDefaults;
using FusionModules;
using Microsoft.Extensions.FileProviders;

[assembly: HostingStartup(typeof(Catalog.API.Module))]

namespace Catalog.API;

internal sealed class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddApplicationServices();
        builder.Services.AddKeyedSingleton<IFileProvider>("pics", (services, _) =>
            new PhysicalFileProvider(Path.Combine(
                services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "Pics")));
    }
    protected override void Configure(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapCatalogApi();
        });

        app.UseStatusCodePages();
    }
}
