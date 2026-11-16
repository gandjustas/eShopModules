namespace Webhooks.API.Services;

internal interface IGrantUrlTesterService
{
    Task<bool> TestGrantUrl(string urlHook, string url, string token);
}
