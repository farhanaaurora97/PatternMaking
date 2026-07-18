using Microsoft.EntityFrameworkCore;
using Pattern.Core.Model;
using PatternPro.DataAccess.Persistence;

namespace PatternPro.DataAccess.Mapping;

internal static class AppDataPersistenceMapper
{
    public static SizeChartStore ToSizeChartStore(
        IReadOnlyList<SizeChartColumnEntity> columns,
        IReadOnlyList<SizeChartRowEntity> rows,
        IReadOnlyList<SizeChartValueEntity> values)
    {
        var colOrder = columns.OrderBy(c => c.SortOrder).ToList();
        var rowOrder = rows.OrderBy(r => r.SortOrder).ToList();
        var byRow = values.GroupBy(v => v.RowId).ToDictionary(g => g.Key, g => g.ToList());

        return new SizeChartStore
        {
            Columns = colOrder.Select(c => c.Label).ToList(),
            Rows = rowOrder.Select(r =>
            {
                var cells = byRow.TryGetValue(r.Id, out var list)
                    ? list.ToDictionary(v => v.ColumnIndex, v => v.Value)
                    : new Dictionary<int, decimal>();
                return new SizeRow
                {
                    MeasurementPoint = r.MeasurementPoint,
                    ToleranceCm = r.ToleranceCm,
                    MeasurementMethod = r.MeasurementMethod ?? string.Empty,
                    Values = colOrder.Select((_, i) => cells.TryGetValue(i, out var v) ? v : 0m).ToList(),
                };
            }).ToList(),
        };
    }

