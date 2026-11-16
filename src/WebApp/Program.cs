using eShop.WebApp.Components;
using eShop.ServiceDefaults;
using FusionModules;

// Состав процесса - HOSTINGSTARTUPASSEMBLIES: хост модулей не знает, а опечатку в их именах ловит ValidateModules
var builder = WebApplication.CreateBuilder(args).ValidateModules();

builder.AddServiceDefaults();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.AddAuthenticationServices();
// Схема JwtBearer и политика Bearer для API модулей: токен проверяет она, а не схема витрины по умолчанию
builder.AddDefaultAuthentication();

// Документы OpenAPI и версии API описывают процесс целиком, поэтому подключаются хостом один раз
builder.Services.AddProblemDetails();

var withApiVersioning = builder.Services.AddApiVersioning(options =>
{
    // Include "api-supported-versions" and "api-deprecated-versions" headers in all responses
    options.ReportApiVersions = true;
});

builder.AddDefaultOpenApi(withApiVersioning);
builder.AddApplicationServices();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Эндпоинты модулей попадают в маршрутизацию хоста; тем же вызовом подключается антифорджери
app.UseModuleRouting(antiforgery: true);
app.UseDefaultOpenApi();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
