# Developer testing guide

How to verify PatternPro is working correctly — automated tests, QA smoke script, local run, and full manual checklist.

---

## Quick QA run (recommended)

With the app running on http://localhost:5001:

```powershell
cd E:\Code\PatternMaking

# 1) Unit tests (business rules)
dotnet test PatternPro.Tests/PatternPro.Tests.csproj

# 2) HTTP smoke test (auth, pages, pattern, draft export)
powershell -ExecutionPolicy Bypass -File tools/qa-smoke-test.ps1

# 3) Full end-to-end workflow (factory QC → certify → factory ZIP)
powershell -ExecutionPolicy Bypass -File tools/qa-full-e2e.ps1
```

**Pass criteria:**

| Suite | Expected |
|-------|----------|
| Unit tests | `Passed: 21` (0 failed) |
| Smoke script | `Passed: 28` — ends with `All automated QA checks passed.` |
| Full E2E script | `Passed: 30` — ends with `FULL E2E TEST PASSED` |

The smoke script covers: login redirect, all main pages, size chart (M waist 84 cm), create pattern, draft pieces, canvas JSON, factory QC gate, draft export (PLT/DXF/HPGL/PDF), logout.

The full E2E script runs the **complete product path**: login → admin/user panels → size chart → create pattern → draft pieces → canvas (required piece names) → nest/library/style sheet → draft export → factory QC → approve → cutter pass → factory ZIP → dashboard → logout.

---

## Level 1 — Build & unit tests (start here)

From repo root:

```powershell
cd E:\Code\PatternMaking

# 1) Clean build
dotnet build Pattern.Web/Pattern.Web.csproj

# 2) Run all automated tests (21 tests)
dotnet test PatternPro.Tests/PatternPro.Tests.csproj
```

**Pass criteria:** `Build succeeded` and `Passed: 21` (0 failed).

### What the unit tests cover

| Test file | What it checks |
|-----------|----------------|
| `SeamValidationServiceTests` | Required pieces, empty geometry, QC errors |
| `ProductionCertificationServiceTests` | Approve, certify, factory-ready logic |
| `ExportServiceFactoryGateTests` | Factory export blocked when not certified; draft export allowed |
| `UserServiceTests` | Login (username or employee ID), registration gate, admin approval, disabled users |
| `PatternAutoRefineServiceTests` | Auto-refine seam allowance, waistband balance, production draft |

These tests do **not** hit the browser or database. They validate core business rules.

---

## Level 2 — Run the app locally

```powershell
# Stop old instance if port is busy
taskkill /IM Pattern.Web.exe /F 2>$null

cd Pattern.Web
dotnet run
```

Open **http://localhost:5001**

### Console checks (first 10 seconds)

| Console message | Meaning |
|-----------------|---------|
| `Data store: PostgreSQL patternpro @ ...` | Using Postgres |
| `Data store: JSON files ...` | Using `App_Data/*.json` |
| `Auth: seed admin 'admin' ensured` | First admin created (only if no users exist) |

**Pass criteria:** App starts with no exception; login page loads.

---

## Level 3 — Manual end-to-end checklist

Use this to test the **full product** like a user would.

### A. Authentication

| # | Step | Expected |
|---|------|----------|
| 1 | Open http://localhost:5001 without login | Redirect to `/Account/Login` |
| 2 | Login `admin` / `Admin@123` (employee ID `ADMIN`) | Dashboard loads; admin sees **Users & permissions** |
| 3 | Open `/Admin` | Admin panel — create users, approve pending |
| 4 | Open `/User` | User profile panel |
| 5 | Sidebar shows your name + **Sign out** | User info visible |
| 6 | Click **Sign out** | Back to login page |
| 7 | `/Account/Register` when `RegistrationEnabled: false` | Redirect to login |
| 8 | Login as **View only** user (create in Admin) | Can view pages; saving blocked |
| 9 | Admin → **Disable** a test user → try login | Login fails |

