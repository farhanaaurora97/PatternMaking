# Go live — production deployment

PatternPro is ready for production when build + tests pass and secrets are configured on the server (not in git).

## Pre-flight checklist

| Check | Command / action |
|-------|------------------|
| Build | `dotnet build Pattern.Web/Pattern.Web.csproj -c Release` |
| Unit tests | `dotnet test PatternPro.Tests/PatternPro.Tests.csproj` |
| Smoke + E2E | App running → `tools/qa-smoke-test.ps1` + `tools/qa-full-e2e.ps1` |
| Secrets | No real passwords in `appsettings.json` (use env vars or server config) |
| Admin password | Not `Admin@123` — set strong password before exposing to network |
| Registration | `Auth:RegistrationEnabled: false` for internal teams |
| Database | PostgreSQL recommended for multi-user production |

---

## 1. Publish the app

```powershell
cd E:\Code\PatternMaking
dotnet publish Pattern.Web/Pattern.Web.csproj -c Release -o ./publish/patternpro
```

Output folder: `publish/patternpro/` — copy this to your server.

---

## 2. PostgreSQL (recommended)

1. Create database and user:

```sql
CREATE DATABASE patternpro;
CREATE USER patternpro_app WITH PASSWORD 'your_strong_password';
GRANT ALL PRIVILEGES ON DATABASE patternpro TO patternpro_app;
```

2. Migrations run automatically on startup when `ConnectionStrings:Postgres` is set.

3. Optional: import existing JSON data:

```powershell
dotnet run --project tools/PatternPro.DbTool -- sync
```

---

## 3. Configuration (secrets on server)

**Do not commit production passwords.** Set environment variables on the server:

| Variable | Example |
|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Postgres` | `Host=db;Port=5432;Database=patternpro;Username=patternpro_app;Password=...` |
| `Auth__SeedAdminUserName` | `admin` |
| `Auth__SeedAdminPassword` | Strong password (used only when **no users exist** yet) |
| `Auth__RegistrationEnabled` | `false` |

Copy `Pattern.Web/appsettings.Production.example.json` as a template for file-based config if you prefer files over env vars.

On first startup with an empty database, the seed admin is created once. **Change the password** via Admin panel or `/User/ChangePassword` immediately after first login.

---

## 4. Run on Windows (Kestrel service)

```powershell
cd publish\patternpro
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=patternpro;Username=patternpro_app;Password=YOUR_PASSWORD"
$env:Auth__SeedAdminPassword = "YOUR_STRONG_PASSWORD"
dotnet Pattern.Web.dll --urls "http://127.0.0.1:5001"
```

Put **IIS** or **nginx** in front for HTTPS. The app enables HSTS and secure cookies in Production.

---

## 5. Reverse proxy (HTTPS)

The app uses `ForwardedHeaders` in Production so HTTPS termination at nginx/IIS works.

**nginx** example:

```nginx
location / {
    proxy_pass http://127.0.0.1:5001;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

Users access `https://your-domain.com` — not plain HTTP on the public internet.

---

## 6. Post-deploy verification

1. Open `/Account/Login` — login page loads over HTTPS.
2. Login as admin — change password if first deploy.
3. Create a test pattern → draft pieces → export draft ZIP.
4. Run smoke test against production URL (update `-BaseUrl`):

```powershell
powershell -ExecutionPolicy Bypass -File tools/qa-smoke-test.ps1 -BaseUrl "https://your-domain.com"
```

5. Confirm console shows `Data store: PostgreSQL` (not JSON files).

---

## 7. Local dev (unchanged)

Copy secrets to gitignored `Pattern.Web/appsettings.Development.json`:

```powershell
copy Pattern.Web\appsettings.Development.example.json Pattern.Web\appsettings.Development.json
```

Edit Postgres password and optional `Auth:SeedAdminPassword` for local seed admin.

```powershell
cd Pattern.Web
dotnet run
```

Open **http://localhost:5001**

---

## JSON-only mode (single user / demo)

If `ConnectionStrings:Postgres` is empty, data lives in `App_Data/*.json`. Fine for solo dev; **not recommended** for team production (no concurrent DB, users in JSON file).

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| `PRODUCTION WARNING` on startup | Set strong passwords via env vars (see section 3) |
| Login redirect loop behind proxy | Ensure `X-Forwarded-Proto: https` reaches the app |
| Factory export 400 | Complete QC + approve + cutter pass on Export page |
| Migrations fail | Check Postgres user permissions and connection string |

See also: [TESTING.md](TESTING.md), [ADMIN_PANEL.md](ADMIN_PANEL.md), [POSTGRES_SYNC.md](POSTGRES_SYNC.md).
