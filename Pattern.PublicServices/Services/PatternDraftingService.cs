using Pattern.Core.Model;
using Pattern.PublicServices.Interfaces;

namespace Pattern.PublicServices.Services;

public class PatternDraftingService : IPatternDraftingService
{
    private const decimal Scale = 3m; // 1 cm = 3 pixels
    private static readonly string[] RequiredPoints =
    [
        "Waist", "Hip", "Front Rise", "Back Rise", "Thigh", "Knee", "Ankle", "Inseam",
    ];

    private readonly ISizeChartService      _sizeChart;
    private readonly IBlockGeneratorService _blockGen;

    public PatternDraftingService(ISizeChartService sizeChart, IBlockGeneratorService blockGen)
    {
        _sizeChart = sizeChart;
        _blockGen  = blockGen;
    }

    public IReadOnlyList<PieceDefinition> DraftPieces(string styleKey, string baseSize)
    {
        // ── 1. Body measurements for chosen size ───────────────────────────
        var columns = _sizeChart.GetColumnLabels();
        var rows    = _sizeChart.GetAll();

        int sizeIdx = columns.ToList().IndexOf(baseSize);
        if (sizeIdx < 0) sizeIdx = 2; // fallback to M

        decimal Get(string point) =>
            rows.FirstOrDefault(r =>
                    r.MeasurementPoint.Equals(point, StringComparison.OrdinalIgnoreCase))
                ?.Values.ElementAtOrDefault(sizeIdx) ?? 0m;

        var measurements = RequiredPoints.ToDictionary(k => k, Get, StringComparer.OrdinalIgnoreCase);
        return DraftPiecesFromMeasurements(styleKey, measurements);
    }

    public Dictionary<string, IReadOnlyList<PieceDefinition>> DraftGradedSet(string styleKey, IEnumerable<string> sizes)
    {
        var result  = new Dictionary<string, IReadOnlyList<PieceDefinition>>();
        int yOffset = 0;

        foreach (var size in sizes)
        {
            var drafted = DraftPieces(styleKey, size);

            var shifted = drafted.Select(p => new PieceDefinition
            {
                Name      = p.Name,
                Category  = p.Category,
                GrainLine = p.GrainLine,
                Cut       = p.Cut,
                Color     = p.Color,
                Points    = p.Points,
                Grain     = p.Grain,
                Cf        = p.Cf,
                Notches   = p.Notches,
                OffsetX   = p.OffsetX,
                OffsetY   = p.OffsetY + yOffset,
            }).ToList();

            int bottom = shifted.Max(p =>
                p.OffsetY + (p.Points.Count > 0 ? p.Points.Max(pt => pt[1]) : 0));
            yOffset = bottom + 100;

            result[size] = shifted.AsReadOnly();
        }

        return result;
    }

    public Dictionary<string, IReadOnlyList<PieceDefinition>> DraftGradedSetFromMeasurements(
        string styleKey,
        string baseSize,
        IEnumerable<string> sizes,
        IReadOnlyDictionary<string, decimal> baseMeasurements)
    {
        var result  = new Dictionary<string, IReadOnlyList<PieceDefinition>>();
        var columns = _sizeChart.GetColumnLabels().ToList();
        var rows    = _sizeChart.GetAll();

        int baseIdx = columns.FindIndex(c => c.Equals(baseSize, StringComparison.OrdinalIgnoreCase));
        if (baseIdx < 0) baseIdx = Math.Min(2, columns.Count - 1);

        decimal ChartValue(string point, int idx) =>
            rows.FirstOrDefault(r => r.MeasurementPoint.Equals(point, StringComparison.OrdinalIgnoreCase))
                ?.Values.ElementAtOrDefault(idx) ?? 0m;

        int yOffset = 0;
        foreach (var size in sizes)
        {
            int targetIdx = columns.FindIndex(c => c.Equals(size, StringComparison.OrdinalIgnoreCase));
            if (targetIdx < 0) continue;

            var sizeMeasurements = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var point in RequiredPoints)
            {
                var baseInput = baseMeasurements.TryGetValue(point, out var customBase)
                    ? customBase
                    : ChartValue(point, baseIdx);
                var delta = ChartValue(point, targetIdx) - ChartValue(point, baseIdx);
                sizeMeasurements[point] = Math.Max(0m, baseInput + delta);
            }

            var drafted = DraftPiecesFromMeasurements(styleKey, sizeMeasurements);
            var shifted = drafted.Select(p => new PieceDefinition
            {
                Name      = p.Name,
                Category  = p.Category,
                GrainLine = p.GrainLine,
                Cut       = p.Cut,
                Color     = p.Color,
                Points    = p.Points,
                Grain     = p.Grain,
                Cf        = p.Cf,
                Notches   = p.Notches,
                OffsetX   = p.OffsetX,
                OffsetY   = p.OffsetY + yOffset,
            }).ToList();

            int bottom = shifted.Max(p =>
                p.OffsetY + (p.Points.Count > 0 ? p.Points.Max(pt => pt[1]) : 0));
            yOffset = bottom + 100;
            result[size] = shifted.AsReadOnly();
        }

