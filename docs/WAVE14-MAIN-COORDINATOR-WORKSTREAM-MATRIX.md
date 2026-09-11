# Wave 14 — Diagnostic Closure Workstream Matrix

**Strategic pivot:** 2026-09-10 BRT

> GitHub live is the official memory and sole authority. Revalidate the relevant issue, PR, branch and SHA before every decision, write, rerun, integration or merge.

Main Coordinator: ChatGPT coordination session designated by the Product Owner.  
Diagnostic co-coordinator: GPT Codex with direct Codespace/application/browser/local-port access.  
Coordination ledger: #286.  
Closure plan: `docs/WAVE14-DIAGNOSTIC-CLOSURE-AND-WAVE15-TRANSFER.md`.

## New scope rule

Wave 14 no longer owns implementation of newly diagnosed corrections. Its remaining responsibility is to close the diagnostic package and transfer deterministic correction contracts to Wave 15.

A finding is ready for transfer only when the record contains, as far as evidence allows: reproduction authority, classification, responsible layer/path, causal mechanism, proposed correction contract, deterministic regression, dependencies and protected boundaries.

## State matrix

| Workstream | Wave 14 state | Owner | Wave 14 deliverable | Correction owner | Closure blocker? |
| --- | --- | --- | --- | --- | --- |
| A — Engineering transport / `Failed to fetch` / latency | DIAGNOSING | Codex | same-window browser/Vite/API/auth-proxy/process/log correlation; identify first divergent layer; separate environment transport from product recovery | Wave 15 only if product mechanism confirmed | **YES** for high-value mechanism/classification |
| B — Working `demo` vs Runtime `eee-demo` | REPRODUCED / DIAGNOSING | Codex | exact bootstrap/checkout/persistence/public-model origin; preserve correct Active/lifecycle rejection semantics | Wave 15 | **YES** |
| C — persisted legacy visual schema crash | CONFIRMED / MECHANISM TO CLOSE | Main + Codex support | exact known-legacy lookup/migration/recovery path vs truly unknown types; Screen + Popup + unknown regression contract | Wave 15 | **YES** |
| D — Engineering recovery/fallback identity | REPRODUCED / DIAGNOSING | Main + Codex | separate initiating transport from product fallback-state responsibility; exact state path and future UX contract | Wave 15 | YES if not covered by A |
| E — Runtime Trends silent return | REPRODUCED / DIAGNOSING | Codex | locate Historian/no-data/realtime/projection/navigation/error path; future data/no-data/error regressions | Wave 15 | **YES** |
| F — Runtime Popup `—` / auto-return | REPRODUCED / DIAGNOSING | Codex | locate binding/realtime/projection/navigation cause; stopped/running/zero/missing/quality/navigation regressions | Wave 15 | **YES** |
| G — Data Source / legacy-canonical mapping | REPRODUCED / TRANSFER PREP | Main | exact mapping/persistence path and round-trip regression contract | Wave 15 | No after contract is documented |
| H — Script Engineering / PO-PRE-07 | DIAGNOSTIC CLOSURE REQUIRED | Codex real-use + Main static | identify concrete gaps in discovery, authoring, validation/errors, binding/trigger/lifecycle, persistence and observable/debuggable runtime flow | Wave 15 | **YES** because developer usability is a primary maturity gap |
| I1 — shared responsive header | DETERMINISTIC / TRANSFER READY | Main | component/CSS location + 1024/~1080/1366/wide regression contract | Wave 15 | No |
| I2 — account-menu accessibility | DETERMINISTIC / TRANSFER READY | Main | component location + accessible-name/keyboard/focus regression contract | Wave 15 | No |
| I3/I4 — Engineering navigation + Lock footprint | DETERMINISTIC / TRANSFER PREP | Main | shell/layout location + scroll/collapse/canvas/Lock-preservation contract | Wave 15 | No after location/contract recorded |
| I5/I6 — resource previews + residual theme | DETERMINISTIC / TRANSFER PREP | Main | component locations and preview/state regressions | Wave 15 | No |
| J — TAGs / Sources identity coherence | REPRODUCED / TRANSFER PREP | Main | selected-identity state path + save/reopen regression | Wave 15 | No |
| K — custom roles/capability sets | SCOPE DECISION / PARKED | Main | decide whether required for near-term product maturity; do not mix with diagnostic fixes | Later Wave 15+ if authorized | No |
| L — P01/P02 accessible-name residue | NEEDS GENERIC-vs-PROJECT CLASSIFICATION | Main | identify component ownership and generic/project-specific scope | Wave 15 if generic/required | No |
| M — language/polish | PARKED | Main | transfer only if still material after functional corrections | Later | No |
| N — Alarm timestamp vs ledger | UNCERTAIN / EVIDENCE ONLY | Codex opportunistic | preserve same-occurrence comparison contract; do not manufacture defect | Wave 15 only if confirmed | No unless naturally reproduced |
| RECHECK-SIM-PUMP-LEVEL | PARKED / NOT CONFIRMED | Codex only on natural return | correlated API/Vite/browser/WS/TAG/process evidence before restart/reopen | Wave 15 only if confirmed product defect | No; **no blind #296 rerun** |

