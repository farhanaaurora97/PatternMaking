# After delivery — ops checklist

## Server IP or Wi‑Fi changed

Team ZIPs embed the database **Host** IP. If PC 1 gets a new LAN address or designers move networks:

1. On **PC 1** (PostgreSQL): confirm Postgres is running on port **5433**.
2. Rebuild and redistribute the ZIP:

```powershell
cd E:\Code\PatternMaking
powershell -ExecutionPolicy Bypass -File tools\publish-team-package.ps1 -PostgresPassword "<server-db-password>"
```

3. Share **`dist\PatternPro-Desktop-1.0-win-x64.zip`** again (USB, Drive, etc.).
4. Designers: extract over old folder or to a new folder; no manual config edits.

Optional firewall/LAN check:

```powershell
powershell -ExecutionPolicy Bypass -File tools\setup-postgres-server.ps1 -Port 5433
```

## Version label (match the ZIP name)

Keep a **git tag** (or release note) aligned with the shipped package name **`PatternPro-Desktop-1.0-win-x64.zip`**:

```powershell
cd E:\Code\PatternMaking
git tag -a desktop-1.0 -m "PatternPro Desktop 1.0 team package for factory pilot"
git push origin desktop-1.0
```

Only tag **after** the matching ZIP is built and smoke-tested. Do not tag uncommitted local-only changes unless that is intentional.

The publish output also includes **`VERSION.txt`** in the ZIP folder (`PatternPro-Desktop-1.0`).

## Quick health checks (any time)

```powershell
dotnet run --project tools/PatternPro.DbTool -- verify-connection
powershell -ExecutionPolicy Bypass -File tools\qa-desktop-delivery-checklist.ps1
powershell -ExecutionPolicy Bypass -File tools\qa-customer-sim-test.ps1 -SkipPublish
```

## Credentials rotation

If passwords were exposed, rerun:

```powershell
powershell -ExecutionPolicy Bypass -File tools\prepare-delivery-security.ps1 -PostgresPassword "<new-db-password>"
```

Then rebuild the team ZIP with the same `-PostgresPassword`.