### B. Pattern workflow

| # | Step | Expected |
|---|------|----------|
| 1 | Dashboard → **+ New style** → Slim, base M | New row (e.g. DN-0xx) |
| 2 | Size Chart → edit a cell → refresh | Value saved |
| 3 | Block Generator → **Generate for pattern** | Pieces drafted |
| 4 | Pattern Pieces → open Canvas | Geometry visible |
| 5 | Canvas → move a point → **Save** | Save succeeds |
| 6 | Graded Nest | All sizes overlay |
| 7 | Library | Pattern listed with geometry |

### C. Export — DXF / PLT / HPGL

Pick one pattern id (e.g. `23`) and style (`slim`).

| # | Step | Expected |
|---|------|----------|
| 1 | `/Export?patternId=23&style=slim` | Export page loads; QC runs |
| 2 | Click **PLT** (or DXF / HPGL) | Format tag updates |
| 3 | Click **Draft ZIP** (no certification needed) | ZIP downloads |
| 4 | Unzip → `canvas/slim_M.plt` (or `.dxf` / `.hpgl`) | File exists, not empty |
| 5 | Open file in text editor | DXF has `CUT` layer; PLT/HPGL has `IN;` and `SP1;` |
| 6 | Fix QC errors → **Approve** → **Record pass** | Factory export enabled |
| 7 | **↓ Factory export** | ZIP + `certification.json` + `manifest.txt` |
| 8 | Dashboard | **Factory ready** count increases |

### D. Factory export gate (must block when wrong)

| # | Step | Expected |
|---|------|----------|
| 1 | New pattern, no canvas save → Export | QC red errors; factory button disabled |
| 2 | Direct URL factory download without certify | Error / forbidden (not a valid ZIP) |
| 3 | Draft export on same pattern | ZIP downloads (not gated) |

### E. Admin panel

| # | Step | Expected |
|---|------|----------|
| 1 | Admin → **+ New user** | User created |
| 2 | Login as new user | Works with assigned role |
| 3 | Non-admin user | No **Users & permissions** in sidebar |

---

## Module-wise testing (test one area at a time)

Start the app first:

```powershell
taskkill /IM Pattern.Web.exe /F 2>$null
cd E:\Code\PatternMaking\Pattern.Web
dotnet run
```

Open **http://localhost:5001**. Run each module below in order. Tick each when pass.

### Module 0 — Build & unit tests (no browser)

```powershell
cd E:\Code\PatternMaking
dotnet build Pattern.Web/Pattern.Web.csproj
dotnet test PatternPro.Tests/PatternPro.Tests.csproj
```

Run **one test file** at a time:

```powershell
dotnet test --filter "FullyQualifiedName~SeamValidationServiceTests"
dotnet test --filter "FullyQualifiedName~ProductionCertificationServiceTests"
dotnet test --filter "FullyQualifiedName~ExportServiceFactoryGateTests"
dotnet test --filter "FullyQualifiedName~UserServiceTests"
dotnet test --filter "FullyQualifiedName~PatternAutoRefineServiceTests"
```

| Pass when |
|-----------|
| Each filter: `Passed: N`, `Failed: 0` |

---

### Module 1 — Authentication & users

| URL | What to test |
|-----|----------------|
| `/Account/Login` | Page loads |
| `/` (logged out) | Redirects to login |
| Login `admin` / `Admin@123` | Dashboard loads |
| `/Admin` | User list, create user |
| `/User` | Profile, change password |
| `/Account/Register` | Redirects (registration closed) |
| Sign out | Back to login |

**Automated:** E2E sections **A** + **H** in `tools/qa-full-e2e.ps1`

**Manual only:** Create viewer user → login → confirm save blocked on Canvas.

---

### Module 2 — Dashboard & Style Sheet

| URL | What to test |
|-----|----------------|
| `/` | Pattern list, charts, Factory ready stat |
| `/Home/ChartsData` | JSON loads |
| **+ New style** modal | Create Denim / Slim / M |
| `/StyleSheet` | PLM fields: season, owner, lifecycle |

