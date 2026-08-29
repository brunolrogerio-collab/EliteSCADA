# LAST CHANGE — EliteSCADA

Date: 2026-08-29

## Current checkpoint

Wave 08 FOLLOW-A is **CLOSED / MERGED / POST-MERGE GREEN**.

- PR #105 `FOLLOW-A: TAG bit access and Modbus bit-level binding` is merged into `main`.
- Merge/main commit: `bb0186cddc54946e8cc829c04a04b99495462304`.
- Pre-merge exact product head: `4e8a3c76753c1ead815c790407601852c6f888e3`.
- CI #541 passed on that exact product head.
- Current `main` was reconciled into FOLLOW-A before merge; reconciliation changed documentation only.
- Post-merge CI #543 passed on exact `main` commit `bb0186cddc54946e8cc829c04a04b99495462304`, including Web build, backend build/test/smoke and Chromium end-to-end.

## Active stage

Wave 08 FOLLOW-B is now the active gate: **Visual Expressions, Boolean Conditions and Analog Fill**.

Canonical contract: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.

FOLLOW-B must consume the stable TAG-bit identity defined and delivered by FOLLOW-A: stable `TagId + selector`; `.NN` is authoring/display syntax only.

Wave 09 remains **BLOCKED** until FOLLOW-B is implemented, accepted, merged and post-merge green.

## CI policy

CI mode remains **NORMAL**. Do not run reassurance CI on unchanged product trees. Final integration/product heads require exact-head green evidence before merge/activation transitions.
