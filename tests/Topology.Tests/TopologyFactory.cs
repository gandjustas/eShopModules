using FusionModules.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eShop.Topology.Tests;

/// <summary>Поднятый хост топологии и то, что в нём зарегистрировано до удаления фоновых служб.</summary>
public interface ITopologyHost : IAsyncDisposable
{
    IServiceProvider Services { get; }

    /// <summary>Регистрации контейнера в том виде, в каком их оставили приложение и модули.</summary>
    IReadOnlyList<ServiceDescriptor> Descriptors { get; }
}

/// <summary>
/// Хост топологии без инфраструктуры: строки подключения разбираются, но никуда не ведут,
/// фоновые службы не запускаются. Тест спрашивает, что собралось, а не что подключилось.
/// </summary>
public sealed class TopologyFactory<TProgram>(string[] modules)
    : ModularWebApplicationFactory<TProgram>(modules), ITopologyHost
    where TProgram : class
{
    const string Postgres = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused";
    const string Redis = "127.0.0.1:1,abortConnect=false,connectTimeout=100";
    const string RabbitMq = "amqp://127.0.0.1:1";

    IReadOnlyList<ServiceDescriptor> descriptors = [];

    // Регистрации запоминаются при сборке хоста, а хост собирается при первом обращении к Services
    public IReadOnlyList<ServiceDescriptor> Descriptors
    {
        get
        {
            _ = Services;
            return descriptors;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseConnectionStrings(
                ("redis", Redis),
                // PaymentProcessor и WebApp называют шину с заглавной буквы, остальные со строчной
                ("eventbus", RabbitMq),
                ("EventBus", RabbitMq),
                ("catalogdb", Postgres),
                ("identitydb", Postgres),
                ("orderingdb", Postgres),
                ("webhooksdb", Postgres))
            // Витрина и клиент вебхуков не стартуют без адреса Identity и собственного адреса
            .UseSetting("IdentityUrl", "http://localhost")
            .UseSetting("CallBackUrl", "http://localhost")
            .UseSetting("Identity:Url", "http://localhost")
            // Раздел Identity есть - значит, OpenAPI ждёт в нём области; у сервисов без него (каталог) их задаёт эта строка
            .UseSetting("Identity:Scopes:test", "test")
            // Как в развёртывании: в Development хост подключает ещё и Scalar и служебные маршруты
            .UseEnvironment("Production")
            // Сначала запомнить регистрации, потом убрать фоновые службы: миграции и воркеры
            // без инфраструктуры падают или зависают для всех топологий сразу
            .ConfigureTestServices(services => descriptors = [.. services])
            .WithoutApplicationHostedServices();
        base.ConfigureWebHost(builder);
    }
}

public static class TopologyHost
{
    /// <summary>Хост топологии: тип Program берётся из таблицы, поэтому фабрика собирается через отражение.</summary>
    public static ITopologyHost Start(Topology topology) =>
        (ITopologyHost)Activator.CreateInstance(
            typeof(TopologyFactory<>).MakeGenericType(topology.Program), [topology.Modules])!;
}
