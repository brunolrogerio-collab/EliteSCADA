# W14-C11 — Post-Correction Preview Validation Gate

**Date:** 2026-09-03 BRT  
**Owner:** W14-C11 audit / Product Owner validation lane  
**State:** **MANDATORY VALIDATION GATE / NO PRODUCT IMPLEMENTATION IN THIS BRANCH**  
**Original audited product-code SHA:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`

> This document records the browser-validation gate that must be executed against the **new corrected Preview/product baseline** after the authorized C12-C18 product corrections are integrated and converged. It exists so that items intentionally left outside an immediate correction package are not later misread as accepted, waived or already homologated.

## 1. Scope and authority

C11 remains an audit, requirements and validation lane. This branch does **not** implement C12-C18 product corrections and does not build the canonical EEE DEMO.

Coordinator-owned correction authority remains on:

`wave14/corrections-integration`

with PR #212 remaining DRAFT until the Coordinator/Development Lead decides otherwise.

The correction-package authority is:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

The Preview is a **validation harness**, not product authority. The product under validation must always be identified by an exact accepted product SHA. Preview/Codespaces infrastructure must not provide behavior that the product itself does not provide.

## 2. When this gate becomes executable

This validation gate is executed only after the Coordinator has established a new corrected baseline through the intended route:

`C12-C17 corrections`
`-> prerequisite integration`
`-> C18 when released`
`-> C10 convergence cycle 2`
`-> exact-head CI/specialized workflows`
`-> new exact product-code freeze`
`-> Preview updated to exercise exactly that accepted candidate`
`-> this C11 Preview validation gate`

A Preview run against the old frozen product `97eefd8...` cannot close findings whose product contracts are being changed by C12-C18.

## 3. Validation-only findings are release gates, not waived work

The following findings were intentionally **not** converted into immediate standalone correction packages because static/product evidence showed a credible architecture and the remaining question was mounted real-browser behavior.

They remain mandatory C11 release gates:

| Finding | Preview validation obligation | Pass condition | Failure consequence |
|---|---|---|---|
| `C11-P2-MEM-04` | Human authoring path from ordinary project: create Memory Source -> create TAG -> configure -> Save -> Publish -> Activate -> Runtime | Entire lifecycle works without hidden IDs/package editing/private fixtures | Reclassify the failing behavior as a product defect/gap and return it to Coordinator correction planning |
| `C11-P2-VIS-03` | Live Analog Fill bound to changing canonical TAG in mounted Runtime | Fill changes smoothly/correctly, clamps/scales as authored and matches Engineering intent | New/remaining visual/runtime product gap before C11 release |
| `C11-P2-DYN-02` | Pump running/fault/trip/unavailable/bad-quality semantics in real Runtime | States are deterministic and critical abnormal states are understandable without relying on color alone | New/remaining Dynamo/runtime semantics gap before C11 release |
| `C11-P2-DYN-03` | Two or more instances of one Dynamo definition with independent bindings/context | GMB01/GMB02-style instances remain independent in Engineering and Active Runtime | New/remaining Dynamo instance/binding gap before C11 release |
| `C11-P2-VIEW-01` | Fixed logical HMI at representative 720p/1080p/1440p/4K viewports | Uniform scaling/letterbox behavior is visually correct; no object reflow; authored geometry remains coherent | New/remaining Runtime viewport/scaling defect before C11 release |
| `C11-P2-FULL-01` | Native fullscreen, no document scroll, overlay and hit-target behavior across representative viewports | Fullscreen composition is stable; no clipping/reflow/scroll leakage; pointer targets remain aligned | New/remaining Runtime shell/composition defect before C11 release |

These items are not “outside Wave 14”. They are **inside Wave 14 acceptance**, but their current disposition is validation rather than speculative correction.

## 4. Corrected findings also require Preview revalidation

C12-C18 completion and green automated CI are necessary but are not sufficient for C11 release. The new Preview must also exercise the user-visible and end-to-end behavior produced by the corrections.

At minimum revalidate, where applicable:

- `SCR-01` / `SIM-01`: a generic project-authored server automation can actually drive changing shared process state through normal Active Runtime contracts;
- `QUAL-01`: simulated bad/non-Good quality is visible through the normal downstream runtime path;
- `EVT-01`: ordinary operational events can be authored/emitted/persisted/queried without abusing Alarm or Audit;
- `TREND-01/02/03`: Trend is authorable as a Screen/Popup object, persists through lifecycle, renders in Active Runtime and supports multiple independent Pens;
- `CMD-02`: authored Screen/Dynamo/Popup interaction can invoke a canonical Operational Command while backend authorization remains authoritative;
- `NAV-01`: configured startup/home Screen is honored after Save/Publish/Activate;
- `POP-02`: authored Popup X/Y persists and renders correctly in the logical coordinate/scaling system;
- `MEM-02`: Internal Memory authoring no longer exposes misleading network-address semantics;
- `BROWSER-01/02`: Alarm Browser and Event Browser are configurable first-class Screen/Popup objects and honor persisted filters/columns/scope;
- `I18N-HIST-01` and affected `I18N-01`: corrected history/browser UI is usable in `pt-BR`, `en` and `es`.

A correction is not considered homologated merely because its branch tests pass. C11 revalidation evaluates the integrated product behavior.

## 5. Integrated chain acceptance marker

`C11-P2-CHAIN-01` is the umbrella acceptance marker and intentionally has no standalone correction package.

The new Preview must prove the assembled canonical chain:

`project-authored Simulation/Server automation`
`-> canonical TAG/shared state`
`-> quality`
`-> Alarm + Operational Event + Historian`
`-> binding`
`-> Dynamo / Screen / Popup`
`-> Trend / Alarm Browser / Event Browser`
`-> operator interaction / authorized Command`

The proof must use normal product contracts and the normal lifecycle:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

If any required link still needs historical `SimulationDriver`, DEMO-only React/CSS/JS, direct DOM manipulation, hidden `.escadapkg` editing, private Driver/host memory access or authorization/licensing bypass, `CHAIN-01` remains unresolved and C11 implementation release is blocked.

## 6. Minimum Preview evidence record

Every C11 validation session against the corrected Preview must record enough information to be reproducible. At minimum:

- exact `wave14/corrections-integration` or accepted converged product SHA being exercised;
- exact new frozen product-code SHA once C10 convergence cycle 2 establishes it;
- Preview harness branch and exact Preview head SHA;
- confirmation that the Preview is exercising the intended product bytes rather than a stale build;
- browser used/version where relevant;
- viewport/resolution used;
- locale (`pt-BR`, `en`, `es`) for multilingual checks;
- project/revision/Published/Active identity where relevant;
- finding IDs exercised;
- observed result: PASS / FAIL / BLOCKED;
- concrete failure behavior when not PASS;
- screenshot/video/log reference when useful for a visual or lifecycle defect.

Evidence must distinguish product SHA from Preview infrastructure SHA. The Preview does not become a second product baseline.

## 7. Required viewport matrix

For HMI composition, scaling and fullscreen acceptance, exercise at least:

- `1280x720`;
- `1920x1080`;
- `2560x1440`;
- `3840x2160`;
- at least one non-16:9 viewport to observe expected letterbox/pillarbox behavior.

The accepted strategy remains a fixed `1920x1080` logical HMI canvas with uniform scaling. Validation is checking the implementation of that strategy, not reopening responsive reflow as a requirement.

## 8. Failure promotion rule

A validation-only item has exactly two legitimate outcomes after real Preview exercise:

1. **PASS:** retain/upgrade the finding based on evidence and record the exact validated SHA; or
2. **FAIL:** classify the concrete observed behavior as a defect/product gap, assign an appropriate correction owner/package through the Coordinator, integrate the fix, rerun required CI, establish a new exact candidate if product code changes, and repeat the affected Preview validation.

There is no third outcome where a failed C11 acceptance requirement is ignored because it did not have a pre-existing correction package.

Likewise, if a corrected C12-C18 finding fails integrated Preview homologation, it is **not closed** merely because the package was delivered.

## 9. PLC boundary

`C11-P2-PLC-01` and `C11-P2-MODBUS-01` remain a later physical-validation gate.

The new Simulation Preview must prove that the HMI/TAG architecture remains logically decoupled from physical mapping. It does not pretend to prove real PLC electrical/protocol behavior.

Later physical validation must record the real PLC, exact Modbus mapping/address semantics and resulting runtime behavior separately.

## 10. C11 release consequence

The existence of C12-C18 correction packages does not release C11 implementation.

C11 can recommend `RELEASE C11 IMPLEMENTATION` only after:

1. blocking product gaps have been corrected or explicitly dispositioned by Coordinator/Development Lead;
2. corrected code has converged on a new exact product SHA with required CI;
3. the new Preview exercises that exact candidate;
4. the validation-only findings in this document pass or any failures are corrected and revalidated;
5. corrected HMI/runtime findings pass integrated browser homologation;
6. `CHAIN-01` can be assembled through normal public product mechanisms;
7. remaining physical PLC-only items are clearly separated as later PLC validation rather than silently claimed as proven.

Until then the C11 recommendation remains:

`KEEP C11 IMPLEMENTATION LOCKED`
