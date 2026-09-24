# Wave 15 — FC0-A Post-FND06 Audit — Third Pass

**Date:** 2026-09-24 BRT  
**Owner:** Main Coordinator  
**Mode:** READ_ONLY / GAP SEARCH  
**Exact product baseline:** `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`  
**Tree:** `674019fbbc21001a2d68deb853c2c0b293e0a5cb`  
**Known broad gate:** EliteSCADA CI `35953557122` — SUCCESS

## Outcome

`THIRD PASS -> NO NEW PRE-FC0A BLOCKER IDENTIFIED`

The authoritative FC0-A release blockers remain unchanged:

1. `W15-P1-01` — Server Script permanent throttle latch / missing bounded automatic recovery.
2. `W15-P1-06` — synthetic no-model Engineering identity/status.

This pass does **not** widen `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`.
P1-06 remains queued and separate.

At completion of this pass, `work/w15-fc0a-p101-server-script-recovery` was revalidated against the exact product baseline:
- identical;
- 0 commits ahead / 0 behind;
- no changed files;
- no PR found for the branch.

## Third-pass scope

This pass deliberately emphasized surfaces that were peripheral to the first two post-FND06 reviews:
- W15-P2-03 shared-shell responsiveness;
- W15-P2-04 account accessibility;
- W15-P2-05 Engineering navigation/scroll;
- W15-P2-06 Engineering Lock footprint;
- W15-P2-07 Templates / Equipment / Dynamos / reusable libraries;
- contextual Help routing;
- FND-05/FND-07 prepared control-state and cross-contract coverage.

## Findings

### TP-01 — W15-P2-03 remains a bounded downstream shell-layout gap

Exact source:
- `web/scada-web/src/app-navigation.css`
- `web/scada-web/src/AppNavigation.tsx`

The privileged shell can render Runtime + Engineering + Audit + Licensing + Help together with brand, theme and account actions.
Above the `max-width: 900px` simplification breakpoint, navigation links retain `min-width: 112px`, while the grid also reserves brand/action space. This leaves a notebook-width range where the shell remains structurally vulnerable to horizontal crowding/overflow.

Current E2E covers a 1024x720 Engineering visual workspace but does not assert global shell/document no-overflow in the relevant breakpoint range.

Classification:
`DOWNSTREAM UI / W15-P2-03 OPEN-REVALIDATED`

Not a Foundation/FC0-A blocker.

### TP-02 — W15-P2-04 should not remain classified as an unimplemented account-accessibility defect

Exact source:
- `web/scada-web/src/auth/UserSessionMenuView.tsx`
- `web/scada-web/src/auth/user-session-menu.css`
- `web/scada-web/tests-e2e/interface-wave-03-readiness.spec.ts`

Current implementation already provides:
- native `details/summary` keyboard semantics;
- explicit accessible account label;
- explicit Escape close + focus restoration;
- native buttons for actions;
- visible focus styles;
- alert semantics for action failures.

Existing E2E asserts the account accessible label and basic use, but does not directly exercise the full keyboard contract (for example Escape/focus restoration).

Classification:
`IMPLEMENTED BEHAVIOR / REGRESSION EVIDENCE PARTIAL`

Wave 15 should convert P2-04 into focused revalidation/regression coverage rather than reimplementing the account menu.

### TP-03 — W15-P2-05 remains open, but visual-editor local mitigation already exists

Exact source:
- `web/scada-web/src/engineering/engineering.css`
- `web/scada-web/src/engineering/visual-editor/VisualEditorLayoutControls.css`
- `web/scada-web/tests-e2e/app-shell.spec.ts`

Positive evidence:
- visual-editor Screen list / palette / inspector have bounded internal vertical scrolling;
- navigation/screens/palette/properties can be collapsed;
- mounted E2E proves the canvas can reclaim width at 1024x720.

Residual:
- the outer Engineering body still uses page-level `min-height` flow rather than a viewport-constrained application frame with independent sidebar/workspace scrolling;
- the Engineering sidebar itself has no bounded desktop vertical scroll contract;
- the page/document therefore remains the main vertical scroll container.

Classification:
`DOWNSTREAM UI / W15-P2-05 PARTIALLY MITIGATED, STILL OPEN`

Not a Foundation/FC0-A blocker.

### TP-04 — W15-P2-06 Engineering Lock footprint remains open and compounds P2-05

Exact source:
- `web/scada-web/src/engineering/EngineeringLockGate.tsx`
- `web/scada-web/src/engineering/engineering-lock.css`

