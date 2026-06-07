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
