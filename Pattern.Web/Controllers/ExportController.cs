using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pattern.Core.Model;
using PatternPro.Core.IServices;
using Pattern.Web.Model;

namespace Pattern.Web.Controllers;

[Authorize]
public class ExportController(
    IExportService exportService,
    IPatternService patternService,
    IPieceService pieceService,
    IProductionCertificationService productionCertification) : Controller
{
    public IActionResult Index(int patternId = 0, string style = "skinny", string? sizes = null, string? source = null)
    {
        var patterns = patternService.GetAll().ToList();
        var selectedPattern = patternId > 0
            ? patterns.FirstOrDefault(p => p.Id == patternId)
            : patterns.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).FirstOrDefault();
        if (selectedPattern is not null)
        {
            patternId = selectedPattern.Id;
            style = ToStyleKey(selectedPattern.Style);
        }
        var selectedSizes = ParseSizes(sizes);
        var styleKey = ToStyleKey(style);

        if (patternId > 0)
            PreparePatternForFactoryExport(patternId, styleKey);

        patterns = patternService.GetAll().ToList();
        selectedPattern = patternId > 0
            ? patterns.FirstOrDefault(p => p.Id == patternId)
            : selectedPattern;

        var validation = productionCertification.ValidateForFactory(patternId, styleKey);

        var vm = new ExportViewModel
        {
            PatternId          = patternId,
            PatternDisplayName = selectedPattern is not null ? $"{selectedPattern.Code} {selectedPattern.Name}" : "—",
            StyleLabel         = patternService.GetStyleDefinition(style).Label,
            PieceCount         = selectedPattern?.PieceCount ?? 9,
            SizeCount          = selectedSizes.Count,
            SizesCsv           = string.Join(",", selectedSizes),
            SelectedFormat     = "DXF",
            CanvasGradeBaseSize = selectedPattern is not null ? selectedPattern.BaseSize : null,
            ApprovedForCutting = selectedPattern?.ApprovedForCutting ?? false,
            CutterTestPassed   = selectedPattern?.CutterTestPassed ?? false,
            CanExportToFactory = validation.CanExportToFactory,
            ApprovedBy         = selectedPattern?.ApprovedBy,
            CutterTestedBy     = selectedPattern?.CutterTestedBy,
            ShrinkagePercent   = selectedPattern?.ShrinkagePercent ?? 0m,
        };

        ViewBag.ExportSource  = source ?? "standard";
        ViewBag.CurrentStyle  = style;
        ViewBag.ValidationIssues = validation.Issues;
        ViewBag.ValidationWarnings = validation.Warnings;
        ViewBag.FactoryExportReady = validation.CanExportToFactory;
        SetLayout("Export", "Export / DXF", style, vm.PatternId > 0 ? vm.PatternId : null);
        return View(vm);
    }

    /// <summary>Apply default seam allowances before QC display. Certification stays explicit (approve + cutter test).</summary>
    private void PreparePatternForFactoryExport(int patternId, string styleKey) =>
        pieceService.ApplyDefaultSeamAllowances(patternId, styleKey);

    [HttpGet]
    public IActionResult ValidateFactory(int patternId, string style = "skinny")
    {
        var styleKey = ToStyleKey(style);
        var report = productionCertification.ValidateForFactory(patternId, styleKey);
        return Ok(new
        {
            report.CanExportToFactory,
            report.ApprovedForCutting,
            report.CutterTestPassed,
            issues = report.Issues,
            warnings = report.Warnings,
        });
    }

    [HttpPost]
    public IActionResult ApproveForCutting([FromBody] ProductionActionRequest req)
    {
        if (req.PatternId <= 0)
            return BadRequest("Select a saved pattern first.");

        var styleKey = ToStyleKey(req.Style ?? "skinny");
        var pre = productionCertification.ValidateForFactory(req.PatternId, styleKey);
        var qcErrors = pre.Issues.Where(i => i.Code is not "NOT_APPROVED" and not "CUTTER_TEST").ToList();
        if (qcErrors.Count > 0)
        {
            return BadRequest(new
            {
                message = "QC must pass before approval.",
                issues = qcErrors,
            });
        }

        var pattern = productionCertification.ApproveForCutting(req.PatternId, req.Actor ?? "Pattern Designer");
        if (pattern is null)
            return BadRequest("Could not approve pattern.");

        return Ok(new { pattern.ApprovedForCutting, pattern.ApprovedAt, pattern.ApprovedBy });
    }

    [HttpPost]
    public IActionResult RevokeApproval([FromBody] ProductionActionRequest req)
    {
        if (req.PatternId <= 0) return BadRequest("Invalid pattern.");
        var pattern = productionCertification.RevokeCuttingApproval(req.PatternId);
        if (pattern is null) return NotFound();
        return Ok(new { pattern.ApprovedForCutting });
    }

    [HttpPost]
    public IActionResult RecordCutterTest([FromBody] CutterTestRequest req)
    {
        if (req.PatternId <= 0) return BadRequest("Invalid pattern.");
        var pattern = productionCertification.RecordCutterTest(
            req.PatternId, req.Passed, req.Actor ?? "Factory", req.Notes);
        if (pattern is null) return NotFound();
        return Ok(new
        {
            pattern.CutterTestPassed,
            pattern.CutterTestedAt,
            pattern.CutterTestedBy,
            pattern.CutterTestNotes,
        });
    }

    [HttpPost]
    public IActionResult CompleteFactoryCertification([FromBody] ProductionActionRequest req)
    {
        if (req.PatternId <= 0)
            return BadRequest("Select a saved pattern first.");

        var styleKey = ToStyleKey(req.Style ?? "skinny");
        var report = productionCertification.CompleteFactoryCertification(
            req.PatternId, styleKey, req.Actor ?? "Pattern Designer");

        if (!report.CanExportToFactory)
        {
            var blockers = report.Issues.Where(i => i.Code is not "NOT_APPROVED" and not "CUTTER_TEST").ToList();
            if (blockers.Count > 0)
            {
                return BadRequest(new
                {
                    message = "Fix blocking QC issues on Canvas before factory certification.",
                    issues = blockers,
                    warnings = report.Warnings,
                });
            }
        }

        return Ok(new
        {
            report.CanExportToFactory,
            report.ApprovedForCutting,
            report.CutterTestPassed,
            issues = report.Issues,
            warnings = report.Warnings,
        });
    }

    [HttpPost]
    public IActionResult SetShrinkage([FromBody] ShrinkageRequest req)
    {
        if (req.PatternId <= 0) return BadRequest("Invalid pattern.");
        var pattern = patternService.SetShrinkagePercent(req.PatternId, req.Percent);
        if (pattern is null) return NotFound();
        return Ok(new { pattern.ShrinkagePercent });
    }

    [HttpPost]
    public IActionResult StartExport([FromBody] ExportRequest req)
    {
        var steps = exportService.GetExportSteps(req.Format);
        return Ok(steps.Select((s, i) => new { step = i, label = s }));
    }

    [HttpGet]
    public IActionResult PreviewPieces(string style = "skinny")
    {
        var pieces = patternService.GetStyleDefinition(style).PieceList;
        return Ok(pieces);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DownloadPackage(
        int patternId = 0,
        string style = "skinny",
        string format = "DXF",
        string? sizes = null,
        string purpose = "factory")
    {
        var patterns = patternService.GetAll().ToList();
        var pattern = patternId > 0
            ? patterns.FirstOrDefault(p => p.Id == patternId)
            : patterns.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).FirstOrDefault();
        if (pattern is not null)
        {
            patternId = pattern.Id;
            style = ToStyleKey(pattern.Style);
        }

        var exportPurpose = ParsePurpose(purpose);
        if (exportPurpose == ExportPurpose.Factory
            && !User.IsInRole(AppRoles.Admin)
            && !User.IsInRole(AppRoles.Designer))
        {
            return Forbid();
        }

        var selectedSizes = ParseSizes(sizes);
        byte[] bytes;
        string contentType;
        string fileName;
        try
        {
            (bytes, contentType, fileName) = exportService.BuildExportPackage(
                style, format, selectedSizes, patternId, exportPurpose);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        if (bytes.Length == 0)
            return BadRequest("Export produced no data.");

        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
        Response.Headers["Pragma"] = "no-cache";

        var ms = new MemoryStream(bytes, writable: false);
        return File(ms, contentType, fileName);
    }

    private static ExportPurpose ParsePurpose(string purpose) => purpose.Trim().ToLowerInvariant() switch
    {
        "clo" or "clo-review" or "cloreview" => ExportPurpose.CloReview,
        "draft" => ExportPurpose.Draft,
        _ => ExportPurpose.Factory,
    };

    private static List<string> ParseSizes(string? sizes)
    {
        var parsed = (sizes ?? "XS,S,M,L,XL,XXL")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant())
            .Distinct()
            .ToList();
        return parsed.Count == 0 ? ["XS", "S", "M", "L", "XL", "XXL"] : parsed;
    }

    private static string ToStyleKey(string styleLabel) =>
        StyleOptionCatalog.StyleKeyFromDisplayLabel(styleLabel);

    private void SetLayout(string controller, string title, string style, int? patternId = null) =>
        ViewData["Layout"] = new LayoutViewModel
        {
            ActiveController = controller,
            PageTitle        = title,
            CurrentStyle     = style,
            CurrentPatternId = patternId,
        };
}

public record ExportRequest(string Format);

public record ProductionActionRequest(int PatternId, string? Style, string? Actor);

public record CutterTestRequest(int PatternId, bool Passed, string? Actor, string? Notes);

public record ShrinkageRequest(int PatternId, decimal Percent);
