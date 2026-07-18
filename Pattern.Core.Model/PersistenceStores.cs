namespace Pattern.Core.Model;

/// <summary>Serialized shape of pieces.json (style + per-pattern canvas geometry).</summary>
public class PiecesStore
{
    public Dictionary<string, List<PieceDefinition>> StyleGeometry { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<int, List<PieceDefinition>> PatternGeometry { get; set; } = new();
}

/// <summary>Serialized shape of patterns.json.</summary>
public class PatternsStore
{
    public int NextId { get; set; } = 22;
    public List<Pattern> Patterns { get; set; } = [];
}

/// <summary>Serialized shape of measurement-profiles.json.</summary>
public class MeasurementProfilesStore
{
    public List<MeasurementProfile> Profiles { get; set; } = [];
}

/// <summary>Serialized shape of size-chart.json.</summary>
public class SizeChartStore
{
    public List<string> Columns { get; set; } = [];
    public List<SizeRow> Rows { get; set; } = [];
}

/// <summary>Serialized shape of grading.json (all fits).</summary>
public class GradingStore
{
    public List<string> Columns { get; set; } = [];
    public int BaseIndex { get; set; } = 2;
    public List<GradingStyleEntry> Styles { get; set; } = [];
}

public class GradingStyleEntry
{
    public string StyleKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<GradingRow> Rows { get; set; } = [];
}

/// <summary>Serialized shape of ease-overrides.json.</summary>
public class EaseOverridesStore
{
    public Dictionary<string, Dictionary<string, decimal>> OverridesByStyle { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
