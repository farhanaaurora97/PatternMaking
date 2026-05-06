using System.Text.Json;
using PatternPro.Core.IServices;
using PatternPro.Business.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        // Output: camelCase for browsers. Input: accept camelCase from fetch(JSON) — required or [FromBody] binds null and returns 400.
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddHttpContextAccessor();

// JSON persistence — saves to App_Data/ next to the web root
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
builder.Services.AddSingleton(new JsonDataStore(dataDir));

// Register application services (PatternPro.Business layer)
builder.Services.AddSingleton<IPatternService, PatternService>();
builder.Services.AddSingleton<ISizeChartService, SizeChartService>();
builder.Services.AddSingleton<IBlockGeneratorService, BlockGeneratorService>();
builder.Services.AddSingleton<IGradingService, GradingService>();
builder.Services.AddSingleton<IPieceService, PieceService>();
builder.Services.AddSingleton<IExportService, ExportService>();
builder.Services.AddSingleton<IPatternDraftingService, PatternDraftingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
