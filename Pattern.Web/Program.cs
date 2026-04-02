using Pattern.PublicServices.Interfaces;
using Pattern.PublicServices.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Register application services (Pattern.PublicServices layer)
builder.Services.AddSingleton<IPatternService, PatternService>();
builder.Services.AddSingleton<ISizeChartService, SizeChartService>();
builder.Services.AddSingleton<IBlockGeneratorService, BlockGeneratorService>();
builder.Services.AddSingleton<IGradingService, GradingService>();
builder.Services.AddSingleton<IPieceService, PieceService>();
builder.Services.AddSingleton<IExportService, ExportService>();

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
