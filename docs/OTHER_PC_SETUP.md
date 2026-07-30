# Second PC setup (copy from main machine)

Use this when you clone PatternPro on another computer (e.g. `E:\All Code\PatternMaking\PatternMaking`).

## Important: use the same branch as main PC

| PC | Branch |
|----|--------|
| **Main PC** (latest factory workflow, Postgres, DbTool) | `arch/patternpro-phase1` |
| **Old branch** (canvas/export only, no phase 1) | `my-custom-branch` |

If the other PC shows `my-custom-branch`, switch:

```powershell
cd "E:\All Code\PatternMaking\PatternMaking"
git fetch origin
git checkout arch/patternpro-phase1
git pull origin arch/patternpro-phase1
```

---

## Fix `git pull` blocked by bin/obj files

If pull fails with **"Your local changes would be overwritten"** and paths under `bin/` or `obj/`:

Those are **build outputs**, not source code. Discard them, then pull:

```powershell
git restore .
git clean -fd
git pull origin arch/patternpro-phase1
```

**Warning:** `git clean -fd` removes **untracked** files. It may delete local `appsettings.Development.json` — recreate it from the example (step 3 below).

Do **not** run `git add` on `bin/` or `obj/` folders.

---

## Do **not** add Kestrel HTTPS to appsettings.json

Remove any block like:

```json
"Kestrel": { "Endpoints": { "Https": { "Url": "https://localhost:5001" } } }
```

Development uses **HTTP** only:

```powershell
git checkout -- Pattern.Web/appsettings.json
```

Open **http://localhost:5001** (not https).

---

## 1. Get latest code

```powershell
cd "E:\All Code\PatternMaking\PatternMaking"
git fetch origin
git checkout arch/patternpro-phase1
git pull origin arch/patternpro-phase1
```

## 2. Install prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Optional: PostgreSQL 14+

## 3. PostgreSQL connection (per machine)

Copy the example file and set **your** password:

```powershell
copy Pattern.Web\appsettings.Development.example.json Pattern.Web\appsettings.Development.json
notepad Pattern.Web\appsettings.Development.json
```

Example content:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=patternpro;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

`appsettings.Development.json` is **local only** (gitignored) — each PC has its own copy.

- PostgreSQL must be **running**
- Database `patternpro` must exist
- Without Postgres, the app uses `Pattern.Web/App_Data/*.json`

## 4. Sync data (optional)

```powershell
dotnet run --project tools/PatternPro.DbTool -- sync
dotnet run --project tools/PatternPro.DbTool -- certify-factory 22 23 24
```

## 5. Run the app

```powershell
taskkill /IM Pattern.Web.exe /F 2>$null
taskkill /IM PatternPro.Web.exe /F 2>$null

cd Pattern.Web
dotnet run
```

Open: **http://localhost:5001**

PatternPro.Web (optional second app): **http://localhost:5002**

## 6. Verify

| Check | OK when |
|-------|---------|
| Console | `Data store: PostgreSQL patternpro @ ...` or `JSON files` |
| Dashboard | http://localhost:5001 |
| Factory ready | 3+ after sync + certify |
| Export | http://localhost:5001/Export?patternId=23&style=slim |

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Wrong branch / missing DbTool | `git checkout arch/patternpro-phase1` then pull |
| Pull blocked by bin/obj | `git restore .` then `git clean -fd` then pull |
| `address already in use` | `taskkill /IM Pattern.Web.exe /F` |
| Postgres error | Start Postgres; fix `appsettings.Development.json` |
| Missing Development json | Copy from `appsettings.Development.example.json` |
| Only slim in ZIP | One pattern + one fit per export — use Dashboard Export per row |
| **Main PC shows ~62 patterns, this PC shows ~21** | See [Missing patterns (21 vs 62)](#missing-patterns-21-vs-62) below |

---

## Missing patterns (21 vs 62)

The **exe/ZIP is only the app**. Patterns live in **PostgreSQL** (or local `App_Data`), not inside the exe.

| Where | Typical count |
|-------|----------------|
| Main PC database (`192.168.1.15:5433`) | ~62 |
| This PC local JSON (`%LocalAppData%\PatternPro\...\App_Data`) | ~21 seed styles |
| This PC local Postgres (`localhost:5432`) | ~25 |

### Option A — Live shared database (recommended)

**1. On the MAIN PC** (Admin PowerShell, from the repo):

```powershell
powershell -ExecutionPolicy Bypass -File tools\allow-lan-postgres.ps1
```

**2. On THIS PC:**

```powershell
powershell -ExecutionPolicy Bypass -File tools\use-team-database.ps1
```

Restart PatternPro. Dashboard should match the main PC count.

If step 2 fails with `no pg_hba.conf entry for host "192.168.1.14"`, step 1 was not done on the main PC yet.

### Option B — USB dump (no network)

**On MAIN PC:**

```powershell
powershell -ExecutionPolicy Bypass -File tools\export-patternpro-db.ps1
```

Copy the Desktop folder `PatternPro-DB-Export` to this PC (USB).

**On THIS PC:**

```powershell
powershell -ExecutionPolicy Bypass -File tools\import-patternpro-db.ps1 -DumpFile "D:\path\to\patternpro-....dump"
```

## Quick copy-paste (other PC)

```powershell
cd "E:\All Code\PatternMaking\PatternMaking"
git fetch origin
git checkout arch/patternpro-phase1
git restore .
git clean -fd
git pull origin arch/patternpro-phase1
copy Pattern.Web\appsettings.Development.example.json Pattern.Web\appsettings.Development.json
notepad Pattern.Web\appsettings.Development.json
dotnet run --project Pattern.Web
```

Then open **http://localhost:5001**.
