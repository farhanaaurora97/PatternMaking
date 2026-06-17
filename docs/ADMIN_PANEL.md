# Admin panel — users & permissions

PatternPro requires **sign-in** for all screens. Administrators manage who can access the app and what each person can do.

## Sign in

| Item | Value |
|------|-------|
| URL | http://localhost:5001 |
| Login page | http://localhost:5001/Account/Login |

Use the **username** and **password** your administrator gave you.

## Sign out (logout)

You can sign out from **two places**:

1. **Sidebar (bottom left)** — under your name → click **Sign out**
2. **Top bar (top right)** — **Sign out** button next to Import / Library

After sign out you return to the login page. The next person on the same PC must sign in with their own account.

## Register people (admin only)

There is **no public registration page**. Only an **Administrator** can add users.

### Steps

1. Sign in as **admin** (see default credentials below if first run).
2. Sidebar → **Users & permissions** (or `/Admin`).
3. Click **+ New user**.
4. Enter **username**, **display name**, **role**, and **password**.
5. Click **Create user**.
6. Send the new person the login URL, username, and password.

### Roles when registering

| Role | Use for |
|------|---------|
| **Administrator** | IT / owner — can register others |
| **Pattern designer** | Pattern team — full edit + export |
| **View only** | Boss / merch — browse only |

To **block** someone: **Disable** on the user list (they cannot sign in).  
To **reset password**: **Edit** → enter new password → Save.

## Default admin (first run)

On first startup, if **no users exist**, the app creates one admin from `appsettings.json`:

```json
"Auth": {
  "SeedAdminUserName": "admin",
  "SeedAdminPassword": "Admin@123"
}
```

**Change this password** after first login via **Admin → Edit user**.

| Item | Value |
|------|-------|
| Sign-in URL | http://localhost:5001/Account/Login |
| Admin panel | Sidebar → **Users & permissions** (Admin role only) |

## Roles

| Role | Sign in | Edit patterns | Factory export | Admin panel |
|------|---------|---------------|----------------|-------------|
| **Administrator** | Yes | Yes | Yes | Yes |
| **Pattern designer** | Yes | Yes | Yes | No |
| **View only** | Yes | No (read-only) | No | No |
| **Disabled** | No | — | — | — |

### View only

- Can open Dashboard, Canvas, Export, etc.
- **POST** actions are blocked (save, approve, grade edits, …)
- Can download **CLO** and **Draft** ZIPs; **not** factory export

### Disabled

- Admin sets **Account active** off, or clicks **Disable** on the user list
- User cannot sign in

## Admin tasks

| Task | How |
|------|-----|
| Add teammate | Admin → **+ New user** → username, display name, role, password |
| Change role | Admin → **Edit** → Role dropdown |
| Reset password | Admin → **Edit** → enter new password |
| Block access | **Disable** or uncheck **Account active** |
| Restore access | **Enable** or check **Account active** |
| Remove user | **Edit** (delete via future) — use **Disable** for safety |

**Note:** You cannot delete the last active administrator.

## Data storage

| Store | Users file / table |
|-------|-------------------|
| JSON mode | `Pattern.Web/App_Data/users.json` |
| PostgreSQL | `patternpro.app_users` |

Migration: `AddAppUsers`.

## Security notes

- Passwords are hashed (ASP.NET Identity hasher); plain text is never stored
- Factory export is enforced on the **server**, not only in the UI
- Use strong passwords in production; do not commit real passwords to git
- Put production admin password in `appsettings.Development.json` (gitignored) or environment variables

## Quick reference

| Action | Where |
|--------|--------|
| Sign in | http://localhost:5001/Account/Login |
| Sign out | Sidebar bottom **Sign out** or top bar **Sign out** |
| Register user | Admin → **+ New user** (Administrator only) |
| Disable user | Admin → **Disable** |
