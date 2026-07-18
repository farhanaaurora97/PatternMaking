namespace Pattern.Core.Model;

/// <summary>Declares which edges of two pieces are sewn together (for docs / validation / future matching).</summary>
public sealed class SeamPairDefinition
{
    public string PieceA { get; set; } = string.Empty;
    public int EdgeIndexA { get; set; }
    public string PieceB { get; set; } = string.Empty;
    public int EdgeIndexB { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Rule-based notch: position on edge [EdgeIndex, EdgeIndex+1] at parameter T (0–1), or DistanceFromStart (px) along that edge.
/// Rules with the same PairId are matching pairs on different pieces (same seam).
/// </summary>
public sealed class NotchRuleDefinition
{
    public string PieceName { get; set; } = string.Empty;
    public int EdgeIndex { get; set; }
    /// <summary>0 = start vertex of edge, 1 = end vertex.</summary>
    public double T { get; set; }
    /// <summary>If set, overrides T as distance in px from edge start along the edge.</summary>
    public double? DistanceFromStart { get; set; }
    /// <summary>Optional id shared with the matching notch on the paired piece.</summary>
    public string? PairId { get; set; }
}

/// <summary>Static seam + notch rules per style (canvas seed topology).</summary>
public static class StyleAssemblyCatalog
{
    public static IReadOnlyList<SeamPairDefinition> GetSeamPairs(string styleKey)
    {
        var k = Normalize(styleKey);
        return k switch
        {
            _ => DefaultSeamPairs(),
        };
    }

    public static IReadOnlyList<NotchRuleDefinition> GetNotchRules(string styleKey)
    {
        var k = Normalize(styleKey);
        return k switch
        {
            "wideLeg" => BottomWearNotchRulesWideLeg(),
            "bootcut" => BottomWearNotchRulesBootcut(),
            _ => BottomWearNotchRulesDefault(),
        };
    }

    private static string Normalize(string styleKey)
    {
        if (string.IsNullOrWhiteSpace(styleKey)) return "skinny";
        var s = styleKey.Trim();
        return s.Equals("wide leg", StringComparison.OrdinalIgnoreCase) ? "wideLeg" : s.ToLowerInvariant();
    }

    /// <summary>Shared defaults for skinny/slim/straight (same piece names and edge topology as seeds).</summary>
    private static List<NotchRuleDefinition> BottomWearNotchRulesDefault() =>
    [
        // Front Leg — waist edge 0, side seam / inseam markers (seed has 9 vertices)
        new() { PieceName = "Front Leg", EdgeIndex = 0, T = 0.12, PairId = "waist_cc_l" },
        new() { PieceName = "Front Leg", EdgeIndex = 0, T = 0.88, PairId = "waist_cc_r" },
        new() { PieceName = "Front Leg", EdgeIndex = 3, T = 0.45, PairId = "hip_front" },
        new() { PieceName = "Front Leg", EdgeIndex = 6, T = 0.5, PairId = "knee_front" },
        // Back Leg
        new() { PieceName = "Back Leg", EdgeIndex = 0, T = 0.12, PairId = "waist_cc_l" },
        new() { PieceName = "Back Leg", EdgeIndex = 0, T = 0.88, PairId = "waist_cc_r" },
        new() { PieceName = "Back Leg", EdgeIndex = 4, T = 0.45, PairId = "hip_back" },
        new() { PieceName = "Back Leg", EdgeIndex = 7, T = 0.5, PairId = "knee_back" },
        // Waistband — CF + sides
        new() { PieceName = "Waistband", EdgeIndex = 0, T = 0.5, PairId = "wb_cf" },
        new() { PieceName = "Waistband", EdgeIndex = 1, T = 0.5 },
        new() { PieceName = "Waistband", EdgeIndex = 3, T = 0.5 },
    ];

    private static List<NotchRuleDefinition> BottomWearNotchRulesWideLeg() =>
    [
        .. BottomWearNotchRulesDefault(),
        new() { PieceName = "Waist Tab", EdgeIndex = 0, T = 0.5 },
    ];

    private static List<NotchRuleDefinition> BottomWearNotchRulesBootcut() =>
    [
        .. BottomWearNotchRulesDefault(),
        new() { PieceName = "Flare Insert", EdgeIndex = 0, T = 0.5, PairId = "flare_top" },
    ];

    private static List<SeamPairDefinition> DefaultSeamPairs() =>
    [
        new() { PieceA = "Front Leg", EdgeIndexA = 0, PieceB = "Back Leg", EdgeIndexB = 0, Label = "Waist seam (partial)" },
        new() { PieceA = "Front Leg", EdgeIndexA = 2, PieceB = "Back Leg", EdgeIndexB = 5, Label = "Side / hip (pattern-dependent)" },
        new() { PieceA = "Front Leg", EdgeIndexA = 6, PieceB = "Back Leg", EdgeIndexB = 7, Label = "Inseam region (pattern-dependent)" },
        new() { PieceA = "Waistband", EdgeIndexA = 0, PieceB = "Front Leg", EdgeIndexB = 0, Label = "Waist attach (reference)" },
    ];
}
