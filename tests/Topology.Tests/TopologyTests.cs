global using Xunit;

namespace eShop.Topology.Tests;

/// <summary>
/// Каждая топология отдаёт то же, что сервис до переезда. Ожидания заморожены в <see cref="Baseline"/>,
/// при переезде меняется только <see cref="Topologies"/>.
/// Сервис сам себе хост (модулей нет): отдаёт ровно то, что в ожиданиях. Сервис - модуль в чужом хосте:
/// хост вносит и своё, поэтому всё, что сервис отдавал раньше, обязано остаться, а лишнее - дело хоста.
/// </summary>
public class TopologyTests
{
    public static TheoryData<string> Names => new(Topologies.Current.Select(t => t.Name));

    [Fact]
    public void EveryService_HasATopology_AndABaseline()
    {
        string[] topologies = [.. Topologies.Current.Select(t => t.Name).Order(StringComparer.Ordinal)];
        string[] baselines = [.. Baseline.Services.Keys.Order(StringComparer.Ordinal)];

        Assert.Equal(baselines, topologies);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Serves_the_routes_of_the_service(string name)
    {
        var topology = Find(name);
        await using var host = TopologyHost.Start(topology);

        Check(
            topology,
            Baseline.Services[name].Routes.Select(route => Prefixed(route, topology.RoutePrefix)),
            TopologyProbe.Routes(host));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Subscribes_to_the_events_of_the_service(string name)
    {
        var topology = Find(name);
        await using var host = TopologyHost.Start(topology);

        Check(topology, Baseline.Services[name].Events, TopologyProbe.Events(host));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Runs_the_background_services_of_the_service(string name)
    {
        var topology = Find(name);
        await using var host = TopologyHost.Start(topology);

        Check(topology, Baseline.Services[name].Hosted, TopologyProbe.Hosted(host));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Registers_the_services_of_the_service(string name)
    {
        var topology = Find(name);
        await using var host = TopologyHost.Start(topology);

        Check(topology, Baseline.Services[name].Services, TopologyProbe.Services(host));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Owns_the_tables_of_the_service(string name)
    {
        var topology = Find(name);
        await using var host = TopologyHost.Start(topology);

        Check(topology, Baseline.Services[name].Tables, TopologyProbe.Tables(host));
    }

    static void Check(Topology topology, IEnumerable<string> expected, string[] actual)
    {
        if (topology.Modules.Length == 0)
        {
            Assert.Equal(Sorted(expected), actual);
            return;
        }

        var lost = Sorted(expected).Where(line => !Contains(actual, line)).ToArray();
        Assert.True(lost.Length == 0, "Сервис перестал отдавать:" + Environment.NewLine + string.Join(Environment.NewLine, lost));
    }

    // "factory x2": фоновых служб, созданных фабрикой, не меньше двух - хост может добавить свои
    static bool Contains(string[] actual, string line)
    {
        if (!line.StartsWith("factory x", StringComparison.Ordinal))
            return actual.Contains(line);

        var wanted = int.Parse(line["factory x".Length..]);
        return actual.Any(a => a.StartsWith("factory x", StringComparison.Ordinal)
            && int.Parse(a["factory x".Length..]) >= wanted);
    }

    static Topology Find(string name) => Topologies.Current.Single(t => t.Name == name);

    static string[] Sorted(IEnumerable<string> lines) => [.. lines.Order(StringComparer.Ordinal)];

    // "GET /items" -> "GET /basket/items"; метод и звёздочка остаются на месте
    static string Prefixed(string route, string prefix)
    {
        if (prefix.Length == 0)
            return route;

        var space = route.IndexOf(' ');
        return $"{route[..space]} {prefix.TrimEnd('/')}{route[(space + 1)..]}";
    }
}
