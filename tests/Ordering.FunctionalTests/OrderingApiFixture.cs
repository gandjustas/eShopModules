extern alias webapp;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using eShop.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using FusionModules.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace eShop.Ordering.FunctionalTests;

public sealed class OrderingApiFixture : ModularWebApplicationFactory<webapp::Program>, IAsyncLifetime
{
    private readonly IHost _app;

    public IResourceBuilder<PostgresServerResource> Postgres { get; private set; }

    private string _postgresConnectionString;

    public OrderingApiFixture()
        : base(["Ordering.API"])
    {
        var options = new DistributedApplicationOptions { AssemblyName = typeof(OrderingApiFixture).Assembly.FullName, DisableDashboard = true };
        var appBuilder = DistributedApplication.CreateBuilder(options);
        Postgres = appBuilder.AddPostgres("OrderingDB");
        _app = appBuilder.Build();
    }

    // The host is WebApp: it needs its own settings, the fake bus connection is never opened
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseSetting("IdentityUrl", "http://identity.test")
            .UseSetting("CallBackUrl", "http://identity.test")
            .UseSetting("Identity:Url", "http://identity.test")
            .UseSetting("ConnectionStrings:EventBus", "amqp://127.0.0.1:1");
        base.ConfigureWebHost(builder);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { $"ConnectionStrings:{Postgres.Resource.Name}", _postgresConnectionString },
                { "Identity:Url", "http://identity.test" }
            });
        });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(new AutoAuthorizeStartupFilter());
            // Политика Bearer проверяла бы токен схемой JwtBearer; тесту хватает пользователя, которого ставит middleware
            services.PostConfigure<AuthorizationOptions>(options =>
                options.AddPolicy(AuthenticationExtensions.BearerPolicy, policy => policy.RequireAuthenticatedUser()));
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

    private class AutoAuthorizeStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                builder.UseMiddleware<AutoAuthorizeMiddleware>();
                next(builder);
            };
        }
    }
}
