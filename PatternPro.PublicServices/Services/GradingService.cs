using Pattern.Core.Model;
using PatternPro.Core.IServices;

namespace PatternPro.Business.Services;

public class GradingService : IGradingService
{
    private readonly object _lock = new();

    // Instance fields so AddColumn can mutate them at runtime.
    private readonly List<string> _columns;
    private readonly Dictionary<string, (string Label, List<GradingRow> Rows)> _data;

    // Index of the M (base-size) column — never changes.
    private int _baseIdx;

    public GradingService()
    {
        _columns = ["XS", "S", "M", "L", "XL", "XXL"];
        _baseIdx = 2; // "M" is at index 2

        _data = new Dictionary<string, (string, List<GradingRow>)>(StringComparer.OrdinalIgnoreCase)
        {
            ["skinny"] = ("Skinny Fit", Rows(
                R("Waist",      -4,    -2,     2,    4,    6    ),
                R("Hip",        -5,    -2.5,   2.5,  5,    8    ),
                R("Front Rise", -0.5,  -0.25,  0.25, 0.5,  0.75 ),
                R("Back Rise",  -0.75, -0.4,   0.4,  0.75, 1    ),
                R("Thigh",      -4,    -2,     2,    4,    6    ),
                R("Knee",       -4,    -2,     2,    4,    6    ),
                R("Ankle",      -3,    -1.5,   1.5,  3,    4.5  ),
                R("Inseam",     -2,    -1,     1,    2,    2    )
            )),
            ["slim"] = ("Slim Fit", Rows(
                R("Waist",      -4,    -2,     2,    4,    6    ),
                R("Hip",        -5,    -2.5,   2.5,  5,    8    ),
                R("Front Rise", -0.5,  -0.25,  0.25, 0.5,  0.75 ),
                R("Back Rise",  -0.75, -0.4,   0.4,  0.75, 1    ),
                R("Thigh",      -3,    -1.5,   1.5,  3,    5    ),
                R("Knee",       -3,    -1.5,   1.5,  3,    5    ),
                R("Ankle",      -2.5,  -1.5,   1.5,  2.5,  4    ),
                R("Inseam",     -2,    -1,     1,    2,    2    )
            )),
            ["straight"] = ("Straight Fit", Rows(
                R("Waist",      -4,    -2,     2,    4,    6    ),
                R("Hip",        -5,    -2.5,   2.5,  5,    8    ),
                R("Front Rise", -0.5,  -0.25,  0.25, 0.5,  0.75 ),
                R("Back Rise",  -0.75, -0.4,   0.4,  0.75, 1    ),
                R("Thigh",      -3,    -1.5,   1.5,  3,    5    ),
                R("Knee",       -3,    -1.5,   1.5,  3,    5    ),
                R("Ankle",      -3,    -1.5,   1.5,  3,    5    ),
                R("Inseam",     -2,    -1,     1,    2,    2    )
            )),
            ["bootcut"] = ("Bootcut Fit", Rows(
                R("Waist",      -4,    -2,     2,    4,    6    ),
                R("Hip",        -5,    -2.5,   2.5,  5,    8    ),
                R("Front Rise", -0.5,  -0.25,  0.25, 0.5,  0.75 ),
                R("Back Rise",  -1,    -0.5,   0.5,  1,    1.5  ),
                R("Thigh",      -3,    -1.5,   1.5,  3,    5    ),
                R("Knee",       -2.5,  -1.5,   1.5,  2.5,  4    ),
                R("Ankle",      -2,    -1,     1,    2,    3    ),
                R("Inseam",     -2,    -1,     1,    2,    2    )
            )),
            ["wideLeg"] = ("Wide Leg Fit", Rows(
                R("Waist",      -5,    -2.5,   2.5,  5,    7    ),
                R("Hip",        -5,    -2.5,   2.5,  5,    8    ),
                R("Front Rise", -0.5,  -0.25,  0.25, 0.5,  0.75 ),
                R("Back Rise",  -0.75, -0.4,   0.4,  0.75, 1    ),
                R("Thigh",      -4,    -2,     2,    4,    6    ),
                R("Knee",       -4,    -2,     2,    4,    6    ),
                R("Ankle",      -3,    -1.5,   1.5,  3,    5    ),
                R("Inseam",     -2,    -1,     1,    2,    2    )
            )),
        };
    }

