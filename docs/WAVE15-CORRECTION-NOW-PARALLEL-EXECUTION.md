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


---

## 11. Mandatory second Preview + Audit before returning to original Wave 15 flow

**Product Owner decision:** the current CORRECTION-NOW program is a temporary correction phase. It does **not** replace the original Wave 15 complete-product plan.

The return sequence is binding:

```text
CORRECTION-NOW
  -> integrated correction candidate
  -> SECOND PREVIEW + AUDIT ROUND
  -> residual correction/recheck if needed
  -> CORRECTION PHASE ACCEPTED
  -> resume original Wave 15 deferred flow
```

There is no direct jump from individual correction PRs to EliteGO/Redundancy/new architecture work.

### Phase R2-0 — correction candidate assembly

Entry:
- C0/C1 material corrections integrated;
- required contract owners frozen/integrated;
- C2 user-facing disconnected surfaces completed to the degree scheduled for this correction round;
- exact integration SHA selected;
- exact-SHA CI/focused regression green;
- local/remote harnesses rebuilt from that exact candidate;
- no known unresolved P0/P1 that invalidates a fresh journey.

Output:
`W15-CORRECTION-ROUND2-CANDIDATE = READY`.

### Phase R2-1 — independent fresh Preview round 2

Run a second fresh-install / first-project journey against the same exact correction candidate.

Two independent views are preferred again:

1. **CODEX black-box/product-visible Preview**
   - clean state;
   - normal UI/Help;
   - no source/internal shortcuts during discovery;
   - create a real first project and reach a useful Runtime.

2. **Product Owner Human Preview**
   - independent clean state;
   - no CODEX navigation hints before Human disposition;
   - normal user path;
   - specifically re-exercise the failures found in round 1.

Minimum Round-2 user path:
- fresh bootstrap/no hidden Demo;
- project creation;
- Data Source Type selection;
- TAG creation/use;
- Working -> Save/Publish -> Activate -> Runtime;
- Security/User administration;
- Screen + Popup authoring;
- rectangle fill/stroke;
- Text literal + dynamic display where integrated;
- NumericInput/setpoint where integrated;
- Animations/context/group workflow where integrated;
- Script object/property authoring where integrated;
- Library/Dynamo preview and reusable-object workflow where integrated;
- TAG Gateway multi-route flow where integrated;
- restart/reopen persistence;
- truthful local/remote failure states.

Do not declare the correction phase accepted merely because CI is green.

### Phase R2-2 — directed audit / Stage 2

After the Round-2 independent journey evidence is sealed, activate the prepared directed verification matrix on the **same exact product candidate**.

Use:
`docs/WAVE15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION.md`

Revalidate the matrix before activation and run the applicable V2-01..V2-20 cases, including:
- Working/Published/Active/Runtime authority;
- transport/recovery truth;
- Editor selection/property behavior;
- Script Engineering;
- Server Script recovery;
- Runtime projection/navigation;
- Popup values;
- Trends/Historian/realtime;
- Authority/Licensing;
- Neutral/detach;
- fresh-project Demo absence;
- Data Source -> TAG;
- Templates;
- Editor first-user usability;
- Library/Dynamo preview;
- icon/toolbox;
- Security/remote A/B behavior.

Stage 2 is verification, not an in-place correction session.

### Phase R2-3 — residual correction loop

If Round 2 or Stage 2 finds a material defect:

```text
finding
 -> bounded issue/contract owner
 -> isolated correction branch
 -> Main/AUD review
 -> integrate
 -> exact affected recheck
```

For any P0/P1 or shared-contract regression, repeat the relevant fresh Round-2 journey before phase acceptance.

Minor bounded residuals require explicit Main/Product Owner disposition; they are not silently carried forward.

### Correction-phase exit gate

Main may record:

`W15-CORRECTION-NOW = ACCEPTED / RETURN_TO_ORIGINAL_W15_FLOW`

only when:
1. second Human Preview has an accepted disposition;
2. CODEX independent Preview has an accepted/bounded disposition;
3. Stage 2 directed verification is complete for applicable items;
4. no material correction finding remains unresolved;
5. exact integrated correction SHA is documented;
6. current contract/control docs are synchronized.

Until that record exists:

`ORIGINAL_W15_DEFERRED_FLOW = HOLD`.

---

## 12. Resume point — original Wave 15 complete-product plan

After the correction-phase exit gate, resume the authoritative original Wave 15 plan from issue #297 rather than inventing a new roadmap.

