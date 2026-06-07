# PatternPro

Technical pattern-making web application for bottom wear (denim, chinos, trousers). ASP.NET Core 8 MVC with a full data access layer and **factory production certification** before CAM export.

**Primary app:** `Pattern.Web` (run this project). `PatternPro.Web` is kept in sync for the same solution layout.

## Tech stack

| Layer | Technology |
|-------|------------|
| Backend | ASP.NET Core 8 MVC (.NET 8) |
| Frontend | Vanilla JavaScript, HTML5 Canvas, Razor |
| Charts | Chart.js (CDN) |
| PDF export | PdfSharpCore 1.3.67 |
| Persistence | JSON (`App_Data/`) **or** PostgreSQL (schema `patternpro`) |
| ORM | Entity Framework Core 8 + Npgsql |
| Styling | Custom CSS |

## Projects

| Project | Purpose |
|---------|---------|
| `Pattern.Core.Model` | Domain models (Pattern, PieceDefinition, production validation, …) |
| `PatternPro.Core` | Service interfaces (`IServices/`) |
| `Pattern.PublicServices` | Service source (compiled into `PatternPro.Business`) |
| `PatternPro.Business` | Service implementations |
| `PatternPro.DataAccess` | `JsonAppDataStore`, `PostgreSqlAppDataStore`, EF migrations, repositories |
| `Pattern.Web.Model` | ViewModels for MVC |
| `Pattern.Web` | **Main** MVC app — controllers, views, static assets |
| `PatternPro.Web` | Parallel entry point (same features, kept in sync) |
| `PatternPro.Tests` | Unit tests (QC, certification, export gating) |
| `tools/PatternPro.DbTool` | CLI: migrations + sync `App_Data` ↔ PostgreSQL |

## Features

| Module | Description |
|--------|-------------|
| **Dashboard** | Pattern CRUD, status workflow, **Factory ready** stat, production badges, Chart.js analytics |
| **Style Sheet** | PLM register: style code, season, designer, owner, lifecycle (Idea → Sampling → Bulk → Cancelled) |
| **Size Chart** | Measurement table (XS–XXL), CSV export |
| **Block Generator** | Fit profiles, ease overrides |
| **Grading** | Style-specific deltas, extrapolation |
| **Pattern Pieces** | Style-shared + pattern-owned pieces |
| **Canvas Editor** | Draw/edit geometry, draft sizes, grain, notches, save to pattern |
| **Graded Nest** | Graded size overlay |
| **Library** | All patterns + geometry status |
| **Export** | DXF / SVG / PDF ZIP — **factory export gated** by QC + approval + cutter test; CLO review & draft exports |

## Production certification (factory floor)

Factory CAM download requires:

1. **Geometry QC** — required pieces (Front Leg, Back Leg, Waistband); errors block, warnings inform
2. **Design approval** — **Approve for cutting** on Export
3. **Cutter/plotter test** — **Record pass** on real equipment

Until all pass, **Factory export** is disabled in the UI and on the server. **CLO review** (base size) and **Draft** ZIPs are **not** gated.

**Dashboard “Factory ready”** counts patterns where **both** `ApprovedForCutting` and `CutterTestPassed` are true. Downloading a ZIP alone does not increment this count.

Details: [docs/PRODUCTION_CERTIFICATION.md](docs/PRODUCTION_CERTIFICATION.md)  
Full step-by-step: [docs/WORKFLOW.md](docs/WORKFLOW.md)  
Industry mapping (areas 1–8): [docs/INDUSTRY_WORKFLOW.md](docs/INDUSTRY_WORKFLOW.md)

## Getting started

**Prerequisites:** .NET 8 SDK. Optional: PostgreSQL 14+.

```powershell
cd PatternMaking\Pattern.Web
dotnet run
```

Open **http://localhost:5001**

**PatternPro.Web** (alternate entry) uses **http://localhost:5002** so it can run alongside `Pattern.Web`:

```powershell
cd PatternMaking\PatternPro.Web
dotnet run
```

On startup, check the console:

- `Data store: PostgreSQL patternpro @ ...` — using Postgres
- `Data store: JSON files ...` — using `Pattern.Web/App_Data/*.json`

**Second PC?** See [docs/OTHER_PC_SETUP.md](docs/OTHER_PC_SETUP.md) — use **http://localhost:5001**, do **not** add Kestrel HTTPS to `appsettings.json`.

## Data storage

### PostgreSQL (recommended)

Set a connection string in `Pattern.Web/appsettings.json` (and/or `appsettings.Development.json`):

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=patternpro;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

- If `ConnectionStrings:Postgres` is **present**, the app uses **PostgreSQL**.
- There is **no** `UsePostgreSql` flag — presence of the connection string selects the store.
- **Migrations run automatically** on startup when Postgres is configured.
- Adjust **port** (`5433` vs `5432`) to match your local server.

