using eShop.EventBus.Abstractions;
using FusionModules.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace eShop.Topology.Tests;

/// <summary>
/// Читает из поднятого хоста те же срезы, что записаны в <see cref="Baseline"/>, в том же виде.
/// Формат строк - часть замороженных ожиданий: менять его значит менять и их.
/// </summary>
public static class TopologyProbe
{
    // Сборки самих сервисов. Общие библиотеки (EventBus, ServiceDefaults, журнал событий) и сборки, на которые
    // сервис опирается (Ordering.Domain, Ordering.Infrastructure, WebAppComponents), сюда не входят:
    // их регистрации ничего не говорят о том, что отдаёт именно этот сервис
    static readonly HashSet<string> ServiceAssemblies = new(StringComparer.Ordinal)
    {
        "Basket.API", "Catalog.API", "Identity.API", "Ordering.API",
        "OrderProcessor", "PaymentProcessor", "WebApp", "WebhookClient", "Webhooks.API",
    };

    public static string[] Routes(ITopologyHost host) =>
    [
        .. host.Services.Routes()
            .Select(route => $"{(route.Method == ModuleRoute.AnyMethod ? "*" : route.Method)} {route.Pattern}")
            .Distinct()
            .Order(StringComparer.Ordinal)
    ];

    public static string[] Events(ITopologyHost host) =>
    [
        .. (host.Services.GetService<IOptions<EventBusSubscriptionInfo>>()?.Value.EventTypes.Keys ?? (IEnumerable<string>)[])
            .Order(StringComparer.Ordinal)
    ];

    public static string[] Hosted(ITopologyHost host)
    {
        var hosted = host.Descriptors
            .Where(d => d.ServiceType == typeof(IHostedService) && !d.IsKeyedService)
            .ToArray();

        var types = hosted
            .Select(d => d.ImplementationType)
            .OfType<Type>()
            .Where(IsOwned)
            .Select(Name);
        var factories = hosted.Count(d => d.ImplementationFactory is not null);

        return [.. types.Concat(factories > 0 ? [$"factory x{factories}"] : []).Order(StringComparer.Ordinal)];
    }

    public static string[] Services(ITopologyHost host) =>
    [
        .. host.Descriptors
            .Where(d => IsOwned(d.ServiceType) || IsOwned(Implementation(d)))
            .Select(Describe)
            .Distinct()
            .Order(StringComparer.Ordinal)
    ];

    public static string[] Tables(ITopologyHost host)
    {
        using var scope = host.Services.CreateScope();
        var contexts = host.Descriptors
            .Select(d => d.ServiceType)
            .Where(type => typeof(DbContext).IsAssignableFrom(type) && type != typeof(DbContext))
            .Distinct();

        return
        [
            .. contexts
                .SelectMany(type => ((DbContext)scope.ServiceProvider.GetRequiredService(type)).Model.GetEntityTypes())
                .Where(entity => entity.GetTableName() is not null)
                .Select(entity => entity.GetSchema() is { } schema
                    ? $"{schema}.{entity.GetTableName()}"
                    : entity.GetTableName()!)
                .Distinct()
                .Order(StringComparer.Ordinal)
        ];
    }

    static Type? Implementation(ServiceDescriptor d) =>
        d.IsKeyedService ? d.KeyedImplementationType : d.ImplementationType;

    static string Describe(ServiceDescriptor d)
    {
        var key = d.IsKeyedService ? $"[{(d.ServiceKey is Type type ? Name(type) : d.ServiceKey)}] " : "";
        var implementation = Implementation(d) is { } impl ? Name(impl) : "factory/instance";
        return $"{d.Lifetime} {key}{Name(d.ServiceType)} -> {implementation}";
    }

    static bool IsOwned(Type? type) => type is not null && ServiceAssemblies.Contains(type.Assembly.GetName().Name!);

    // Вложенный тип пишется через точку, как в самом коде
    static string Name(Type type) => type.ToString().Replace('+', '.');
}
