# Second PC setup (copy from main machine)

Use this when you clone or copy PatternPro to another computer (e.g. `E:\All Code\PatternMaking\PatternMaking`).

## Do **not** add Kestrel HTTPS to appsettings.json

Some editors suggest:

```json
"Kestrel": { "Endpoints": { "Https": { "Url": "https://localhost:5001" } } }
```

**Remove that.** Development uses plain **HTTP** only. HTTPS redirect runs in Production only (see `Program.cs`).

If you already added it:

```powershell
git checkout -- Pattern.Web/appsettings.json
```

## 1. Get latest code from main PC

On the **main PC**, commit and push your changes. On the **other PC**:

```powershell
cd "E:\All Code\PatternMaking\PatternMaking"
git pull
```

If you have local edits you do not need:

```powershell
git checkout -- Pattern.Web/appsettings.json
git pull
```

## 2. Install prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Optional: PostgreSQL 14+ (same as main PC)

## 3. PostgreSQL connection (per machine)

Edit **`Pattern.Web/appsettings.Development.json`** (this file is local; adjust password/port for **this** PC):

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=patternpro;Username=postgres;Password=YOUR_PASSWORD_HERE"
  }
}
```

Do the same in `PatternPro.Web/appsettings.Development.json` if you use that app.

- PostgreSQL must be **running** on this PC.
- Create database `patternpro` if it does not exist.
- If Postgres is not installed, the app uses `Pattern.Web/App_Data/*.json` instead (data will not match main PC until you sync).

## 4. Sync data from JSON → Postgres (optional)

From repo root:

```powershell
dotnet run --project tools/PatternPro.DbTool -- sync
dotnet run --project tools/PatternPro.DbTool -- certify-factory 22 23 24
```

## 5. Run the app

Stop any old instance first:

```powershell
taskkill /IM Pattern.Web.exe /F 2>$null
taskkill /IM PatternPro.Web.exe /F 2>$null
```

**Primary app:**

```powershell
cd Pattern.Web
dotnet run
```

Open: **http://localhost:5001** (not https)

**Alternate app (second terminal):**

```powershell
cd PatternPro.Web
dotnet run
```

Open: **http://localhost:5002**

## 6. Verify

| Check | OK when |
|-------|---------|
| Console | `Data store: PostgreSQL patternpro @ ...` or `JSON files` |
| Dashboard | http://localhost:5001 loads |
| Export | http://localhost:5001/Export?patternId=23&style=slim |

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `address already in use` | `taskkill /IM Pattern.Web.exe /F` then run again |
| Postgres connection error | Start PostgreSQL; fix password/port in `appsettings.Development.json` |
| `Failed to determine the https port` | Pull latest code (HTTPS redirect disabled in Development) |
| Build file locked | Stop running app, then `dotnet build` |
| Only slim downloads | Export is **one pattern + one fit** per ZIP — open Export from each dashboard row |

## URLs (same on every PC)

| Screen | URL |
|--------|-----|
| Dashboard | http://localhost:5001 |
| Canvas DN-023 | http://localhost:5001/Canvas?patternId=23&style=slim |
| Export DN-023 | http://localhost:5001/Export?patternId=23&style=slim |

## What main PC changed (pull to get this)

- **No HTTPS in Development** — use `http://localhost:5001`
- **PatternPro.Web** on port **5002** (no conflict with Pattern.Web)
- **DbTool**: `sync`, `certify-factory`, `seed-style`
- Factory workflow docs: [WORKFLOW.md](WORKFLOW.md), [POSTGRES_SYNC.md](POSTGRES_SYNC.md)
