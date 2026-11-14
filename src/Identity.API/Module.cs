using FusionModules;

[assembly: HostingStartup(typeof(Identity.API.Module))]

namespace Identity.API;

internal sealed class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddControllersWithViews();

        builder.AddNpgsqlDbContext<ApplicationDbContext>("identitydb");
        builder.Services.AddMigration<ApplicationDbContext, UsersSeed>();

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();


        builder.Services.AddTransient<ILoginService<ApplicationUser>, EFLoginService>();
        builder.Services.AddTransient<IRedirectService, RedirectService>();
        builder.Services.AddTransient<IProfileService, ProfileService>();

        builder.Services.AddIdentityServer(options =>
        {
            //options.IssuerUri = "null";
            options.Authentication.CookieLifetime = TimeSpan.FromHours(2);

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
        .AddDeveloperSigningCredential();
    }

    protected override void Configure(IApplicationBuilder app)
    {
        // This cookie policy fixes login issues with Chrome 80+ using HTTP
        app.UseCookiePolicy(new CookiePolicyOptions { MinimumSameSitePolicy = SameSiteMode.Lax });
        app.UseIdentityServer();
        app.UseAuthorization();

        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
