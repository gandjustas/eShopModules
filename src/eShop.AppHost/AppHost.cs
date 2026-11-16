using System.IO.Pipes;
using eShop.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddForwardedHeaders();
builder.AddAzureContainerAppEnvironment("aca");

var redis = builder.AddRedis("redis");
var rabbitMq = builder.AddRabbitMQ("eventbus")
    .WithLifetime(ContainerLifetime.Persistent);
var postgres = builder.AddPostgres("postgres")
    .WithImage("ankane/pgvector")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = postgres.AddDatabase("catalogdb");
var identityDb = postgres.AddDatabase("identitydb");
var orderDb = postgres.AddDatabase("orderingdb");
var webhooksDb = postgres.AddDatabase("webhooksdb");

var launchProfileName = ShouldUseHttpForEndpoints() ? "http" : "https";

// Сервисы - модули хоста webapp: один процесс, а состав задаёт HOSTINGSTARTUPASSEMBLIES.
// Порядок важен: Identity первым, а миграции Ordering идут до воркера OrderProcessor, который читает их таблицы.
string[] modules =
[
    "Identity.API", "Catalog.API", "Basket.API", "Ordering.API",
    "Webhooks.API", "OrderProcessor", "PaymentProcessor",
];

var webApp = builder.AddDotnetProject("webapp", "../WebApp", o => o.LaunchProfileName = launchProfileName)
    .WithExternalHttpEndpoints()
    .WithUrls(c => c.Urls.ForEach(u => u.DisplayText = $"Online Store ({u.Endpoint?.EndpointName})"))
    .WithReference(redis)
    .WithReference(rabbitMq).WaitFor(rabbitMq)
    .WithReference(catalogDb)
    .WithReference(identityDb)
    .WithReference(orderDb).WaitFor(orderDb)
    .WithReference(webhooksDb)
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", string.Join(';', modules));

redis.WithParentRelationship(webApp);

var webAppEndpoint = webApp.GetEndpoint(launchProfileName);

// Identity, сервисы и сама витрина живут по одному адресу
webApp.WithEnvironment("IdentityUrl", webAppEndpoint)
      .WithEnvironment("Identity__Url", webAppEndpoint)
      .WithEnvironment("CallBackUrl", webAppEndpoint);

// Вызовы между частями идут по прежним именам сервисов, но на адрес самого хоста
foreach (var service in new[] { "basket-api", "catalog-api", "ordering-api", "webhooks-api" })
{
    webApp.WithEnvironment($"services__{service}__{launchProfileName}__0", webAppEndpoint);
}

// gRPC корзины идёт по HTTP/2 и поддерживается только на HTTPS-эндпоинте. Под профилем http (ESHOP_USE_HTTP_ENDPOINTS=1)
// HTTPS-эндпоинта у хоста нет, и корзина через gRPC недоступна
if (launchProfileName == "https")
{
    webApp.WithEnvironment("services__basket-api__https__0", webAppEndpoint);
}

// Reverse proxies
builder.AddYarp("mobile-bff")
    .WithExternalHttpEndpoints()
    .ConfigureMobileBffRoutes(webApp, webApp, webApp);

// Apps
var webhooksClient = builder.AddDotnetProject("webhooksclient", "../WebhookClient", o => o.LaunchProfileName = launchProfileName)
    .WithEnvironment($"services__webhooks-api__{launchProfileName}__0", webAppEndpoint)
    .WithEnvironment("IdentityUrl", webAppEndpoint);

// Set UseFoundry=true to provision Microsoft Foundry for chat and embeddings.
bool useFoundry = Extensions.IsFoundryEnabled(builder.Configuration);
if (useFoundry)
{
    builder.AddFoundry(webApp, webApp);
}

bool useOllama = false;
if (useOllama)
{
    builder.AddOllama(webApp, webApp);
}

// Wire up the callback urls (self referencing)
webhooksClient.WithEnvironment("CallBackUrl", webhooksClient.GetEndpoint(launchProfileName));

// Identity has a reference to all of the apps for callback urls
webApp.WithEnvironment("BasketApiClient", webAppEndpoint)
      .WithEnvironment("OrderingApiClient", webAppEndpoint)
      .WithEnvironment("WebhooksApiClient", webAppEndpoint)
      .WithEnvironment("WebhooksWebClient", webhooksClient.GetEndpoint(launchProfileName))
      .WithEnvironment("WebAppClient", webAppEndpoint);

builder.Build().Run();

// For test use only.
// Looks for an environment variable that forces the use of HTTP for all the endpoints. We
// are doing this for ease of running the Playwright tests in CI.
static bool ShouldUseHttpForEndpoints()
{
    const string EnvVarName = "ESHOP_USE_HTTP_ENDPOINTS";
    var envValue = Environment.GetEnvironmentVariable(EnvVarName);

    // Attempt to parse the environment variable value; return true if it's exactly "1".
    return int.TryParse(envValue, out int result) && result == 1;
}
