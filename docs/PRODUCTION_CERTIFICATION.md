# Production certification

PatternPro treats **factory CAM export** as a gated release. CLO3D review and internal draft exports are not gated.

## Workflow

```
Canvas (save pieces) → QC validation → Design approval → Cutter test → Factory ZIP
                              ↓
                    CLO review ZIP (base size only, anytime)
                    Draft ZIP (full graded, no gate)
```

## Gates (factory export)

| Gate | Where | Blocks if |
|------|--------|-----------|
| Saved pattern | `patternId > 0` | No pattern selected |
| Geometry QC | `SeamValidationService` | Missing Front/Back Leg or Waistband; invalid polygons |
| Design approval | Pattern.`ApprovedForCutting` | Not approved |
| Cutter test | Pattern.`CutterTestPassed` | Not recorded as passed |

Warnings (seam length mismatch, missing grain, no SA) do **not** block export.

## UI

**Export** page → **Production QC — Factory floor**

- Run QC automatically on load (`ValidateFactory`)
- **Approve for cutting** / **Revoke**
- **Record pass** / **Record fail** (cutter test)
- **Shrinkage %** → saved on pattern, written to factory `manifest.txt`
- **↓ Factory export** — enabled only when `CanExportToFactory`
- **CLO review ZIP** — `purpose=clo`
- **Draft ZIP** — `purpose=draft`

**Dashboard** shows a badge per pattern:

- **Factory ready** — approved + cutter test passed
- **Approved** — approved, cutter test pending
- **QC pending** — neither (or partial)

## API (`ExportController`)

| Method | Route | Purpose |
|--------|--------|---------|
| GET | `/Export/ValidateFactory?patternId=&style=` | QC report JSON |
| POST | `/Export/ApproveForCutting` | Body: `{ patternId, style?, actor? }` |
| POST | `/Export/RevokeApproval` | Body: `{ patternId }` |
| POST | `/Export/RecordCutterTest` | Body: `{ patternId, passed, actor?, notes? }` |
| POST | `/Export/SetShrinkage` | Body: `{ patternId, percent }` |
| GET | `/Export/DownloadPackage?...&purpose=factory\|clo\|draft` | ZIP download |

## Server enforcement

`ExportService.BuildExportPackage(..., ExportPurpose.Factory)` calls `IProductionCertificationService.ValidateForFactory` and throws `InvalidOperationException` if export is not allowed. Do not rely on UI alone.

## Database columns (`patternpro.patterns`)

- `ApprovedForCutting`, `ApprovedAt`, `ApprovedBy`
- `CutterTestPassed`, `CutterTestedAt`, `CutterTestedBy`, `CutterTestNotes`
- `CloReviewCompleted`, `CloReviewNotes` (informational)
- `ShrinkagePercent`

Migration: `AddProductionCertificationColumns`.

## End-to-end checklist (one pattern)

1. Create/open pattern on Dashboard → **Canvas** → edit/save pieces (Front Leg, Back Leg, Waistband minimum).
2. **Export** — resolve red **Blocking issues**.
3. **Approve for cutting**.
4. Run trial on plotter/cutter → **Record pass**.
5. **↓ Factory export** — download ZIP; verify `manifest.txt` shows certification lines.
6. Optional: **CLO review ZIP** before step 3 for drape only.
