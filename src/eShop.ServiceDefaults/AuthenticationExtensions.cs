using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace eShop.ServiceDefaults;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Политика для API: токен Bearer, а не схема по умолчанию. В общем хосте со страницами схема по умолчанию -
    /// cookie и OIDC, и API без этой политики отвечал бы переадресацией на вход вместо 401.
    /// </summary>
    public const string BearerPolicy = "Bearer";

    public static IServiceCollection AddDefaultAuthentication(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // {
        //   "Identity": {
        //     "Url": "http://identity",
        //     "Audience": "basket"
        //    }
        // }

        var identitySection = configuration.GetSection("Identity");

        if (!identitySection.Exists())
        {
            // No identity section, so no authentication
            return services;
        }

        // prevent from mapping "sub" claim to nameidentifier.
        JsonWebTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

        services.AddAuthentication().AddJwtBearer(options =>
        {
            var identityUrl = identitySection.GetRequiredValue("Url");
            var audience = identitySection.GetRequiredValue("Audience");

            options.Authority = identityUrl;
            options.RequireHttpsMetadata = false;
            options.Audience = audience;
            // Токены могут выдаваться на других адресах того же Identity (например, на портах для мобильных клиентов)
            var extraIssuers = identitySection.GetSection("ValidIssuers").Get<string[]>() ?? [];
            options.TokenValidationParameters.ValidIssuers = [identityUrl, .. extraIssuers];
            options.TokenValidationParameters.ValidateAudience = false;
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(BearerPolicy, policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        return services;
    }
}