**Automated (full dashboard module):**

```powershell
powershell -ExecutionPolicy Bypass -File tools/qa-dashboard-test.ps1
```

Covers: page shell, stats, charts API, create/search/sort, set status, cycle status, lifecycle, style sheet fields, due date, duplicate, delete, factory-ready count.

Also in smoke/E2E: Dashboard page, charts API (**G1**, **G2**), create pattern (**C1**).

**Manual only (browser):**

| # | Step | Expected |
|---|------|----------|
| 1 | Open `/` after login | Three charts render (status donut, fit stacked bar, pant types) |
| 2 | Click a **category tab** (e.g. Denim) | Table filters to that product line |
| 3 | Type in **Search patterns…** | Rows filter live |
| 4 | Click **+ Add** or **＋ Pattern** | New-style modal opens; create Denim / Slim / M |
| 5 | Click a **status slice** on the donut chart | Table filters to that status; **All statuses** clears filter |
| 6 | Select a row → **⊕ Duplicate Pattern** (header) | Copy appears as Draft |
| 7 | Change **status** dropdown on a row | Toast + row updates |
| 8 | Set **due date** on a row | Date saves; due-this-week strip updates if in current week |
| 9 | `/StyleSheet` | Edit season / owner / lifecycle on a row → saves |

---

### Module 2b — Style Sheet (PLM register)

| URL | What to test |
|-----|----------------|
| `/StyleSheet` | PLM table: code, season, designer, owner, lifecycle |
| `/StyleSheet/Rows` | JSON list, search, sort |
| `/Home/UpdateStyleSheet` | Save season, owner, designer |
| `/Home/SetLifecycle` | Idea → Sampling → Bulk (when certified) / Cancelled |

**Automated (full Style Sheet module):**

```powershell
powershell -ExecutionPolicy Bypass -File tools/qa-style-sheet-test.ps1
```

Covers: page shell, Rows API, create with PLM fields, update fields, lifecycle transitions, Bulk gate (blocked until approve + cutter pass), invalid lifecycle rejected, Pieces link, cleanup.

**Manual only (browser):**

| # | Step | Expected |
|---|------|----------|
| 1 | Sidebar → **Style Sheet** | Page loads; lifecycle legend visible |
| 2 | Click **Sampling** tab | Rows filter to Sampling lifecycle |
| 3 | **All lifecycles** button | Clears filter |
| 4 | Search `DN-` | Rows filter by code |
| 5 | Edit **Season** on a row → blur/tab away | Toast “Style sheet saved” |
| 6 | Change **Lifecycle** dropdown | Toast “Lifecycle updated” |
| 7 | Try **Bulk** on uncertified style | Error toast; stays previous lifecycle |
| 8 | **+ New style** | Same modal as Dashboard; set season + owner |
| 9 | **Pattern** link on a row | Opens Pattern Pieces for that style |

**Pass when:** PLM fields persist after refresh; Bulk only allowed after factory certification + Graded/Done status.

---

### Module 3 — Size Chart

| URL | What to test |
|-----|----------------|
| `/SizeChart` | Table loads (XS–XXL) |
| Edit waist cell for M | Save → refresh → still there |
| `/SizeChart/ExportCsv` | CSV downloads |
| `POST /SizeChart/AddColumn` | Add size (extrapolated values) |
| `POST /SizeChart/AddRow` | Add measurement row |
| `POST /SizeChart/UpdateCell` | Edit cell value |

**Automated (full Size Chart module):**

```powershell
powershell -ExecutionPolicy Bypass -File tools/qa-size-chart-test.ps1
```

Covers: page shell, XS–XXL columns, M waist = 84 cm, CSV export, update cell + restore, row metadata, add column/row, duplicate rejection, Grading column sync.

Also in smoke/E2E: **B1**, **B2**.