    // ── Public API ──────────────────────────────────────────────────────

    public IReadOnlyList<string> GetColumnLabels()
    {
        lock (_lock) return _columns.ToList();
    }

    public IReadOnlyList<GradingRow> GetGradingTable(string styleKey)
    {
        lock (_lock)
            return _data.TryGetValue(styleKey, out var d) ? d.Rows : _data["skinny"].Rows;
    }

    public string GetStyleLabel(string styleKey)
    {
        lock (_lock)
            return _data.TryGetValue(styleKey, out var d) ? d.Label : "Skinny Fit";
    }

    public void AddColumn(string label)
    {
        lock (_lock)
        {
            foreach (var (_, rows) in _data.Values)
            {
                foreach (var row in rows)
                {
                    var d = row.Deltas;
                    double next = d.Count >= 2 ? 2 * d[^1] - d[^2]
                                : d.Count == 1 ? d[0]
                                : 0;
                    d.Add(next);
                }
            }
            _columns.Add(label);
        }
    }

    public string ExportCsv(string styleKey)
    {
        lock (_lock)
        {
            var rows = _data.TryGetValue(styleKey, out var d) ? d.Rows : _data["skinny"].Rows;
            var header = "Measurement," + string.Join(",", _columns.Select((c, i) =>
                i == _baseIdx ? $"{c}(Base)" : c));
            var lines = new List<string> { header };
            foreach (var r in rows)
            {
                var cells = r.Deltas.Select((v, i) =>
                    i == r.BaseIndex ? "0" : v > 0 ? $"+{v:0.##}" : v.ToString("0.##"));
                lines.Add($"{r.MeasurementPoint},{string.Join(",", cells)}");
            }
            return string.Join("\n", lines);
        }
    }

    public (bool Ok, string? Error) AddRow(string styleKey, string measurementPoint, string? copyFromPoint)
    {
        if (string.IsNullOrWhiteSpace(measurementPoint))
            return (false, "Measurement point name is required.");

        lock (_lock)
        {
            if (!_data.TryGetValue(styleKey, out var entry))
                return (false, $"Style '{styleKey}' not found.");

            var point = measurementPoint.Trim();

            if (entry.Rows.Any(r => r.MeasurementPoint.Equals(point, StringComparison.OrdinalIgnoreCase)))
                return (false, $"'{point}' already exists in this grading table.");

            List<double> deltas;

            if (!string.IsNullOrWhiteSpace(copyFromPoint))
            {
                var source = entry.Rows.FirstOrDefault(r =>
                    r.MeasurementPoint.Equals(copyFromPoint.Trim(), StringComparison.OrdinalIgnoreCase));

                if (source is null)
                    return (false, $"Copy-from row '{copyFromPoint}' not found.");

                deltas = [.. source.Deltas];
            }
            else
            {
                // Zero deltas — same count as current columns
                deltas = Enumerable.Repeat(0.0, _columns.Count).ToList();
            }

            entry.Rows.Add(new GradingRow
            {
                MeasurementPoint = point,
                BaseIndex        = _baseIdx,
                Deltas           = deltas,
            });

            return (true, null);
        }
    }

    // ── Seed helpers ────────────────────────────────────────────────────

    private GradingRow R(string point, double xs, double s, double l, double xl, double xxl) =>
        new() { MeasurementPoint = point, BaseIndex = _baseIdx, Deltas = [xs, s, 0, l, xl, xxl] };

    private static List<GradingRow> Rows(params GradingRow[] rows) => [.. rows];
}
