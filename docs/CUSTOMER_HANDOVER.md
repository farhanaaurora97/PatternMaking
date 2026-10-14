# Customer handover (send with the Desktop ZIP)

When you run **`tools/publish-team-package.ps1`**, the ZIP folder automatically includes:

| File | Purpose |
|------|---------|
| **HANDOVER.txt** | One-page install, server IP/port, requirements, support |
| **MULTI_PC_SETUP.md** | Full multi-PC / PostgreSQL guide |
| **VERSION.txt** | Label matching `PatternPro-Desktop-1.0-win-x64.zip` |
| **README.txt** | Quick start for designers |

**Do not put passwords in the ZIP.** Distribute **`dist/delivery-credentials-*.txt`** (or individual messages) only to each user.

## Before you send

1. Fill in **ADMIN CONTACT** in `HANDOVER.txt` (edit after publish, or edit `tools/publish-desktop-windows.ps1` handover block).
2. Confirm **Host** in `appsettings.Team.json` inside the ZIP is PC 1’s **LAN IP** (not `localhost`).
3. Attach or link training images when ready: `docs/screenshots/` (`dashboard.png`, `canvas.png`, `export.png`, `size-chart.png`).

## Per-user logins

- Prefer **one account per person** (Pattern designer / Admin roles).
- Avoid sharing the **admin** account in production.
- **Forgot password:** user asks an Administrator → **Admin → Edit** → new password.

## Requirements (remind designers)

- Windows **10/11** 64-bit  
- **WebView2** (usually already installed)  
- Same **Wi‑Fi or VPN** as the database server  
- Server PC **on** while the team works (PostgreSQL on port **5433**)

## After delivery

See **[AFTER_DELIVERY.md](AFTER_DELIVERY.md)** (IP change → new ZIP, git tag `desktop-1.0`, credential rotation).

## Step 6 — test like a customer

```powershell
powershell -ExecutionPolicy Bypass -File tools\qa-customer-sim-test.ps1
```

On a **second PC**: extract ZIP → login → **Refresh** → **Canvas** → **Export** once. Console on startup should show **PostgreSQL** (team shared server), not “local JSON under App_Data”.
