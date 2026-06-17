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
| Unit tests | `Passed: 18` (0 failed) |
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

# 2) Run all automated tests (18 tests)
dotnet test PatternPro.Tests/PatternPro.Tests.csproj
```

**Pass criteria:** `Build succeeded` and `Passed: 18` (0 failed).

### What the unit tests cover

| Test file | What it checks |
|-----------|----------------|
| `SeamValidationServiceTests` | Required pieces, empty geometry, QC errors |
| `ProductionCertificationServiceTests` | Approve, certify, factory-ready logic |
| `ExportServiceFactoryGateTests` | Factory export blocked when not certified; draft export allowed |
| `UserServiceTests` | Login (username or employee ID), registration gate, admin approval, disabled users |

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
| **Unit tests** | 18/18 pass |
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
