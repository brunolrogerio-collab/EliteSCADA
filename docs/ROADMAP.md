# EliteSCADA Roadmap

**Status date:** 2026-09-10 BRT  
**Active direction:** **WAVE 14 — DIAGNOSTIC CLOSURE / WAVE 15 — CORRECTIONS NEXT / WAVE 13 — PRESERVED AND PAUSED UNTIL PRODUCT MATURITY**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable resume point: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Wave 14 closure contract: `docs/WAVE14-DIAGNOSTIC-CLOSURE-AND-WAVE15-TRANSFER.md`.  
CI policy: `docs/CI-VALIDATION-POLICY.md`.

> GitHub live remains the authority for current implementation, exact refs, issue/PR state and CI. This roadmap defines sequencing and scope, not permission to bypass live validation.

## Current validated foundation

- Waves 03–10: **COMPLETE / MERGED**.
- Seven communication Drivers shared convergence + L2 + integrated L3: **COMPLETE / ACCEPTED**.
- Demo/hardware-bound licensing and offline License Generator: **IMPLEMENTED / ACCEPTED / MERGED**.
- Pre-Wave-11 owner-usability gate: **COMPLETE / ACCEPTED / MERGED**.
- Wave 11 Active persisted Engineering HMI Runtime + owner-test `.escadapkg`: **COMPLETE / ACCEPTED / CLOSED**.
- Wave 12 Hardening: **COMPLETE / ACCEPTED / CLOSED**.
- Wave 13 repository-side Windows release/signing checkpoint: **PRESERVED / PAUSED**.
- Wave 14 accepted C26 corrections and post-C26 real-use audit evidence: **PRESERVED**; remaining Wave 14 scope is now diagnostic closure and transfer, not broad correction implementation.

## Ordered path to product maturity

```text
Waves 03–12   Core product/runtime/engineering foundation                               COMPLETE / ACCEPTED
Wave 14       Product-owner audit + finish diagnostics + correction contracts           ACTIVE / CLOSING
Main          Integrate accepted Wave 14 baseline + diagnostic transfer                 NEXT AFTER W14 EXIT GATES
Wave 15       Product corrections + developer-functional maturity                       NEXT ACTIVE DEVELOPMENT WAVE
Preview       Fresh Codespace Preview from corrected Wave 15 baseline                   AFTER W15 CORRECTIONS
Audit         Real browser audit + diagnostic/log reading                               AFTER FRESH W15 PREVIEW
Maturity      Product Owner maturity decision                                            AFTER W15 VALIDATION
Wave 13       Signed Windows x64 + Authenticode release verification                    PAUSED UNTIL EXPLICIT MATURITY DECISION
Driver L4     Physical hardware/site validation                                         LATER
FINAL         EliteSCADA v0.1 full product validation/release path                      LATER
```

The numerical order remains intentionally non-linear. Wave 13 release/signing work is not useful while core Engineering and Runtime workflows still expose material product defects or are not practical for a developer.

## Wave 14 — diagnostic closure

Wave 14 grew beyond a useful implementation boundary. The Product Owner changed its exit strategy on 2026-09-10 BRT.

Wave 14 now closes by **understanding and documenting the product**, not by attempting to fix every observed problem.

For each material finding intended for Wave 15, Wave 14 should preserve:

- reproduction/evidence authority;
- classification and severity;
- responsible subsystem/layer;
- concrete code/API/schema/lifecycle/projection path when determinable;
- causal mechanism;
- proposed generic correction contract;
- deterministic regression/acceptance proof;
- dependencies and ordering constraints;
- explicit security/runtime/lifecycle boundaries that must not be weakened.

Priority diagnostic closure areas:

- Engineering `Failed to fetch` / forwarding / route latency;
- Engineering Working `demo` vs Runtime Active `eee-demo`;
- persisted legacy visual schema crash in Screen/Popup editors;
- Runtime Trends silent return;
- Runtime Popup value mismatch/auto-return;
- Engineering recovery/fallback identity;
- deterministic responsive/accessibility/navigation/editor UI defects;
- Screen/Popup Editor developer usability gaps;
- Script Engineering / PO-PRE-07 end-to-end developer usability gaps.

Newly diagnosed product fixes should not expand Wave 14. They transfer to Wave 15 unless the Product Owner explicitly changes the boundary.

