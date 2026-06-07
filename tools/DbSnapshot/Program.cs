using Microsoft.EntityFrameworkCore;
using Npgsql;
using PatternPro.DataAccess;
using PatternPro.DataAccess.Persistence;

var connStr =
    Environment.GetEnvironmentVariable("PATTERNPRO_PG")
    ?? "Host=localhost;Port=5433;Database=patternpro;Username=postgres;Password=1234";

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

// Apply migrations + import legacy app_kv.pieces JSON into relational tables
var opts = new DbContextOptionsBuilder<PatternProDbContext>().UseNpgsql(connStr).Options;
await using (var db = new PatternProDbContext(opts))
    await db.Database.MigrateAsync();

var pgStore = new PostgreSqlAppDataStore(new SimpleDbContextFactory(opts));
pgStore.ImportLegacyAppKvIfNeeded();

static async Task<long> Count(NpgsqlConnection c, string sql)
{
    await using var cmd = new NpgsqlCommand(sql, c);
    return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
}

Console.WriteLine("=== PatternPro DB snapshot ===");
Console.WriteLine($"Database: {conn.Database} @ {conn.Host}:{conn.Port}");
Console.WriteLine();

var tables = new (string Label, string Sql)[]
{
    ("patterns", "SELECT COUNT(*) FROM patternpro.patterns"),
    ("pieces", "SELECT COUNT(*) FROM patternpro.pieces"),
    ("piece_vertices", "SELECT COUNT(*) FROM patternpro.piece_vertices"),
    ("app_kv keys", "SELECT COUNT(*) FROM patternpro.app_kv"),
    ("size_chart_rows", "SELECT COUNT(*) FROM patternpro.size_chart_rows"),
    ("grading_styles", "SELECT COUNT(*) FROM patternpro.grading_styles"),
    ("measurement_profiles", "SELECT COUNT(*) FROM patternpro.measurement_profiles"),
    ("ease_overrides", "SELECT COUNT(*) FROM patternpro.ease_overrides"),
};

foreach (var (label, sql) in tables)
    Console.WriteLine($"{label,-22} {await Count(conn, sql)}");

Console.WriteLine();
Console.WriteLine("--- Recent patterns ---");
await using (var cmd = new NpgsqlCommand(
    "SELECT \"Id\", \"Code\", \"Name\", \"Style\", \"Status\" FROM patternpro.patterns ORDER BY \"Id\" DESC LIMIT 10", conn))
await using (var r = await cmd.ExecuteReaderAsync())
{
    while (await r.ReadAsync())
        Console.WriteLine($"  {r.GetInt32(0)}  {r.GetString(1)}  {r.GetString(2)}  style={r.GetString(3)}  {r.GetString(4)}");
}

Console.WriteLine();
Console.WriteLine("--- app_kv ---");
await using (var cmd = new NpgsqlCommand("SELECT \"Key\", LEFT(\"Value\", 60) FROM patternpro.app_kv ORDER BY \"Key\"", conn))
await using (var r = await cmd.ExecuteReaderAsync())
{
    while (await r.ReadAsync())
        Console.WriteLine($"  {r.GetString(0)}  {(r.IsDBNull(1) ? "" : r.GetString(1))}");
}

sealed class SimpleDbContextFactory(DbContextOptions<PatternProDbContext> options)
    : IDbContextFactory<PatternProDbContext>
{
    public PatternProDbContext CreateDbContext() => new(options);
}