    public static GradingStore ToGradingStore(
        GradingMetaEntity? meta,
        IReadOnlyList<GradingColumnEntity> columns,
        IReadOnlyList<GradingStyleEntity> styles,
        IReadOnlyList<GradingRowEntity> rows,
        IReadOnlyList<GradingDeltaEntity> deltas)
    {
        var colOrder = columns.OrderBy(c => c.SortOrder).ToList();
        var deltasByRow = deltas.GroupBy(d => d.RowId).ToDictionary(g => g.Key, g => g.ToList());

        return new GradingStore
        {
            BaseIndex = meta?.BaseIndex ?? 2,
            Columns = colOrder.Select(c => c.Label).ToList(),
            Styles = styles
                .OrderBy(s => s.StyleKey, StringComparer.OrdinalIgnoreCase)
                .Select(s =>
                {
                    var styleRows = rows
                        .Where(r => r.StyleKey.Equals(s.StyleKey, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(r => r.SortOrder);
                    return new GradingStyleEntry
                    {
                        StyleKey = s.StyleKey,
                        Label = s.Label,
                        Rows = styleRows.Select(r =>
                        {
                            var cells = deltasByRow.TryGetValue(r.Id, out var list)
                                ? list.ToDictionary(d => d.ColumnIndex, d => d.Delta)
                                : new Dictionary<int, double>();
                            return new GradingRow
                            {
                                MeasurementPoint = r.MeasurementPoint,
                                BaseIndex = r.BaseIndex,
                                Deltas = colOrder.Select((_, i) => cells.TryGetValue(i, out var v) ? v : 0d).ToList(),
                            };
                        }).ToList(),
                    };
                }).ToList(),
        };
    }

    public static List<MeasurementProfile> ToProfiles(
        IReadOnlyList<MeasurementProfileEntity> profiles,
        IReadOnlyList<MeasurementProfileValueEntity> values)
    {
        var byProfile = values.GroupBy(v => v.ProfileId).ToDictionary(g => g.Key, g => g.ToList());
        return profiles
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p =>
            {
                var map = byProfile.TryGetValue(p.Id, out var list)
                    ? list.ToDictionary(v => v.MeasurementPoint, v => v.Value, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                return new MeasurementProfile { Name = p.Name, Measurements = map };
            }).ToList();
    }

    public static EaseOverridesStore ToEaseStore(IReadOnlyList<EaseOverrideEntity> rows)
    {
        var store = new EaseOverridesStore();
        foreach (var row in rows)
        {
            if (!store.OverridesByStyle.TryGetValue(row.StyleKey, out var map))
            {
                map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                store.OverridesByStyle[row.StyleKey] = map;
            }
            map[row.MeasurementPoint] = row.Value;
        }
        return store;
    }

    public static void ApplySizeChart(PatternProDbContext db, SizeChartStore store)
    {
        db.SizeChartValues.RemoveRange(db.SizeChartValues);
        db.SizeChartRows.RemoveRange(db.SizeChartRows);
        db.SizeChartColumns.RemoveRange(db.SizeChartColumns);

        var columns = store.Columns.Select((label, i) => new SizeChartColumnEntity
        {
            SortOrder = i,
            Label = label,
        }).ToList();
        db.SizeChartColumns.AddRange(columns);
        db.SaveChanges();

        var colCount = columns.Count;
        for (var ri = 0; ri < store.Rows.Count; ri++)
        {
            var row = store.Rows[ri];
            var entity = new SizeChartRowEntity
            {
                SortOrder = ri,
                MeasurementPoint = row.MeasurementPoint,
                ToleranceCm = row.ToleranceCm,
                MeasurementMethod = row.MeasurementMethod ?? string.Empty,
            };
            db.SizeChartRows.Add(entity);
            db.SaveChanges();

            for (var ci = 0; ci < colCount && ci < row.Values.Count; ci++)
            {
                db.SizeChartValues.Add(new SizeChartValueEntity
                {
                    RowId = entity.Id,
                    ColumnIndex = ci,
                    Value = row.Values[ci],
                });
            }
        }
    }

    public static void ApplyGrading(PatternProDbContext db, GradingStore store)
    {
        db.GradingDeltas.RemoveRange(db.GradingDeltas);
        db.GradingRows.RemoveRange(db.GradingRows);
        db.GradingStyles.RemoveRange(db.GradingStyles);
        db.GradingColumns.RemoveRange(db.GradingColumns);

        var meta = db.GradingMeta.FirstOrDefault();
        if (meta is null)
        {
            meta = new GradingMetaEntity { Id = 1, BaseIndex = store.BaseIndex };
            db.GradingMeta.Add(meta);
        }
        else
        {
            meta.BaseIndex = store.BaseIndex;
        }

        db.GradingColumns.AddRange(store.Columns.Select((label, i) => new GradingColumnEntity
        {
            SortOrder = i,
            Label = label,
        }));

        foreach (var style in store.Styles)
        {
            db.GradingStyles.Add(new GradingStyleEntity
            {
                StyleKey = style.StyleKey,
                Label = style.Label,
            });
        }

        db.SaveChanges();

        foreach (var style in store.Styles)
        {
            for (var ri = 0; ri < style.Rows.Count; ri++)
            {
                var row = style.Rows[ri];
                var entity = new GradingRowEntity
                {
                    StyleKey = style.StyleKey,
                    SortOrder = ri,
                    MeasurementPoint = row.MeasurementPoint,
                    BaseIndex = row.BaseIndex,
                };
                db.GradingRows.Add(entity);
                db.SaveChanges();

                for (var ci = 0; ci < row.Deltas.Count; ci++)
                {
                    db.GradingDeltas.Add(new GradingDeltaEntity
                    {
                        RowId = entity.Id,
                        ColumnIndex = ci,
                        Delta = row.Deltas[ci],
                    });
                }
            }
        }
    }

    public static void ApplyProfiles(PatternProDbContext db, IEnumerable<MeasurementProfile> profiles)
    {
        db.MeasurementProfileValues.RemoveRange(db.MeasurementProfileValues);
        db.MeasurementProfiles.RemoveRange(db.MeasurementProfiles);

        foreach (var profile in profiles)
        {
            var entity = new MeasurementProfileEntity { Name = profile.Name.Trim() };
            db.MeasurementProfiles.Add(entity);
            db.SaveChanges();

            foreach (var kv in profile.Measurements)
            {
                db.MeasurementProfileValues.Add(new MeasurementProfileValueEntity
                {
                    ProfileId = entity.Id,
                    MeasurementPoint = kv.Key,
                    Value = kv.Value,
                });
            }
        }
    }

    public static void ApplyEaseOverrides(PatternProDbContext db, EaseOverridesStore store)
    {
        db.EaseOverrides.RemoveRange(db.EaseOverrides);
        foreach (var (style, map) in store.OverridesByStyle)
        {
            foreach (var (point, value) in map)
            {
                db.EaseOverrides.Add(new EaseOverrideEntity
                {
                    StyleKey = style,
                    MeasurementPoint = point,
                    Value = value,
                });
            }
        }
    }
}
