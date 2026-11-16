namespace Webhooks.API.Services;

internal interface IWebhooksRetriever
{

    Task<IEnumerable<WebhookSubscription>> GetSubscriptionsOfType(WebhookType type);
}