**Manual only (browser):**

| # | Step | Expected |
|---|------|----------|
| 1 | Sidebar → **Size Chart** | Table loads; **M — Base** column highlighted |
| 2 | Click **Waist** row, **M** cell → change to `85` → tab away | Toast “Size chart saved” |
| 3 | Refresh page | M waist still `85` (change back to `84` when done) |
| 4 | **↓ Export CSV** | File downloads; open in Excel — Waist M matches |
| 5 | **＋ Add Size** → `3XL` | New column; values extrapolated |
| 6 | **＋ Add measurement** → copy from Waist | New row appears |

**Pass when:** M waist = **84 cm** (default) or your edited value persists after refresh.

---

### Module 4 — Block Generator & Grading

| URL | What to test |
|-----|----------------|
| `/BlockGenerator?style=slim` | Fit profile, ease rules |
| Generate block for pattern | Pieces drafted |
| `/Grading?style=slim` | Delta table |
| Edit one grade delta | Save → refresh → persisted |

#### Module 4a — Block Generator

**Automated (full Block Generator module):**

```powershell
powershell -ExecutionPolicy Bypass -File tools/qa-block-generator-test.ps1
```

Covers: page shell (all 5 fits), ease list/formulas, SaveEase + persist + ResetEase, GenerateBlock API, DraftPieces with required pieces (Front Leg, Back Leg, Waistband).

Also in smoke/E2E: **B3**, draft via **D1**.

**Manual only (browser):**

| # | Step | Expected |
|---|------|----------|
| 1 | `/BlockGenerator?style=slim` | Ease table, fit profile, drafting formulas |
| 2 | Click **Thigh** ease value → type `3` → Enter | Toast “Ease updated”; value shows +3 cm |
| 3 | **Reset to Default** | Thigh back to +2 cm |
| 4 | **Generate Block** | Success toast (formulas applied) |
| 5 | Open a pattern → **Pattern Pieces** → **Generate Pattern** | 9 pieces for slim; Front/Back Leg + Waistband |

**Pass when:** Ease edits persist after refresh; Generate Pattern drafts bottom-wear pieces from size chart M.

#### Module 4b — Grading

**Automated (full Grading module):**

```powershell
powershell -ExecutionPolicy Bypass -File tools/qa-grading-test.ps1
```

Covers: page shell (all 5 fits), XS–XXL columns, CSV export, Waist L default +2, UpdateDelta + restore, base column blocked, AddColumn, AddRow, duplicates rejected.

Also in smoke/E2E: **B4**.

**Manual only (browser):**

| # | Step | Expected |
|---|------|----------|
| 1 | `/Grading?style=slim` | Delta table; **M** base column highlighted |
| 2 | Switch **Bootcut** tab | Different ankle deltas vs slim |
| 3 | Edit **Waist / L** cell → `3` → tab away | Toast “Grading saved” |
| 4 | Refresh | L delta still `+3` (restore to `+2` when done) |
| 5 | **↓ Export** | CSV downloads; Waist L matches |
| 6 | **＋ Add Size** → `3XL` | New column on all fits |
| 7 | **＋ Add Row** → copy from Waist | New measurement row |

**Pass when:** Grade deltas persist after refresh; M base always shows **0**.

---

### Module 5 — Pattern Pieces & auto-refine

| URL | What to test |
|-----|----------------|
| `/Pieces?patternId=ID&style=slim` | Piece list (9 for slim) |
| **⬡ Generate Pattern** | Draft + auto-refine |
| **⚙ Auto-refine** | Re-runs balance |

**Pass when:** Front Leg, Back Leg, Waistband present; seam allowance set.

**Automated (full Pattern Pieces module):**

```powershell
powershell -ExecutionPolicy Bypass -File tools/qa-pieces-test.ps1
```

Covers: page shell, DraftPieces (9 slim pieces), required pieces + seam allowance + geometry, RefinePieces, AddPiece/DeletePiece, straight fit (8 pieces), dashboard piece count.

