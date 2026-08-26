using Pattern.Core.Model;
using PatternPro.Core.IServices;
using PatternPro.Core.Persistence.Repositories;

namespace PatternPro.Business.Services;

public class PieceService : IPieceService, IReloadableAppData
{
    private static readonly Dictionary<string, StyleDefinition> _styles =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["skinny"]   = new() { Label="Skinny Fit",   PieceCount=9, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Coin Pocket","Front Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["slim"]     = new() { Label="Slim Fit",     PieceCount=9, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Coin Pocket","Front Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["straight"] = new() { Label="Straight Fit", PieceCount=8, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Side Pocket Bag","Back Patch Pocket","Belt Loop"] },
        ["bootcut"]  = new() { Label="Bootcut Fit",  PieceCount=9, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Fly Shield","Side Pocket Bag","Back Patch Pocket","Belt Loop","Flare Insert"] },
        ["wideLeg"]  = new() { Label="Wide Leg Fit", PieceCount=8, PieceList=["Front Leg","Back Leg","Waistband","Fly Facing","Side Pocket Bag","Back Patch Pocket","Belt Loop","Waist Tab"] },
    };

    private static readonly List<PieceDefinition> _skinnyDefaults =
    [
        new() { Name="Front Leg",         Category="Body Panels",        GrainLine="Straight", Cut="Cut 2", Color="#1a1a6e", OffsetX=30,   OffsetY=20,  Points=[[142,60],[242,60],[265,155],[250,185],[248,360],[178,380],[108,360],[106,185],[91,155]],       Grain=[[190,95],[190,345]],  Cf=[[178,60],[178,380]],  Notches=[[174,60],[182,60],[106,250],[250,250]] },
        new() { Name="Back Leg",          Category="Body Panels",        GrainLine="Straight", Cut="Cut 2", Color="#1a1a6e", OffsetX=310,  OffsetY=20,  Points=[[100,60],[260,60],[295,85],[310,155],[280,190],[278,360],[178,385],[78,360],[76,190],[46,155]], Grain=[[190,95],[190,345]],  Cf=[[178,60],[178,385]],  Notches=[[174,60],[182,60],[76,270],[278,270]] },
        new() { Name="Waistband",         Category="Body Panels",        GrainLine="Straight", Cut="Cut 1", Color="#2626a0", OffsetX=620,  OffsetY=20,  Points=[[60,60],[380,60],[380,110],[60,110]],                                                          Grain=[[210,63],[210,107]], Cf=[[220,60],[220,110]],  Notches=[[219,60],[221,60]] },
        new() { Name="Fly Facing",        Category="Closures",           GrainLine="Straight", Cut="Cut 2", Color="#534AB7", OffsetX=1060, OffsetY=20,  Points=[[60,60],[110,60],[110,190],[80,210],[60,190]],                                                  Grain=[[84,70],[84,195]],   Cf=null,                  Notches=[[85,60],[85,100]] },
        new() { Name="Fly Shield",        Category="Closures",           GrainLine="Straight", Cut="Cut 1", Color="#534AB7", OffsetX=1195, OffsetY=20,  Points=[[60,60],[95,60],[95,200],[60,200]],                                                            Grain=[[76,70],[76,190]],   Cf=null,                  Notches=[[77,60],[77,100]] },
        new() { Name="Coin Pocket",       Category="Pockets",            GrainLine="Straight", Cut="Cut 2", Color="#0F6E56", OffsetX=1320, OffsetY=20,  Points=[[60,60],[120,60],[120,110],[80,125],[60,110]],                                                  Grain=[[88,65],[88,120]],   Cf=null,                  Notches=[[90,60]] },
        new() { Name="Front Pocket Bag",  Category="Pockets",            GrainLine="Straight", Cut="Cut 2", Color="#0F6E56", OffsetX=1060, OffsetY=260, Points=[[60,60],[160,60],[160,200],[80,220],[60,180]],                                                  Grain=[[108,70],[108,210]], Cf=null,                  Notches=[[60,110],[160,110]] },
        new() { Name="Back Patch Pocket", Category="Pockets",            GrainLine="Straight", Cut="Cut 2", Color="#854F0B", OffsetX=1320, OffsetY=260, Points=[[60,60],[160,60],[165,155],[55,155]],                                                          Grain=[[112,70],[112,148]], Cf=[[112,60],[112,155]],  Notches=[[111,60],[113,60]] },
        new() { Name="Belt Loop",         Category="Hardware & Details", GrainLine="Straight", Cut="Cut 8", Color="#854F0B", OffsetX=1440, OffsetY=260, Points=[[60,60],[85,60],[85,160],[60,160]],                                                            Grain=[[72,70],[72,150]],   Cf=null,                  Notches=[] },
    ];

    private static readonly int[][] _basePiecePoints = [[100, 50], [300, 50], [300, 400], [100, 400]];

    private readonly IPieceRepository _pieces;
    private readonly Dictionary<string, List<PieceDefinition>> _defsPerStyle;
    private readonly Dictionary<int,    List<PieceDefinition>> _defsPerPattern;

    public PieceService(IPieceRepository pieces)
    {
        _pieces = pieces;
        var saved = pieces.Load();

        // Style geometry: use saved if present, else seed skinny defaults
        _defsPerStyle = saved.StyleGeometry.Count > 0
            ? saved.StyleGeometry
            : new(StringComparer.OrdinalIgnoreCase)
              {
                  ["skinny"] = _skinnyDefaults.Select(CloneDef).ToList(),
              };

        _defsPerPattern = saved.PatternGeometry;
    }

    // ── Persist ─────────────────────────────────────────────────────

    private void Persist() =>
        _pieces.Save(new PiecesStore
        {
            StyleGeometry   = _defsPerStyle,
            PatternGeometry = _defsPerPattern,
        });

    private void EnsureFresh() => ReloadFromStoreIfAvailable();

    public void ReloadFromStore() => ReloadFromStoreIfAvailable();

    private void ReloadFromStoreIfAvailable()
    {
        var saved = _pieces.Load();
        _defsPerStyle.Clear();
        foreach (var (key, list) in saved.StyleGeometry)
            _defsPerStyle[key] = list.Select(CloneDef).ToList();

        _defsPerPattern.Clear();
        foreach (var (key, list) in saved.PatternGeometry)
            _defsPerPattern[key] = list.Select(CloneDef).ToList();

        if (_defsPerStyle.Count == 0)
        {
            _defsPerStyle["skinny"] = _skinnyDefaults.Select(CloneDef).ToList();
        }
    }

    // ── Queries ──────────────────────────────────────────────────────

    public StyleDefinition GetStyleDefinition(string styleKey)
    {
        var key = StyleOptionCatalog.NormalizeStyleKey(styleKey);
        if (_styles.TryGetValue(key, out var s))
            return s;

        var template = _styles[StyleOptionCatalog.TemplateStyleKey(key)];
        return new StyleDefinition
        {
            Label = StyleOptionCatalog.FormatFitDisplayLabel(key),
            PieceCount = template.PieceCount,
            PieceList = [.. template.PieceList],
        };
    }

    public IReadOnlyList<string> GetPieceList(string styleKey) =>
        GetStyleDefinition(styleKey).PieceList;

    public IReadOnlyList<PieceDefinition> GetPieceDefinitions(string styleKey)
    {
        EnsureFresh();
        if (_defsPerStyle.TryGetValue(styleKey, out var list))
            return list.AsReadOnly();

        List<PieceDefinition> defaults = styleKey.Equals("skinny", StringComparison.OrdinalIgnoreCase)
            ? _skinnyDefaults.Select(CloneDef).ToList()
            : BuildStylePieceListFromSkinny(styleKey);

        _defsPerStyle[styleKey] = defaults;
        Persist();
        return defaults.AsReadOnly();
    }

    /// <summary>
    /// Seed straight / slim / bootcut / wide-leg from skinny geometry where piece names match,
    /// so the canvas is usable without only placeholder rectangles.
    /// </summary>
    private List<PieceDefinition> BuildStylePieceListFromSkinny(string styleKey)
    {
        var style        = GetStyleDefinition(styleKey);
        var skinnyPieces = _defsPerStyle.TryGetValue("skinny", out var sl)
            ? sl
            : _skinnyDefaults;
        var byName = skinnyPieces.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

        var list = new List<PieceDefinition>(style.PieceList.Count);
        for (var i = 0; i < style.PieceList.Count; i++)
        {
            var name = style.PieceList[i];
            if (byName.TryGetValue(name, out var src))
            {
                list.Add(CloneDef(src));
                continue;
            }

            if (name.Equals("Side Pocket Bag", StringComparison.OrdinalIgnoreCase)
                && byName.TryGetValue("Front Pocket Bag", out var fp))
            {
                var sp = CloneDef(fp);
                sp.Name = "Side Pocket Bag";
                list.Add(sp);
                continue;
            }

            if (name.Equals("Flare Insert", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new PieceDefinition
                {
                    Name        = name,
                    Category    = "Body Panels",
                    GrainLine   = "Straight",
                    Cut         = "Cut 2",
                    Color       = "#7c3aed",
                    Points      = [.. _basePiecePoints],
                    Grain       = [[200, 60], [200, 340]],
                    OffsetX     = 1520,
                    OffsetY     = 260,
                });
                continue;
            }

            if (name.Equals("Waist Tab", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new PieceDefinition
                {
                    Name        = name,
                    Category    = "Hardware & Details",
                    GrainLine   = "Straight",
                    Cut         = "Cut 4",
                    Color       = "#854F0B",
                    Points      = [[60, 60], [120, 60], [120, 100], [60, 100]],
                    Grain       = [[90, 65], [90, 95]],
                    OffsetX     = 1600,
                    OffsetY     = 260,
                });
                continue;
            }

            list.Add(new PieceDefinition
            {
                Name    = name,
                Cut     = "Cut 2",
                Color   = DefaultColor(i),
                Points  = [.. _basePiecePoints],
                OffsetX = 30 + i * 45,
                OffsetY = 400,
            });
        }

        return list;
    }

    public IReadOnlyList<PieceDefinition> GetPieceDefinitions(int patternId, string styleKey)
    {
        EnsureFresh();
        if (_defsPerPattern.TryGetValue(patternId, out var list))
            return list.AsReadOnly();

        var source = GetPieceDefinitions(styleKey);
        var copy   = source.Select(CloneDef).ToList();

        _defsPerPattern[patternId] = copy;
        Persist();
        return copy.AsReadOnly();
    }

    public void ResetPatternFromStyle(int patternId, string styleKey)
    {
        if (patternId <= 0) return;
        var copy = GetPieceDefinitions(styleKey).Select(CloneDef).ToList();
        _defsPerPattern[patternId] = copy;
        ApplyDefaultSeamAllowances(patternId, styleKey);
        Persist();
    }

    public IReadOnlyList<int[]> GetBasePiecePoints() => _basePiecePoints;

    // ── Mutations ────────────────────────────────────────────────────

    public (bool Ok, string? Error) UpdatePieceGeometry(
        string styleKey, string pieceName,
        List<int[]> points, int offsetX, int offsetY,
        List<int[]>? grain, List<int[]>? cf, List<int[]>? notches,
        double seamAllowance = 0, string? seamAllowanceJoin = null,
        List<PieceEdge>? edges = null,
        List<PieceInternalLine>? internalLines = null)
    {
        if (!_defsPerStyle.TryGetValue(styleKey, out var list))
            return (false, $"Style '{styleKey}' not found.");

        var piece = list.FirstOrDefault(d => d.Name.Equals(pieceName, StringComparison.OrdinalIgnoreCase));
        if (piece is null)
            return (false, $"Piece '{pieceName}' not found in style '{styleKey}'.");

        piece.Points  = points;
        piece.OffsetX = offsetX;
        piece.OffsetY = offsetY;
        if (grain   is not null) piece.Grain   = grain;
        if (cf      is not null) piece.Cf      = cf;
        if (notches is not null) piece.Notches = notches;
        piece.SeamAllowance = seamAllowance;
        if (!string.IsNullOrWhiteSpace(seamAllowanceJoin))
            piece.SeamAllowanceJoin = seamAllowanceJoin.Trim().ToLowerInvariant();
        if (edges is not null)
            piece.Edges = PieceSeamAllowanceHelper.CloneEdges(edges);
        if (internalLines is not null)
            piece.InternalLines = PieceInternalLine.CloneList(internalLines);
        Persist();
        return (true, null);
    }

    public (bool Ok, string? Error) UpdatePatternPieceGeometry(
        int patternId, string styleKey, string pieceName,
        List<int[]> points, int offsetX, int offsetY,
        List<int[]>? grain, List<int[]>? cf, List<int[]>? notches,
        double seamAllowance = 0, string? seamAllowanceJoin = null,
        List<PieceEdge>? edges = null,
        List<PieceInternalLine>? internalLines = null)
    {
        if (!_defsPerPattern.ContainsKey(patternId))
            _ = GetPieceDefinitions(patternId, styleKey);

        var list  = _defsPerPattern[patternId];
        var piece = list.FirstOrDefault(d => d.Name.Equals(pieceName, StringComparison.OrdinalIgnoreCase));
        if (piece is null)
            return (false, $"Piece '{pieceName}' not found in pattern '{patternId}'.");

        piece.Points  = points;
        piece.OffsetX = offsetX;
        piece.OffsetY = offsetY;
        if (grain   is not null) piece.Grain   = grain;
        if (cf      is not null) piece.Cf      = cf;
        if (notches is not null) piece.Notches = notches;
        piece.SeamAllowance = seamAllowance;
        if (!string.IsNullOrWhiteSpace(seamAllowanceJoin))
            piece.SeamAllowanceJoin = seamAllowanceJoin.Trim().ToLowerInvariant();
        if (edges is not null)
            piece.Edges = PieceSeamAllowanceHelper.CloneEdges(edges);
        if (internalLines is not null)
            piece.InternalLines = PieceInternalLine.CloneList(internalLines);
        Persist();
        return (true, null);
    }

    public (bool Ok, string? Error, PieceDefinition? Piece) CreateStylePiece(
        string styleKey, string name, string category, string cut, string color,
        List<int[]> points, int ox, int oy)
    {
        if (string.IsNullOrWhiteSpace(name)) return (false, "Piece name is required.", null);
        if (points.Count < 3)               return (false, "At least 3 points required.", null);

        var list    = _defsPerStyle.ContainsKey(styleKey)
            ? _defsPerStyle[styleKey]
            : (List<PieceDefinition>)GetPieceDefinitions(styleKey);
        var trimmed = name.Trim();
        if (list.Any(d => d.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            return (false, $"A piece named '{trimmed}' already exists.", null);

        var piece = BuildNewPiece(trimmed, category, cut, color, points, ox, oy);
        list.Add(piece);
        Persist();
        return (true, null, piece);
    }

    public (bool Ok, string? Error, PieceDefinition? Piece) CreatePatternPiece(
        int patternId, string styleKey, string name, string category, string cut, string color,
        List<int[]> points, int ox, int oy)
    {
        if (string.IsNullOrWhiteSpace(name)) return (false, "Piece name is required.", null);
        if (points.Count < 3)               return (false, "At least 3 points required.", null);

        if (!_defsPerPattern.ContainsKey(patternId))
            _ = GetPieceDefinitions(patternId, styleKey);

        var list    = _defsPerPattern[patternId];
        var trimmed = name.Trim();
        if (list.Any(d => d.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            return (false, $"A piece named '{trimmed}' already exists.", null);

        var piece = BuildNewPiece(trimmed, category, cut, color, points, ox, oy);
        _defsPerPattern[patternId].Add(piece);
        Persist();
        return (true, null, piece);
    }

    public (bool Ok, string? Error, PieceDefinition? Piece) AddPiece(
        int patternId, string styleKey, string pieceName, string category,
        string cut, string grainLine, string color, string description)
    {
        if (string.IsNullOrWhiteSpace(pieceName))
            return (false, "Piece name is required.", null);

        var name = pieceName.Trim();
        var defs = GetPieceDefinitions(patternId, styleKey);
        if (defs.Any(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return (false, $"A piece named '{name}' already exists in this pattern.", null);

        var piece = new PieceDefinition
        {
            Name        = name,
            Category    = string.IsNullOrWhiteSpace(category)  ? "Hardware & Details" : category.Trim(),
            Cut         = string.IsNullOrWhiteSpace(cut)        ? "Cut 2"              : cut.Trim(),
            GrainLine   = string.IsNullOrWhiteSpace(grainLine)  ? "Straight"           : grainLine.Trim(),
            Color       = string.IsNullOrWhiteSpace(color)      ? "#a78bfa"            : color.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Points      = [.. _basePiecePoints],
        };

        _defsPerPattern[patternId].Add(piece);
        Persist();
        return (true, null, piece);
    }

    public (bool Ok, string? Error) DeletePiece(int patternId, string pieceName)
    {
        if (string.IsNullOrWhiteSpace(pieceName))
            return (false, "Piece name is required.");

        if (!_defsPerPattern.TryGetValue(patternId, out var defs))
            return (false, "Pattern pieces not found.");

        var existing = defs.FirstOrDefault(d => d.Name.Equals(pieceName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return (false, $"Piece '{pieceName}' not found in this pattern.");

        defs.Remove(existing);
        Persist();
        return (true, null);
    }

    public void ReplacePatternPieces(int patternId, IEnumerable<PieceDefinition> pieces)
    {
        _defsPerPattern[patternId] = pieces.ToList();
        Persist();
    }

    public void ReplaceStylePieces(string styleKey, IEnumerable<PieceDefinition> pieces)
    {
        _defsPerStyle[styleKey] = pieces.Select(CloneDef).ToList();
        Persist();
    }

    public IReadOnlySet<int> GetSavedPatternIds()
    {
        EnsureFresh();
        return _defsPerPattern.Keys.ToHashSet();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static PieceDefinition BuildNewPiece(
        string name, string category, string cut, string color,
        List<int[]> points, int ox, int oy)
    {
        var xs    = points.Select(p => p[0]).ToArray();
        var ys    = points.Select(p => p[1]).ToArray();
        var cx    = (xs.Min() + xs.Max()) / 2;
        var grain = new List<int[]> { new[] { cx, ys.Min() + 10 }, new[] { cx, ys.Max() - 10 } };

        return new PieceDefinition
        {
            Name      = name,
            Category  = string.IsNullOrWhiteSpace(category) ? "Body Panels" : category.Trim(),
            Cut       = string.IsNullOrWhiteSpace(cut)      ? "Cut 2"       : cut.Trim(),
            GrainLine = "Straight",
            Color     = string.IsNullOrWhiteSpace(color)    ? "#a78bfa"     : color.Trim(),
            Points    = points,
            Grain     = grain,
            OffsetX   = ox,
            OffsetY   = oy,
        };
    }

    public int ApplyDefaultSeamAllowances(int patternId, string styleKey, double seamAllowanceCm = 1.0)
    {
        _ = GetPieceDefinitions(patternId, styleKey);
        if (!_defsPerPattern.TryGetValue(patternId, out var list))
            return 0;

        var px = seamAllowanceCm * SeamGeometry.PixelsPerCm;
        var updated = 0;
        foreach (var piece in list)
        {
            if (piece.SeamAllowance > 0.0001)
                continue;
            if (piece.Category.Contains("Hardware", StringComparison.OrdinalIgnoreCase))
                continue;
            piece.SeamAllowance = px;
            if (string.IsNullOrWhiteSpace(piece.SeamAllowanceJoin))
                piece.SeamAllowanceJoin = "miter";
            updated++;
        }

        if (updated > 0)
            Persist();
        return updated;
    }

    private static PieceDefinition CloneDef(PieceDefinition d) => new()
    {
        Name        = d.Name,
        Category    = d.Category,
        GrainLine   = d.GrainLine,
        Cut         = d.Cut,
        Color       = d.Color,
        Description = d.Description,
        Points      = [.. d.Points],
        Edges       = d.Edges is null ? null : PieceSeamAllowanceHelper.CloneEdges(d.Edges),
        Grain       = d.Grain   is null ? null : [.. d.Grain],
        Cf          = d.Cf      is null ? null : [.. d.Cf],
        Notches     = d.Notches is null ? null : [.. d.Notches],
        InternalLines = d.InternalLines is null ? null : PieceInternalLine.CloneList(d.InternalLines),
        OffsetX     = d.OffsetX,
        OffsetY     = d.OffsetY,
        SeamAllowance = d.SeamAllowance,
        SeamAllowanceJoin = d.SeamAllowanceJoin,
    };

    private static string DefaultColor(int index) => (index % 6) switch
    {
        0 => "#a78bfa",
        1 => "#60a5fa",
        2 => "#34d399",
        3 => "#fbbf24",
        4 => "#f87171",
        _ => "#e879f9",
    };
}
