namespace eShop.Identity.API.Services
{
    internal interface IRedirectService
    {
        string ExtractRedirectUriFromReturnUrl(string url);
    }
}
