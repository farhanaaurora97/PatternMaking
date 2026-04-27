using Pattern.Core.Model;
using Pattern.PublicServices.Interfaces;

namespace Pattern.PublicServices.Services;

public class PatternService : IPatternService
{
    private static readonly Dictionary<string, (string Prefix, string Label)> Categories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["denim"] = ("DN", "Denim"),
            ["chinos"] = ("CH", "Chinos"),
            ["trousers"] = ("TR", "Trousers"),
            ["cargo"] = ("CG", "Cargo"),
            ["joggers"] = ("JG", "Joggers"),
            ["linen"] = ("LN", "Linen"),
            ["leather"] = ("LE", "Leather"),
            ["palazzo"] = ("PA", "Palazzo"),
            ["shorts"] = ("SH", "Shorts"),
            ["sweatpants"] = ("SW", "Sweatpants"),
            ["corduroy"] = ("CD", "Corduroy"),
            ["dress"] = ("DR", "Dress"),
            ["workwear"] = ("WK", "Workwear"),
        };

    private static readonly List<Core.Model.Pattern> _defaultPatterns =
    [
        new() { Id = 1,  Code = "DN-001", Name = "Skinny Classic",    Style = "Skinny",   BaseSize = "M", PieceCount = 9, Status = "Graded",     Date = "2025-01-14", Designer = "Pattern Designer", Category = "Denim",      CreatedAt = new DateTime(2026, 4, 2, 10, 0, 0), DueDate = null },
        new() { Id = 2,  Code = "DN-002", Name = "Slim Tapered",      Style = "Slim",     BaseSize = "M", PieceCount = 9, Status = "InProgress", Date = "2025-01-12", Designer = "Pattern Designer", Category = "Denim",      CreatedAt = new DateTime(2026, 4, 3, 14, 0, 0), DueDate = new DateTime(2026, 4, 4) },
        new() { Id = 3,  Code = "DN-003", Name = "Straight Classic",  Style = "Straight", BaseSize = "M", PieceCount = 8, Status = "Draft",      Date = "2025-01-10", Designer = "Pattern Designer", Category = "Denim",      CreatedAt = new DateTime(2026, 4, 1, 9, 0, 0), DueDate = new DateTime(2026, 4, 5) },
        new() { Id = 4,  Code = "CH-001", Name = "Chino Slim",        Style = "Slim",     BaseSize = "M", PieceCount = 7, Status = "Graded",     Date = "2025-01-11", Designer = "Pattern Designer", Category = "Chinos",     CreatedAt = new DateTime(2026, 4, 4, 11, 0, 0), DueDate = null },
        new() { Id = 5,  Code = "CH-002", Name = "Chino Regular",     Style = "Straight", BaseSize = "L", PieceCount = 7, Status = "Graded",     Date = "2024-12-20", Designer = "Pattern Designer", Category = "Chinos",     CreatedAt = new DateTime(2026, 3, 28, 8, 0, 0), DueDate = null },
        new() { Id = 6,  Code = "CH-003", Name = "Chino Tapered",     Style = "Slim",     BaseSize = "M", PieceCount = 8, Status = "InProgress", Date = "2025-01-09", Designer = "Pattern Designer", Category = "Chinos",     CreatedAt = new DateTime(2026, 4, 5, 16, 0, 0), DueDate = new DateTime(2026, 4, 3) },
        new() { Id = 7,  Code = "TR-001", Name = "Classic Trouser",   Style = "Straight", BaseSize = "M", PieceCount = 10, Status = "Graded",    Date = "2024-12-15", Designer = "Pattern Designer", Category = "Trousers",   CreatedAt = new DateTime(2026, 3, 31, 9, 0, 0), DueDate = null },
        new() { Id = 8,  Code = "TR-002", Name = "Pleated Trouser",   Style = "Straight", BaseSize = "L", PieceCount = 12, Status = "InProgress", Date = "2025-01-08", Designer = "Pattern Designer", Category = "Trousers",   CreatedAt = new DateTime(2026, 3, 25, 10, 0, 0), DueDate = new DateTime(2026, 4, 2) },
        new() { Id = 9,  Code = "CG-001", Name = "Cargo Regular",     Style = "Straight", BaseSize = "L", PieceCount = 14, Status = "Graded",    Date = "2024-12-10", Designer = "Pattern Designer", Category = "Cargo",      CreatedAt = new DateTime(2026, 3, 24, 12, 0, 0), DueDate = null },
        new() { Id = 10, Code = "CG-002", Name = "Cargo Slim",        Style = "Slim",     BaseSize = "M", PieceCount = 13, Status = "Draft",     Date = "2025-01-06", Designer = "Pattern Designer", Category = "Cargo",      CreatedAt = new DateTime(2026, 3, 26, 13, 0, 0), DueDate = new DateTime(2026, 4, 5) },
        new() { Id = 11, Code = "JG-001", Name = "Jogger Classic",    Style = "Slim",     BaseSize = "M", PieceCount = 7, Status = "Graded",     Date = "2024-12-05", Designer = "Pattern Designer", Category = "Joggers",    CreatedAt = new DateTime(2026, 2, 10, 9, 0, 0), DueDate = null },
        new() { Id = 12, Code = "SW-001", Name = "Fleece Sweatpant",  Style = "Straight", BaseSize = "S", PieceCount = 6, Status = "Pending",    Date = "2025-01-14", Designer = "Pattern Designer", Category = "Sweatpants", CreatedAt = new DateTime(2026, 4, 5, 8, 0, 0), DueDate = new DateTime(2026, 4, 6) },
        new() { Id = 13, Code = "LN-001", Name = "Linen Relaxed",     Style = "Wide Leg", BaseSize = "M", PieceCount = 8, Status = "Graded",     Date = "2024-11-28", Designer = "Pattern Designer", Category = "Linen",      CreatedAt = new DateTime(2026, 1, 5, 9, 0, 0), DueDate = null },
        new() { Id = 14, Code = "LE-001", Name = "Leather Slim",      Style = "Slim",     BaseSize = "M", PieceCount = 9, Status = "Done",       Date = "2024-11-20", Designer = "Pattern Designer", Category = "Leather",    CreatedAt = new DateTime(2025, 11, 20, 9, 0, 0), DueDate = null },
        new() { Id = 15, Code = "PA-001", Name = "Palazzo Wide",      Style = "Wide Leg", BaseSize = "M", PieceCount = 7, Status = "Graded",     Date = "2024-11-15", Designer = "Pattern Designer", Category = "Palazzo",    CreatedAt = new DateTime(2025, 11, 15, 9, 0, 0), DueDate = null },
        new() { Id = 16, Code = "SH-001", Name = "Bermuda Short",     Style = "Straight", BaseSize = "M", PieceCount = 5, Status = "Draft",      Date = "2025-01-04", Designer = "Pattern Designer", Category = "Shorts",     CreatedAt = new DateTime(2026, 3, 22, 9, 0, 0), DueDate = new DateTime(2026, 3, 30) },
        new() { Id = 17, Code = "CD-001", Name = "Corduroy 5-Pocket", Style = "Slim",     BaseSize = "M", PieceCount = 9, Status = "InProgress", Date = "2025-01-03", Designer = "Pattern Designer", Category = "Corduroy",   CreatedAt = new DateTime(2026, 3, 18, 9, 0, 0), DueDate = null },
        new() { Id = 18, Code = "DR-001", Name = "Tuxedo Stripe",     Style = "Straight", BaseSize = "M", PieceCount = 10, Status = "Graded",    Date = "2024-12-22", Designer = "Pattern Designer", Category = "Dress",      CreatedAt = new DateTime(2025, 12, 22, 9, 0, 0), DueDate = null },
        new() { Id = 19, Code = "WK-001", Name = "Carpenter Utility", Style = "Straight", BaseSize = "L", PieceCount = 11, Status = "Draft",     Date = "2025-01-02", Designer = "Pattern Designer", Category = "Workwear",   CreatedAt = new DateTime(2026, 1, 2, 9, 0, 0), DueDate = null },
        new() { Id = 20, Code = "DN-004", Name = "Bootcut Flare",     Style = "Bootcut",  BaseSize = "M", PieceCount = 9, Status = "Draft",      Date = "2025-01-08", Designer = "Pattern Designer", Category = "Denim",      CreatedAt = new DateTime(2026, 3, 29, 9, 0, 0), DueDate = new DateTime(2026, 4, 1) },
        new() { Id = 21, Code = "DN-005", Name = "Wide Leg Comfort",  Style = "Wide Leg", BaseSize = "M", PieceCount = 8, Status = "Pending",    Date = "2025-01-05", Designer = "Pattern Designer", Category = "Denim",      CreatedAt = new DateTime(2026, 3, 27, 9, 0, 0), DueDate = new DateTime(2026, 4, 4) },
    ];

    private readonly JsonDataStore _store;
    private readonly List<Core.Model.Pattern> _patterns;
    private int _nextId;

    private static readonly string[] _statusCycle = ["Pending", "Draft", "InProgress", "Graded", "Done"];

    public PatternService(JsonDataStore store)
    {
        _store = store;
        var saved = store.LoadPatternsStore();
        if (saved is not null && saved.Patterns.Count > 0)
        {
            _patterns = saved.Patterns;
            _nextId   = saved.NextId;
        }
        else
        {
            _patterns = [.. _defaultPatterns];
            _nextId   = 22;
        }
    }

    private void Persist() => _store.SavePatterns(_patterns, _nextId);

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

    public Core.Model.Pattern Create(string name, string styleKey, string baseSize, string designer, string categoryKey)
    {
        var def        = GetStyleDefinition(styleKey);
        var styleLabel = _styleLabels.TryGetValue(styleKey, out var lbl) ? lbl : styleKey;

        if (!Categories.TryGetValue(categoryKey ?? "denim", out var cat))
            cat = Categories["denim"];

        var code = $"{cat.Prefix}-{_nextId:D3}";

        var now = DateTime.Now;
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
            Category   = cat.Label,
            CreatedAt  = now,
            DueDate    = null,
        };

        _patterns.Insert(0, pattern);
        Persist();
        return pattern;
    }

    public Core.Model.Pattern? CycleStatus(int id)
    {
        var pattern = _patterns.FirstOrDefault(p => p.Id == id);
        if (pattern is null) return null;

        var idx        = Array.IndexOf(_statusCycle, pattern.Status);
        pattern.Status = _statusCycle[(idx + 1) % _statusCycle.Length];
        pattern.Date   = DateTime.Today.ToString("yyyy-MM-dd");
        Persist();
        return pattern;
    }

    public Core.Model.Pattern? SetStatus(int id, string status)
    {
        if (Array.IndexOf(_statusCycle, status) < 0) return null;
        var pattern = _patterns.FirstOrDefault(p => p.Id == id);
        if (pattern is null) return null;

        pattern.Status = status;
        pattern.Date   = DateTime.Today.ToString("yyyy-MM-dd");
        Persist();
        return pattern;
    }

    public bool Delete(int id)
    {
        var pattern = _patterns.FirstOrDefault(p => p.Id == id);
        if (pattern is null) return false;
        _patterns.Remove(pattern);
        Persist();
        return true;
    }

    public Core.Model.Pattern? Duplicate(int id)
    {
        var source = _patterns.FirstOrDefault(p => p.Id == id);
        if (source is null) return null;

        var prefix = Categories.Values
            .FirstOrDefault(c => c.Label.Equals(source.Category, StringComparison.OrdinalIgnoreCase))
            .Prefix ?? "XX";
        var code = $"{prefix}-{_nextId:D3}";

        var copy = new Core.Model.Pattern
        {
            Id         = _nextId++,
            Code       = code,
            Name       = $"Copy of {source.Name}",
            Style      = source.Style,
            BaseSize   = source.BaseSize,
            PieceCount = source.PieceCount,
            Status     = "Draft",
            Date       = DateTime.Today.ToString("yyyy-MM-dd"),
            Designer   = source.Designer,
            Category   = source.Category,
            CreatedAt  = DateTime.Now,
            DueDate    = null,
        };

        _patterns.Insert(0, copy);
        Persist();
        return copy;
    }

    public IReadOnlyList<Core.Model.Pattern> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _patterns.AsReadOnly();

        return _patterns
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Style.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Status.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Code.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || (!string.IsNullOrEmpty(p.Category) && p.Category.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .AsReadOnly();
    }

    public Core.Model.Pattern? SetDueDate(int id, DateTime? date)
    {
        var pattern = _patterns.FirstOrDefault(p => p.Id == id);
        if (pattern is null) return null;
        pattern.DueDate = date;
        pattern.Date    = DateTime.Today.ToString("yyyy-MM-dd");
        Persist();
        return pattern;
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
            "due"    => ascending ? patterns.OrderBy(p => p.DueDate ?? DateTime.MaxValue) : patterns.OrderByDescending(p => p.DueDate ?? DateTime.MinValue),
            _        => patterns,
        };
        return ordered.ToList().AsReadOnly();
    }
}
