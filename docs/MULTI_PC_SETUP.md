# Multi-PC setup — one PostgreSQL server, many Desktop PCs

Use this when **PC 1** runs PostgreSQL and **PC 2, 3, 4** run PatternPro Desktop with the **same shared database**.

---

## Simple handover (recommended)

**On PC 1 (your PC — one command):**

```powershell
cd E:\Code\PatternMaking
powershell -ExecutionPolicy Bypass -File tools\publish-team-package.ps1
```

This builds **`dist\PatternPro-Desktop-1.0-win-x64.zip`** with your Wi-Fi IP and database already configured.

**On other PCs — nothing to install or configure:**

1. Extract the ZIP
2. Double-click **`PatternPro.Desktop.exe`** (or **`START-PatternPro.bat`**)
3. Login: **admin** / **Admin@123**

No editing JSON. No deleting App_Data. No PostgreSQL on other PCs.

**Requirements:** same office Wi-Fi; PC 1 ON with PostgreSQL running. If your IP changes, re-run `publish-team-package.ps1` and send the new ZIP.

---

## Architecture

```
PC 1 (192.168.1.10)              PC 2, 3, 4
┌─────────────────────┐            ┌─────────────────────┐
│ PostgreSQL :5433    │ ◄──────────│ PatternPro Desktop  │
│ database: patternpro│   LAN      │ appsettings.Team.json│
└─────────────────────┘            └─────────────────────┘
```

All designers see the same patterns, size charts, grading, and users.

---

## Quick setup (automated scripts)

### PC 1 — database server

1. Install PostgreSQL; create database **`patternpro`**.
2. Edit PostgreSQL **`postgresql.conf`** and **`pg_hba.conf`** (see below).
3. Run (Administrator recommended for firewall):

```powershell
cd E:\Code\PatternMaking
powershell -ExecutionPolicy Bypass -File tools/setup-postgres-server.ps1 -Port 5433
```

4. Seed data once:

```powershell
dotnet run --project tools/PatternPro.DbTool -- sync
dotnet run --project tools/PatternPro.DbTool -- reset-admin-password
```

**PC 1** keeps `Pattern.Web/appsettings.Development.json` with **`Host=localhost`**.

---

### PC 2, 3, 4 — Desktop clients

1. Copy **`dist/PatternPro-Desktop-win-x64/`** (or publish ZIP) to each PC.
2. Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup-desktop-client.ps1 -ServerHost 192.168.1.10 -Password YOUR_PASSWORD
```

Or interactive (prompts for IP and password):

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup-desktop-client.ps1
```

This creates **`appsettings.Team.json`** next to `PatternPro.Desktop.exe` and tests the connection.

3. Start **`PatternPro.Desktop.exe`**.

Console should show:

```text
[PatternPro Desktop] Config: PostgreSQL patternpro @ 192.168.1.10:5433 (team (shared server)).
```

---

## Manual config (without script)

Copy `PatternPro.Desktop/appsettings.Team.example.json` → **`appsettings.Team.json`** next to the `.exe`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=192.168.1.10;Port=5433;Database=patternpro;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Auth": {
    "SeedAdminUserName": "admin",
    "SeedAdminPassword": "Admin@123"
  }
}
```

Same file on every designer PC. **`Host`** = PC 1’s IP only.

---

## PostgreSQL manual steps (PC 1)

**`postgresql.conf`:**

```conf
listen_addresses = '*'
port = 5433
```

**`pg_hba.conf`** (adjust subnet):

```conf
host    patternpro    postgres    192.168.1.0/24    scram-sha-256
```

Restart PostgreSQL service.

**Windows Firewall:** allow inbound **TCP 5433** (office network only).

---

## Verify from any PC

```powershell
cd E:\Code\PatternMaking
dotnet run --project tools/PatternPro.DbTool -- verify-connection
```

From a client PC before Desktop is configured, test with server IP:

```powershell
$env:ConnectionStrings__Postgres = "Host=192.168.1.10;Port=5433;Database=patternpro;Username=postgres;Password=YOUR_PASSWORD"
dotnet run --project tools/PatternPro.DbTool -- verify-connection
```

Or:

```powershell
Test-NetConnection 192.168.1.10 -Port 5433
```

---

## Users and security

| Task | Action |
|------|--------|
| First login | `admin` / `Admin@123` on any PC |
| Team accounts | **Admin** → **+ New user** — one login per designer |
| Change default password | Admin panel or `reset-admin-password` on server |
| Do not expose Postgres to internet | Office LAN / VPN only |

---

## Optional: Web browser on PC 2–4

Run Web on **PC 1** only:

```powershell
cd Pattern.Web
dotnet run --urls "http://0.0.0.0:5001"
```

Others open: **`http://192.168.1.10:5001`** (same database).

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `Connection refused` | Postgres not running on PC 1; wrong port |
| `Timeout` | Firewall on PC 1; wrong IP; not same network |
| `password authentication failed` | Wrong password in `appsettings.Team.json` |
| Still shows `localhost` | Missing `appsettings.Team.json` next to `.exe` |
| PC 1 off | Other PCs cannot connect — server must stay on |

---

## Related docs

- [OTHER_PC_SETUP.md](OTHER_PC_SETUP.md) — clone repo on another dev machine
- [DEPLOYMENT.md](DEPLOYMENT.md) — production web deployment
- [TESTING.md](TESTING.md) — QA scripts (web HTTP)