## Wave 14 -> `main` exit route

The intended route remains controlled:

1. complete the diagnostic closure/transfer package;
2. integrate canonical C11 through the authorized Wave 14 integration path;
3. propagate final diagnostic/roadmap/handoff documentation onto the integration route without treating the Preview branch as a product route;
4. require exact integration-head universal and impact-required validation;
5. diagnose red CI before any rerun;
6. merge the authorized Wave 14 integration PR to `main` only when closure criteria are satisfied;
7. validate the exact new `main`;
8. close obsolete Wave 14 audit/diagnostic/validation surfaces without merging any PR marked validation-only or `MUST NEVER MERGE` and without destructive branch cleanup.

The Product Owner decision on 2026-09-10 provides the required separate authorization for the Wave 14 integration PR to merge to `main` **after** these closure and validation conditions are met. It is not early/bypass authorization.

## Wave 15 — corrections and developer-functional maturity

Wave 15 is no longer a minor feedback/refinement bucket. It becomes the primary correction wave after Wave 14 is integrated.

Wave 15 starts from the exact validated new `main` and imports the Wave 14 diagnostic transfer as its initial backlog.

### Primary Wave 15 priorities

1. confirmed P1 lifecycle/identity/runtime correctness defects;
2. Screen/Popup Editor reliability and practical authoring;
3. Script Engineering practical authoring, discovery, validation and debugging;
4. Runtime Trends/Popup/recovery behaviors whose mechanisms were closed in Wave 14;
5. Engineering shell/navigation/responsive/accessibility correctness;
6. remaining deterministic product defects and usability barriers in dependency order.

### Screen/Popup Editor maturity bar

A normal developer must be able to perform representative authoring from the product UI without editing repository files, database rows or manual APIs. Supported visible controls must function. Unsupported actions must not pretend to be functional. Selection, move/resize, copy/paste, undo/redo, lock, supported grouping/alignment/z-order, zoom/pan, grid/snap, Properties, content editing, bindings and preview must form a coherent workflow. Persisted legacy objects must migrate/degrade safely and must not blank the application.

### Script Engineering maturity bar

A normal developer must be able to discover project objects and TAGs, author a representative script, receive understandable validation/errors, connect the script to the intended trigger/binding/lifecycle, save/publish/activate as applicable, and observe/debug runtime behavior through the product. Hidden APIs or documentation snippets alone do not make the feature developer-functional.

## Wave 15 validation endgame

After Wave 15 corrections are combined and exact-SHA green:

`corrected Wave 15 baseline -> fresh Codespace Preview -> technical readiness -> real browser audit -> diagnostic/log reading -> targeted correction/recheck where justified -> Product Owner maturity decision`

The Wave 14 Preview remains historical/audit evidence. A fresh Wave 15 Preview must be generated from the Wave 15 corrected baseline.

## Wave 13 — preserved, not automatically next

Issue #205 and PR #207 remain the preserved signed-Windows/AuthentiCode work checkpoint.

Wave 13 stays paused until the Product Owner explicitly decides that EliteSCADA has reached sufficient maturity for signing/release work to resume. Neither closing Wave 14 nor completing Wave 15 automatically resumes Wave 13.

When Wave 13 is eventually resumed, it must re-audit the then-current `main` rather than signing the historical pre-maturity product snapshot.

## Quality locks

- canonical Engineering/public model authority remains intact;
- Runtime derives from persisted Active Engineering, never mutable Working;
- security is enforced in the backend;
- no Driver-to-Driver coupling or canonical TAG/cache/event bypass;
- licensing remains host-owned and fail-closed;
- private licensing/signing keys never enter GitHub, normal CI or distributed builds;
- no Preview bootstrap password in repository/workflow/images/packages/logs;
- no test weakening to manufacture green evidence;
- EliteSCADA CI remains the universal integration/merge gate, with specialized CI impact-based;
- no direct `main` mutation outside the authorized PR route;
- no blind workflow rerun;
- Runtime/Active remains independent of `.escadalib`;
- Alarm / Operational Event / Audit remain distinct;
- no EEE-specific workaround for a generic product defect;
- Wave 13 signing/trust and DNP3 commercial-distribution gates remain separate from product maturity.