Also in E2E **D1**–**D4**; unit `PatternAutoRefineServiceTests`.

**Manual only (browser):**

| # | Step | Expected |
|---|------|----------|
| 1 | Open **Pattern Pieces** for a pattern | Cards grouped: Body Panels, Closures, Pockets |
| 2 | **⬡ Generate Pattern** | 9 pieces for slim; Fabric Cut Summary updates |
| 3 | **⚙ Auto-refine** | Page reloads; pieces unchanged count |
| 4 | Filter **Pockets** | Only pocket group visible |
| 5 | **＋ Add Piece** → name + category → Add | New card appears |
| 6 | **Open All in Canvas** | Canvas loads with pieces |

---

### Module 6 — Canvas editor

| URL | What to test |
|-----|----------------|
| `/Canvas?patternId=ID&style=slim` | Pieces visible |
| Move point → **Save All** | Persists after refresh |
| Toggle SA / Grain / Notches | Display updates |

**Manual only:** Drag point, undo, draw piece (browser).

**Automated:** E2E **D3**, **D4**; smoke canvas JSON

---

### Module 7 — Nest & Library

| URL | What to test |
|-----|----------------|
| `/Nest?style=slim` | Graded overlay |
| `/Library` | Pattern list |

**Automated:** E2E **D5**, **D6**

---

### Module 8 — Export (draft)

| URL | What to test |
|-----|----------------|
| `/Export?patternId=ID&style=slim` | QC panel |
| Draft ZIP — DXF, PLT, HPGL, PDF | Each downloads |

**Pass when:** Unzip → `canvas/slim_M.dxf` exists.

**Automated:** E2E **E1**; smoke draft export

---

### Module 9 — Factory QC & certification

| URL | What to test |
|-----|----------------|
| `/Export/ValidateFactory?patternId=ID&style=slim` | JSON QC |
| Factory download before approve | **Blocked** (400) |
| Approve + Record pass | Factory ZIP works |
| Dashboard | Factory ready +1 |

**Automated:** E2E **F1**–**F6**; unit certification + export gate tests

---

### Module 10 — Seam validation (unit only)

```powershell
dotnet test --filter "FullyQualifiedName~SeamValidationServiceTests"
```

---

## Module test order (recommended)

| Day | Modules |
|-----|---------|
| 1 | 0 + 1 (unit + auth) |
| 2 | 2 + 3 (dashboard + size chart) |
| 3 | 4 + 5 (block + pieces) |
| 4 | 6 + 7 (canvas + nest) |
| 5 | 8 + 9 (export + factory) |

After all modules pass, run full automation:

```powershell
dotnet test PatternPro.Tests/PatternPro.Tests.csproj
powershell -ExecutionPolicy Bypass -File tools/qa-smoke-test.ps1
powershell -ExecutionPolicy Bypass -File tools/qa-full-e2e.ps1
```

---

## Level 4 — API smoke tests

After login (cookie session), or use browser while logged in.

### QC JSON

```
GET /Export/ValidateFactory?patternId=23&style=slim
```

Expected: JSON with `canExportToFactory`, `issues`, `warnings`.

### Download URLs

```
GET /Export/DownloadPackage?patternId=23&style=slim&format=DXF&purpose=draft
GET /Export/DownloadPackage?patternId=23&style=slim&format=PLT&purpose=draft
GET /Export/DownloadPackage?patternId=23&style=slim&format=HPGL&purpose=draft
```

Expected: ZIP file download (`application/zip`).

Factory purpose requires certification:

```
GET /Export/DownloadPackage?patternId=23&style=slim&format=PLT&purpose=factory
```

Expected when **not** certified: 400 Bad Request with message.  
Expected when **certified**: ZIP download.

---

## Level 5 — PostgreSQL vs JSON

### JSON mode (quick dev test)

Comment out `ConnectionStrings:Postgres` in `appsettings.json` → restart.

