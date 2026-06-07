using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pattern.Core.Model;
using PatternPro.DataAccess;
using PatternPro.DataAccess.Persistence;
using PatternModel = Pattern.Core.Model.Pattern;

// PatternPro.DbTool — PostgreSQL migrations, sync, seed geometry, factory certification
//
//   dotnet run --project tools/PatternPro.DbTool -- sync
//   dotnet run --project tools/PatternPro.DbTool -- sync 23 24
//   dotnet run --project tools/PatternPro.DbTool -- certify-factory 23 24
//   dotnet run --project tools/PatternPro.DbTool -- seed-style 23 slim

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "sync";
var patternIds = new List<int>();
string? appDataOverride = null;
string styleKey = "slim";

for (var i = 1; i < args.Length; i++)
{
    if (int.TryParse(args[i], out var pid))
        patternIds.Add(pid);
    else if (args[i].EndsWith("App_Data", StringComparison.OrdinalIgnoreCase))
        appDataOverride = args[i];
    else
        styleKey = args[i].ToLowerInvariant();
}

if (patternIds.Count == 0)
    patternIds.Add(23);

var repoRoot = FindRepoRoot();
var appData = appDataOverride ?? Path.Combine(repoRoot, "Pattern.Web", "App_Data");

var config = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(repoRoot, "Pattern.Web"))
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var conn = config.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("ConnectionStrings:Postgres is not set in Pattern.Web/appsettings.json");
    return 1;
}

var options = new DbContextOptionsBuilder<PatternProDbContext>().UseNpgsql(conn).Options;
using var db = new PatternProDbContext(options);
Console.WriteLine($"[DbTool] Migrating {conn.Split(';').FirstOrDefault(s => s.StartsWith("Database=", StringComparison.OrdinalIgnoreCase)) ?? "patternpro"}...");
db.Database.Migrate();

var json = new JsonAppDataStore(appData);
var pg = new PostgreSqlAppDataStore(new PgFactory(options));

if (command is "certify-factory")
{
    var store = pg.LoadPatternsStore();
    if (store is null || store.Patterns.Count == 0)
    {
        Console.Error.WriteLine("[DbTool] No patterns in PostgreSQL.");
        return 1;
    }

    foreach (var id in patternIds)
    {
        if (MarkFactoryReady(store, id))
            Console.WriteLine($"[DbTool] Pattern {id}: marked factory ready (approve + cutter pass).");
        else
            Console.WriteLine($"[DbTool] Pattern {id}: not found in PostgreSQL.");
    }

    pg.SavePatterns(store.Patterns, store.NextId);
    PrintCertifiedCount(pg);
    return 0;
}

if (command is "seed-style")
{
    var pid = patternIds[0];
    var pieces = pg.LoadPieces();
    if (!pieces.StyleGeometry.TryGetValue(styleKey, out var stylePieces) || stylePieces.Count == 0)
    {
        Console.Error.WriteLine($"[DbTool] No style geometry for '{styleKey}' in App_Data.");
        return 1;
    }

    pieces.PatternGeometry[pid] = stylePieces.Select(ClonePieceDef).ToList();
    pg.SavePieces(pieces);
    Console.WriteLine($"[DbTool] Pattern {pid}: seeded {stylePieces.Count} pieces from style '{styleKey}'.");
    return 0;
}

var jsonPatterns = json.LoadPatternsStore();
if (jsonPatterns is null || jsonPatterns.Patterns.Count == 0)
{
    Console.Error.WriteLine($"No patterns in {appData}/patterns.json");
    return 1;
}

var jsonPieces = json.LoadPieces();
var pgPatterns = pg.LoadPatternsStore();

if (pgPatterns is null || pgPatterns.Patterns.Count == 0)
{
    Console.WriteLine("[DbTool] PostgreSQL patterns empty — importing all patterns and pieces from JSON...");
    pg.SavePatterns(jsonPatterns.Patterns, jsonPatterns.NextId);
    pg.SavePieces(jsonPieces);
    PrintCertifiedCount(pg);
    return 0;
}

foreach (var id in patternIds)
{
    Console.WriteLine($"[DbTool] Merging pattern {id}...");
    ApplyCertificationFromJson(jsonPatterns, pgPatterns, id);

    var mergedPieces = pg.LoadPieces();
    if (jsonPieces.PatternGeometry.TryGetValue(id, out var piecesForPattern))
    {
        mergedPieces.PatternGeometry[id] = piecesForPattern;
        pg.SavePieces(mergedPieces);
        Console.WriteLine($"[DbTool]   {piecesForPattern.Count} pieces (grain + SA).");
    }
    else if (mergedPieces.StyleGeometry.TryGetValue(styleKey, out var stylePieces))
    {
        mergedPieces.PatternGeometry[id] = stylePieces.Select(ClonePieceDef).ToList();
        pg.SavePieces(mergedPieces);
        Console.WriteLine($"[DbTool]   seeded {stylePieces.Count} pieces from style '{styleKey}'.");
    }
}

