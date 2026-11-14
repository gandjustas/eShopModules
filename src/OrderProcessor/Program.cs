
var builder = WebApplication.CreateBuilder(args);

builder.AddBasicServiceDefaults();


var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
