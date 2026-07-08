using Microsoft.AspNetCore.Components.Authorization;
using Pattern.Core.Model;
using PatternPro.Business.Services;
using PatternPro.Core.IServices;
using PatternPro.DataAccess;
using PatternPro.Desktop.Auth;
using PatternPro.Desktop.Services;

namespace PatternPro.Desktop;

internal static class DesktopServiceRegistration
{
    public static MauiAppBuilder AddPatternProBackend(this MauiAppBuilder builder)
    {
        var dataDir = DesktopPaths.ResolveAppDataDirectory();
        builder.Services.AddPatternProDataAccess(builder.Configuration, dataDir);

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
        builder.Services.AddSingleton<DesktopCanvasHost>();
        builder.Services.AddSingleton<DashboardDataService>();
        builder.Services.AddSingleton<DesktopToastService>();

        builder.Services.AddSingleton<DesktopAuthService>();
        builder.Services.AddSingleton<PatternProAuthStateProvider>();
        builder.Services.AddSingleton<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<PatternProAuthStateProvider>());

        builder.Services.AddAuthorizationCore(options =>
        {
            options.AddPolicy("CanEdit", p => p.RequireRole(AppRoles.Admin, AppRoles.Designer));
            options.AddPolicy("CanExportFactory", p => p.RequireRole(AppRoles.Admin, AppRoles.Designer));
        });

        return builder;
    }
}
