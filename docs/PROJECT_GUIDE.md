# PatternPro — Complete Project Guide

**For:** managers, pattern designers, factory floor staff, and developers  
**Product:** Technical pattern-making for **bottom wear** (denim, chinos, trousers)  
**Goal:** Take a style from idea → graded pattern → **factory-certified cutter files** (DXF / HPGL / PLT)

---

## Table of contents

1. [What is PatternPro?](#1-what-is-patternpro)
2. [Who uses what](#2-who-uses-what)
3. [Quick start (5 minutes)](#3-quick-start-5-minutes)
4. [Full workflow — first to last](#4-full-workflow--first-to-last)
5. [Every screen explained](#5-every-screen-explained)
6. [Factory certification](#6-factory-certification)
7. [Export formats for cutters](#7-export-formats-for-cutters)
8. [Data storage and team setup](#8-data-storage-and-team-setup)
9. [For developers — architecture](#9-for-developers--architecture)
10. [Glossary](#10-glossary)
11. [Troubleshooting](#11-troubleshooting)
12. [Related documents](#12-related-documents)

---

## 1. What is PatternPro?

PatternPro is a **local web application** that replaces a scattered spreadsheet + CAD workflow with one connected system:

| Without PatternPro | With PatternPro |
|--------------------|-----------------|
| Size chart in Excel | Shared **Size Chart** in the app |
| Block rules in notes | **Block Generator** with saved ease |
| Pieces drawn in separate files | **Canvas** editor per style |
| “Is it ready for the cutter?” unclear | **Production QC** + approval + cutter test |
| Files emailed as attachments | **Factory export** ZIP with certification |

### What it makes

- **Garment type:** Bottom wear only — skinny, slim, straight, bootcut, wide leg
- **Sizes:** XS, S, M, L, XL, XXL (graded from a base size, usually M)
- **Output:** ZIP files containing cutter-ready geometry per size

### What it does not do (today)

- Tops, dresses, jackets, or other categories
- Direct USB/network send to a plotter (you download files and load them in your cutter software)
- Full enterprise PLM/ERP (it has a lightweight **Style Sheet**, not a full merchandising system)

---

## 2. Who uses what

### Manager / boss

| Need | Where to look |
|------|----------------|
| How many styles are factory-ready? | **Dashboard** → **Factory ready** stat |
| Which styles are stuck? | Dashboard pattern table → badges: QC pending / Approved / Factory ready |
| Season, owner, lifecycle | **Style Sheet** |
| Is bulk production allowed? | Style Sheet → **Bulk** lifecycle (only when pattern is certified) |

### Pattern designer / technical team

| Task | Screens |
|------|---------|
| Create a new style | Dashboard → **+ New style** |
| Enter measurements | **Size Chart** |
| Set fit and ease | **Block Generator** |
| Edit grade rules | **Grading** |
| Draw and save pieces | **Canvas** |
| Check all sizes visually | **Graded Nest** (optional) |
| Release to factory | **Export** |

### Factory floor / cutter operator

| Task | Screens |
|------|---------|
| Run trial cut on plotter | Physical machine (outside app) |
| Confirm trial passed | **Export** → **Record pass** |
| Download cutter files | **Export** → pick **DXF**, **HPGL**, or **PLT** → **Factory export** |
| Read certification | Open ZIP → `manifest.txt` and `certification.json` |

### Developer / IT

| Task | Reference |
|------|-----------|
| Run locally | [Quick start](#3-quick-start-5-minutes) |
| PostgreSQL + sync | [POSTGRES_SYNC.md](POSTGRES_SYNC.md) |
| Second PC clone | [OTHER_PC_SETUP.md](OTHER_PC_SETUP.md) |
| Code structure | [Architecture](#9-for-developers--architecture) |

---

## 3. Quick start (5 minutes)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Optional: PostgreSQL 14+ (recommended for teams)

### Run the app

```powershell
cd PatternMaking\Pattern.Web
dotnet run
```

Open **http://localhost:5001**

On startup, the console shows which data store is active:

- `Data store: PostgreSQL patternpro @ ...` — team database
- `Data store: JSON files ...` — local files in `Pattern.Web/App_Data/`

### First pattern in 5 steps

1. **Dashboard** → **+ New style** → pick fit (e.g. Slim), base size M
2. **Size Chart** → confirm body measurements
3. **Block Generator** → **Generate for pattern** (drafts pieces)
4. **Canvas** → review/edit → **Save**
5. **Export** → complete QC → approve → cutter pass → download ZIP

---

## 4. Full workflow — first to last

This is the **complete path** for one bottom-wear style (e.g. DN-023 Slim Tapered).

```
┌─────────────┐    ┌──────────────┐    ┌─────────────────┐    ┌────────────┐
│ Style Sheet │ →  │  Size Chart  │ →  │ Block Generator │ →  │  Grading   │
│  (register) │    │  (measure)   │    │  (ease/block)   │    │  (deltas)  │
└─────────────┘    └──────────────┘    └─────────────────┘    └────────────┘
                                                                      │
┌─────────────┐    ┌──────────────┐    ┌─────────────────┐           │
│  Dashboard  │ ←  │    Export    │ ←  │     Canvas      │ ←  ┌──────┴──────┐
│ (visibility)│    │ (factory ZIP)│    │  (edit/save)    │    │Pattern Pieces│
└─────────────┘    └──────────────┘    └─────────────────┘    └─────────────┘
```

### Step 0 — Before the app

| # | Action |
|---|--------|
| 0.1 | Install .NET 8 SDK |
| 0.2 | Clone/copy the repo |
| 0.3 | Optional: configure PostgreSQL (see [Section 8](#8-data-storage-and-team-setup)) |
| 0.4 | Run `dotnet run` in `Pattern.Web` |
| 0.5 | Confirm console data store message |

### Step 1 — Register the style (PLM)

**Screen:** Style Sheet (`/StyleSheet`) or Dashboard **+ New style**

| Field | Example | Notes |
|-------|---------|-------|
| Style code | `DN-023` | Auto-generated from type + ID |
| Style name | `Slim Tapered` | Product name |
| Season | `SS26` | Collection |
| Designer | `A. Khan` | Technical designer |
| Owner | `Merch team` | Business owner |
| Lifecycle | Idea → Sampling → Bulk | Merchandising gate |
| Fit | Slim | Determines piece list and block rules |
| Base size | M | Canvas master size for grading |

**Two statuses to understand:**

- **Lifecycle** (PLM) — Idea / Sampling / Bulk / Cancelled — on Style Sheet
- **Pattern status** (technical) — Pending / Draft / In Progress / Graded / Done — on Dashboard

They are independent. A style can be **Sampling** in PLM while the pattern is still **In Progress**.

### Step 2 — Size chart (body measurements)

**Screen:** Size Chart (`/SizeChart`)

- Enter **Points of Measure (POM)** for XS through XXL
- Set **tolerance** (± cm) and **measurement method** per row
- Export CSV for merchandising if needed

**Output:** Shared measurement table used by block generation and grading.

### Step 3 — Block generator (fit + ease)

**Screen:** Block Generator (`/BlockGenerator?style=slim`)

- Review default **ease** per measurement (waist, hip, thigh, etc.)
- Override ease values if needed (saved per fit)
- Click **Generate for pattern** to draft all pieces from size chart + ease

**Output:** Piece geometry written to the pattern, ready for Canvas.

### Step 4 — Grading (size deltas)

**Screen:** Grading (`/Grading?style=slim`)

- Review grade **deltas** from base size M to other sizes
- Edit individual deltas if the standard grade is wrong for this style

**Output:** Grade rules used when export creates files for each size.

### Step 5 — Pattern pieces (checklist)

**Screen:** Pattern Pieces (`/Pieces?patternId=23&style=slim`)

- See the piece list for this fit (e.g. Front Leg, Back Leg, Waistband, pockets, fly, etc.)
- **Auto-draft** if pieces are missing
- Open **Canvas** for each piece

**Required pieces for factory QC:** Front Leg, Back Leg, Waistband.

### Step 6 — Canvas (edit and save)

**Screen:** Canvas (`/Canvas?patternId=23&style=slim`)

| Action | Purpose |
|--------|---------|
| Move vertices | Shape the pattern outline |
| Set grain line | Sewing direction for factory |
| Add notches | Match pieces during sewing |
| Set seam allowance | Cut line vs stitch line |
| Save | Persist geometry (required before export) |

Optional: run **Validate factory** from Canvas to check QC without leaving the editor.

### Step 7 — Graded nest (optional visual check)

**Screen:** Graded Nest (`/Nest?style=slim`)

- Overlays all graded sizes on one view
- Confidence check before export — not required for certification

### Step 8 — Library (optional overview)

**Screen:** Library (`/Library`)

- Browse all patterns and geometry status across the team

### Step 9 — Export (factory release)

**Screen:** Export (`/Export?patternId=23&style=slim`)

See [Section 6](#6-factory-certification) and [Section 7](#7-export-formats-for-cutters) for detail.

**Sub-steps:**

1. Page loads → geometry QC runs automatically
2. Fix **red blocking** issues on Canvas
3. **Approve for cutting** (design sign-off)
4. Run trial on plotter/cutter → **Record pass**
5. Optional: set **Shrinkage %**
6. Select format: **DXF**, **HPGL**, or **PLT**
7. Click **↓ Factory export** → download ZIP

**Not gated (available anytime):**

- **CLO review ZIP** — base size only, for 3D drape review
- **Draft ZIP** — full graded set, internal use

### Step 10 — Dashboard (team visibility)

**Screen:** Dashboard (`/`)

- Pattern row shows badge: **QC pending** / **Approved** / **Factory ready**
- **Factory ready** count = patterns with both approval + cutter test passed
- Move Style Sheet lifecycle to **Bulk** when certified and ready for production

### Done checklist

| Goal | Done when |
|------|-----------|
| Design complete | Pieces saved on Canvas; pattern status Graded/Done |
| Factory release | QC clean + approved + cutter pass + factory ZIP downloaded |
| Team visibility | Dashboard **Factory ready** includes your pattern |
| Bulk production | Style Sheet lifecycle = Bulk |

---

## 5. Every screen explained

### Navigation (sidebar)

| Menu item | Route | Purpose |
|-----------|-------|---------|
| Dashboard | `/` | Pattern list, stats, create new style |
| Style Sheet | `/StyleSheet` | PLM register: season, owner, lifecycle |
| Size Chart | `/SizeChart` | Body measurement table |
| Block Generator | `/BlockGenerator` | Fit ease rules, generate block |
| Grading | `/Grading` | Size delta table |
| Pattern Pieces | `/Pieces` | Piece checklist per pattern |
| Library | `/Library` | All patterns overview |
| Canvas Editor | `/Canvas` | Draw/edit geometry |
| Graded Nest | `/Nest` | Multi-size overlay view |
| Export / DXF | `/Export` | QC, certification, download |

### Fits and piece lists

| Fit | Pieces (typical) |
|-----|------------------|
| Skinny | 9 — includes coin pocket, front pocket bag |
| Slim | 9 — same as skinny |
| Straight | 8 — side pocket bag, no coin pocket |
| Bootcut | 9 — adds flare insert |
| Wide leg | 8 — adds waist tab |

Every fit includes **Front Leg**, **Back Leg**, and **Waistband** (required for factory QC).

### Dashboard stats

| Stat | Meaning |
|------|---------|
| Total patterns | All registered styles |
| Factory ready | Count where `ApprovedForCutting` AND `CutterTestPassed` are true |
| Pending | Patterns not yet complete |
| Recent activity | Latest changes from your pattern data |

---

## 6. Factory certification

Factory export is **intentionally gated**. This prevents uncertified geometry from reaching the cutting room.

### Certification flow

```
Canvas (save pieces)
        ↓
   Geometry QC ──────────→ CLO review ZIP (anytime, base size)
        ↓
 Design approval (Approve for cutting)
        ↓
 Cutter/plotter test (Record pass)
        ↓
 Factory export ZIP ─────→ Draft ZIP (anytime, all sizes)
```

### Gates (all must pass for factory export)

| Gate | What it checks | Blocks if |
|------|----------------|-----------|
| Saved pattern | `patternId > 0` | No pattern selected |
| Geometry QC | Required pieces, valid polygons | Missing Front/Back Leg or Waistband; invalid geometry |
| Design approval | `ApprovedForCutting` flag | Not approved |
| Cutter test | `CutterTestPassed` flag | Trial not recorded as passed |

### QC blocking errors (red)

| Code | Meaning | Fix |
|------|---------|-----|
| `NO_PIECES` | Nothing saved | Save on Canvas |
| `MISSING_PIECE` | Required piece absent | Add/draft Front Leg, Back Leg, or Waistband |
| `INVALID_PIECE` | Fewer than 3 points | Redraw piece on Canvas |
| `NOT_APPROVED` | No design sign-off | Click **Approve for cutting** |
| `CUTTER_TEST` | No cutter trial recorded | Click **Record pass** after machine trial |

### QC warnings (yellow — do not block export)

| Code | Meaning |
|------|---------|
| `NO_GRAIN` | Grain line missing on a required piece |
| `SEAM_LENGTH` | Matching seam edges differ by more than 0.75 cm |
| `WAIST_BALANCE` | Waist attach length vs waistband edge mismatch |
| `NO_SA` | No seam allowance on a cut piece |

### Export page buttons

| Button | Action |
|--------|--------|
| Approve for cutting | Design sign-off (blocked if geometry QC has errors) |
| Revoke | Remove approval |
| Record pass / Record fail | Log cutter trial result |
| Shrinkage % | Save shrinkage allowance on pattern |
| ↓ Factory export | Download certified ZIP (gated) |
| CLO review ZIP | Base-size package for 3D review (not gated) |
| Draft ZIP | Full graded package for internal use (not gated) |

### Factory ZIP contents

| File | Contents |
|------|----------|
| `canvas/{style}_{size}.dxf` (or `.hpgl` / `.plt`) | One file per size |
| `manifest.txt` | Pattern id, sizes, certification flags, shrinkage |
| `certification.json` | Approver, cutter test, QC issues (factory only) |
| `README` (plotter) | Instructions for cutter operators |

### Server enforcement

The UI gate is not the only protection. `ExportService` calls `ValidateForFactory` on the server and **throws an error** if factory export is not allowed. Do not bypass this with manual URL tricks in production.

---

## 7. Export formats for cutters

Choose the format on the **Export** page before downloading.

### DXF (recommended for CAM software)

| Property | Value |
|----------|-------|
| Extension | `.dxf` |
| Standard | AutoCAD R12-style ASCII |
| Units | **Millimeters** (`$INSUNITS=4`) |
| Layers | `CUT`, `SA`, `GRAIN`, `NOTCH` |
| Best for | Gerber, Lectra, Optitex, AutoCAD-compatible CAM |

### HPGL (plotter language)

| Property | Value |
|----------|-------|
| Extension | `.hpgl` |
| Standard | Hewlett-Packard Graphics Language |
| Units | 1016 plotter units per inch (40 units/mm) |
| Pens | SP1=CUT, SP2=SA, SP3=GRAIN, SP4=NOTCH |
| Best for | HPGL-compatible plotters and many CAM cutters |

### PLT (HPGL with .plt extension)

| Property | Value |
|----------|-------|
| Extension | `.plt` |
| Content | Same HPGL command stream as above |
| Difference | File extension only — some legacy cutter drivers expect `.plt` |
| Best for | Older plotter drivers, shop-floor tools that import `.plt` |

### Download URL format

```
/Export/DownloadPackage?patternId=23&style=slim&format=PLT&purpose=factory
```

| Parameter | Values |
|-----------|--------|
| `patternId` | Pattern database ID |
| `style` | `skinny`, `slim`, `straight`, `bootcut`, `wideLeg` |
| `format` | `DXF`, `HPGL`, `PLT` |
| `purpose` | `factory` (gated), `clo` (base size), `draft` (all sizes) |

### Workflow for cutter operators

1. Designer completes certification on Export page
2. Operator downloads factory ZIP
3. Extract files from ZIP
4. Import `.dxf`, `.hpgl`, or `.plt` into your cutter software
5. Run trial cut → designer or operator records **pass** in the app
6. Production cuts use the same certified files

**Note:** PatternPro does not send files directly to the machine. Your cutter software handles the final import.

---

## 8. Data storage and team setup

### JSON mode (single user / demo)

- Remove or comment out `ConnectionStrings:Postgres` in `appsettings.json`
- Data stored in `Pattern.Web/App_Data/*.json`
- Good for: one designer, quick trials, no database install

### PostgreSQL mode (team / factory)

- Set connection string in `Pattern.Web/appsettings.json` or `appsettings.Development.json`
- Schema: `patternpro`
- Migrations run automatically on startup
- Good for: multiple PCs, shared patterns, persistent certification flags

Example connection string:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=patternpro;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

### Syncing data between machines

```powershell
# From repo root — import JSON → PostgreSQL, merge certification
dotnet run --project tools/PatternPro.DbTool -- sync

# Mark patterns factory-ready in the database
dotnet run --project tools/PatternPro.DbTool -- certify-factory 22 23 24
```

After sync: **restart the app** and refresh Dashboard.

### Second PC setup

See [OTHER_PC_SETUP.md](OTHER_PC_SETUP.md) for:

- Branch to use (`arch/patternpro-phase1`)
- Local `appsettings.Development.json` per machine
- Git pull issues with `bin/` / `obj/`
- Use **http://localhost:5001** (not HTTPS in development)

### PatternPro.Web (alternate entry)

| App | URL | Notes |
|-----|-----|-------|
| `Pattern.Web` | http://localhost:5001 | **Primary** — full factory certification |
| `PatternPro.Web` | http://localhost:5002 | Parallel entry, same features |

---

## 9. For developers — architecture

### Solution structure

```
PatternMaking/
├── Pattern.Core.Model/          Domain models (Pattern, PieceDefinition, QC)
├── PatternPro.Core/             Service interfaces (IServices/)
├── Pattern.PublicServices/      Service implementations (compiled into Business)
├── PatternPro.Business/         Business layer project
├── PatternPro.DataAccess/       JSON + PostgreSQL stores, EF migrations
├── Pattern.Web.Model/           MVC ViewModels
├── Pattern.Web/                 Main MVC app (controllers, views, static assets)
├── PatternPro.Web/              Parallel entry point
├── PatternPro.Tests/              Unit tests (QC, certification, export)
└── tools/PatternPro.DbTool/     CLI: migrations, sync, certify
```

### Registered services (`Pattern.Web/Program.cs`)

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

All services are **singletons**. `PatternService.GetAll()` reloads from the active store so dashboard certification stays in sync with PostgreSQL.

### Key API endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/Export/ValidateFactory` | QC report JSON |
| POST | `/Export/ApproveForCutting` | Design approval |
| POST | `/Export/RevokeApproval` | Revoke approval |
| POST | `/Export/RecordCutterTest` | Log cutter trial |
| POST | `/Export/SetShrinkage` | Save shrinkage % |
| GET | `/Export/DownloadPackage` | Download ZIP |
| POST | `/SizeChart/UpdateCell` | Edit measurement cell |
| POST | `/Grading/UpdateDelta` | Edit grade delta |
| POST | `/BlockGenerator/GenerateBlockForPattern` | Draft pieces for pattern |
| GET | `/Canvas/ValidateFactory` | QC from canvas |

### Database columns (certification)

On `patternpro.patterns`:

- `ApprovedForCutting`, `ApprovedAt`, `ApprovedBy`
- `CutterTestPassed`, `CutterTestedAt`, `CutterTestedBy`, `CutterTestNotes`
- `ShrinkagePercent`
- `CloReviewCompleted`, `CloReviewNotes`

### Running tests

```powershell
dotnet test PatternPro.Tests/PatternPro.Tests.csproj
```

Tests cover: seam validation, production certification, export gating.

### Tech stack

| Layer | Technology |
|-------|------------|
| Backend | ASP.NET Core 8 MVC (.NET 8) |
| Frontend | Vanilla JavaScript, HTML5 Canvas, Razor |
| Charts | Chart.js |
| Persistence | JSON (`App_Data/`) or PostgreSQL (`patternpro` schema) |
| ORM | Entity Framework Core 8 + Npgsql |

---

## 10. Glossary

| Term | Meaning |
|------|---------|
| **POM** | Point of Measure — a body measurement row on the size chart |
| **Block** | Base pattern shape before style-specific design changes |
| **Ease** | Extra cm added to body measurements for fit and comfort |
| **Grade / grading** | Size-to-size deltas (how measurements change from M to L, etc.) |
| **Piece** | One pattern part (e.g. Front Leg) |
| **Grain** | Fabric direction line — critical for sewing |
| **Notch** | Small mark to align pieces during sewing |
| **Seam allowance (SA)** | Extra width beyond the stitch line for cutting |
| **QC** | Quality check — geometry validation before factory release |
| **Factory ready** | Pattern approved for cutting AND cutter test passed |
| **CAM** | Computer-aided manufacturing — cutter/plotter software |
| **HPGL** | Plotter command language used by many cutters |
| **PLT** | File extension for HPGL streams on some cutter drivers |
| **CLO** | CLO3D — 3D garment simulation (review export only) |
| **Lifecycle** | PLM status: Idea → Sampling → Bulk → Cancelled |
| **Pattern status** | Technical workflow: Pending → Draft → In Progress → Graded → Done |

---

## 11. Troubleshooting

| Problem | Likely cause | Fix |
|---------|--------------|-----|
| Factory ready stays **0** | Approval or cutter pass not saved | Complete both on Export; restart app; confirm PostgreSQL |
| Factory export button disabled | QC errors or missing certification | Fix red issues; approve; record cutter pass |
| Wrong pattern on Export | URL missing `patternId` | Use `/Export?patternId=23&style=slim` |
| Data not updating after DbTool | App cache / wrong store | Restart app; check console for PostgreSQL |
| Port 5001 in use | Another instance running | `taskkill /IM Pattern.Web.exe /F` |
| Postgres connection error | Server not running / wrong password | Start Postgres; fix `appsettings.Development.json` |
| Pull blocked by bin/obj | Build outputs tracked locally | `git restore .` then `git clean -fd` then pull |
| Missing pieces in ZIP | Pattern not saved on Canvas | Open Canvas → Save all pieces |
| Cutter file won't open | Wrong format for your software | Try DXF for CAM, PLT/HPGL for plotters |

---

## 12. Related documents

| Document | Contents |
|----------|----------|
| [WORKFLOW.md](WORKFLOW.md) | Short step-by-step workflow |
| [INDUSTRY_WORKFLOW.md](INDUSTRY_WORKFLOW.md) | How app maps to garment industry areas 1–8 |
| [PRODUCTION_CERTIFICATION.md](PRODUCTION_CERTIFICATION.md) | Certification gates, API, database columns |
| [STYLE_SHEET.md](STYLE_SHEET.md) | PLM register fields and lifecycle |
| [POSTGRES_SYNC.md](POSTGRES_SYNC.md) | DbTool sync and certify commands |
| [OTHER_PC_SETUP.md](OTHER_PC_SETUP.md) | Clone repo on a second computer |
| [ADMIN_PANEL.md](ADMIN_PANEL.md) | Login, roles, user management |
| [README.md](../README.md) | Technical overview and project structure |

---

## One-page summary (share with your team)

**PatternPro** is our in-house pattern system for **trousers and denim**. One style goes through: **register → size chart → block → grading → canvas → export**. Factory files are **not released** until geometry passes QC, a designer **approves for cutting**, and the **cutter trial passes**. Download **DXF**, **HPGL**, or **PLT** from the Export page and load into cutter software. Managers track progress on the **Dashboard** (**Factory ready** count). Developers run `dotnet run` in `Pattern.Web` at **http://localhost:5001**.
