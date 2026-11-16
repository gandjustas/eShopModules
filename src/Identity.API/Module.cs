using Microsoft.AspNetCore.Authentication.Cookies;
using FusionModules;

[assembly: HostingStartup(typeof(Identity.API.Module))]

namespace Identity.API;

internal sealed class Module : ModuleBase
{
    private const string PathPrefix = "/identity";
    private const string LoginPath = $"{PathPrefix}/Account/Login";

    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        // Контроллеры и представления лежат в сборке модуля, а не в сборке хоста
        builder.Services.AddControllersWithViews()
                .AddApplicationPart(typeof(Module).Assembly);

        builder.AddNpgsqlDbContext<ApplicationDbContext>("identitydb");
        builder.Services.AddMigration<ApplicationDbContext, UsersSeed>();

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();


        // Страницы входа живут под /identity: протокольные адреса IdentityServer (/connect/*, /.well-known/*)
        // остаются в корне, а корень сайта принадлежит витрине
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = LoginPath;
            options.LogoutPath = $"{PathPrefix}/Account/Logout";
            options.AccessDeniedPath = $"{PathPrefix}/Account/AccessDenied";
        });

        builder.Services.AddTransient<ILoginService<ApplicationUser>, EFLoginService>();
        builder.Services.AddTransient<IRedirectService, RedirectService>();

        builder.Services.AddIdentityServer(options =>
        {
            //options.IssuerUri = "null";
            options.Authentication.CookieLifetime = TimeSpan.FromHours(2);

            // Сессию входа держит cookie Identity. Без этого IdentityServer читает схему по умолчанию процесса,
            // а в общем хосте это cookie витрины, и после входа пользователь снова оказывается на странице входа
            options.Authentication.CookieAuthenticationScheme = IdentityConstants.ApplicationScheme;

            // Identity и витрина на одном сайте, поэтому Lax хватает. SameSite=None без Secure браузер отбрасывает,
            // и на http (ESHOP_USE_HTTP_ENDPOINTS=1) вход не удавался: cookie сессии не доходила до /connect/authorize/callback
            options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
            options.Authentication.CheckSessionCookieSameSiteMode = SameSiteMode.Lax;

            options.UserInteraction.LoginUrl = LoginPath;
            options.UserInteraction.LogoutUrl = $"{PathPrefix}/Account/Logout";
            options.UserInteraction.ConsentUrl = $"{PathPrefix}/Consent";
            options.UserInteraction.ErrorUrl = $"{PathPrefix}/Home/Error";
            options.UserInteraction.DeviceVerificationUrl = $"{PathPrefix}/Device";

            options.Events.RaiseErrorEvents = true;
            options.Events.RaiseInformationEvents = true;
            options.Events.RaiseFailureEvents = true;
            options.Events.RaiseSuccessEvents = true;

            // TODO: Remove this line in production.
            options.KeyManagement.Enabled = false;
        })
        .AddInMemoryIdentityResources(Config.GetResources())
        .AddInMemoryApiScopes(Config.GetApiScopes())
        .AddInMemoryApiResources(Config.GetApis())
        .AddInMemoryClients(Config.GetClients(builder.Configuration))
        .AddAspNetIdentity<ApplicationUser>()
        // TODO: Not recommended for production - you need to store your key material somewhere secure
        // Ключ лежит рядом со сборкой модуля: корень содержимого в общем хосте - папка хоста, и там он не нужен
        .AddDeveloperSigningCredential(filename: Path.Combine(
            Path.GetDirectoryName(typeof(Module).Assembly.Location)!, "tempkey.jwk"));

        // AddAspNetIdentity ставит cookie входа SameSite=None, а без Secure (http) браузер её отбрасывает
        builder.Services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
            options => options.Cookie.SameSite = SameSiteMode.Lax);

        // После AddAspNetIdentity: он регистрирует свой IProfileService, а побеждает последняя регистрация
        builder.Services.AddTransient<IProfileService, ProfileService>();
    }

    protected override void Configure(IApplicationBuilder app)
    {
        // This cookie policy fixes login issues with Chrome 80+ using HTTP
        app.UseCookiePolicy(new CookiePolicyOptions { MinimumSameSitePolicy = SameSiteMode.Lax });
        app.UseIdentityServer();
        app.UseAuthorization();

        app.UseEndpoints(endpoints => endpoints.MapControllerRoute(
            name: "identity",
            pattern: $"{PathPrefix.TrimStart('/')}/{{controller=Home}}/{{action=Index}}/{{id?}}"));
    }
}