## Diagnostic dependency graph

`A TRANSPORT CORRELATION -> D RECOVERY CLASSIFICATION + better confidence for E/F`

`B WORKING/RUNTIME ORIGIN -> G/J/H Engineering-domain correction planning`

`C LEGACY SCHEMA MECHANISM -> Wave 15 editor compatibility correction`

`H SCRIPT ENGINEERING GAP ANALYSIS -> Wave 15 developer-functional script plan`

Deterministic I/J items can be transferred without blocking the live A/B/E/F diagnostics once their component/path and regression contracts are explicit.

## Wave 14 exit gates

Wave 14 may move to integration/closure when:

1. A/B/C/E/F/H have a usable mechanism/path/correction-contract record or an explicitly justified remaining uncertainty that cannot be resolved without new external evidence;
2. deterministic UI/editor findings have concrete component/location + regression contracts;
3. the transfer package is internally reconciled with the latest audit chronology;
4. newly diagnosed fixes have not been mixed into Wave 14 branches merely to reduce the backlog;
5. canonical C11 and integration topology are revalidated;
6. selected closure documentation is ready to propagate to the integration route.

## Integration sequence after diagnostic closure

1. revalidate #263 and integrate canonical C11 into `wave14/corrections-integration` through the authorized PR route;
2. propagate selected final diagnostic/roadmap/handoff documentation to the integration branch without merging Preview harness content;
3. exact-SHA run universal and impact-required validation;
4. diagnose any red result before rerun;
5. revalidate #212 head/base/mergeability and expected head SHA;
6. use the Product Owner's conditional 2026-09-10 authorization to merge #212 -> `main` only when closure and validation gates are satisfied;
7. validate exact new `main`;
8. close Wave 14 issues and obsolete audit/diagnostic/validation PRs without merging MUST-NEVER-MERGE routes and without deleting preserved branches;
9. create Wave 15 issue/branch from exact validated new `main`.

## Wave 15 priority transfer

Wave 15 becomes the correction wave. Highest product-maturity priorities include:

1. developer-functional Screen/Popup Editor;
2. developer-functional Script Engineering;
3. confirmed Working/project identity lifecycle defect;
4. confirmed Runtime Trends/Popup/recovery mechanisms;
5. deterministic Engineering shell/responsive/accessibility defects;
6. remaining confirmed defects in dependency order.

A normal developer must be able to author Screens/Popups and Scripts end-to-end through the UI without repository/database/manual API intervention. Supported visible controls must work. Unsupported controls must not masquerade as functional. Script flow must include discoverability, understandable validation/errors, binding/trigger/lifecycle integration, persistence and observable/debuggable runtime behavior.

## Post-Wave 15 validation

`Wave 15 corrections -> exact-SHA green -> fresh Codespace Preview -> technical readiness -> real browser audit -> diagnostic/log reading -> targeted correction/recheck -> Product Owner maturity decision`

Wave 13 remains preserved and paused until that later Product Owner maturity decision explicitly resumes it.

## Permanent boundaries

- GitHub live wins over stale docs/chat for current state;
- #290 is Preview/evidence only and not a route to `main`;
- #296 is diagnostic-only / MUST NEVER MERGE / no blind rerun;
- validation-only/MUST-NEVER-MERGE PRs remain non-merge routes;
- no direct `main` mutation; use the authorized PR route;
- no force push, destructive rebase or branch deletion;
- never weaken tests, security, Identity, authentication, authorization, Engineering Lock, Licensing, lifecycle, package, Active Runtime authority, Historian semantics or Drivers;
- Runtime/Active remains independent of `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- no EEE-specific workaround for a generic product defect;
- Wave 15 completion does not automatically resume Wave 13.
