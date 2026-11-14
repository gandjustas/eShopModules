using FusionModules;

var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();
builder.AddDefaultAuthentication();

var app = builder.Build();
app.UseModuleRouting();
app.MapDefaultEndpoints();

app.Run();
