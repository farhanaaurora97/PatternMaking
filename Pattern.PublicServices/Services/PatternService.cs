using Pattern.Core.Model;
using Pattern.PublicServices.Interfaces;

namespace Pattern.PublicServices.Services;

public class PatternService : IPatternService
{
    private readonly List<Core.Model.Pattern> _patterns =
    [
        new() { Id = 1, Code = "DN-001", Name = "Skinny Classic",   Style = "Skinny",   BaseSize = "M", PieceCount = 9, Status = "Graded",     Date = "2025-01-14", Designer = "Pattern Designer" },
        new() { Id = 2, Code = "DN-002", Name = "Slim Tapered",     Style = "Slim",     BaseSize = "M", PieceCount = 9, Status = "InProgress", Date = "2025-01-12", Designer = "Pattern Designer" },
        new() { Id = 3, Code = "DN-003", Name = "Straight Classic", Style = "Straight", BaseSize = "M", PieceCount = 8, Status = "Draft",      Date = "2025-01-10", Designer = "Pattern Designer" },
        new() { Id = 4, Code = "DN-004", Name = "Bootcut Flare",    Style = "Bootcut",  BaseSize = "M", PieceCount = 9, Status = "Draft",      Date = "2025-01-08", Designer = "Pattern Designer" },
        new() { Id = 5, Code = "DN-005", Name = "Wide Leg Comfort", Style = "Wide Leg", BaseSize = "M", PieceCount = 8, Status = "Pending",    Date = "2025-01-05", Designer = "Pattern Designer" },
    ];

    private int _nextId = 6;

    private static readonly string[] _statusCycle = ["Pending", "Draft", "InProgress", "Graded", "Done"];

    private static readonly Dictionary<string, StyleDefinition> _styles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["skinny"]   = new() { Label = "Skinny Fit",   PieceCount = 9, PieceList = ["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Coin Pocket","Front Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["slim"]     = new() { Label = "Slim Fit",     PieceCount = 9, PieceList = ["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Coin Pocket","Front Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["straight"] = new() { Label = "Straight Fit", PieceCount = 8, PieceList = ["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Side Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["bootcut"]  = new() { Label = "Bootcut Fit",  PieceCount = 9, PieceList = ["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Side Pocket Bag","Back Patch Pocket","Belt Loop","Flare Insert"] },
        ["wideLeg"]  = new() { Label = "Wide Leg Fit", PieceCount = 8, PieceList = ["Front Leg","Back Leg","Waistband","Fly Facing","Side Pocket Bag","Back Patch Pocket","Belt Loop","Waist Tab"] },
    };

    private static readonly Dictionary<string, string> _styleLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["skinny"]   = "Skinny",
        ["slim"]     = "Slim",
        ["straight"] = "Straight",
        ["bootcut"]  = "Bootcut",
        ["wideLeg"]  = "Wide Leg",
    };

    public IReadOnlyList<Core.Model.Pattern> GetAll() => _patterns.AsReadOnly();

    public StyleDefinition GetStyleDefinition(string styleKey) =>
        _styles.TryGetValue(styleKey, out var def) ? def : _styles["skinny"];

    public Core.Model.Pattern Create(string name, string styleKey, string baseSize, string designer)
    {
        var def        = GetStyleDefinition(styleKey);
        var styleLabel = _styleLabels.TryGetValue(styleKey, out var lbl) ? lbl : styleKey;
        var code       = $"DN-{_nextId:D3}";

        var pattern = new Core.Model.Pattern
        {
            Id         = _nextId++,
            Code       = code,
            Name       = name,
            Style      = styleLabel,
            BaseSize   = baseSize,
            PieceCount = def.PieceCount,
            Status     = "Draft",
            Date       = DateTime.Today.ToString("yyyy-MM-dd"),
            Designer   = designer,
        };

        _patterns.Insert(0, pattern);
        return pattern;
    }

    public Core.Model.Pattern? CycleStatus(int id)
    {
        var pattern = _patterns.FirstOrDefault(p => p.Id == id);
        if (pattern is null) return null;

        var idx        = Array.IndexOf(_statusCycle, pattern.Status);
        pattern.Status = _statusCycle[(idx + 1) % _statusCycle.Length];
        pattern.Date   = DateTime.Today.ToString("yyyy-MM-dd");
        return pattern;
    }

    public bool Delete(int id)
    {
        var pattern = _patterns.FirstOrDefault(p => p.Id == id);
        if (pattern is null) return false;
        _patterns.Remove(pattern);
        return true;
    }

    public IReadOnlyList<Core.Model.Pattern> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _patterns.AsReadOnly();

        return _patterns
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Style.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Status.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<Core.Model.Pattern> Sort(IEnumerable<Core.Model.Pattern> patterns, string column, bool ascending)
    {
        var ordered = column.ToLower() switch
        {
            "name"   => ascending ? patterns.OrderBy(p => p.Name)       : patterns.OrderByDescending(p => p.Name),
            "style"  => ascending ? patterns.OrderBy(p => p.Style)      : patterns.OrderByDescending(p => p.Style),
            "base"   => ascending ? patterns.OrderBy(p => p.BaseSize)   : patterns.OrderByDescending(p => p.BaseSize),
            "pieces" => ascending ? patterns.OrderBy(p => p.PieceCount) : patterns.OrderByDescending(p => p.PieceCount),
            "status" => ascending ? patterns.OrderBy(p => p.Status)     : patterns.OrderByDescending(p => p.Status),
            "date"   => ascending ? patterns.OrderBy(p => p.Date)       : patterns.OrderByDescending(p => p.Date),
            _        => patterns,
        };
        return ordered.ToList().AsReadOnly();
    }
}