using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace eShop.EventBus.Abstractions;

public class EventBusSubscriptionInfo
{
    // Имя события - набор типов: событие с одним именем может быть объявлено в нескольких сервисах,
    // и когда они живут в одном процессе, каждый получает его в своём типе
    public Dictionary<string, HashSet<Type>> EventTypes { get; } = [];

    public JsonSerializerOptions JsonSerializerOptions { get; } = new(DefaultSerializerOptions);

    internal static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault ? CreateDefaultTypeResolver() : JsonTypeInfoResolver.Combine()
    };

#pragma warning disable IL2026
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
    private static IJsonTypeInfoResolver CreateDefaultTypeResolver()
        => new DefaultJsonTypeInfoResolver();
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026
}
