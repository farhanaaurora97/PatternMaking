# Industry workflow (areas 1–8)

How PatternPro maps to garment-industry steps and what was built for each.

| # | Industry need | App module | What you do |
|---|---------------|------------|-------------|
| 1 | Style register / PLM | **Style Sheet** + **Dashboard** | Code, season, owner, lifecycle, revision, due date |
| 2 | Size spec (POM) | **Size Chart** | XS–XXL table, tolerance ± cm, measurement method, edit cells |
| 3 | Block + ease | **Block Generator** | Fit ease rules; **Generate for pattern** drafts pieces to canvas |
| 4 | Piece breakdown | **Pattern Pieces** | Piece list, auto-draft, piece #, material, on-fold flags |
| 5 | Hand edit outlines | **Canvas** | Move points, grain, notches; **Validate factory** QC from canvas |
| 6 | Multi-size grading | **Grading** + export | Delta table (editable); canvas master graded on export |
| 7 | Cutter files | **Export** | DXF (mm), HPGL, PLT — layers/pens CUT/SA/GRAIN/NOTCH |
| 8 | Factory gate | **Export** QC | QC → approve → **manual** cutter test → factory ZIP + `certification.json` |

## 1 — Style register (PLM)

- **Style Sheet** (`/StyleSheet`): season, designer, owner, lifecycle (Idea → Sampling → Bulk → Cancelled)
- **Dashboard**: pattern status (Pending → Done), factory-ready count, real **Recent activity** from your patterns
- **Revision** field (e.g. Proto-1, SMS-2) on each style row
- **Bulk lifecycle** only allowed when pattern is Graded/Done **and** factory certified (approve + cutter pass)

## 2 — Size spec

- **Size Chart**: body/garment measurements per size
- **Tolerance** (± cm) and **measurement method** per POM row
- API: `POST /SizeChart/UpdateCell`, `POST /SizeChart/UpdateRowMeta`
- CSV export for sharing with merchandising

## 3 — Block + ease

- Default ease per fit (skinny/slim/straight/bootcut/wide leg); overrides persisted
- **`POST /BlockGenerator/GenerateBlockForPattern?patternId=&styleKey=`** — drafts all pieces from size chart + ease, applies catalog notches, saves to pattern

## 4 — Pattern pieces

- Standard bottom pieces per fit; **auto-draft** applies **NotchGrainResolver** (rule notches + grain)
- Piece metadata: **PieceNumber**, **Material**, **OnFold** (stored with geometry)

## 5 — Canvas

- Digital pattern editing (vertices, grain, CF, notches, seam allowance)
- **`GET /Canvas/ValidateFactory?patternId=&style=`** — run production QC without leaving canvas

## 6 — Grading

- Per-fit delta table from base size M
- **`POST /Grading/UpdateDelta`** — edit grade rules
- Export grades canvas master to all sizes using measurement deltas

## 7 — Export / CAM

- ZIP: **DXF**, **HPGL**, or **PLT** per size (select on Export page)
- **DXF coordinates in mm** (`$INSUNITS=4`); HPGL/PLT in standard plotter units (1016/in)
- Factory ZIP includes **`certification.json`** (approver, cutter test, QC issues)

## 8 — Controlled factory release

- Factory export blocked until: geometry QC clean, **Approve for cutting**, **Record cutter pass** (no auto-pass)
- Dashboard **Factory ready** = both flags true
- Draft and CLO review exports are **not** gated

## End-to-end (one style)

1. Style Sheet → create row (season, owner, Idea)
2. Size chart → confirm POMs + tolerance
3. Block generator → generate block for pattern
4. Pieces → verify list → Canvas → save
5. Grading → adjust deltas if needed
6. Export → QC → approve → cutter test → factory ZIP
7. Style Sheet → lifecycle **Bulk** (when certified)
8. Dashboard → **Factory ready** count increases

See also: [STYLE_SHEET.md](STYLE_SHEET.md), [WORKFLOW.md](WORKFLOW.md), [PRODUCTION_CERTIFICATION.md](PRODUCTION_CERTIFICATION.md).
