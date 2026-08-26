using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pattern.Core.Model;
using PatternPro.Core.Persistence;
using PatternPro.DataAccess.Mapping;
using PatternPro.DataAccess.Persistence;

namespace PatternPro.DataAccess;

/// <summary>
/// PostgreSQL-backed store with the same behavior as file-based <c>JsonDataStore</c>.
/// Uses table <c>patterns</c> plus <c>app_kv</c> for next id and pieces JSON.
/// </summary>
public class PostgreSqlAppDataStore : IAppDataStore, IDataAccessLayer
{
    public const string KeyPieces = "pieces";
    public const string KeyPatternNextId = "pattern_next_id";
    public const string KeyMeasurementProfiles = "measurement_profiles";
    public const string KeySizeChart = "size_chart";
    public const string KeyGrading = "grading";
    public const string KeyEaseOverrides = "ease_overrides";
    private static string PatternSizeChartKey(int patternId) => $"size_chart_pattern_{patternId}";
    private const string KindOutline = "outline";
    private const string KindGrain = "grain";
    private const string KindCf = "cf";
    private const string KindNotch = "notch";
    private const string KindEdgeQuad = "edge_q";
    private const string KindEdgeCubic1 = "edge_c1";
    private const string KindEdgeCubic2 = "edge_c2";
    private const string KindEdgeSa = "edge_sa";
    private const string KindIlineStart = "iline_s";
    private const string KindIlineEnd = "iline_e";
    private const string KindIlineLabel = "iline_l";

    private readonly IDbContextFactory<PatternProDbContext> _factory;

    public PostgreSqlAppDataStore(IDbContextFactory<PatternProDbContext> factory) =>
        _factory = factory;

    public PiecesStore LoadPieces()
    {
        using var db = _factory.CreateDbContext();
        if (!db.Pieces.Any())
            TryImportPiecesFromKv(db);

        var pieces = db.Pieces
            .Include(p => p.Vertices)
            .AsNoTracking()
            .OrderBy(p => p.PatternId.HasValue ? 1 : 0)
            .ThenBy(p => p.StyleKey)
            .ThenBy(p => p.PatternId)
            .ThenBy(p => p.PieceOrder)
            .ToList();

        if (pieces.Count > 0)
            return BuildPiecesStore(pieces);

        return new PiecesStore();
    }

    /// <summary>
    /// Moves legacy <c>app_kv.pieces</c> JSON into <c>pieces</c> / <c>piece_vertices</c> when relational tables are empty.
    /// Called on startup and before the first <see cref="LoadPieces"/>.
    /// </summary>
    public void ImportLegacyAppKvIfNeeded()
    {
        using var db = _factory.CreateDbContext();
        if (!db.Pieces.Any())
            TryImportPiecesFromKv(db);
    }

