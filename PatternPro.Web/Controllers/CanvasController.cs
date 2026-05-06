using Microsoft.AspNetCore.Mvc;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

public class CanvasController(
    IPieceService pieceService,
    IPatternDraftingService draftingService,
    ISizeChartService sizeChartService) : Controller
{
    // ── Page ────────────────────────────────────────────────────────
    public IActionResult Index(int patternId = 0, string style = "skinny", int piece = 0)
    {
        var styleKey = FitStyleKeys.Normalize(style);

        // Only load pieces when coming from a specific pattern
        var names = patternId > 0
            ? pieceService.GetPieceDefinitions(patternId, styleKey).Select(d => d.Name).ToList()
            : [];

        var vm = new CanvasViewModel
        {
            PatternId          = patternId,
            StyleKey           = styleKey,
            PieceNames         = names,
            SelectedPieceIndex = names.Count == 0 ? 0 : Math.Clamp(piece, 0, names.Count - 1),
        };
        SetLayout("Canvas", "Canvas Editor", styleKey, vm.PatternId > 0 ? vm.PatternId : null);
        return View(vm);
    }

    // ── GET: all piece geometry for the canvas engine ───────────────
    [HttpGet]
    public IActionResult PieceData(int patternId = 0, string style = "skinny")
    {
        // Return empty when no pattern is selected — canvas starts blank
        if (patternId == 0)
            return Ok(Array.Empty<CanvasPieceDto>());

        var styleKey = FitStyleKeys.Normalize(style);
        var defs = pieceService.GetPieceDefinitions(patternId, styleKey);
        var dtos = defs.Select(d => new CanvasPieceDto
        {
            Name    = d.Name,
            Cut     = d.Cut,
            Col     = d.Color,
            Pts     = d.Points,
            Grain   = d.Grain,
            Cf      = d.Cf,
            Notches = d.Notches,
            Ox      = d.OffsetX,
            Oy      = d.OffsetY,
        });
        return Ok(dtos);
    }

    // ── POST: persist one piece's full geometry after canvas edits ──
    [HttpPost]
    public IActionResult SavePiece([FromBody] SavePieceRequest? req)
    {
        if (req is null)
            return BadRequest(new { error = "Request body is required." });
        if (req.Pts is null || req.Pts.Count < 3)
            return BadRequest(new { error = "At least 3 points required." });

        var styleKey = FitStyleKeys.Normalize(req.Style);
        var (ok, error) = req.PatternId > 0
            ? pieceService.UpdatePatternPieceGeometry(
                req.PatternId, styleKey, req.Name,
                req.Pts, req.Ox, req.Oy, req.Grain, req.Cf, req.Notches)
            : pieceService.UpdatePieceGeometry(
                styleKey, req.Name,
                req.Pts, req.Ox, req.Oy, req.Grain, req.Cf, req.Notches);

        return ok ? Ok() : BadRequest(new { error });
    }

    // ── POST: save every piece at once (Save All button) ────────────
    [HttpPost]
    public IActionResult SaveAllPieces([FromBody] SaveAllPiecesRequest? req)
    {
        if (req is null || req.Pieces is null)
            return BadRequest(new { error = "Request body is required." });

        var errors = new List<string>();
        var styleKey = FitStyleKeys.Normalize(req.Style);
        foreach (var p in req.Pieces)
        {
            if (p.Pts is null || p.Pts.Count < 3) { errors.Add($"{p.Name}: need ≥ 3 points"); continue; }
            var (ok, err) = req.PatternId > 0
                ? pieceService.UpdatePatternPieceGeometry(
                    req.PatternId, styleKey, p.Name, p.Pts, p.Ox, p.Oy, p.Grain, p.Cf, p.Notches)
                : pieceService.UpdatePieceGeometry(
                    styleKey, p.Name, p.Pts, p.Ox, p.Oy, p.Grain, p.Cf, p.Notches);
            if (!ok) errors.Add($"{p.Name}: {err}");
        }
        return errors.Count == 0
            ? Ok(new { saved = req.Pieces.Count })
            : BadRequest(new { errors });
    }

    // ── GET: computed measurements for one piece ────────────────────
    [HttpGet]
    public IActionResult Measurements(int patternId = 0, string style = "skinny", string piece = "")
    {
        if (string.IsNullOrWhiteSpace(piece))
            return BadRequest(new { error = "piece name required" });

        var styleKey = FitStyleKeys.Normalize(style);
        var defs = patternId > 0
            ? pieceService.GetPieceDefinitions(patternId, styleKey)
            : pieceService.GetPieceDefinitions(styleKey);
        var def  = defs.FirstOrDefault(d => d.Name.Equals(piece, StringComparison.OrdinalIgnoreCase));
        if (def is null) return NotFound(new { error = $"Piece '{piece}' not found." });

        var pts = def.Points;
        var n   = pts.Count;
        if (n < 3) return BadRequest(new { error = "Not enough points to measure." });

        var edges    = new List<double>(n);
        double perim = 0;
        for (int i = 0; i < n; i++)
        {
            var a   = pts[i];
            var b   = pts[(i + 1) % n];
            var len = Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));
            perim  += len;
            edges.Add(Math.Round(len, 1));
        }

        double area = 0;
        for (int i = 0; i < n; i++)
        {
            int j  = (i + 1) % n;
            area  += pts[i][0] * (double)pts[j][1];
            area  -= pts[j][0] * (double)pts[i][1];
        }
        area = Math.Abs(area) / 2.0;

        var xs = pts.Select(p => p[0]).ToArray();
        var ys = pts.Select(p => p[1]).ToArray();

        return Ok(new PieceMeasurementsDto
        {
            PieceName   = def.Name,
            Perimeter   = Math.Round(perim, 1),
            Area        = Math.Round(area, 0),
            BboxW       = xs.Max() - xs.Min(),
            BboxH       = ys.Max() - ys.Min(),
            EdgeLengths = edges,
        });
    }

    // ── POST: create a new piece from user-drawn canvas points ───────
    [HttpPost]
    public IActionResult CreatePiece([FromBody] CreatePieceRequest? req)
    {
        if (req is null)
            return BadRequest(new { error = "Request body is required." });
        if (req.Pts is null || req.Pts.Count < 3)
            return BadRequest(new { error = "At least 3 points required." });

        var styleKey = FitStyleKeys.Normalize(req.Style);
        var (ok, error, piece) = req.PatternId > 0
            ? pieceService.CreatePatternPiece(
                req.PatternId, styleKey, req.Name, req.Category, req.Cut, req.Color, req.Pts, req.Ox, req.Oy)
            : pieceService.CreateStylePiece(
                styleKey, req.Name, req.Category, req.Cut, req.Color, req.Pts, req.Ox, req.Oy);

        if (!ok) return BadRequest(new { error });

        return Ok(new CanvasPieceDto
        {
            Name    = piece!.Name,
            Cut     = piece.Cut,
            Col     = piece.Color,
            Pts     = piece.Points,
            Grain   = piece.Grain,
            Cf      = piece.Cf,
            Notches = piece.Notches ?? [],
            Ox      = piece.OffsetX,
            Oy      = piece.OffsetY,
        });
    }

    // ── GET: auto-draft pieces for one or more sizes ────────────────
    [HttpGet]
    public IActionResult DraftSizes(string style = "skinny",
        [FromQuery(Name = "sizes")] string[]? sizes = null)
    {
        sizes ??= ["S", "M", "XL", "XXL"];
        var styleKey = FitStyleKeys.Normalize(style);
        var gradedSet = draftingService.DraftGradedSet(styleKey, sizes);

        var result = gradedSet.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(d => new CanvasPieceDto
            {
                Name    = d.Name,
                Cut     = d.Cut,
                Col     = d.Color,
                Pts     = d.Points,
                Grain   = d.Grain,
                Cf      = d.Cf,
                Notches = d.Notches ?? [],
                Ox      = d.OffsetX,
                Oy      = d.OffsetY,
            }).ToList()
        );

        return Ok(result);
    }

    [HttpPost]
    public IActionResult DraftFromMeasurements([FromBody] DraftFromMeasurementsRequest? req)
    {
        if (req is null)
            return BadRequest(new { error = "Request body is required." });

        var sizes = req.Sizes?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    ?? [];
        if (sizes.Length == 0)
            return BadRequest(new { error = "Select at least one size." });

        var baseMeasurements = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Waist"] = req.Waist,
            ["Hip"] = req.Hip,
            ["Front Rise"] = req.FrontRise,
            ["Back Rise"] = req.BackRise,
            ["Thigh"] = req.Thigh,
            ["Knee"] = req.Knee,
            ["Ankle"] = req.Ankle,
            ["Inseam"] = req.Inseam,
        };

        if (baseMeasurements.Values.Any(v => v <= 0))
            return BadRequest(new { error = "All body measurements must be greater than zero." });

        var styleKey = FitStyleKeys.Normalize(req.Style);
        var gradedSet = draftingService.DraftGradedSetFromMeasurements(styleKey, req.BaseSize, sizes, baseMeasurements);
        var result = gradedSet.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(d => new CanvasPieceDto
            {
                Name = d.Name,
                Cut = d.Cut,
                Col = d.Color,
                Pts = d.Points,
                Grain = d.Grain,
                Cf = d.Cf,
                Notches = d.Notches ?? [],
                Ox = d.OffsetX,
                Oy = d.OffsetY,
            }).ToList()
        );

        return Ok(result);
    }

    [HttpPost]
    public IActionResult RecommendSize([FromBody] RecommendSizeRequest? req)
    {
        if (req is null)
            return BadRequest(new { error = "Request body is required." });

        var measurements = ToMeasurementsDictionary(
            req.Waist, req.Hip, req.FrontRise, req.BackRise, req.Thigh, req.Knee, req.Ankle, req.Inseam);
        if (measurements.Values.Any(v => v <= 0))
            return BadRequest(new { error = "All body measurements must be greater than zero." });

        var recommended = draftingService.RecommendClosestSize(req.BaseSize, measurements);
        return Ok(new { size = recommended });
    }

    [HttpGet]
    public IActionResult MeasurementProfiles()
    {
        var rows = sizeChartService.GetMeasurementProfiles();
        var result = rows.Select(p => new
        {
            name = p.Name,
            waist = p.Measurements.TryGetValue("Waist", out var waist) ? waist : 0m,
            hip = p.Measurements.TryGetValue("Hip", out var hip) ? hip : 0m,
            frontRise = p.Measurements.TryGetValue("Front Rise", out var fr) ? fr : 0m,
            backRise = p.Measurements.TryGetValue("Back Rise", out var br) ? br : 0m,
            thigh = p.Measurements.TryGetValue("Thigh", out var thigh) ? thigh : 0m,
            knee = p.Measurements.TryGetValue("Knee", out var knee) ? knee : 0m,
            ankle = p.Measurements.TryGetValue("Ankle", out var ankle) ? ankle : 0m,
            inseam = p.Measurements.TryGetValue("Inseam", out var inseam) ? inseam : 0m,
        });
        return Ok(result);
    }

    [HttpPost]
    public IActionResult SaveMeasurementProfile([FromBody] SaveMeasurementProfileRequest? req)
    {
        if (req is null)
            return BadRequest(new { error = "Request body is required." });

        var measurements = ToMeasurementsDictionary(
            req.Waist, req.Hip, req.FrontRise, req.BackRise, req.Thigh, req.Knee, req.Ankle, req.Inseam);
        if (measurements.Values.Any(v => v <= 0))
            return BadRequest(new { error = "All body measurements must be greater than zero." });

        var (ok, error) = sizeChartService.SaveMeasurementProfile(req.Name, measurements);
        if (!ok) return BadRequest(new { error });
        return Ok(new { saved = true });
    }

    private static Dictionary<string, decimal> ToMeasurementsDictionary(
        decimal waist, decimal hip, decimal frontRise, decimal backRise,
        decimal thigh, decimal knee, decimal ankle, decimal inseam) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Waist"] = waist,
            ["Hip"] = hip,
            ["Front Rise"] = frontRise,
            ["Back Rise"] = backRise,
            ["Thigh"] = thigh,
            ["Knee"] = knee,
            ["Ankle"] = ankle,
            ["Inseam"] = inseam,
        };

    private void SetLayout(string controller, string title, string currentStyle = "skinny", int? patternId = null) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle      = title,
            CurrentStyle   = currentStyle,
            CurrentPatternId = patternId,
        };
}