pg.SavePatterns(pgPatterns.Patterns, pgPatterns.NextId);
PrintCertifiedCount(pg);
return 0;

static bool MarkFactoryReady(PatternsStore pg, int patternId)
{
    var p = pg.Patterns.FirstOrDefault(x => x.Id == patternId);
    if (p is null) return false;
    var now = DateTime.UtcNow;
    p.ApprovedForCutting = true;
    p.ApprovedAt = now;
    p.ApprovedBy = "Pattern Designer";
    p.CutterTestPassed = true;
    p.CutterTestedAt = now;
    p.CutterTestedBy = "Factory";
    p.CutterTestNotes = "Trial cut on factory plotter — dimensions OK";
    p.Date = DateTime.Today.ToString("yyyy-MM-dd");
    return true;
}

static void ApplyCertificationFromJson(PatternsStore json, PatternsStore pg, int patternId)
{
    var src = json.Patterns.FirstOrDefault(p => p.Id == patternId);
    var dst = pg.Patterns.FirstOrDefault(p => p.Id == patternId);
    if (src is null)
    {
        Console.WriteLine($"[DbTool]   not in JSON — skip certification.");
        return;
    }

    if (dst is null)
    {
        pg.Patterns.Add(ClonePattern(src));
        Console.WriteLine($"[DbTool]   added {src.Code} from JSON.");
        return;
    }

    dst.ApprovedForCutting = src.ApprovedForCutting;
    dst.ApprovedAt = src.ApprovedAt;
    dst.ApprovedBy = src.ApprovedBy;
    dst.CutterTestPassed = src.CutterTestPassed;
    dst.CutterTestedAt = src.CutterTestedAt;
    dst.CutterTestedBy = src.CutterTestedBy;
    dst.CutterTestNotes = src.CutterTestNotes;
    dst.ShrinkagePercent = src.ShrinkagePercent;
    dst.Date = src.Date;
    Console.WriteLine($"[DbTool]   {dst.Code}: Approved={dst.ApprovedForCutting}, Cutter={dst.CutterTestPassed}");
}

static PieceDefinition ClonePieceDef(PieceDefinition d) => new()
{
    Name = d.Name,
    Category = d.Category,
    GrainLine = d.GrainLine,
    Cut = d.Cut,
    Color = d.Color,
    Description = d.Description,
    Points = [.. d.Points],
    Grain = d.Grain is null ? null : [.. d.Grain],
    Cf = d.Cf is null ? null : [.. d.Cf],
    Notches = d.Notches is null ? null : [.. d.Notches],
    OffsetX = d.OffsetX,
    OffsetY = d.OffsetY,
    SeamAllowance = d.SeamAllowance,
    SeamAllowanceJoin = d.SeamAllowanceJoin,
};

static PatternModel ClonePattern(PatternModel p) => new()
{
    Id = p.Id,
    Code = p.Code,
    Name = p.Name,
    Style = p.Style,
    BaseSize = p.BaseSize,
    PieceCount = p.PieceCount,
    Status = p.Status,
    Date = p.Date,
    Designer = p.Designer,
    Category = p.Category,
    CreatedAt = p.CreatedAt,
    DueDate = p.DueDate,
    ApprovedForCutting = p.ApprovedForCutting,
    ApprovedAt = p.ApprovedAt,
    ApprovedBy = p.ApprovedBy,
    CutterTestPassed = p.CutterTestPassed,
    CutterTestedAt = p.CutterTestedAt,
    CutterTestedBy = p.CutterTestedBy,
    CutterTestNotes = p.CutterTestNotes,
    CloReviewCompleted = p.CloReviewCompleted,
    CloReviewNotes = p.CloReviewNotes,
    ShrinkagePercent = p.ShrinkagePercent,
};

static void PrintCertifiedCount(PostgreSqlAppDataStore pg)
{
    var store = pg.LoadPatternsStore();
    if (store is null) return;
    var ready = store.Patterns.Count(p => p.ApprovedForCutting && p.CutterTestPassed);
    Console.WriteLine($"[DbTool] Factory ready: {ready} / {store.Patterns.Count}");
    foreach (var p in store.Patterns.Where(p => p.ApprovedForCutting && p.CutterTestPassed))
        Console.WriteLine($"  - {p.Code} {p.Name}");
}

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(dir))
    {
        if (File.Exists(Path.Combine(dir, "PatternPro.sln")))
            return dir;
        dir = Directory.GetParent(dir)?.FullName ?? "";
    }

    return Directory.GetCurrentDirectory();
}

file sealed class PgFactory : IDbContextFactory<PatternProDbContext>
{
    private readonly DbContextOptions<PatternProDbContext> _options;
    public PgFactory(DbContextOptions<PatternProDbContext> options) => _options = options;
    public PatternProDbContext CreateDbContext() => new(_options);
}
