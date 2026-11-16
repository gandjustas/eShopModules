extern alias webapp;

using FusionModules.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Topology.Tests;

/// <summary>
/// Все модули в одном процессе: то, чего не видит ни одна топология из одного модуля, -
/// сталкиваются ли они друг с другом.
/// </summary>
public class HostTests
{
    static readonly string[] AllModules =
    [
        "Basket.API", "Catalog.API", "Identity.API", "Ordering.API",
        "Webhooks.API", "OrderProcessor", "PaymentProcessor",
    ];

    static ITopologyHost StartAll() =>
        TopologyHost.Start(new("All", typeof(webapp::Program), AllModules));

    [Fact]
    public async Task AllModules_StartTogether()
    {
        await using var host = StartAll();

        // Хост собирается при первом обращении: здесь падают столкновения регистраций и маршрутов
        Assert.NotNull(host.Services);
    }

    [Fact]
    public async Task AllModules_ServeNoRouteTwice()
    {
        await using var host = StartAll();

        // Одинаковые метод и маршрут у двух модулей дают AmbiguousMatchException на запрос
        var twice = host.Services.Routes()
            .GroupBy(route => $"{route.Method} {route.Pattern}")
            .Where(group => group.Select(route => route.Module).Distinct().Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(route => route.Module ?? "-"))}")
            .ToArray();

        Assert.True(twice.Length == 0, "Маршруты двух модулей совпадают:" + Environment.NewLine + string.Join(Environment.NewLine, twice));
    }

    [Fact]
    public async Task AllModules_ServeTheRoutesOfEveryService()
    {
        await using var host = StartAll();
        var served = TopologyProbe.Routes(host);

        var lost = Topologies.Current
            .Where(topology => topology.Modules.Length > 0)
            .SelectMany(topology => Baseline.Services[topology.Name].Routes
                .Select(route => (topology.Name, Route: Prefixed(route, topology.RoutePrefix))))
            .Where(item => !served.Contains(item.Route))
            .Select(item => $"{item.Name}: {item.Route}")
            .ToArray();

        Assert.True(lost.Length == 0, "Рядом с другими модулями пропали маршруты:" + Environment.NewLine + string.Join(Environment.NewLine, lost));
    }

    // AddIdentity в модуле Identity ставит свои схемы по умолчанию; витрина читала бы пользователя из cookie Identity
    [Fact]
    public async Task Storefront_KeepsItsDefaultSchemes()
    {
        await using var host = StartAll();
        var schemes = host.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.Equal("Cookies", (await schemes.GetDefaultAuthenticateSchemeAsync())?.Name);
        Assert.Equal("OpenIdConnect", (await schemes.GetDefaultChallengeSchemeAsync())?.Name);
        Assert.Equal("Cookies", (await schemes.GetDefaultSignInSchemeAsync())?.Name);
    }

    // AddAspNetIdentity тоже регистрирует свой IProfileService; побеждает последняя регистрация, и она должна быть своей:
    // чужая отдаёт в userinfo только имя, а витрина берёт адрес доставки из claims профиля
    [Fact]
    public async Task Identity_AnswersWithItsOwnProfileService()
    {
        await using var host = TopologyHost.Start(new("Identity", typeof(webapp::Program), ["Identity.API"]));
        var contract = Type.GetType("Duende.IdentityServer.Services.IProfileService, Duende.IdentityServer", throwOnError: true)!;

        await using var scope = host.Services.CreateAsyncScope();
        var profile = scope.ServiceProvider.GetRequiredService(contract);

        Assert.Equal("Identity.API", profile.GetType().Assembly.GetName().Name);
    }

    static string Prefixed(string route, string prefix)
    {
        if (prefix.Length == 0)
            return route;

        var space = route.IndexOf(' ');
        return $"{route[..space]} {prefix.TrimEnd('/')}{route[(space + 1)..]}";
    }
}
