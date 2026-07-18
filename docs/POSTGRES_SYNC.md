# PostgreSQL sync and factory certification

When `ConnectionStrings:Postgres` is set in `Pattern.Web/appsettings.json`, the app uses **PostgreSQL**, not `App_Data` JSON files.

## Sync JSON → PostgreSQL (DN-023 factory ready)

From the repo root:

```bash
dotnet run --project tools/PatternPro.DbTool -- sync
```

This will:

1. Apply EF migrations (`patternpro` schema)
2. If the database has **no patterns** — import all patterns and pieces from `Pattern.Web/App_Data`
3. If patterns already exist — merge **factory certification** for pattern **23** (DN-023 Slim) and update its pieces (grain, seam allowance)

Certify another pattern id (from JSON flags):

```bash
dotnet run --project tools/PatternPro.DbTool -- sync 23 24
```

Mark factory ready in PostgreSQL (approve + cutter pass, no JSON needed):

```bash
dotnet run --project tools/PatternPro.DbTool -- certify-factory 23 24
```

Load real slim template geometry for a pattern (replaces placeholder blocks):

```bash
dotnet run --project tools/PatternPro.DbTool -- seed-style 23 slim
```

Custom App_Data folder:

```bash
dotnet run --project tools/PatternPro.DbTool -- sync "E:\Code\PatternMaking\Pattern.Web\App_Data"
```

## After sync

1. Restart **Pattern.Web** (console should show `Data store: PostgreSQL ...`)
2. Refresh the **Dashboard** — **Factory ready** shows the count of patterns with approval + cutter pass (e.g. DN-022, DN-023)
3. Export: `/Export/Index?patternId=23&style=slim`

## Connection string

Default in `Pattern.Web/appsettings.json`:

```
Host=localhost;Port=5433;Database=patternpro;Username=postgres;Password=...
```

Ensure PostgreSQL is running and migrations have been applied at least once (the DbTool runs them automatically).