When unlocked, `EngineeringLockGate` always mounts the full `EngineeringLockManagement` section before `EngineeringApp`.
That management surface contains summary, secret field, configure/lock actions and lifecycle guidance and occupies a full-width multi-row block on every Engineering route.

Authority/Lock semantics are sound and must not be weakened. The gap is purely composition/density.

Classification:
`DOWNSTREAM UI / W15-P2-06 OPEN-REVALIDATED`

Preferred correction is a compact/disclosed management entry point that preserves backend authority, diagnostics and explicit lifecycle semantics.

### TP-05 — W15-P2-07 is partially closed; scope must be refined

Exact source:
- `web/scada-web/src/engineering/EngineeringApp.tsx`
- `web/scada-web/src/engineering/visual-editor/DynamoLibraryPalette.tsx`
- `web/scada-web/src/engineering/ReusableLibraryWorkspace.tsx`

Positive evidence:
- the visual-editor Dynamo palette already offers search/category filtering, a selected-Dynamo preview, dimensions, public parameter interface and equipment-path authoring;
- reusable libraries expose identity/version/hash, resource kind/key and dependency closure metadata.

Residual:
- top-level Templates and Equipment surfaces remain generic tabular listings without rich preview/inspection;
- the top-level Dynamos listing is also generic, although insertion-time Dynamo preview exists inside the visual editor;
- reusable-library resources can be incorporated after list/dependency inspection, but there is no resource-content/visual preview surface before Use.

Classification:
`DOWNSTREAM PRODUCT UX / W15-P2-07 PARTIALLY IMPLEMENTED`

Do not rebuild the existing Dynamo insertion palette. Concentrate future scope on Template/Equipment inspection and resource preview/metadata where it is still absent.

### TP-06 — contextual Help is surface-level, not section-contextual

Exact source:
- `web/scada-web/src/AppNavigation.tsx`
- `src/Scada.Api/Runtime/ContextualHelpApi.cs`

The shipped Help catalog contains specific topics such as `scripts.server`, but the global shell resolves every `/engineering...` location to `engineering.overview`.
Repository search found no Engineering section-level `/help?topic=...` links outside the Help app itself.

Classification:
`PRODUCT CONVERGENCE / HELP UX GAP`

Not a pre-FC0A blocker. Preserve as a later Wave 15 contextual-Help closure item.

### TP-07 — FND-07 has a pre-activation cross-contract coverage gap: Engineering Lock × detach/switch

Existing product contract states that a locked application still permits approved application replacement/import/restore administration under backend Authority, without treating the Engineering Lock secret as an Authority credential.

Prepared FND-07 correctly preserves lifecycle/Authority/licensing contracts, but its current acceptance matrix does not explicitly cover how installation detach/neutral-bootstrap interacts with Engineering Lock.

This pass does **not** invent a new detach semantic. It adds a pre-activation requirement:
- FND-07 must explicitly reconcile detach/switch with the existing Engineering Lock replacement/recovery exemptions;
- backend Authority remains authoritative;
- no protected Engineering content may leak;
- no second credential/lock bypass may be invented silently;
- whichever path is used must have deterministic locked/unlocked regression coverage.

Classification:
`FND-07 PRE-ACTIVATION CONTRACT-COVERAGE GAP / NOT A CURRENT FC0-A BLOCKER`

### TP-08 — FND-05/FND-07 control metadata was stale

Both prepared control planes still described themselves as blocked on the post-FND06 audit and retained a pre-FND06 provisional checkpoint even though:
- FND-06 is VERIFIED/FROZEN at `560ac9d...`;
- the audit completed with `CHANGES_REQUIRED`;
- second pass found no new pre-FC0A blocker;
- both Foundations remain HOLD specifically until P1-01/P1-06 closure and FC0-A release approval.

Control metadata should be corrected without activating product work.

## Release impact

No release-state change:

- DEV-EDITOR — HOLD
- DEV-SCRIPT-ENGINEERING — HOLD
- DEV-AUTHORITY-UX — HOLD
- DEV-LICENSING-UX — HOLD
- FND-05 — HOLD
- FND-07 — HOLD

Active sequential correction remains:
`FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`

Queued correction remains:
`FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`

After both are integrated with exact evidence, Main must rerun the affected FC0-A audit rows before any release.

## Third-pass conclusion

`THIRD PASS -> NO NEW PRE-FC0A BLOCKER IDENTIFIED`

The pass found useful downstream refinements and one FND-07 pre-activation coverage gap, but nothing that should interrupt, widen or mix into the active P1-01 correction.