The deferred major work resumes with:

1. **#298 EliteGO**
   - distinct companion application;
   - consume public/versioned EliteSCADA contracts;
   - redundancy-oriented companion functions;
   - never become a second industrial authority.

2. **#299 Redundancy / HA**
   - continue from already-established Foundation/authority work;
   - complete the accepted distributed-authority/failover product capability;
   - coordinate its public contract with EliteGO.

3. **remaining original Wave 15 complete-product work**
   - integration of corrections + Editor + Scripts + EliteGO + HA;
   - configuration/package topology;
   - bounded Trends/Popup/P2 retests;
   - exact-SHA complete-product CI.

4. **#300 final complete-product assembly + fresh Preview**
   - this is a later final Wave 15 gate;
   - it is distinct from the correction Round-2 Preview described above;
   - final Preview must include representative EliteGO + redundancy behavior in addition to the corrected EliteSCADA core.

Important distinction:

```text
Correction Preview Round 2
    validates the corrected current product
    and gates return to deferred Wave 15 work.

#300 Final Wave 15 Preview
    validates the complete product after EliteGO + HA + all deferred Wave 15 work.
```

Do not collapse these two gates.

Wave 13 signing/release work remains separately paused unless the Product Owner later changes that decision.


---

## 13. Central validation queue — environment-capability aware

Parallel development must not assume every chat can run Docker, browsers, local services, Codespaces or latency injection.

All lanes consume `C-TEST-EVIDENCE-01`.

### DEV responsibility

DEV must:
- implement the bounded correction;
- add/maintain deterministic regressions;
- run the highest evidence tier available;
- return exact candidate SHA/tree;
- state environment capability truthfully.

DEV is **not blocked merely because E3/E4 is unavailable** if the code/test candidate is otherwise reviewable.

### Main validation routing

After a candidate handoff, Main routes missing evidence in this order:

1. **Exact-SHA GitHub CI (E2)**
   - compile/build;
   - unit/integration;
   - existing component/e2e jobs.

2. **Shared CODEX / capable local harness executor (E3)**
   - mounted Docker DB/API/Web;
   - real UI;
   - restart/persistence/lifecycle;
   - focused acceptance journey.

3. **Remote/Codespace executor (E4)**
   - only when remote-path/forwarding/timing is materially relevant.

4. **Product Owner Human Preview (E5)**
   - scheduled Preview checkpoints;
   - not used as routine developer test labor.

### CODEX queue

The existing shared CODEX remains one sequential executor.

It has two kinds of missions:
- high-risk correction ownership, such as current #354;
- exact-candidate validation missions after DEV handoff when mounted/local evidence is needed.

Main does not create a second imaginary CODEX. Candidates queue behind the active CODEX mission unless another genuinely capable executor exists.

### Candidate states

Use:

- `DEV_READY_FOR_REVIEW` — implementation returned; some evidence may still be missing;
- `WAIT_E2_CI`;
- `WAIT_E3_MOUNTED_VALIDATION`;
- `WAIT_E4_REMOTE_VALIDATION`;
- `MAIN_ACCEPTED_FOR_INTEGRATION`;
- `CHANGES_REQUIRED`.

Do not conflate `DEV_READY_FOR_REVIEW` with accepted/integrable.

### User-facing acceptance

For #303/#355/#367/#368/#364/#365/#369:
- DEV may return without E3 if environment cannot mount product;
- Main review then requires a capable E3 executor before final acceptance;
- screenshots generated from isolated component mocks do not replace normal mounted product evidence when the issue is an end-to-end authoring flow.

### Remote diagnostic special case (#359/#307)

AUD-REMOTE may not possess both a local injectable environment and a real Codespace.

Therefore:
- it runs whichever A/B/C legs are genuinely available;
- it prepares exact instrumentation/procedure for missing legs;
- returns `ENV_CAPABILITY_GAP` for unavailable legs;
- Main routes the remaining leg to CODEX/local harness or a real Codespace executor.

`C-TRANSPORT-01` cannot be frozen from source inspection alone; discriminating A/B/C evidence remains required.

### Product Owner role

The Product Owner is not used to compensate for agent environment limitations.

Human testing remains reserved for:
- planned UX checkpoints;
- independent Preview rounds;
- product-level subjective/ergonomic acceptance.

Routine compile/test/runtime validation belongs to CI/CODEX/harness executors.
