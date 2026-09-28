# Wave 15 — CORRECTION-NOW Parallel Execution Control

**State:** ACTIVE  
**Control branch:** `coord/w15-correction-now-parallel-control`  
**Shared contracts:** `docs/WAVE15-CORRECTION-NOW-SHARED-CONTRACTS.md`  
**Live integration at control creation:** `wave15/corrections-integration@00d17e716b877e4cc00e25ea093f53f0da485c24`

This file is the common execution map for parallel ChatGPT/CODEX DEV/AUD chats.

Every agent must revalidate GitHub live before work. If this file conflicts with a newer Main order or live issue comment, the newer Main order wins.

## 1. Global operating rules

1. No direct writes to `main` or `wave15/corrections-integration`.
2. One bounded branch per lane.
3. One contract owner for every shared public semantic.
4. Consumers never invent missing shared semantics; return `BLOCKED_CONTRACT`.
5. Product Owner is not a message bus between agents; handoffs go to GitHub owner/control surfaces.
6. A DEV never self-merges or declares VERIFIED/FROZEN.
7. Mounted UI evidence is mandatory for user-facing corrections.
8. All product branches start from an exact integration SHA published by Main.
9. After any upstream shared-contract integration, downstream consumers revalidate/rebase against the new integration HEAD before continuing.
10. Avoid parallel edits to the same high-conflict composition files unless Main explicitly partitions exact files/symbols.

## 2. Active topology

### LANE A — P0 Runtime / project authority

**Role:** CODEX-AUTHORITY  
**State:** ACTIVE  
**Issue:** #354  
**Branch:** `work/w15-p0-demo-runtime-authority-correction`  
**Base:** `00d17e716b877e4cc00e25ea093f53f0da485c24`  
**Owns contract:** `C-AUTHORITY-01`

Mission:
- eliminate unexplained Demo Runtime authority/fallback;
- preserve valid explicit Demo licensing/session behavior;
- freeze Working/Published/Active/Runtime identity semantics;
- return exact candidate + contract block.

This lane is high risk and integrates before broad downstream authority-sensitive work.

### LANE B — Data Source / TAG first-user path

**Role:** DEV-DATA  
**State:** ACTIVE  
**Issue:** #355  
**Branch:** `work/w15-p1-datasource-authoring-correction`  
**Base:** `00d17e716b877e4cc00e25ea093f53f0da485c24`  
**Consumes:** `C-SURFACE-01`  
**Potential dependency:** `C-TRANSPORT-01`

Mission:
- prove/fix local mounted Data Source -> TAG -> Runtime path;
- keep local-pass/remote-fail differential;
- if only latency reproduces, stop UI-specific timing invention and hand back to C-TRANSPORT owner.

### LANE C — First-user Editor correction

**Role:** DEV-EDITOR-CORE  
**State:** ACTIVE  
**Issues:** #303 + bounded #357/#367/#368 slice  
**Branch:** `work/w15-editor-first-user-correction`  
**Base:** `00d17e716b877e4cc00e25ea093f53f0da485c24`  
**Owns contract:** `C-VISUAL-IDENTITY-01`  
**Consumes:** `C-SURFACE-01`

First slice only:
- Property readability;
- rectangle fill/stroke;
- Text literal content + rename;
- selection synchronization;
- top/collapse/layout corrections;
- Screen/Popup parity;
- save/reopen + undo/redo.

This lane must return a **contract proposal for stable object reference/property metadata** before advanced dynamics/scripts are released.

### LANE D — Remote/Security A/B diagnostics

**Role:** AUD-REMOTE / DIAGNOSTIC  
**State:** ACTIVE_DIAGNOSTIC  
**Issues:** #359 + #307  
**Branch:** `work/w15-security-remote-ab-diagnostic`  
**Base:** `00d17e716b877e4cc00e25ea093f53f0da485c24`  
**Owns contract:** `C-TRANSPORT-01`

Order:
1. local normal;
2. local injected latency/jitter;
3. remote/Codespace.

No product mutation until root layer is bounded and Main issues correction order.

---

## 3. Prepared downstream lanes

These lanes may inspect/read and prepare a handoff, but product implementation waits for the stated contract gate.

### LANE E — Advanced Editor dynamics + HMI IO

**Role:** DEV-HMI-DYNAMICS-IO  
**State:** WAIT_CONTRACT  
**Issues:** #367 + #368  
**Required before start:** `C-VISUAL-IDENTITY-01 = FROZEN_FOR_CONSUMERS`  
**Owns next:** `C-VISUAL-DYNAMIC-01` and extension of `C-TAG-WRITE-01`

Mission after release:
- Animations surface;
- visibility/conditions/Analog Fill;
- typed range->color/style;
- dynamic Text/value display;
- `core.numericInput`;
- Popup/touch setpoint flow.

### LANE F — Script object/property authoring

**Role:** DEV-SCRIPT-OBJECT  
**State:** WAIT_CONTRACT  
**Issue:** #369  
**Required before start:** `C-VISUAL-IDENTITY-01 = FROZEN_FOR_CONSUMERS`

Mission after release:
- object browser;
- property browser from canonical registry;
- generated/assisted `visual_property_read/write/clear`;
- tween assistance;
- visual event context;
- rename-safe stable references.

No new Python runtime architecture.

### LANE G — Reusable objects integration

**Role:** DEV-REUSE  
**State:** PREPARED_READ_ONLY_AUDIT  
**Issues:** #356 + #365 + #308  
**Owns:** `C-REUSE-01`  
**Consumes eventually:** `C-VISUAL-IDENTITY-01`

May audit current Template/Equipment/Dynamo schemas and propose the relationship contract now.

