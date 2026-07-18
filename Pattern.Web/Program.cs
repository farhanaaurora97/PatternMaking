using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PatternPro.Business.Services;
using PatternPro.Core.IServices;
using PatternPro.DataAccess;
using Pattern.Web;

var builder = WebApplication.CreateBuilder(args);

var mvc = builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        // Output: camelCase for browsers. Input: accept camelCase from fetch(JSON) — required or [FromBody] binds null and returns 400.
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
mvc.AddPatternProGlobalAuth();
if (builder.Environment.IsDevelopment())
    mvc.AddRazorRuntimeCompilation();
builder.Services.AddHttpContextAccessor();
builder.Services.AddPatternProAuth(builder.Environment);

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
builder.Services.AddPatternProDataAccess(builder.Configuration, dataDir);

// Register application services (PatternPro.Business layer)
builder.Services.AddSingleton<IPatternService, PatternService>();
builder.Services.AddSingleton<ISizeChartService, SizeChartService>();
builder.Services.AddSingleton<IBlockGeneratorService, BlockGeneratorService>();
builder.Services.AddSingleton<IGradingService, GradingService>();
builder.Services.AddSingleton<IPieceService, PieceService>();
builder.Services.AddSingleton<IExportService, ExportService>();
builder.Services.AddSingleton<IPatternDraftingService, PatternDraftingService>();
builder.Services.AddSingleton<ISeamValidationService, SeamValidationService>();
builder.Services.AddSingleton<IProductionCertificationService, ProductionCertificationService>();
builder.Services.AddSingleton<IUserService, UserService>();

var app = builder.Build();

app.Services.MigratePatternProDatabase();
app.SeedAdminUser();
app.LogProductionReadinessWarnings();

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    });
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
