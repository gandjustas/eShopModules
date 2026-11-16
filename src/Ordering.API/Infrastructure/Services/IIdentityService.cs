namespace eShop.Ordering.API.Infrastructure.Services;

internal interface IIdentityService
{
    string GetUserIdentity();

    string GetUserName();
}

