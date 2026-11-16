extern alias webapp;

using System.Reflection;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using FusionModules.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShop.Catalog.FunctionalTests;

public sealed class CatalogApiFixture : ModularWebApplicationFactory<webapp::Program>, IAsyncLifetime
{
    private readonly IHost _app;

    public IResourceBuilder<PostgresServerResource> Postgres { get; private set; }
    private string _postgresConnectionString;

    public CatalogApiFixture()
        : base(["Catalog.API"])
    {
        var options = new DistributedApplicationOptions { AssemblyName = typeof(CatalogApiFixture).Assembly.FullName, DisableDashboard = true };
        var appBuilder = DistributedApplication.CreateBuilder(options);
        Postgres = appBuilder.AddPostgres("CatalogDB")
            .WithImage("ankane/pgvector")
            .WithImageTag("latest");
        _app = appBuilder.Build();
    }

    // The host is WebApp: it needs its own settings, the fake bus connection is never opened
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseSetting("IdentityUrl", "http://identity.test")
            .UseSetting("CallBackUrl", "http://identity.test")
            .UseSetting("Identity:Url", "http://identity.test")
            .UseSetting("ConnectionStrings:EventBus", "amqp://127.0.0.1:1")
            // The host has one module, so the storefront services that need the other modules cannot be built
            .UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        base.ConfigureWebHost(builder);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"ConnectionStrings:{Postgres.Resource.Name}", _postgresConnectionString },
                });
        });
        return base.CreateHost(builder);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _app.StopAsync();
        if (_app is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _app.Dispose();
        }
    }

    public async ValueTask InitializeAsync()
    {
        await _app.StartAsync();
        _postgresConnectionString = await Postgres.Resource.GetConnectionStringAsync();
    }
}
