# Intelligence preview: profile persistence and bounded history (#864)

## State — 2026-11-04

Product baseline: d48febfc. Correction PR #865; no claim of deployment until exact candidate and post-merge checks complete.

The owner reported that profiles disappeared after save/apply/activate and a Runtime round trip. Stored revisions 1–7 had no profiles or assignments; revision7 contained only one selected scope. This does not demonstrate deletion of a persisted profile or establish the exact sequence of the owner's actions.

Two authoring defects were identified: scope saves used the full Engineering reload callback, unmounting unsaved profile edits; configuration saves reset a separately edited scope selection. Use the existing in-place snapshot refresh and preserve the scope selection during configuration adoption. A full-page departure warns about pending local edits; internal Engineering navigation also asks whether to discard them. Cancelling keeps the mounted editor intact. Save rejection leaves the draft intact and explains controlled-unit prerequisites. No autosave or authorization bypass was introduced. Help/manual distinguish configuration, scope selection and revision save/publication/activation.

## Real local preview evidence

With owner authorization, installed an ESLIC3 test licence signed by the existing generator/key, enabling Intelligence without adding HA or changing trust anchors/Portal. Private rollback material remains outside Git. Licence validity ends 2026-11-18 UTC; the existing online binding remains separate and must be considered during later renewal tests.

Created independent profiles/assignments for both pumps through the authenticated integrated browser. Current and frequency have Historian capture enabled; voltage does not, so the final test profiles contain only current/frequency. Both profiles survived revision save, publication, activation and re-entry to Engineering. Both scopes were explicitly rearmed on revision43 and shown ready. No Authority changes or process commands were issued.

Window 2026-11-04 14:00–14:05 America/Sao_Paulo:

| Pump | Current Good samples | Frequency Good samples | Rejected samples |
| --- | ---: | ---: | ---: |
| 01 | 195 | 190 | 0 |
| 02 | 191 | 190 | 0 |

Both reports correctly show no exact timestamp intersection; do not claim synchronized acquisition, efficiency or a predictive result. These are simulated application signals observed through the real API/Historian/UI.

The integrated-browser date fill operation initially displayed values without committing the controlled form state. Native date controls plus a keyboard change confirmed the actual state, and the short-window queries succeeded. Do not classify that automation issue as a product date-input defect.

## Signed cursor regression

Code review found a separate reproducible product defect: profile coverage shares a 50,000-observation budget across signals. If the first signal consumes a non-page-aligned count, the next signal eventually shrinks its page size while reusing a cursor. Historical Query signs the page size into its fingerprint and correctly rejects that cursor.

A regression uses the real HistoricalQueryService, validator and signed cursor codec with a bounded synthetic dataset provider: 199 rows in the first signal, then 49,800 / 49,801 / 49,802 rows in the next. Before correction, the exact-limit and over-limit cases failed (2/3), while the below-limit case passed. Keep page size constant per signal; existing pre-retention checks return the density error without a partial report. Do not raise row limits or loosen cursor validation. The long-window preview error is consistent with this defect; exact live response-code equivalence still requires revalidation after deployment.

## Validation and continuation

Final authoring/full-shell UI18/18 and TypeScript passed. The first full-shell fixture returned numeric scope kinds instead of the API snapshot enum names; normalizing that fixture fixed its disabled-button assertion, without changing the product for that test. The original analytical suite passed226/226; the new three cursor cases and complete analytical suite passed229/229 on the correction. Full Engineering shell regression covers independent scope saving without profile unmount and cancelled section navigation. Record final counts and exact CI IDs in #864/#865. Initial CI41853262709 stopped before tests because the PR declaration was missing; declaration corrected, exact-head dispatch41889873840 tracked separately. New code must receive its own SHA validation.

Preview currently runs the integrated baseline, not this candidate. Preserve application, licence and volumes on update. Then repeat the long-window density boundary and both pump queries in the integrated browser. Existing issue#863 owns contrast/theme repairs; no new HA, Portal, driver or Intelligence method is introduced here.
