using Pattern.Core.Model;

namespace PatternPro.Tests;

internal static class TestPieceFactory
{
    public static PieceDefinition Rect(string name, int width = 120, double seamAllowance = 1.0) => new()
    {
        Name = name,
        Points = [[0, 0], [width, 0], [width, 60], [0, 60]],
        Grain = [[20, 10], [20, 50]],
        SeamAllowance = seamAllowance,
    };

    public static IReadOnlyList<PieceDefinition> MinimalFactorySet() =>
    [
        Rect("Front Leg"),
        Rect("Back Leg"),
        Rect("Waistband", 80),
    ];
}