Product implementation that touches Editor insertion/reference identity waits until C-VISUAL-IDENTITY freezes.

### LANE H — TAG Gateway

**Role:** DEV-GATEWAY  
**State:** READY_AFTER_C0  
**Issue:** #364  
**Consumes:** `C-GATEWAY-01` existing base, `C-SURFACE-01`; may consume C-TRANSPORT for remote behavior.

Can execute in parallel after the first C0 correction candidate is under Main review if file overlap remains low.

Do not mix Gateway route semantics into Data Source UI.

---

## 4. Merge/integration queue

Main integrates sequentially. Suggested order based on current evidence:

### Queue Q0 — correctness authority
1. #354 P0 authority correction.
2. Any required immediate regression-only follow-up.

### Queue Q1 — first-user product path
3. #355 Data Source/TAG correction **if independent of C-TRANSPORT**.
4. #303 first-user Editor basic correction.

#355 and #303 may swap order if one finishes earlier and their changed files are independent.

### Queue Q2 — shared contract consumers
5. C-VISUAL-IDENTITY freeze/integration from Editor lane.
6. release #369 Script object UX.
7. release #367/#368 dynamics/HMI IO.

Script and HMI-Dynamics may develop in parallel **after** the same exact visual-identity contract is frozen.

### Queue Q3 — disconnected product surfaces
8. #356/#365/#308 reusable objects.
9. #364 Gateway.

These may run in parallel when changed-file overlap is low.

### Queue Q4 — remote correction
10. #307/#359 correction after diagnostic contract freeze.
11. re-run concrete #355/#359 remote acceptance scenarios.

### Queue Q5 — integrated verification
12. activate Stage 2 V2-01..V2-20 on exact integrated candidate.
13. corrections discovered by Stage 2 get new bounded branches/orders.
14. only then prepare the next Human Preview.

---

## 5. Contract and branch handoff format

Every DEV return must start:

`<ROLE> -> MAIN COORDINATOR — <ORDER/ISSUE> HANDOFF`

Include:
- branch;
- exact HEAD;
- exact tree;
- exact base;
- changed-file list;
- contract IDs owned/consumed;
- tests;
- mounted UI evidence for user-facing work;
- remaining uncertainty;
- explicit non-actions;
- whether downstream consumers may now start.

If the lane owns a contract, include:

`CONTRACT PROPOSAL — <ID>`

with:
- public semantics;
- stable identifiers/types;
- lifecycle/precedence;
- negative rules;
- compatibility/migration;
- consumer list.

No downstream agent treats a DEV proposal as frozen until Main records:

`<ID> = FROZEN_FOR_CONSUMERS @ <exact integrated SHA>`.

---

## 6. AUD model

For every product correction candidate:

### AUD-1 — bounded review
Independent chat/reviewer:
- reads exact issue/order;
- compares exact base->candidate;
- checks scope;
- checks contract compliance;
- validates negative boundaries;
- does not fix product in the AUD branch unless Main explicitly switches mode.

### AUD-2 — mounted acceptance
For user-facing work:
- use normal product UI;
- perform exact acceptance journey;
- capture errors/states;
- do not use internal source/API shortcuts to manufacture a pass.

### AUD-3 — integration revalidation
After Main integrates:
- rerun focused regression on integration HEAD;
- check no conflict altered public behavior.

---

## 7. Conflict-avoidance map

High-conflict files/surfaces likely include:
- Engineering app composition/navigation;
- visual editor workspace/canvas/property inspector;
- shared Engineering visual contracts/registry;
- Runtime projection/authority/persistence paths;
- common request/fetch wrappers.

Rules:
- only Editor lane edits primary visual workspace composition until its first contract slice returns;
- Script lane avoids shared visual contract edits and consumes exported metadata;
- HMI Dynamics lane waits for identity/property contract;
- Reuse lane may audit schemas but waits before editor-reference integration;
- Remote diagnostic lane does not patch common request wrappers until C-TRANSPORT proposal is reviewed.

---

## 8. Human Preview checkpoints

Do not wait for all work.

### Preview C0
After #354 + first-user Data Source path:
- fresh project;
- no Demo leak;
- Data Source -> TAG -> Runtime.

### Preview UX-A
After basic #303 correction:
- rectangle color/stroke;
- Text content/name;
- workspace hierarchy;
- Screen/Popup save/reopen.

### Preview UX-B
After dynamics/HMI IO + Scripts:
- animation;
- live Text/display;
- numeric setpoint;
- script object/property access.

### Preview UX-C
After reusable objects + Gateway:
- Template/Equipment/Dynamo;
- visual preview;
- Gateway multi-route.

### Final directed verification
Stage 2, then complete fresh Preview.

---

## 9. Current branch states

At creation of this control:

`work/w15-p0-demo-runtime-authority-correction` -> ACTIVE  
`work/w15-p1-datasource-authoring-correction` -> ACTIVE  
`work/w15-editor-first-user-correction` -> ACTIVE  
`work/w15-security-remote-ab-diagnostic` -> ACTIVE_DIAGNOSTIC

PR #362 local operator remains PARKED / NO LIFECYCLE / NO MERGE pending separate full review of its expanded scope.

## 10. Immediate Main actions

1. monitor CODEX #354 handoff;
2. obtain C-VISUAL-IDENTITY proposal from Editor lane before releasing Script/Dynamics;
3. obtain C-TRANSPORT proposal from diagnostic lane before remote timing correction;
4. keep downstream lanes prepared but contract-gated;
5. integrate only exact reviewed candidates sequentially;
6. update this control file whenever a contract freezes or lane changes state.
