# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 BRT  
**Operational state:** **WAVE 14 #211 ACTIVE / C12–C19 COMBINED AUTOMATED VALIDATION GREEN / FRESH CODESPACE VISUAL HOMOLOGATION PENDING / C11 IMPLEMENTATION LOCKED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only and validation-overlay commits do not redefine product-code authority.

## Current combined product candidate

C19 was isolated-accepted on product SHA:

`43d2734b18dc8b78caaa917b9bf6a381ca47b202`

It was composed into `wave14/corrections-integration` through PR #257 with history preserved.

Exact combined C12–C19 PRODUCT CANDIDATE:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

Tree:

`da6b406ac111cb40b99e5b13031601eb71606ddd`

This candidate is **not yet the new final product freeze** because the required fresh-Codespace visual/Product Owner browser homologation has not yet been recorded.

## Combined automated validation on 3fda880

Direct exact-candidate runs:

- EliteSCADA CI #1370 / `33934982242` — SUCCESS;
- Wave11 Active HMI Runtime #298 / `33934982300` — SUCCESS;
- Preview Licensing CI #320 / `33934982254` — SUCCESS;
- L3 Seven-Driver Lab #276 / `33934982215` — SUCCESS;
- Interop Lab Smoke #197 / `33934982216` — SUCCESS.

Dedicated C03 carry-forward:

- Wave 14 C03 DNP3 Adapter #113 / `33935067545` — SUCCESS;
- triggered by documentation-only overlay PR #260;
- overlay head `80a4bc7255ca46a0a52c401abed09dc4da0b4e2f` has direct product parent `3fda880...` and changes only the C03 documentation trigger;
- #260 was closed without merge after evidence collection.

Therefore the combined candidate has coherent automated C12–C19 carry-forward evidence across the required six gates.

## Test Preview carry-forward

The proven Preview infrastructure from historical validation overlay `38fce9f72f032f648c6ccda7cc86d00278a7eee6` was reapplied onto a disposable branch whose direct product parent is `3fda880...`.

Validation harness SHA:

`086b5d6c92ccfa4f5dc7e947c787ffd953acc5e2`

The diff from the product candidate contains only seven Preview infrastructure files (`.devcontainer`, `test-preview.yml`, Preview fixture and launcher), with no Preview-specific product correction.

Validation-only PR #262 targeted `main` solely to trigger the existing workflow and was closed without merge.

- Test Preview run `33935493882` — SUCCESS.

The run validated exact devcontainer SDK, disposable machine identity, Compose/TimescaleDB setup, automatic launch contract, backend/frontend setup, ephemeral administrator credential, normal Preview launcher lifecycle, browser entry and Pyodide asset.

This automated harness evidence does **not** replace the runbook-required fresh Codespace visual/Product Owner homologation.

## Current blocking boundary

The next required evidence is a **fresh Codespace / real browser visual homologation** using the prepared current Preview composition and the repository runbook.

Required acceptance includes real visual/use checks such as Engineering readability, changed pt-BR/en/es surfaces, Save -> Publish -> Activate, operator Runtime, scaling, no document scroll/reflow, Screen/Popup/Dynamo behavior and representative live alarm/event/trend/history behavior.

The current coordination surface does not expose a Codespaces/browser-interaction capability, so this evidence cannot be truthfully manufactured from CI or HTTP smoke.

Do not declare the new exact product freeze until that real-browser carry-forward is recorded.

## C19 architecture state

C19 remains accepted. Preserve:

- normal Operational Event Engineering authoring via protected Preview/CAS/Apply;
- generic `emit_operational_event(definition_id, message=None, context=None)`;
- C14 Active Runtime as canonical event authority;
- `ServerScriptRuntimeManager._revisionGate` as the single revision-serialization gate for Script TAG/Server Memory/Operational Event access;
- `ServerScriptRunner.py` as a normal Debug/publish output asset.

Do not restore the obsolete separate C19 gate/`AsyncLocal` design.

## Hard boundaries

- PR #212 remains OPEN/DRAFT and must NEVER merge to `main` without later explicit Product Owner authorization;
- C11 remains `IMPLEMENTATION LOCKED`;
- Wave13 #205/#207 remains PAUSED until final accepted Wave14 bytes;
- no weakening tests/security/identity/authorization/licensing/lifecycle;
- diagnose any red before rerun;
- no EEE-specific service/Driver/package/private runtime/DEMO-only bypass;
- Alarm / Operational Event / Audit remain distinct;
- backend Active revision remains canonical authority.

## Route after real-browser carry-forward

1. record real fresh-Codespace/browser evidence against the current product + Preview harness composition;
2. if it is coherent, declare a new exact product freeze with PRODUCT SHA `3fda880...` (unless a real defect requires new product bytes first);
3. resume remaining C11 browser evidence: Analog Fill live behavior, Dynamo operational/bad-quality state, two independent Dynamo instances, canonical Runtime resolutions/scaling, fullscreen/no-scroll/overlays without reflow, and one integrated living chain;
4. only when every C11 blocker is actually cleared, explicitly record `RELEASE C11 IMPLEMENTATION`;
5. construct the living deterministic EEE Simulation entirely through ordinary generic EliteSCADA mechanisms;
6. perform final Wave14 acceptance;
7. only then resume Wave13 #205/#207 packaging/signing on the final accepted bytes.

## Mandatory references

Read in order:

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. C19 implementation/progress documents;
6. C11 pre-DEMO / Pass-2 / HMI clarification / canonical DEMO requirements;
7. `docs/CI-VALIDATION-POLICY.md`;
8. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
9. live issue #211 and PR #212.

Validation PRs #260 and #262 are closed without merge. Historical #259 remains validation-only and must never merge.
