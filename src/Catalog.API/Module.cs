using FusionModules;
using Microsoft.Extensions.FileProviders;

[assembly: HostingStartup(typeof(Catalog.API.Module))]

namespace Catalog.API;

internal sealed class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.AddApplicationServices();

        // Картинки лежат рядом со сборкой модуля: корень содержимого в общем хосте - папка хоста
        builder.Services.AddKeyedSingleton<IFileProvider>("pics", (_, _) =>
            new PhysicalFileProvider(Path.Combine(CatalogFiles.Root, "Pics")));
    }

    protected override void Configure(IApplicationBuilder app)
    {
        // Только API отвечает ProblemDetails на 404 и другие ответы без тела; страницы витрины - нет
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), api => api.UseStatusCodePages());

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapCatalogApi();
        });
    }
}
