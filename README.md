# PatternPro

A technical pattern-making web application for bottom wear (jeans/trousers). Built with ASP.NET Core 8 MVC — no frontend framework, no ORM, no database.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 MVC (.NET 8) |
| Frontend | Vanilla JavaScript, HTML5 Canvas, Razor/cshtml |
| Charts | Chart.js (CDN) |
| PDF Export | PdfSharpCore 1.3.67 |
| Persistence | JSON files (App_Data/) |
| Styling | Custom CSS (no Bootstrap / Tailwind) |

## Projects

| Project | Purpose |
|---|---|
| `Pattern.Core.Model` | Domain models (Pattern, PieceDefinition, SizeRow, etc.) |
| `Pattern.PublicServices` | Business logic — 7 service interfaces + implementations |
| `Pattern.Web.Model` | ViewModels and DTOs for the MVC layer |
| `Pattern.Web` | ASP.NET Core MVC app — controllers, views, JS, CSS |
| `Pattern.Infrastructure` | Reserved (empty) |

## Features

| Module | Description |
|---|---|
| **Dashboard** | Pattern CRUD, status workflow, Chart.js analytics |
| **Size Chart** | Measurement table (XS–XXL), add rows/columns, CSV export |
| **Block Generator** | 5 fit profiles, ease overrides and reset |
| **Grading** | Style-specific delta tables, extrapolation, CSV export |
| **Pattern Pieces** | Dual-level piece hierarchy (style-shared + pattern-owned) |
| **Canvas Editor** | HTML5 canvas drawing, point editing, grain/CF/notches, auto-draft |
| **Graded Nest** | Overlay visualization of XS–XXL graded pieces |
| **Library** | All patterns listing with saved geometry status |
| **Export** | DXF (AutoCAD 2000), SVG, PDF — packaged as ZIP |

## Getting Started

**Prerequisites:** .NET 8 SDK

```bash
git clone <repo-url>
cd PatternMaking/Pattern.Web
dotnet run
```

App runs at **http://localhost:5001**

If port 5001 is already in use (Windows PowerShell):

```powershell
netstat -ano | findstr :5001
taskkill /PID <pid> /F
```

## Project Structure

```
PatternMaking/
├── Pattern.Core.Model/          # Domain models
├── Pattern.PublicServices/      # Services & interfaces
│   └── Services/
│       ├── PatternService.cs
│       ├── SizeChartService.cs
│       ├── GradingService.cs
│       ├── BlockGeneratorService.cs
│       ├── PieceService.cs
│       ├── PatternDraftingService.cs
│       ├── ExportService.cs
│       └── JsonDataStore.cs     # File-based persistence
├── Pattern.Web.Model/           # ViewModels
├── Pattern.Web/                 # MVC Web App
│   ├── Controllers/
│   ├── Views/
│   ├── wwwroot/
│   │   ├── js/                  # dashboard.js, canvas.js, export.js, etc.
│   │   └── css/
│   ├── App_Data/                # JSON persistence files (auto-created)
│   └── Program.cs
└── Pattern.Infrastructure/      # Reserved
```

## Services

| Interface | Implementation | Lifetime |
|---|---|---|
| `IPatternService` | `PatternService` | Singleton |
| `ISizeChartService` | `SizeChartService` | Singleton |
| `IGradingService` | `GradingService` | Singleton |
| `IBlockGeneratorService` | `BlockGeneratorService` | Singleton |
| `IPieceService` | `PieceService` | Singleton |
| `IPatternDraftingService` | `PatternDraftingService` | Singleton |
| `IExportService` | `ExportService` | Singleton |

## Data Persistence

All data is stored as JSON files in `Pattern.Web/App_Data/` (auto-created on first run):

- `patterns.json` — Pattern records
- `pieces.json` — Pattern piece geometry
- `measurement-profiles.json` — Custom body measurement profiles

No database required. Data persists across restarts.

## Fits Supported

- Skinny (9 pre-drawn pieces with full geometry)
- Slim Tapered
- Straight Leg
- Bootcut
- Wide Leg

## Export Formats

| Format | Details |
|---|---|
| **DXF** | AutoCAD R12-style ASCII (AC1009): `LINE` entities on layer 0 — better Illustrator import than LWPOLYLINE |
| **SVG** | Paths + viewBox; UTF-8 without BOM for strict importers |
| **PDF** | One PDF per piece via PdfSharpCore |
| **Package** | All formats delivered as a ZIP with manifest.txt |

## NuGet Dependencies

| Package | Version | Purpose |
|---|---|---|
| PdfSharpCore | 1.3.67 | PDF generation for export |

All other functionality uses built-in .NET 8 libraries only.
