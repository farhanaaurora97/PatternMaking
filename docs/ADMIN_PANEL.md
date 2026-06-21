# Admin panel — users & permissions

PatternPro requires **sign-in** for all screens. Administrators manage who can access the app, employee IDs, roles, and account approval.

## URLs

| Screen | URL |
|--------|-----|
| Sign in | http://localhost:5001/Account/Login |
| **Admin panel** | http://localhost:5001/Admin |
| **My account** (any signed-in user) | http://localhost:5001/User |
| Change password | http://localhost:5001/User/ChangePassword |

## Sign in

Use **username** or **employee ID** and the password your administrator gave you.

## Sign out (logout)

You can sign out from **two places**:

1. **Sidebar (bottom left)** — under your name → **Sign out**
2. **Top bar (top right)** — **Sign out** button next to Import / Library

After sign out you return to the login page. On a shared PC, the next person must sign in with their own account.

## Add people (admin only)

**Public registration is off by default** (`Auth:RegistrationEnabled: false`). Only an **Administrator** can add users from the admin panel.

### Steps

1. Sign in as **admin** (see default credentials below if first run).
2. Sidebar → **Admin panel** (or open http://localhost:5001/Admin).
3. Click **+ New user**.
4. Enter **employee ID**, **username**, **display name**, **role**, and **password**.
5. Click **Create user**.
6. Send the new person the login URL, employee ID (or username), and password.

### Employee ID

- Required for every user (e.g. `EMP-1042`).
- Must be **unique** across the system.
- Can be used to **sign in** instead of username.
- Shown in the sidebar footer and on **My account**.

### Roles when creating users

| Role | Use for |
|------|---------|
| **Administrator** | IT / owner — full access + admin panel |
| **Pattern designer** | Pattern team — full edit + factory export |
| **View only** | Boss / merch — browse only |

To **block** someone: **Disable** on the user list (or uncheck **Account active** on Edit).  
To **approve** a pending account: **Approve** on the user list.  
To **reset password**: **Edit** → enter new password → Save.  
To **remove** a user: **Edit** → delete (you cannot delete the last active administrator).

## Optional public registration

If `Auth:RegistrationEnabled` is set to `true` in config:

1. User opens http://localhost:5001/Account/Register
2. Fills employee ID, name, password
3. Account is created **inactive** when `RequireAdminApproval: true`
4. Admin → **Approve** on the user list before they can sign in

For factory pilot, keep registration **disabled** and use **+ New user** only.

## Default admin (first run)

On first startup, if **no users exist**, the app creates one admin from config:

```json
"Auth": {
  "SeedAdminUserName": "admin",
  "SeedAdminPassword": "Admin@123"
}
```

| Field | Value |
|-------|-------|
| Sign-in URL | http://localhost:5001/Account/Login |
| Username | `admin` |
| Employee ID | `ADMIN` |
| Password | `Admin@123` (change after first login) |
| Admin panel | Sidebar → **Admin panel** or http://localhost:5001/Admin |

**Change the password** after first login via **Admin → Edit user** or **My account → Change password**.

## User panel (all signed-in users)

Sidebar → **My account** (`/User`):

- View employee ID, username, display name, role, account status
- Link to change password
- Link to Dashboard / patterns

Administrators also use the admin panel; designers and view-only users land on Dashboard after login.

## Roles

| Role | Sign in | Edit patterns | Factory export | Admin panel |
|------|---------|---------------|----------------|-------------|
| **Administrator** | Yes | Yes | Yes | Yes |
| **Pattern designer** | Yes | Yes | Yes | No |
| **View only** | Yes | No (read-only) | No | No |
| **Disabled / pending** | No | — | — | — |

### View only

- Can open Dashboard, Canvas, Export, etc.
- **POST** actions are blocked (save, approve, grade edits, …)
- Can download **CLO** and **Draft** ZIPs; **not** factory export

### Disabled / pending approval

- **Pending approval** — registered or created inactive; admin clicks **Approve**
- **Disabled** — admin clicked **Disable** or unchecked **Account active**
- User cannot sign in until approved and active

## Admin tasks

| Task | How |
|------|-----|
| Add teammate | Admin → **+ New user** → employee ID, username, display name, role, password |
| Change role | Admin → **Edit** → Role dropdown |
| Reset password | Admin → **Edit** → enter new password |
| Approve new account | Admin → **Approve** |
| Block access | **Disable** or uncheck **Account active** |
| Restore access | **Approve** / **Enable** or check **Account active** |
| Remove user | Admin → **Edit** → delete (not the last active admin) |

## Data storage

| Store | Users |
|-------|-------|
| JSON mode | `Pattern.Web/App_Data/users.json` |
| PostgreSQL | `patternpro.app_users` |

Migration: `AddAppUsers`.

## Security notes

- Passwords are hashed (ASP.NET Identity hasher); plain text is never stored
- Factory export is enforced on the **server**, not only in the UI
- Use strong passwords in production; do not commit real passwords to git
- Put production admin password in environment variables or gitignored `appsettings.Development.json`
- Set `RegistrationEnabled: false` for internal factory teams

## Quick reference

| Action | Where |
|--------|--------|
| Sign in | http://localhost:5001/Account/Login (username or employee ID) |
| Sign out | Sidebar bottom **Sign out** or top bar **Sign out** |
| Admin panel | http://localhost:5001/Admin or sidebar **Admin panel** |
| My account | http://localhost:5001/User or sidebar **My account** |
| Register user | Admin → **+ New user** (Administrator only) |
| Approve user | Admin → **Approve** |
| Disable user | Admin → **Disable** |
