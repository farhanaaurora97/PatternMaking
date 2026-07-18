namespace Pattern.Core.Model;

/// <summary>Maps industry garment POM codes to standard drafting measurement keys.</summary>
public static class SizeChartPomResolver
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Waist"]      = ["Waist", "BO"],
        ["Hip"]        = ["Hip", "C2", "C1"],
        ["Front Rise"] = ["Front Rise", "R1"],
        ["Back Rise"]  = ["Back Rise", "RB1"],
        ["Thigh"]      = ["Thigh", "DOT"],
        ["Knee"]       = ["Knee", "DWK"],
        ["Ankle"]      = ["Ankle", "DWO"],
        ["Inseam"]     = ["Inseam", "MS"],
    };

    public static decimal ResolveValue(IReadOnlyList<SizeRow> rows, string standardPoint, int columnIndex)
    {
        if (!Aliases.TryGetValue(standardPoint, out var aliases))
            aliases = [standardPoint];

        foreach (var alias in aliases)
        {
            var row = rows.FirstOrDefault(r =>
                r.MeasurementPoint.Equals(alias, StringComparison.OrdinalIgnoreCase));
            if (row is not null && columnIndex >= 0 && columnIndex < row.Values.Count)
                return row.Values[columnIndex];
        }

        return 0m;
    }
}
