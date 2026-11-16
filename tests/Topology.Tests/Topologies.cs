extern alias basket;
extern alias catalog;
extern alias identity;
extern alias ordering;
extern alias orderprocessor;
extern alias payment;
extern alias webapp;
extern alias webhookclient;
extern alias webhooks;

namespace eShop.Topology.Tests;

/// <summary>
/// Топология: какой хост поднимает тест, какие модули он загружает и под каким префиксом
/// отдаёт маршруты. Это то же, что решает развёртывание: образ, HOSTINGSTARTUPASSEMBLIES и путь.
/// </summary>
/// <param name="Name">Сервис до переезда; по нему берётся замороженный <see cref="Baseline"/>.</param>
/// <param name="Program">Тип Program хоста: из его сборки поднимается приложение.</param>
/// <param name="Modules">Имена сборок модулей, как в HOSTINGSTARTUPASSEMBLIES. Пусто: хост один.</param>
/// <param name="RoutePrefix">Префикс, под которым хост отдаёт маршруты сервиса; пусто, если без префикса.</param>
public sealed record Topology(string Name, Type Program, string[] Modules, string RoutePrefix = "");

/// <summary>
/// Единственный файл, который правится при переезде сервиса в модуль: тип хоста, модули и префикс.
/// Ожидания (<see cref="Baseline"/>) и тесты не меняются, поэтому красный тест значит, что модуль
/// после переезда отдаёт не то же, что сервис до него.
/// </summary>
public static class Topologies
{
    // Сегодня каждый сервис сам себе хост. Его модуль - сборка самого хоста: [HostingStartup]
    // в ней ASP.NET Core применяет сам, поэтому список модулей пуст.
    public static readonly Topology[] Current =
    [
        new("Basket.API", typeof(webapp::Program), ["Basket.API"]),
        new("Catalog.API", typeof(webapp::Program), ["Catalog.API"]),
        new("Identity.API", typeof(identity::Program), []),
        new("Ordering.API", typeof(webapp::Program), ["Ordering.API"]),
        new("OrderProcessor", typeof(webapp::Program), ["OrderProcessor"]),
        new("PaymentProcessor", typeof(webapp::Program), ["PaymentProcessor"]),
        new("WebApp", typeof(webapp::Program), []),
        new("WebhookClient", typeof(webhookclient::Program), []),
        new("Webhooks.API", typeof(webapp::Program), ["Webhooks.API"]),
    ];
}
