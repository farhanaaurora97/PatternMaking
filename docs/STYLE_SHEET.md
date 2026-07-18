# Style Sheet (PLM register)

The **Style Sheet** is the merchandising / PLM row for each bottom-wear style. It does **not** store pattern geometry (that stays on **Canvas** and in `pieces`).

## Open it

- Sidebar → **Style Sheet** (`/StyleSheet`)
- Dashboard → **Style Sheet** link above the pattern table

## Fields (industry style sheet)

| Field | Example | Meaning |
|-------|---------|---------|
| **Style code** | `DN-023` | Auto from pant type + next id |
| **Style name** | `Slim Tapered` | Product name on the row |
| **Season** | `SS26`, `FW25` | Collection season |
| **Designer** | `A. Khan` | Technical / creative designer |
| **Owner** | `Merch team` | Who owns the style in PLM |
| **Lifecycle** | Idea → Sampling → Bulk → Cancelled | Merchandising gate |
| **Pattern status** | Draft → Graded → Done | Pattern-room workflow (read-only on this screen) |
| **Due** | date | Milestone deadline |

## Two statuses (important)

1. **Lifecycle** (PLM) — on Style Sheet; idea / sampling / bulk / cancelled  
2. **Pattern status** (technical) — on Dashboard; pending / draft / in progress / graded / done  

They are **separate**. A style can be **Lifecycle = Sampling** while **Pattern status = In Progress**.

## API

| Method | Route |
|--------|--------|
| POST | `/Home/SetLifecycle` — `{ id, lifecycleStatus }` |
| POST | `/Home/UpdateStyleSheet` — `{ id, season?, owner?, designer? }` |

New styles: **+ New style** uses the same modal as Dashboard; set season, owner, and lifecycle at create time.
