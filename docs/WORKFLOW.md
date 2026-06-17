# PatternPro workflow (first to last)

> **Full documentation:** [PROJECT_GUIDE.md](PROJECT_GUIDE.md) — complete guide for managers, designers, factory floor, and developers.

One bottom-wear style (e.g. denim slim), from app start to factory ZIP.

## Before the app

1. Install .NET 8 SDK.
2. Optional: PostgreSQL + `ConnectionStrings:Postgres` in `Pattern.Web/appsettings.json`.
3. Run: `dotnet run --project Pattern.Web` → http://localhost:5001
4. Console: `Data store: PostgreSQL` or `JSON files`.

## Steps (designer order)

| # | Screen | Route | You do | Output |
|---|--------|-------|--------|--------|
| 0 | Dashboard | `/` | Create pattern (category, fit, base size) | Pattern job (e.g. DN-023) |
| 1 | Size chart | `/SizeChart` | Enter body measurements XS–XXL | Shared measurement table |
| 2 | Block generator | `/BlockGenerator?style=slim` | Set ease, generate block | Fit / ease rules |
| 3 | Grading | `/Grading?style=slim` | Set deltas from base M | Grade rules |
| 4 | Pattern pieces | `/Pieces?patternId=&style=` | List pieces, auto-draft, open canvas | Piece checklist |
| 5 | Canvas | `/Canvas?patternId=&style=` | Draw/edit, save, optional multi-size draft | Saved geometry (required for QC) |
| 6 | Graded nest | `/Nest?style=` | Visual check all sizes overlaid | Confidence check (optional) |
| 7 | Library | `/Library` | Browse all patterns | Optional overview |
| 8 | Export | `/Export?patternId=&style=` | QC → approve → cutter pass → factory ZIP | Production files |
| 9 | Dashboard | `/` | Check **Factory ready** badge and stat | Team visibility |

## Export sub-steps (step 8)

1. Page loads → geometry QC runs.
2. Fix **red blocking** issues on Canvas.
3. **Approve for cutting**.
4. **Record pass** (cutter/plotter trial).
5. Optional: **Shrinkage %**.
6. **↓ Factory export** (gated).

Not gated: **CLO review ZIP** (`purpose=clo`), **Draft ZIP** (`purpose=draft`).

## Factory ready (dashboard)

Counts patterns with **both**:

- `ApprovedForCutting`
- `CutterTestPassed`

Downloading a ZIP does **not** increment this count unless those flags are saved (via Export UI or DbTool sync).

## PostgreSQL sync

After editing `App_Data` or using DbTool:

```powershell
dotnet run --project tools/PatternPro.DbTool -- sync
```

See [POSTGRES_SYNC.md](POSTGRES_SYNC.md).

## Certification details

See [PRODUCTION_CERTIFICATION.md](PRODUCTION_CERTIFICATION.md).

## Done when

| Goal | Done when |
|------|-----------|
| Design | Pieces saved on Canvas; status Graded/Done |
| Factory | QC clean + approved + cutter pass + factory ZIP downloaded |
| Dashboard | **Factory ready** includes your pattern |