    public void SavePieces(PiecesStore store)
    {
        using var db = _factory.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        try
        {
            db.PieceVertices.ExecuteDelete();
            db.Pieces.ExecuteDelete();

            var entities = BuildPieceEntities(store);
            if (entities.Count > 0)
                db.Pieces.AddRange(entities);

            var legacyRow = db.AppKeyValues.FirstOrDefault(x => x.Key == KeyPieces);
            if (legacyRow is not null)
                db.AppKeyValues.Remove(legacyRow);

            db.SaveChanges();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public PatternsStore? LoadPatternsStore()
    {
        using var db = _factory.CreateDbContext();
        var patterns = db.Patterns.AsNoTracking().OrderBy(p => p.Id).ToList();
        if (patterns.Count == 0)
            return null;

        var nextRow = db.AppKeyValues.AsNoTracking().FirstOrDefault(x => x.Key == KeyPatternNextId);
        var maxId = patterns.Max(p => p.Id);
        var nextId = maxId + 1;
        if (nextRow is not null && int.TryParse(nextRow.Value, out var parsed) && parsed > maxId)
            nextId = parsed;

        return new PatternsStore { NextId = nextId, Patterns = patterns };
    }

    public IReadOnlyList<MeasurementProfile> LoadMeasurementProfiles()
    {
        using var db = _factory.CreateDbContext();
        if (!db.MeasurementProfiles.Any())
            TryImportMeasurementProfilesFromKv(db);

        var profiles = db.MeasurementProfiles.AsNoTracking().OrderBy(p => p.Name).ToList();
        if (profiles.Count == 0)
            return [];

        var values = db.MeasurementProfileValues.AsNoTracking().ToList();
        return AppDataPersistenceMapper.ToProfiles(profiles, values);
    }

    public void SaveMeasurementProfiles(IEnumerable<MeasurementProfile> profiles)
    {
        using var db = _factory.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        try
        {
            AppDataPersistenceMapper.ApplyProfiles(db, profiles);
            RemoveLegacyKv(db, KeyMeasurementProfiles);
            db.SaveChanges();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public SizeChartStore LoadSizeChart()
    {
        using var db = _factory.CreateDbContext();
        if (!db.SizeChartColumns.Any() || !db.SizeChartRows.Any())
            TryImportSizeChartFromKv(db);

        if (!db.SizeChartColumns.Any() || !db.SizeChartRows.Any())
            return new SizeChartStore();

        var columns = db.SizeChartColumns.AsNoTracking().ToList();
        var rows = db.SizeChartRows.AsNoTracking().ToList();
        var values = db.SizeChartValues.AsNoTracking().ToList();
        return AppDataPersistenceMapper.ToSizeChartStore(columns, rows, values);
    }

    public void SaveSizeChart(SizeChartStore store)
    {
        using var db = _factory.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        try
        {
            AppDataPersistenceMapper.ApplySizeChart(db, store);
            RemoveLegacyKv(db, KeySizeChart);
            db.SaveChanges();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public SizeChartStore? LoadPatternSizeChart(int patternId)
    {
        using var db = _factory.CreateDbContext();
        return TryReadKv<SizeChartStore>(db, PatternSizeChartKey(patternId));
    }

    public void SavePatternSizeChart(int patternId, SizeChartStore store)
    {
        using var db = _factory.CreateDbContext();
        var key = PatternSizeChartKey(patternId);
        var json = JsonSerializer.Serialize(store, PersistenceJson.CompactOptions);
        var row = db.AppKeyValues.FirstOrDefault(x => x.Key == key);
        if (row is null)
            db.AppKeyValues.Add(new AppKeyValue { Key = key, Value = json });
        else
            row.Value = json;
        db.SaveChanges();
    }

    public void DeletePatternSizeChart(int patternId)
    {
        using var db = _factory.CreateDbContext();
        RemoveLegacyKv(db, PatternSizeChartKey(patternId));
        db.SaveChanges();
    }

    public GradingStore LoadGrading()
    {
        using var db = _factory.CreateDbContext();
        if (!db.GradingStyles.Any())
            TryImportGradingFromKv(db);

        if (!db.GradingStyles.Any())
            return new GradingStore();

        var meta = db.GradingMeta.AsNoTracking().FirstOrDefault();
        var columns = db.GradingColumns.AsNoTracking().ToList();
        var styles = db.GradingStyles.AsNoTracking().ToList();
        var rows = db.GradingRows.AsNoTracking().ToList();
        var deltas = db.GradingDeltas.AsNoTracking().ToList();
        return AppDataPersistenceMapper.ToGradingStore(meta, columns, styles, rows, deltas);
    }

    public void SaveGrading(GradingStore store)
    {
        using var db = _factory.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        try
        {
            AppDataPersistenceMapper.ApplyGrading(db, store);
            RemoveLegacyKv(db, KeyGrading);
            db.SaveChanges();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public EaseOverridesStore LoadEaseOverrides()
    {
        using var db = _factory.CreateDbContext();
        if (!db.EaseOverrides.Any())
            TryImportEaseFromKv(db);

        var rows = db.EaseOverrides.AsNoTracking().ToList();
        return rows.Count == 0
            ? new EaseOverridesStore()
            : AppDataPersistenceMapper.ToEaseStore(rows);
    }

    public void SaveEaseOverrides(EaseOverridesStore store)
    {
        using var db = _factory.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        try
        {
            AppDataPersistenceMapper.ApplyEaseOverrides(db, store);
            RemoveLegacyKv(db, KeyEaseOverrides);
            db.SaveChanges();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void SavePatterns(IEnumerable<Pattern.Core.Model.Pattern> patterns, int nextId)
    {
        var list = patterns.Select(CloneForDatabase).ToList();
        using var db = _factory.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        try
        {
            db.Patterns.ExecuteDelete();
            db.ChangeTracker.Clear();
            if (list.Count > 0)
                db.Patterns.AddRange(list);

            var row = db.AppKeyValues.FirstOrDefault(x => x.Key == KeyPatternNextId);
            var nextStr = nextId.ToString();
            if (row is null)
                db.AppKeyValues.Add(new AppKeyValue { Key = KeyPatternNextId, Value = nextStr });
            else
                row.Value = nextStr;

            db.SaveChanges();
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static Pattern.Core.Model.Pattern CloneForDatabase(Pattern.Core.Model.Pattern p) =>
        new()
        {
            Id = p.Id,
            Code = p.Code,
            Revision = p.Revision,
            Name = p.Name,
            FabricStretchPercent = p.FabricStretchPercent,
            Style = p.Style,
            BaseSize = p.BaseSize,
            PieceCount = p.PieceCount,
            Status = p.Status,
            Date = p.Date,
            Designer = p.Designer,
            Season = p.Season,
            Owner = p.Owner,
            LifecycleStatus = p.LifecycleStatus,
            Category = p.Category,
            CreatedAt = NormalizeUtc(p.CreatedAt),
            DueDate = p.DueDate.HasValue ? NormalizeUtc(p.DueDate.Value) : null,
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

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static PiecesStore BuildPiecesStore(IReadOnlyList<PieceEntity> pieces)
    {
        var store = new PiecesStore
        {
            StyleGeometry = new(StringComparer.OrdinalIgnoreCase),
            PatternGeometry = new(),
        };

        foreach (var entity in pieces)
        {
            var piece = ToPieceDefinition(entity);
            if (entity.PatternId.HasValue)
            {
                if (!store.PatternGeometry.TryGetValue(entity.PatternId.Value, out var list))
                {
                    list = [];
                    store.PatternGeometry[entity.PatternId.Value] = list;
                }

                list.Add(piece);
            }
            else
            {
                var styleKey = entity.StyleKey ?? "skinny";
                if (!store.StyleGeometry.TryGetValue(styleKey, out var list))
                {
                    list = [];
                    store.StyleGeometry[styleKey] = list;
                }

                list.Add(piece);
            }
        }

        return store;
    }

    private static PieceDefinition ToPieceDefinition(PieceEntity entity)
    {
        var vertices = entity.Vertices
            .OrderBy(v => v.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.PointOrder)
            .ToList();

        return new PieceDefinition
        {
            Name = entity.Name,
            PieceNumber = entity.PieceNumber,
            Material = entity.Material,
            OnFold = entity.OnFold,
            Cut = entity.Cut,
            Color = entity.Color,
            Category = entity.Category,
            GrainLine = entity.GrainLine,
            Description = entity.Description,
            Points = ToPoints(vertices, KindOutline),
            Edges = ToEdges(vertices, ToPoints(vertices, KindOutline).Count),
            Grain = ToOptionalPoints(vertices, KindGrain),
            Cf = ToOptionalPoints(vertices, KindCf),
            Notches = ToOptionalPoints(vertices, KindNotch),
            InternalLines = ToInternalLines(vertices),
            OffsetX = entity.OffsetX,
            OffsetY = entity.OffsetY,
            SeamAllowance = entity.SeamAllowance,
            SeamAllowanceJoin = entity.SeamAllowanceJoin,
        };
    }

    private static List<PieceEntity> BuildPieceEntities(PiecesStore store)
    {
        var result = new List<PieceEntity>();

        foreach (var kvp in store.StyleGeometry.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            for (var i = 0; i < kvp.Value.Count; i++)
                result.Add(ToPieceEntity(kvp.Value[i], i, kvp.Key, null));
        }

        foreach (var kvp in store.PatternGeometry.OrderBy(k => k.Key))
        {
            for (var i = 0; i < kvp.Value.Count; i++)
                result.Add(ToPieceEntity(kvp.Value[i], i, null, kvp.Key));
        }

        return result;
    }

    private static PieceEntity ToPieceEntity(PieceDefinition piece, int pieceOrder, string? styleKey, int? patternId)
    {
        var entity = new PieceEntity
        {
            PatternId = patternId,
            StyleKey = styleKey,
            PieceOrder = pieceOrder,
            Name = piece.Name,
            PieceNumber = piece.PieceNumber,
            Material = piece.Material,
            OnFold = piece.OnFold,
            Cut = piece.Cut,
            Color = piece.Color,
            Category = piece.Category,
            GrainLine = piece.GrainLine,
            Description = piece.Description,
            OffsetX = piece.OffsetX,
            OffsetY = piece.OffsetY,
            SeamAllowance = piece.SeamAllowance,
            SeamAllowanceJoin = piece.SeamAllowanceJoin,
        };

        AddVertices(entity.Vertices, KindOutline, piece.Points);
        AddEdgeVertices(entity.Vertices, piece);
        AddVertices(entity.Vertices, KindGrain, piece.Grain);
        AddVertices(entity.Vertices, KindCf, piece.Cf);
        AddVertices(entity.Vertices, KindNotch, piece.Notches);
        AddInternalLineVertices(entity.Vertices, piece.InternalLines);
        return entity;
    }

    private static List<PieceInternalLine>? ToInternalLines(IReadOnlyCollection<PieceVertexEntity> vertices)
    {
        var starts = vertices
            .Where(v => v.Kind.Equals(KindIlineStart, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.PointOrder)
            .ToList();
        if (starts.Count == 0) return null;

        var ends = vertices
            .Where(v => v.Kind.Equals(KindIlineEnd, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(v => v.PointOrder);

        var labelChars = vertices
            .Where(v => v.Kind.Equals(KindIlineLabel, StringComparison.OrdinalIgnoreCase))
            .GroupBy(v => v.PointOrder / 20)
            .ToDictionary(g => g.Key, g => new string(g.OrderBy(v => v.PointOrder % 20).Select(v => (char)v.X).ToArray()));

        var lines = new List<PieceInternalLine>();
        foreach (var s in starts)
        {
            if (!ends.TryGetValue(s.PointOrder, out var e)) continue;
            var label = labelChars.TryGetValue(s.PointOrder, out var lbl) && !string.IsNullOrWhiteSpace(lbl)
                ? lbl
                : "Guide";
            lines.Add(new PieceInternalLine
            {
                Label = label,
                X1 = s.X,
                Y1 = s.Y,
                X2 = e.X,
                Y2 = e.Y,
            });
        }

        return lines.Count == 0 ? null : lines;
    }

    private static void AddInternalLineVertices(ICollection<PieceVertexEntity> target, List<PieceInternalLine>? lines)
    {
        if (lines is null || lines.Count == 0) return;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            target.Add(new PieceVertexEntity { Kind = KindIlineStart, PointOrder = i, X = line.X1, Y = line.Y1 });
            target.Add(new PieceVertexEntity { Kind = KindIlineEnd, PointOrder = i, X = line.X2, Y = line.Y2 });

            var label = (line.Label ?? "Guide").Trim();
            if (string.IsNullOrEmpty(label)) label = "Guide";
            if (label.Length > 16) label = label[..16];
            for (var c = 0; c < label.Length; c++)
            {
                target.Add(new PieceVertexEntity
                {
                    Kind = KindIlineLabel,
                    PointOrder = i * 20 + c,
                    X = label[c],
                    Y = 0,
                });
            }
        }
    }

    private static void AddVertices(ICollection<PieceVertexEntity> target, string kind, List<int[]>? points)
    {
        if (points is null || points.Count == 0)
            return;

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.Length < 2)
                continue;

            target.Add(new PieceVertexEntity
            {
                Kind = kind,
                PointOrder = i,
                X = p[0],
                Y = p[1],
            });
        }
    }

    private static List<int[]> ToPoints(IReadOnlyCollection<PieceVertexEntity> vertices, string kind) =>
        vertices
            .Where(v => v.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.PointOrder)
            .Select(v => new[] { v.X, v.Y })
            .ToList();

    private static List<int[]>? ToOptionalPoints(IReadOnlyCollection<PieceVertexEntity> vertices, string kind)
    {
        var list = ToPoints(vertices, kind);
        return list.Count == 0 ? null : list;
    }

    private static List<PieceEdge>? ToEdges(IReadOnlyCollection<PieceVertexEntity> vertices, int pointCount)
    {
        if (pointCount == 0)
            return null;

        var edges = Enumerable.Range(0, pointCount).Select(_ => new PieceEdge { Kind = "line" }).ToList();

        foreach (var v in vertices.Where(x => x.Kind.Equals(KindEdgeQuad, StringComparison.OrdinalIgnoreCase)))
        {
            if (v.PointOrder < 0 || v.PointOrder >= edges.Count) continue;
            var sa = edges[v.PointOrder].SeamAllowance;
            edges[v.PointOrder] = new PieceEdge { Kind = "quad", C1 = [v.X, v.Y], SeamAllowance = sa };
        }

        foreach (var group in vertices
                     .Where(x => x.Kind.Equals(KindEdgeCubic1, StringComparison.OrdinalIgnoreCase))
                     .GroupBy(x => x.PointOrder))
        {
            if (group.Key < 0 || group.Key >= edges.Count) continue;
            var c1 = group.First();
            var c2 = vertices.FirstOrDefault(x =>
                x.Kind.Equals(KindEdgeCubic2, StringComparison.OrdinalIgnoreCase) && x.PointOrder == group.Key);
            if (c2 is null) continue;
            edges[group.Key] = new PieceEdge
            {
                Kind = "cubic",
                C1 = [c1.X, c1.Y],
                C2 = [c2.X, c2.Y],
                SeamAllowance = edges[group.Key].SeamAllowance,
            };
        }

        foreach (var v in vertices.Where(x => x.Kind.Equals(KindEdgeSa, StringComparison.OrdinalIgnoreCase)))
        {
            if (v.PointOrder < 0 || v.PointOrder >= edges.Count) continue;
            edges[v.PointOrder].SeamAllowance = v.X / 100.0;
        }

        return edges.Any(e => e.Kind != "line" || e.SeamAllowance > 0.0001) ? edges : null;
    }

    private static void AddEdgeVertices(ICollection<PieceVertexEntity> target, PieceDefinition piece)
    {
        if (piece.Edges is null || piece.Points.Count == 0)
            return;

        for (var i = 0; i < piece.Edges.Count && i < piece.Points.Count; i++)
        {
            var edge = piece.Edges[i];
            if (edge.Kind == "quad" && edge.C1 is { Length: >= 2 } c1)
            {
                target.Add(new PieceVertexEntity { Kind = KindEdgeQuad, PointOrder = i, X = c1[0], Y = c1[1] });
                continue;
            }

            if (edge.Kind == "cubic" && edge.C1 is { Length: >= 2 } cc1 && edge.C2 is { Length: >= 2 } cc2)
            {
                target.Add(new PieceVertexEntity { Kind = KindEdgeCubic1, PointOrder = i, X = cc1[0], Y = cc1[1] });
                target.Add(new PieceVertexEntity { Kind = KindEdgeCubic2, PointOrder = i, X = cc2[0], Y = cc2[1] });
            }

            if (edge.SeamAllowance > 0.0001)
            {
                target.Add(new PieceVertexEntity
                {
                    Kind = KindEdgeSa,
                    PointOrder = i,
                    X = (int)Math.Round(edge.SeamAllowance * 100),
                    Y = 0,
                });
            }
        }
    }

    private void TryImportPiecesFromKv(PatternProDbContext db)
    {
        var store = TryReadKv<PiecesStore>(db, KeyPieces);
        if (store is null)
            return;
        if (store.StyleGeometry.Count == 0 && store.PatternGeometry.Count == 0)
            return;

        var entities = BuildPieceEntities(store);
        if (entities.Count == 0)
            return;

        db.Pieces.AddRange(entities);
        RemoveLegacyKv(db, KeyPieces);
        db.SaveChanges();
    }

    private void TryImportSizeChartFromKv(PatternProDbContext db)
    {
        var store = TryReadKv<SizeChartStore>(db, KeySizeChart);
        if (store is null || store.Rows.Count == 0)
            return;
        AppDataPersistenceMapper.ApplySizeChart(db, store);
        RemoveLegacyKv(db, KeySizeChart);
        db.SaveChanges();
    }

    private void TryImportGradingFromKv(PatternProDbContext db)
    {
        var store = TryReadKv<GradingStore>(db, KeyGrading);
        if (store is null || store.Styles.Count == 0)
            return;
        AppDataPersistenceMapper.ApplyGrading(db, store);
        RemoveLegacyKv(db, KeyGrading);
        db.SaveChanges();
    }

    private void TryImportMeasurementProfilesFromKv(PatternProDbContext db)
    {
        var store = TryReadKv<MeasurementProfilesStore>(db, KeyMeasurementProfiles);
        if (store is null || store.Profiles.Count == 0)
            return;
        AppDataPersistenceMapper.ApplyProfiles(db, store.Profiles);
        RemoveLegacyKv(db, KeyMeasurementProfiles);
        db.SaveChanges();
    }

    private void TryImportEaseFromKv(PatternProDbContext db)
    {
        var store = TryReadKv<EaseOverridesStore>(db, KeyEaseOverrides);
        if (store is null || store.OverridesByStyle.Count == 0)
            return;
        AppDataPersistenceMapper.ApplyEaseOverrides(db, store);
        RemoveLegacyKv(db, KeyEaseOverrides);
        db.SaveChanges();
    }

    private static T? TryReadKv<T>(PatternProDbContext db, string key) where T : class
    {
        var row = db.AppKeyValues.FirstOrDefault(x => x.Key == key);
        if (row is null || string.IsNullOrWhiteSpace(row.Value))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(row.Value, PersistenceJson.CompactOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void RemoveLegacyKv(PatternProDbContext db, string key)
    {
        var row = db.AppKeyValues.FirstOrDefault(x => x.Key == key);
        if (row is not null)
            db.AppKeyValues.Remove(row);
    }
}
