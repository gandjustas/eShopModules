using System.Text.Json.Serialization;
using eShop.OrderProcessor.Events;

namespace eShop.OrderProcessor.Extensions;

[JsonSerializable(typeof(GracePeriodConfirmedIntegrationEvent))]
partial class IntegrationEventContext : JsonSerializerContext
{

}
