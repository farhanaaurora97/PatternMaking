using Pattern.Core.Model;

namespace PatternPro.Business.Services;

/// <summary>Factory defaults used when no persisted data exists yet.</summary>
internal static class AppDataDefaults
{
    /// <summary>Legacy M waist (cm) — used to detect and upgrade old seed data.</summary>
    public const decimal LegacyBaseWaistCm = 68m;

    public static SizeChartStore CreateDefaultSizeChart() => new()
    {
        Columns = ["XS", "S", "M", "L", "XL", "XXL"],
        Rows =
        [
            Row("Waist",        [76, 80, 84, 88, 92, 96],   1.0m, "Body circumference at natural waist, tape parallel to floor"),
            Row("Hip",          [92, 96, 100, 104, 108, 112], 1.0m, "Body circumference at fullest seat, 20 cm below waist"),
            Row("Front Rise",   [25, 26, 27, 28, 29, 30],     0.5m, "Body front rise: crotch to front waist along curve"),
            Row("Back Rise",    [37, 38, 39, 40, 41, 42],     0.5m, "Body back rise: crotch to back waist along curve"),
            Row("Crotch Depth", [27, 28, 29, 30, 31, 32],     0.5m, "Sitting height: waist to seat (reference POM)"),
            Row("Thigh",        [54, 57, 60, 63, 66, 69],     1.0m, "Body circumference 2.5 cm below crotch"),
            Row("Knee",         [36, 38, 40, 42, 44, 46],     1.0m, "Body circumference at knee center, leg relaxed"),
            Row("Ankle",        [32, 34, 36, 38, 40, 42],     1.0m, "Body circumference at narrowest point above ankle"),
            Row("Inseam",       [78, 79, 80, 80, 80, 80],     1.0m, "Inner leg: crotch to floor (barefoot)"),
            Row("Outseam",      [104, 106, 108, 109, 110, 111], 1.0m, "Outer leg: side waist to floor"),
        ],
    };

    public static bool IsLegacyDefaultSizeChart(SizeChartStore store)
    {
        if (store.Rows.Count == 0) return false;
        var waist = store.Rows.FirstOrDefault(r =>
            r.MeasurementPoint.Equals("Waist", StringComparison.OrdinalIgnoreCase));
        if (waist is null || waist.Values.Count < 3) return false;
        return waist.Values[2] == LegacyBaseWaistCm;
    }

    public static GradingStore CreateDefaultGrading()
    {
        const int baseIdx = 2;
        return new GradingStore
        {
            Columns = ["XS", "S", "M", "L", "XL", "XXL"],
            BaseIndex = baseIdx,
            Styles =
            [
                Style("skinny", "Skinny Fit", baseIdx,
                    R("Waist", baseIdx, -4, -2, 2, 4, 6),
                    R("Hip", baseIdx, -4, -2, 2, 4, 6),
                    R("Front Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Back Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Thigh", baseIdx, -3, -1.5, 1.5, 3, 4.5),
                    R("Knee", baseIdx, -2, -1, 1, 2, 3),
                    R("Ankle", baseIdx, -2, -1, 1, 2, 3),
                    R("Inseam", baseIdx, -2, -1, 0, 0, 0)),
                Style("slim", "Slim Fit", baseIdx,
                    R("Waist", baseIdx, -4, -2, 2, 4, 6),
                    R("Hip", baseIdx, -4, -2, 2, 4, 6),
                    R("Front Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Back Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Thigh", baseIdx, -3, -1.5, 1.5, 3, 4.5),
                    R("Knee", baseIdx, -2, -1, 1, 2, 3),
                    R("Ankle", baseIdx, -2, -1, 1, 2, 3),
                    R("Inseam", baseIdx, -2, -1, 0, 0, 0)),
                Style("straight", "Straight Fit", baseIdx,
                    R("Waist", baseIdx, -4, -2, 2, 4, 6),
                    R("Hip", baseIdx, -4, -2, 2, 4, 6),
                    R("Front Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Back Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Thigh", baseIdx, -3, -1.5, 1.5, 3, 4.5),
                    R("Knee", baseIdx, -2, -1, 1, 2, 3),
                    R("Ankle", baseIdx, -2, -1, 1, 2, 3),
                    R("Inseam", baseIdx, -2, -1, 0, 0, 0)),
                Style("bootcut", "Bootcut Fit", baseIdx,
                    R("Waist", baseIdx, -4, -2, 2, 4, 6),
                    R("Hip", baseIdx, -4, -2, 2, 4, 6),
                    R("Front Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Back Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Thigh", baseIdx, -3, -1.5, 1.5, 3, 4.5),
                    R("Knee", baseIdx, -2, -1, 1, 2, 3),
                    R("Ankle", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Inseam", baseIdx, -2, -1, 0, 0, 0)),
                Style("wideLeg", "Wide Leg Fit", baseIdx,
                    R("Waist", baseIdx, -4, -2, 2, 4, 6),
                    R("Hip", baseIdx, -4, -2, 2, 4, 6),
                    R("Front Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Back Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Thigh", baseIdx, -3, -1.5, 1.5, 3, 4.5),
                    R("Knee", baseIdx, -2, -1, 1, 2, 3),
                    R("Ankle", baseIdx, -2, -1, 1, 2, 3),
                    R("Inseam", baseIdx, -2, -1, 0, 0, 0)),
            ],
        };
    }

    public static EaseOverridesStore CreateDefaultEaseOverrides() => new();

    private static SizeRow Row(string point, decimal[] values, decimal toleranceCm, string method) =>
        new()
        {
            MeasurementPoint = point,
            Values = [.. values],
            ToleranceCm = toleranceCm,
            MeasurementMethod = method,
        };

    private static GradingStyleEntry Style(string key, string label, int baseIdx, params GradingRow[] rows) =>
        new() { StyleKey = key, Label = label, Rows = [.. rows] };

    private static GradingRow R(string point, int baseIdx, double xs, double s, double l, double xl, double xxl) =>
        new() { MeasurementPoint = point, BaseIndex = baseIdx, Deltas = [xs, s, 0, l, xl, xxl] };
}