        return result;
    }

    public string RecommendClosestSize(string baseSize, IReadOnlyDictionary<string, decimal> baseMeasurements)
    {
        var columns = _sizeChart.GetColumnLabels().ToList();
        var rows = _sizeChart.GetAll();
        if (columns.Count == 0) return "M";

        int baseIdx = columns.FindIndex(c => c.Equals(baseSize, StringComparison.OrdinalIgnoreCase));
        if (baseIdx < 0) baseIdx = Math.Min(2, columns.Count - 1);

        decimal ChartValue(string point, int idx) =>
            rows.FirstOrDefault(r => r.MeasurementPoint.Equals(point, StringComparison.OrdinalIgnoreCase))
                ?.Values.ElementAtOrDefault(idx) ?? 0m;

        string bestSize = columns[Math.Clamp(baseIdx, 0, columns.Count - 1)];
        decimal bestScore = decimal.MaxValue;

        foreach (var (label, idx) in columns.Select((c, i) => (c, i)))
        {
            decimal score = 0m;
            foreach (var point in RequiredPoints)
            {
                var baseInput = baseMeasurements.TryGetValue(point, out var customBase)
                    ? customBase
                    : ChartValue(point, baseIdx);
                var chartValue = ChartValue(point, idx);
                var diff = chartValue - baseInput;
                score += diff * diff;
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestSize = label;
            }
        }

        return bestSize;
    }

    private IReadOnlyList<PieceDefinition> DraftPiecesFromMeasurements(string styleKey, IReadOnlyDictionary<string, decimal> measurements)
    {
        decimal M(string key) => measurements.TryGetValue(key, out var value) ? value : 0m;

        var waist  = M("Waist");
        var hip    = M("Hip");
        var fr     = M("Front Rise");
        var br     = M("Back Rise");
        var thigh  = M("Thigh");
        var knee   = M("Knee");
        var ankle  = M("Ankle");
        var inseam = M("Inseam");

        var ease = _blockGen.GetEffectiveEase(styleKey);
        decimal E(string k) => ease.TryGetValue(k, out var v) ? v : 0m;

        var aWaist  = waist  + E("Waist");
        var aHip    = hip    + E("Hip");
        var aThigh  = thigh  + E("Thigh");
        var aKnee   = knee   + E("Knee");
        var aAnkle  = ankle  + E("Ankle");
        var aFr     = fr     + E("Front Rise");
        var aBr     = br     + E("Back Rise");
        var aInseam = inseam + E("Inseam");

        var wQ  = aWaist / 4;
        var hQ  = aHip   / 4;
        var thQ = aThigh / 4;
        var knQ = aKnee  / 4;
        var anQ = aAnkle / 4;

        var cef = aHip / 16;
        var ceb = aHip / 8;

        int fHipY    = Px(aFr  * 0.55m);
        int fCrotchY = Px(aFr);
        int fKneeY   = Px(aFr  + aInseam * 0.45m);
        int fHemY    = Px(aFr  + aInseam);

        int bCrotchY = Px(aBr);
        int bKneeY   = Px(aBr  + aInseam * 0.45m);
        int bHemY    = Px(aBr  + aInseam);
        int bHipY    = Px(aBr  * 0.55m);

        var pieces = new List<PieceDefinition>
        {
            DraftFrontLeg(cef, wQ, hQ, thQ, knQ, anQ, fHipY, fCrotchY, fKneeY, fHemY),
            DraftBackLeg (ceb, wQ, hQ, thQ, knQ, anQ, bHipY, bCrotchY, bKneeY, bHemY),
            DraftWaistband(aWaist),
            DraftFlyFacing(aFr),
            DraftFlyShield(aFr),
        };

        if (styleKey is "skinny" or "slim")
            pieces.Add(DraftCoinPocket());

        pieces.Add(DraftFrontPocketBag());
        pieces.Add(DraftBackPatchPocket());
        pieces.Add(DraftBeltLoop());

        LayoutPieces(pieces);
        return pieces.AsReadOnly();
    }

    // ── Piece drafters ─────────────────────────────────────────────────────

    private static PieceDefinition DraftFrontLeg(
        decimal cef, decimal wQ, decimal hQ, decimal thQ, decimal knQ, decimal anQ,
        int hipY, int crotchY, int kneeY, int hemY)
    {
        int cx   = Px(cef);
        int wPx  = Px(wQ),  hPx  = Px(hQ),  thPx = Px(thQ);
        int knPx = Px(knQ), anPx = Px(anQ);
        int cfX  = cx + wPx / 2;

        List<int[]> pts =
        [
            [cx,                            0       ],  // waist inseam
            [cx + wPx,                      0       ],  // waist side
            [cx + hPx,                      hipY    ],  // hip side
            [cx + thPx,                     crotchY ],  // thigh side
            [cx + knPx,                     kneeY   ],  // knee side
            [cx + anPx,                     hemY    ],  // hem side
            [cx - anPx,                     hemY    ],  // hem inseam
            [cx - knPx,                     kneeY   ],  // knee inseam
            [cx - thPx,                     crotchY ],  // thigh inseam
            [0,          crotchY - Px(cef * 0.5m)   ],  // crotch ext tip
        ];

        return new PieceDefinition
        {
            Name      = "Front Leg",
            Category  = "Body Panels",
            Cut       = "Cut 2",
            Color     = "#1a1a6e",
            GrainLine = "Straight",
            Points    = pts,
            Grain     = [[cfX, 15], [cfX, hemY - 15]],
            Cf        = [[cx, 0],   [cx, hemY]],
            Notches   = [[cx - 1, 0], [cx + 1, 0], [cx - knPx, kneeY]],
        };
    }

    private static PieceDefinition DraftBackLeg(
        decimal ceb, decimal wQ, decimal hQ, decimal thQ, decimal knQ, decimal anQ,
        int hipY, int crotchY, int kneeY, int hemY)
    {
        decimal bwQ  = wQ  * 1.05m + 1m;  // wider waist (dart allowance)
        decimal bthQ = thQ * 1.15m;        // wider seat

        int cx    = Px(ceb);
        int bwPx  = Px(bwQ), hPx = Px(hQ), bthPx = Px(bthQ);
        int knPx  = Px(knQ), anPx = Px(anQ);
        int cfX   = cx + bwPx / 2;

        List<int[]> pts =
        [
            [cx,                              0       ],
            [cx + bwPx,                       0       ],
            [cx + hPx,                        hipY    ],
            [cx + bthPx,                      crotchY ],
            [cx + knPx,                       kneeY   ],
            [cx + anPx,                       hemY    ],
            [cx - anPx,                       hemY    ],
            [cx - knPx,                       kneeY   ],
            [cx - bthPx,                      crotchY ],
            [0,          crotchY - Px(ceb * 0.35m)    ],
        ];

        return new PieceDefinition
        {
            Name      = "Back Leg",
            Category  = "Body Panels",
            Cut       = "Cut 2",
            Color     = "#1a1a6e",
            GrainLine = "Straight",
            Points    = pts,
            Grain     = [[cfX, 15], [cfX, hemY - 15]],
            Cf        = [[cx, 0],   [cx, hemY]],
            Notches   = [[cx - 1, 0], [cx + 1, 0], [cx - knPx, kneeY]],
        };
    }

    private static PieceDefinition DraftWaistband(decimal adjWaist)
    {
        int w   = Px(adjWaist / 2 + 2m);
        int h   = Px(3.5m);
        int cfX = Px(adjWaist / 4 + 1m);

        return new PieceDefinition
        {
            Name      = "Waistband",
            Category  = "Body Panels",
            Cut       = "Cut 1",
            Color     = "#2626a0",
            GrainLine = "Straight",
            Points    = [[0, 0], [w, 0], [w, h], [0, h]],
            Grain     = [[w / 2, 3], [w / 2, h - 3]],
            Cf        = [[cfX, 0], [cfX, h]],
            Notches   = [[cfX - 1, 0], [cfX + 1, 0]],
        };
    }

    private static PieceDefinition DraftFlyFacing(decimal adjFr)
    {
        int w = Px(5m);
        int h = Px(adjFr * 0.65m);

        return new PieceDefinition
        {
            Name      = "Fly Facing",
            Category  = "Closures",
            Cut       = "Cut 2",
            Color     = "#534AB7",
            GrainLine = "Straight",
            Points    = [[0, 0], [w, 0], [w, h], [w / 2, h + Px(1.5m)], [0, h]],
            Grain     = [[w / 2, 5], [w / 2, h - 5]],
            Notches   = [[w / 2, 5], [w / 2, Px(3m)]],
        };
    }

    private static PieceDefinition DraftFlyShield(decimal adjFr)
    {
        int w = Px(4m);
        int h = Px(adjFr * 0.65m);

        return new PieceDefinition
        {
            Name      = "Fly Shield",
            Category  = "Closures",
            Cut       = "Cut 1",
            Color     = "#534AB7",
            GrainLine = "Straight",
            Points    = [[0, 0], [w, 0], [w, h], [0, h]],
            Grain     = [[w / 2, 5], [w / 2, h - 5]],
        };
    }

    private static PieceDefinition DraftCoinPocket()
    {
        int w = Px(8m);
        int h = Px(6m);

        return new PieceDefinition
        {
            Name      = "Coin Pocket",
            Category  = "Pockets",
            Cut       = "Cut 2",
            Color     = "#0F6E56",
            GrainLine = "Straight",
            Points    = [[0, 0], [w, 0], [w, h - Px(1.5m)], [w / 2, h], [0, h - Px(1.5m)]],
            Grain     = [[w / 2, 3], [w / 2, h - 3]],
        };
    }

    private static PieceDefinition DraftFrontPocketBag()
    {
        int w = Px(12m);
        int h = Px(18m);

        return new PieceDefinition
        {
            Name      = "Front Pocket Bag",
            Category  = "Pockets",
            Cut       = "Cut 2",
            Color     = "#0F6E56",
            GrainLine = "Straight",
            Points    = [[0, 0], [w, 0], [w, h - Px(3m)], [w / 2, h], [0, h - Px(3m)]],
            Grain     = [[w / 2, 5], [w / 2, h - 15]],
            Notches   = [[0, Px(6m)], [w, Px(6m)]],
        };
    }

    private static PieceDefinition DraftBackPatchPocket()
    {
        int w = Px(14m);
        int h = Px(14m);

        return new PieceDefinition
        {
            Name      = "Back Patch Pocket",
            Category  = "Pockets",
            Cut       = "Cut 2",
            Color     = "#854F0B",
            GrainLine = "Straight",
            Points    = [[0, 0], [w, 0], [w, h], [0, h]],
            Grain     = [[w / 2, 5], [w / 2, h - 5]],
            Cf        = [[w / 2, 0], [w / 2, h]],
            Notches   = [[w / 2 - 1, 0], [w / 2 + 1, 0]],
        };
    }

    private static PieceDefinition DraftBeltLoop()
    {
        int w = Px(2.5m);
        int h = Px(9m);

        return new PieceDefinition
        {
            Name      = "Belt Loop",
            Category  = "Hardware & Details",
            Cut       = "Cut 8",
            Color     = "#854F0B",
            GrainLine = "Straight",
            Points    = [[0, 0], [w, 0], [w, h], [0, h]],
            Grain     = [[w / 2, 3], [w / 2, h - 3]],
        };
    }

    // ── Layout ─────────────────────────────────────────────────────────────

    private static void LayoutPieces(List<PieceDefinition> pieces)
    {
        int x = 30, y = 20, col = 0, rowMaxH = 0;

        foreach (var p in pieces)
        {
            var xs = p.Points.Select(pt => pt[0]).ToArray();
            var ys = p.Points.Select(pt => pt[1]).ToArray();
            int pw = xs.Max() - xs.Min();
            int ph = ys.Max() - ys.Min();

            p.OffsetX = x;
            p.OffsetY = y;

            rowMaxH = Math.Max(rowMaxH, ph);
            x      += pw + 40;
            col++;

            if (col != 3) continue;
            x       = 30;
            y      += rowMaxH + 40;
            rowMaxH = 0;
            col     = 0;
        }
    }

    private static int Px(decimal cm) => (int)Math.Round(cm * Scale);
}