- Data in `Pattern.Web/App_Data/`
- Users in `App_Data/users.json`
- Good for: solo dev, no DB install

### PostgreSQL mode (team / production-like)

Set `ConnectionStrings:Postgres` → restart.

- Migrations run on startup
- Users in `patternpro.app_users`
- Sync tool:

```powershell
dotnet run --project tools/PatternPro.DbTool -- sync
```

**Pass criteria:** Same manual checklist works in both modes.

---

## Level 6 — Verify export file content

### DXF (millimeters)

Open `canvas/slim_M.dxf` in a text editor:

- Contains `$INSUNITS` / mm values
- Layer `CUT` lines present
- File size > 1 KB for a real pattern

### PLT / HPGL

Open `canvas/slim_M.plt`:

- Starts with `IN;`
- Contains `SP1;` (cut), optionally `SP2;` (seam allowance)
- Ends with `PG;`

---

## Quick “is it correct?” summary

| Area | Correct when |
|------|----------------|
| **Build** | `dotnet build` — 0 errors |
| **Unit tests** | 21/21 pass |
| **Login** | Required; roles work |
| **Pattern** | Create → canvas save → data persists after refresh |
| **Export draft** | ZIP with 6 size files in `canvas/` |
| **Export factory** | Blocked until QC + approve + cutter pass |
| **Formats** | DXF, PLT, HPGL all produce non-empty files |
| **Dashboard** | Factory ready count matches certified patterns |

---

## Common failures

| Symptom | Fix |
|---------|-----|
| Port 5001 in use | `taskkill /IM Pattern.Web.exe /F` |
| Build locked | Stop running `Pattern.Web.exe` |
| Login fails | Check `Auth:SeedAdmin*` in appsettings; or `App_Data/users.json` |
| Empty export ZIP | Save pieces on Canvas first |
| Factory export disabled | Complete QC → Approve → Record pass |
| Postgres errors | Start Postgres; check connection string port/password |
| E2E D4 false failure (old scripts) | `Canvas/PieceData` returns a JSON **array**; use `Get-PieceList` in `tools/qa-*.ps1` (not `$data.pieces` on an array) |

---

## QA scripts (`tools/`)

| Script | Checks |
|--------|----------|
| `qa-smoke-test.ps1` | 28 HTTP checks — pages, pattern create, draft export, logout |
| `qa-full-e2e.ps1` | 30 checks — full workflow through factory certification ZIP |
| `qa-dashboard-test.ps1` | Dashboard module — all Home APIs, stats, CRUD, factory-ready count |
| `qa-style-sheet-test.ps1` | Style Sheet module — PLM fields, lifecycle gate, Rows API |
| `qa-size-chart-test.ps1` | Size Chart module — CSV, cells, add column/row, grading sync |
| `qa-block-generator-test.ps1` | Block Generator — ease, formulas, GenerateBlock, DraftPieces |
| `qa-grading-test.ps1` | Grading — deltas, CSV, UpdateDelta, AddColumn/AddRow |
| `qa-pieces-test.ps1` | Pattern Pieces — draft, refine, geometry, add/delete piece |

Both scripts use `Get-PieceList` when reading `GET /Canvas/PieceData` because the API returns a top-level JSON array of pieces, not `{ "pieces": [...] }`. In PowerShell, `$array.pieces` on a deserialized array collects a property from each element and breaks piece-name checks.

---

## One-command dev check

```powershell
cd E:\Code\PatternMaking
dotnet build Pattern.Web/Pattern.Web.csproj
dotnet test PatternPro.Tests/PatternPro.Tests.csproj
powershell -ExecutionPolicy Bypass -File tools/qa-smoke-test.ps1
powershell -ExecutionPolicy Bypass -File tools/qa-full-e2e.ps1
```

If all pass, core logic and the full HTTP workflow are OK. Walk through **Level 3** once per release for browser-only steps (canvas drag/save, visual QC).
