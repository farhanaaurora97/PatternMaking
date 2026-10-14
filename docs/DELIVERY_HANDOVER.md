# PatternPro Desktop — delivery handover pack

Send this document **with** `PatternPro-Desktop-1.0-win-x64.zip`. Keep **per-user passwords** in a separate secure channel (not inside the ZIP).

---

## What designers receive

1. **ZIP:** `PatternPro-Desktop-1.0-win-x64.zip` (team build — database already configured).
2. **This handover** (or the shorter `HANDOVER_SEND_WITH_ZIP.txt` from `dist/`).
3. **Personal login** — username + password from the administrator (one row per person).

**Install (designer PC):**

1. Extract the ZIP to any folder (Desktop is fine).
2. Run **`START-PatternPro.bat`** or **`PatternPro.Desktop.exe`**.
3. Sign in with **your** username and password.

No PostgreSQL install. No editing JSON on designer PCs.

---

## Server (PC 1 — database)

| Item | Value |
|------|--------|
| **Role** | PostgreSQL server + optional admin workstation |
| **Database** | `patternpro` |
| **Port** | `5433` |
| **LAN host (Wi‑Fi)** | `192.168.0.188` *(re-check with `ipconfig` before go-live)* |
| **VPN (if used)** | `100.82.176.113` *(Tailscale/similar — only if team uses it)* |

The team ZIP is built with one of these IPs baked into `appsettings.Team.json`. If the server IP changes, rebuild the ZIP:

```powershell
cd E:\Code\PatternMaking
powershell -ExecutionPolicy Bypass -File tools\publish-team-package.ps1 -PostgresPassword "<server-db-password>"
```

**PC 1 must stay on** (or PostgreSQL running on a dedicated server) while designers work — otherwise others cannot sync patterns/users.

Full setup: [MULTI_PC_SETUP.md](MULTI_PC_SETUP.md).

---

## Administrators

| Name | Username | Role |
|------|----------|------|
| Primary admin | `admin` | Administrator |
| Secondary admin | `sukel.ali` | Administrator |

**Do not share one admin password with the whole team.** Create or reset **per-user** passwords in the app: **Admin panel → Edit user → New password**.

Password list for delivery lives on the server PC only, e.g. `dist/delivery-credentials-*.txt` (gitignored — **never** attach to email or commit).

---

## Per-user credentials (template)

Copy this table, fill passwords offline, and hand each person **only their row** (Signal/USB/in person).

| Display name | Username | Role | Password (fill in) |
|--------------|----------|------|--------------------|
| Administrator | `admin` | Admin | *(set at delivery)* |
| Sukel | `sukel.ali` | Admin | |
| Farhana | `HO-0397-00068` | Designer | |
| Sukel Ali | `HO-0418-00044` | Designer | |
| Adnan | `adnan` | Designer | |

Usernames are **login names** (some match employee IDs).

---

## Requirements (every designer PC)

- **Windows 10 or 11** (64-bit)
- **Microsoft Edge WebView2 Runtime** (usually already installed; [download](https://developer.microsoft.com/microsoft-edge/webview2/) if the app fails to start)
- **Same network as the database server:** office Wi‑Fi **or** the agreed VPN
- **Not required:** .NET SDK, PostgreSQL, or this git repo

---

## Support (tell the team)

| Issue | What to do |
|-------|------------|
| **Forgot password** | Contact an **Administrator** → Admin panel → **Edit** your user → set a new password. There is no self-service “Forgot password” email. |
| **Library / canvas looks old** | Use **Refresh** in the top bar (reloads from PostgreSQL). |
| **Cannot connect / login fails** | Confirm **PC 1** is on, PostgreSQL is running, and you are on the same Wi‑Fi/VPN. Ask admin for a **new ZIP** if the server IP changed. |
| **SmartScreen blocks the app** | **More info → Run anyway** (unsigned dev/team build). |
| **Export** | Files save under **`Downloads`**. Factory export needs QC + approval on the Export page. |

Startup errors (if any): `%LocalAppData%\PatternPro\startup-error.txt`

---

## Training screenshots (optional)

Capture from a running app (Win+Shift+S) into `docs/screenshots/` for README/GitHub:

| File | Screen |
|------|--------|
| `dashboard.png` | Dashboard |
| `size-chart.png` | Size Chart |
| `canvas.png` | Canvas Editor |
| `export.png` | Export / DXF |

Cypress (web) screenshots: `e2e/cypress/screenshots/` after `npm run cy:screenshots` with Pattern.Web on port 5001.

---

## Quick workflow reminder

**Style Sheet → Size Chart → Block Generator → Grading → Pattern Pieces → Canvas → Export**

More detail: [PROJECT_GUIDE.md](PROJECT_GUIDE.md).

---

*Generated for PatternPro Desktop team delivery. Update server IP and admin contacts before each rollout.*
