using Pattern.Core.Model;

namespace PatternPro.Business.Services;

/// <summary>Factory defaults used when no persisted data exists yet.</summary>
internal static class AppDataDefaults
{
    public static SizeChartStore CreateDefaultSizeChart() => new()
    {
        Columns = ["XS", "S", "M", "L", "XL", "XXL"],
        Rows =
        [
            new() { MeasurementPoint = "Waist",        Values = [60, 64, 68, 72, 76, 80] },
            new() { MeasurementPoint = "Hip",          Values = [84, 88, 92, 96, 100, 106] },
            new() { MeasurementPoint = "Front Rise",   Values = [25, 25.5m, 26, 26.5m, 27, 27.5m] },
            new() { MeasurementPoint = "Back Rise",    Values = [34, 35, 36, 37, 38, 39] },
            new() { MeasurementPoint = "Crotch Depth", Values = [26, 27, 28, 29, 30, 31] },
            new() { MeasurementPoint = "Thigh",        Values = [50, 53, 56, 59, 62, 66] },
            new() { MeasurementPoint = "Knee",         Values = [34, 36, 38, 40, 42, 44] },
            new() { MeasurementPoint = "Ankle",        Values = [29, 31, 33, 35, 37, 39] },
            new() { MeasurementPoint = "Inseam",       Values = [77, 78, 79, 80, 80, 80] },
            new() { MeasurementPoint = "Outseam",      Values = [103, 104.5m, 106, 107.5m, 109, 110] },
        ],
    };

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
                    R("Hip", baseIdx, -5, -2.5, 2.5, 5, 8),
                    R("Front Rise", baseIdx, -0.5, -0.25, 0.25, 0.5, 0.75),
                    R("Back Rise", baseIdx, -0.75, -0.4, 0.4, 0.75, 1),
                    R("Thigh", baseIdx, -4, -2, 2, 4, 6),
                    R("Knee", baseIdx, -4, -2, 2, 4, 6),
                    R("Ankle", baseIdx, -3, -1.5, 1.5, 3, 4.5),
                    R("Inseam", baseIdx, -2, -1, 1, 2, 2)),
                Style("slim", "Slim Fit", baseIdx,
                    R("Waist", baseIdx, -4, -2, 2, 4, 6),
                    R("Hip", baseIdx, -5, -2.5, 2.5, 5, 8),
                    R("Front Rise", baseIdx, -0.5, -0.25, 0.25, 0.5, 0.75),
                    R("Back Rise", baseIdx, -0.75, -0.4, 0.4, 0.75, 1),
                    R("Thigh", baseIdx, -3, -1.5, 1.5, 3, 5),
                    R("Knee", baseIdx, -3, -1.5, 1.5, 3, 5),
                    R("Ankle", baseIdx, -2.5, -1.5, 1.5, 2.5, 4),
                    R("Inseam", baseIdx, -2, -1, 1, 2, 2)),
                Style("straight", "Straight Fit", baseIdx,
                    R("Waist", baseIdx, -4, -2, 2, 4, 6),
                    R("Hip", baseIdx, -5, -2.5, 2.5, 5, 8),
                    R("Front Rise", baseIdx, -0.5, -0.25, 0.25, 0.5, 0.75),
                    R("Back Rise", baseIdx, -0.75, -0.4, 0.4, 0.75, 1),
                    R("Thigh", baseIdx, -3, -1.5, 1.5, 3, 5),
                    R("Knee", baseIdx, -3, -1.5, 1.5, 3, 5),
                    R("Ankle", baseIdx, -3, -1.5, 1.5, 3, 5),
                    R("Inseam", baseIdx, -2, -1, 1, 2, 2)),
                Style("bootcut", "Bootcut Fit", baseIdx,
                    R("Waist", baseIdx, -4, -2, 2, 4, 6),
                    R("Hip", baseIdx, -5, -2.5, 2.5, 5, 8),
                    R("Front Rise", baseIdx, -0.5, -0.25, 0.25, 0.5, 0.75),
                    R("Back Rise", baseIdx, -1, -0.5, 0.5, 1, 1.5),
                    R("Thigh", baseIdx, -3, -1.5, 1.5, 3, 5),
                    R("Knee", baseIdx, -2.5, -1.5, 1.5, 2.5, 4),
                    R("Ankle", baseIdx, -2, -1, 1, 2, 3),
                    R("Inseam", baseIdx, -2, -1, 1, 2, 2)),
                Style("wideLeg", "Wide Leg Fit", baseIdx,
                    R("Waist", baseIdx, -5, -2.5, 2.5, 5, 7),
                    R("Hip", baseIdx, -5, -2.5, 2.5, 5, 8),
                    R("Front Rise", baseIdx, -0.5, -0.25, 0.25, 0.5, 0.75),
                    R("Back Rise", baseIdx, -0.75, -0.4, 0.4, 0.75, 1),
                    R("Thigh", baseIdx, -4, -2, 2, 4, 6),
                    R("Knee", baseIdx, -4, -2, 2, 4, 6),
                    R("Ankle", baseIdx, -3, -1.5, 1.5, 3, 5),
                    R("Inseam", baseIdx, -2, -1, 1, 2, 2)),
            ],
        };
    }

    public static EaseOverridesStore CreateDefaultEaseOverrides() => new();

    private static GradingStyleEntry Style(string key, string label, int baseIdx, params GradingRow[] rows) =>
        new() { StyleKey = key, Label = label, Rows = [.. rows] };

    private static GradingRow R(string point, int baseIdx, double xs, double s, double l, double xl, double xxl) =>
        new() { MeasurementPoint = point, BaseIndex = baseIdx, Deltas = [xs, s, 0, l, xl, xxl] };
}
