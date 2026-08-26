using PatternPro.Core.IServices;

namespace PatternPro.Desktop.Services;

/// <summary>
/// Reloads all singleton caches from PostgreSQL/JSON (team Desktop — other PC saved data).
/// </summary>
public sealed class DesktopDataRefreshService
{
    private readonly IPatternService _patterns;
    private readonly IPieceService _pieces;
    private readonly IGradingService _grading;
    private readonly ISizeChartService _sizeChart;
    private readonly IBlockGeneratorService _blockGenerator;

    public DesktopDataRefreshService(
        IPatternService patterns,
        IPieceService pieces,
        IGradingService grading,
        ISizeChartService sizeChart,
        IBlockGeneratorService blockGenerator)
    {
        _patterns = patterns;
        _pieces = pieces;
        _grading = grading;
        _sizeChart = sizeChart;
        _blockGenerator = blockGenerator;
    }

    public void ReloadAllFromStore()
    {
        Reload(_patterns);
        Reload(_pieces);
        Reload(_grading);
        Reload(_sizeChart);
        Reload(_blockGenerator);
    }

    private static void Reload(object service)
    {
        if (service is IReloadableAppData reloadable)
            reloadable.ReloadFromStore();
    }
}
