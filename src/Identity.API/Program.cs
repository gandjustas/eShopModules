using FusionModules;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();


// Apply database migration automatically. Note that this approach is not
// recommended for production scenarios. Consider generating SQL scripts from
// migrations instead.





var app = builder.Build();
//app.UseModuleRouting();
app.MapDefaultEndpoints();

app.UseStaticFiles();
app.UseRouting();


app.Run();