Manual migration (optional):

```powershell
dotnet ef database update --project PatternPro.DataAccess --startup-project Pattern.Web
```

**Sync JSON → PostgreSQL** (after editing `App_Data` or certifying patterns in JSON):

```powershell
cd PatternMaking
dotnet run --project tools/PatternPro.DbTool -- sync
```

Certify a specific pattern id from JSON into Postgres:

```powershell
dotnet run --project tools/PatternPro.DbTool -- certify 23
```

See [docs/POSTGRES_SYNC.md](docs/POSTGRES_SYNC.md).

### JSON-only mode

Remove or comment out `ConnectionStrings:Postgres`. Data is read/written under:

`Pattern.Web/App_Data/` — `patterns.json`, `pieces.json`, size chart, grading, ease overrides, measurement profiles.

## Project structure

```
PatternMaking/
├── Pattern.Core.Model/
├── PatternPro.Core/
├── Pattern.PublicServices/
├── PatternPro.Business/
├── PatternPro.DataAccess/
├── Pattern.Web.Model/
├── Pattern.Web/                  # Main web app
├── PatternPro.Web/
├── PatternPro.Tests/
├── tools/
│   └── PatternPro.DbTool/
└── docs/
    ├── PRODUCTION_CERTIFICATION.md
    └── POSTGRES_SYNC.md
```

## Services (`Program.cs`)

| Interface | Implementation |
|-----------|----------------|
| `IPatternService` | `PatternService` |
| `ISizeChartService` | `SizeChartService` |
| `IGradingService` | `GradingService` |
| `IBlockGeneratorService` | `BlockGeneratorService` |
| `IPieceService` | `PieceService` |
| `IPatternDraftingService` | `PatternDraftingService` |
| `IExportService` | `ExportService` |
| `ISeamValidationService` | `SeamValidationService` |
| `IProductionCertificationService` | `ProductionCertificationService` |

All registered as **singletons**. `PatternService.GetAll()` reloads from the active store so dashboard certification stays in sync with PostgreSQL.

## Persistence

| Store | Location |
|-------|----------|
| JSON | `Pattern.Web/App_Data/` |
| PostgreSQL | Schema `patternpro` — `patterns`, `pieces`, `piece_vertices`, size chart & grading tables, `app_kv` |

Pattern rows include: `ApprovedForCutting`, `ApprovedAt`, `ApprovedBy`, `CutterTestPassed`, `CutterTestedAt`, `CutterTestedBy`, `ShrinkagePercent`, … (migration `AddProductionCertificationColumns`).

## Export

| Format | Notes |
|--------|-------|
| **DXF** | R12-style ASCII — layers CUT, SA, GRAIN, NOTCH |
| **SVG** | Combined per-size layouts |
| **PDF** | One PDF per piece (PdfSharpCore) |
| **ZIP** | Includes `manifest.txt`; factory ZIP includes certification metadata |

Download query parameter `purpose`:

| Value | Gated? |
|-------|--------|
| `factory` (default) | Yes — QC + approval + cutter test |
| `clo` | No — CLO review (base size) |
| `draft` | No — full graded draft |

Example:

`/Export/DownloadPackage?patternId=23&style=slim&format=DXF&purpose=factory`

## Tests

```powershell
dotnet test PatternPro.Tests/PatternPro.Tests.csproj
```

## Fits supported

Skinny, Slim, Straight, Bootcut, Wide Leg

## NuGet (main)

| Package | Purpose |
|---------|---------|
| PdfSharpCore | PDF export |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL |
| Microsoft.EntityFrameworkCore.* | Migrations & design-time |

## Typical workflow (one style)

1. **Dashboard** — create pattern (category, fit, base size)
2. **Size chart** — body measurements
3. **Block generator** / **Grading** — ease and size deltas```````````````````````````````````````````````````````````````````````````````````````````````````````````````````````````````````````````````
4. **Pattern pieces** — list and auto-draft
5. **Canvas** — edit geometry, optional multi-size draft
6. **Export** — QC → approve → cutter pass → **Factory export**
7. **Dashboard** — **Factory ready** count and row badge update

## Troubleshooting

| Issue | Check |
|-------|--------|
| Factory ready stays **0** | Approve + cutter pass on **Export**; restart app; confirm console shows **PostgreSQL** |
| Data not updating after DbTool | Restart app or refresh dashboard (patterns reload from DB) |
| Port 5001 in use | Another app is on 5001 — use **http://localhost:5001** or stop `Pattern.Web.exe`; use **PatternPro.Web** on **5002** instead |
| Wrong pattern on Export | Use `?patternId=` in URL; Canvas session does not override when `patternId` is set |
