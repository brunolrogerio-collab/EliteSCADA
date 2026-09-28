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


---

## 14. Live candidate queue after first parallel DEV handoffs

### #370 / DEV-DATA
Candidate:
`6083c2a1c91aed682405367f06c0eb10223ddf51`

State:
`MAIN_CODE_REVIEW_PASS / E2_ACCEPTED / WAIT_E3_MOUNTED_VALIDATION`

No further DEV mutation currently ordered.

### #371 / DEV-EDITOR-CORE
Candidate:
`2e288bbf8b9b0540aa19d1ca40278c16a0e11a05`

State:
`MAIN_CODE_REVIEW_PASS / E2_ACCEPTED / WAIT_E3_MOUNTED_VALIDATION / C-VISUAL-IDENTITY_NOT_YET_FROZEN`

Main review accepted the bounded Editor direction but added the objectKey compatibility-alias rule to C-VISUAL-IDENTITY.

### #359/#307 / AUD-REMOTE
State:
`E0_DIAGNOSTIC_ACCEPTED / WAIT_E3_A_B / WAIT_E4_C / NO_PRODUCT_MUTATION`

### Shared CODEX sequential validation queue

Current active mission remains #354.

After #354 handoff/review, preferred queued use of the same existing CODEX is:

1. exact #371 E3 mounted Editor validation;
2. exact #370 E3 mounted Data Source -> TAG -> Runtime validation;
3. #359/#307 E3-A local-normal + E3-B deterministic latency/jitter diagnostic.

E4-C must run on a real supported Codespace/forwarded path and may use a separate capable remote executor.

This queue is planning only until Main activates each exact mission after revalidating live GitHub.


### #372 / #354 CODEX-AUTHORITY candidate

Candidate:
`c84dee88268ecf786737cf3ec9364d0113d5f028`

State:
`MAIN_CODE_REVIEW_PASS / E2_ACCEPTED / WAIT_E3_MOUNTED_AUTHORITY_VALIDATION / NO_MERGE`.

This P0 candidate remains ahead of the queued #371/#370 E3 missions.

Required sequence now:
1. #354 exact candidate E3 fresh/first-project/Active/restart authority validation;
2. if PASS, Main integration of #354 and C-AUTHORITY freeze;
3. revalidate/rebase downstream candidates as necessary against the new integration HEAD;
4. then #371 E2/E3 and #370 E2/E3 according to conflict/dependency review.

Do not validate #371 as final E3 and then merge #354 underneath it if overlapping integration changes would invalidate the evidence.


---

## 15. #354 integrated — downstream recomposition gate

#354 was accepted at E2+E3 and merged as:
`wave15/corrections-integration@33e514eb3f5cf8f984779c0091069387741e7296`

Tree:
`d7219cbbc27d39bdebb54193d6a71b9d3f65db2f`

`C-AUTHORITY-01 = FROZEN_FOR_CONSUMERS`.

The one-off Playwright APIRequestContext export body-read timeout is recorded under #307 as a non-blocking observation; no product cause was proven.

### Downstream consequence

Old #371/#370 candidate branches were created from `00d17e...` and are now diverged from live integration.

They must not receive final E3 against stale composition.

Required next parallel actions:
- DEV-EDITOR-CORE: recompose #371 onto exact integration `33e514...`, preserve scope, rerun E2, return exact candidate.
- DEV-DATA: recompose #370 onto exact integration `33e514...`, preserve scope, rerun E2, return exact candidate.
- shared CODEX: WAIT until the recomposed #371 exact candidate is posted; then Main will issue its E3 order.
- DEV-REUSE: contract audit accepted as MAIN_REVIEWED_DRAFT; no implementation until C-VISUAL-IDENTITY freeze.
- AUD-REMOTE: remains waiting for E3-A/B + E4-C.
- HMI Dynamics/Script Object remain WAIT_C-VISUAL-IDENTITY.

Preferred sequence:
`#371 recomposed E2 -> #371 E3 -> integrate #371 -> freeze C-VISUAL-IDENTITY -> release #367/#368/#369 -> #370 E3/integration -> remaining correction lanes`.

#370 may recompose/E2 in parallel while #371 is being prepared.


---

## 16. Combined E3 for recomposed Editor + Data Source candidates

Both downstream candidates have now returned on the same integrated authority baseline:

- #371 / DEV-EDITOR-CORE:
  - HEAD `38506f1ea8d281287949b3fb23f26dbd319ffa43`
  - tree `f4843811a53b4dc91ca301d3594f0a85b574bb35`
  - exact E2 T1 `36374037238` SUCCESS
  - PR mergeable=true
- #370 / DEV-DATA:
  - HEAD `bc39f5fe64f59e73b1f0e0e413196cb917af50b4`
  - tree `eae78480d565aa5020df58ae79fa9b53910a5168`
  - exact E2 T1 `36374028034` SUCCESS
  - PR mergeable=true
- shared base:
  `wave15/corrections-integration@33e514eb3f5cf8f984779c0091069387741e7296`

The two PR diffs have **zero changed-file overlap**.

### Validation strategy

Main accepts a single **combined E3 composition** instead of two separate mounted sessions.

Shared CODEX must locally compose the exact #371 + #370 heads without pushing product changes, compute and report the resulting exact combined tree, and validate that tree in one real mounted PostgreSQL/API/Web/browser session.

This combined E3 is valid for both candidates only if:
1. local composition is clean and contains exactly the union of #371 + #370 over base `33e514...`;
2. no extra product/test/workflow changes are introduced by CODEX;
3. the combined tree is recorded before testing;
4. both full journeys pass in the same mounted composition;
5. after E3 PASS, Main merges #371 then #370 and verifies the final integration tree is **byte-identical** to the E3-validated combined tree.

If the final tree differs, the combined E3 evidence does not authorize acceptance and the changed final composition must be revalidated.

### Combined mounted journey

The E3 session must cover:
- #354 authority regression sanity: fresh/no-Active remains neutral; no hidden Demo application;
- #371 Editor:
  - Screen + Popup;
  - rectangle fill/stroke/stroke width;
  - Text literal edit;
  - Key rename with stable Id;
  - Canvas/Outliner/Properties synchronization;
  - local disclosure/layout behavior;
  - Undo/Redo;
  - Save/Reopen;
- #370 Data Source:
  - normal Project -> Data Source -> Type selection -> configure/apply;
  - TAG creation and binding/use;
  - Save/Reopen;
  - service restart/persistence;
  - Publish/Activate;
  - Runtime reads the intended Active project/revision/TAG path;
- no DataSource-specific timeout/retry/Codespace workaround;
- no Design-mode process write.

The catalog error/reload injected failure already has exact E2 mounted regression coverage; combined E3 primarily proves the real normal lifecycle and cross-surface composition.

### Integration order after PASS

Preferred:
1. merge #371;
2. merge #370;
3. verify final integration tree == E3 combined tree;
4. freeze C-VISUAL-IDENTITY-01;
5. release #367/#368/#369;
6. keep #355 remote-specific closure pending C-TRANSPORT E4 where applicable.


---

## 17. Combined E3 accepted and #371 + #370 integrated

CODEX handoff:
#305 comment `5863212189`.

Disposition:
`COMBINED_E3_PASS`.

Validated combined tree:
`bf0b43ff9ea1211443d614487f4a05bab2ee2c97`.

Main integration:
1. #371 merged as `c027b990cc1f1d6b994456e0554680fd14fcf938`;
2. #370 merged as `50b2750c73623b7ffef77f0ca93755c3e8278676`;
3. final integration tree = `bf0b43ff9ea1211443d614487f4a05bab2ee2c97`.

Therefore:
- #303/#371 first-user Editor correction = INTEGRATED;
- #355/#370 generic local Data Source correction = INTEGRATED;
- C-VISUAL-IDENTITY-01 = FROZEN_FOR_CONSUMERS;
- #355 remains open only for remote/Codespace root closure under C-TRANSPORT where applicable.

### Parallel releases from integration 50b275...

Lane E — DEV-HMI-DYNAMICS-IO:
`ACTIVE / EXACT_BASE=50b2750c73623b7ffef77f0ca93755c3e8278676`.
Consume frozen C-VISUAL-IDENTITY and C-TAG-WRITE. Implement bounded #367/#368 first slice; no identity or Runtime write-service redesign.

Lane F — DEV-SCRIPT-OBJECT:
`ACTIVE / EXACT_BASE=50b2750c73623b7ffef77f0ca93755c3e8278676`.
Consume frozen C-VISUAL-IDENTITY. Existing visual Python capabilities only; new authoring emits Id-based references and includes bounded legacy Key-reference compatibility handling.

Lane G — DEV-REUSE:
`ACTIVE_R1 / EXACT_BASE=50b2750c73623b7ffef77f0ca93755c3e8278676`.
First implementation slice only: stable-reference compatibility seam + validation/import/package tests from the Main-reviewed C-REUSE draft. No Editor authoring/Preview UI in R1.

Lane H — DEV-GATEWAY:
remains `READY / WAIT_MAIN_RELEASE` for one more integration interval to avoid simultaneous Engineering navigation conflict with newly released E/F lanes.

### Shared CODEX next mission

After accepting combined E3, shared CODEX moves to #359/#307 local A/B diagnostic on the exact integrated tree:
- E3-A local normal;
- E3-B deterministic latency/jitter;
- no product mutation;
- E4-C real Codespace remains separate and required before remote-root closure.


---

## 18. Live queue after local transport A/B and first E/F/G candidates

Shared CODEX transport discriminator:
`A_PASS_B_PASS_REMOTE_E4_REQUIRED`.

Authoritative integration remains:
`wave15/corrections-integration@50b2750c73623b7ffef77f0ca93755c3e8278676`
tree `bf0b43ff9ea1211443d614487f4a05bab2ee2c97`.

### E / #367+#368
PR #373:
- HEAD `c10f4e5115333b6de4bd43a1f001dcf2bcb271da`;
- mergeable=true;
- T1 `36378897614` = FAILURE;
- Common sanity, .NET and Web build passed;
- Focused Chromium profile failed.

State:
`CHANGES_REQUIRED / E2_CHROMIUM_FAIL / NO_E3 / NO_MERGE`.

DEV-HMI-DYNAMICS-IO must diagnose/fix the exact Chromium failure without widening scope, rerun E2 and return a new exact candidate.

### F / #369
PR #374:
- HEAD `09a24f6e16ff3c152e0155d83b386342685a12b1`;
- mergeable=true;
- T1 `36379229072` SUCCESS.

State:
`E2_GREEN / WAIT_DEV_HANDOFF / E3_REQUIRED_BEFORE_INTEGRATION`.

### G / #365 R1
PR #375:
- HEAD `8c47e7944f1e025992b203a45a5caaf146819e9c`;
- mergeable=true;
- T1 `36379540510` SUCCESS.

State:
`E2_GREEN / WAIT_DEV_HANDOFF_MAIN_REVIEW`.

R1 is backend/contracts/persistence-first; Main may accept it after code/contract review at E2 if the handoff confirms no user-facing UI semantics were added.

Important overlap:
#373 and #375 both touch `src/Scada.Engineering/Contracts/EngineeringContracts.cs`.
Whichever is integrated first requires the other candidate to be recomposed/revalidated before integration.

### H / #364
Released now:
`ACTIVE_PRODUCT_CORRECTION / EXACT_BASE=50b2750c73623b7ffef77f0ca93755c3e8278676`.

Current #373/#374/#375 changed-file sets do not occupy the Gateway-specific navigation/panel files identified by #364 as its primary UX surface. H may proceed in parallel, but must not redesign C-GATEWAY-01 runtime semantics.

### Shared CODEX next route
rev0104:
real Codespace/forwarded E4-C only. If real Codespace capability is unavailable, return `ENV_CAPABILITY_GAP / E4`; do not substitute another local proxy.


---

## 19. #369 Main review complete

DEV-SCRIPT-OBJECT handoff:
#369 comment `5863713919`.

Exact candidate:
- PR #374;
- HEAD `09a24f6e16ff3c152e0155d83b386342685a12b1`;
- tree `2c123369c1554fad67e63ad4ecc06f769cb67841`;
- exact T1 `36379229072` SUCCESS.

Main independently reviewed the key authoring/runtime seams.

Disposition:
`MAIN_CODE_REVIEW_PASS / E2_ACCEPTED / WAIT_E3_MOUNTED_SCRIPT_OBJECT_VALIDATION / NO_MERGE`.

Accepted compatibility boundary:
- new authoring emits stable `visualDefinitionId/visualObjectId` + canonical property key;
- mutable Key is not emitted as new identity;
- existing objectKey/objectId runtime aliases remain compatibility-only;
- literal legacy Key references are detected/warned, never silently retargeted;
- ambiguous/reused Key produces no migration target;
- dynamic Python expressions are not guessed.

E3 must also explicitly execute the newly added focused model/reference/provider specs that were committed but not individually selected by the routed E2 Playwright profile.

Shared CODEX remains on rev0104 E4-C real Codespace first.

After rev0104 returns, Main may combine #374 E3 with a corrected E/#373 candidate if:
1. #373 is E2-green;
2. both exact inputs are composed over the same then-current integration;
3. Main records an exact combined tree before mounted execution;
4. final integration tree is byte-identical to the validated composition.

Do not merge #374 before E3.


---

## 20. E4 environment gap repaired with dedicated harness; E/F/G review queue

rev0104 returned:
`ENV_CAPABILITY_GAP / E4`
before remote product testing because the no-delta Codespace lacked the exact .NET SDK and durable DB/container capability.

This is an environment/harness gap, not a product finding.

Main prepared:
`preview/w15-e4-codespace-harness-50b275@604806d012a3caaa804c7ce505a12b38cde3a939`
tree `e7c40ad428c6db693cb57be5d897d9ce1c702c2c`.

Canonical product remains:
`50b2750c73623b7ffef77f0ca93755c3e8278676`
tree `bf0b43ff9ea1211443d614487f4a05bab2ee2c97`.

Base -> harness delta is exactly four homologation paths:
- .devcontainer/devcontainer.json
- .devcontainer/docker-compose.yml
- .devcontainer/initialize-preview-machine-id.sh
- scripts/preview/launch-w15-e4-transport.sh

No product source/test/workflow file differs.

Shared CODEX rev0105 retries E4-C on a fresh Codespace from that exact harness.

### E / #367+#368
PR #373 exact candidate:
`fd25b9f01a3fa55800dbcab7147aab96b083548e`
tree `6da8b4e20f09631b1ef774a545b74434783c88de`.
T1 `36381809937` SUCCESS.

Main disposition:
`MAIN_CODE_REVIEW_PASS / E2_ACCEPTED / WAIT_COMBINED_E3_WITH_#374 / NO_MERGE`.

### F / #369
PR #374:
`09a24f6e16ff3c152e0155d83b386342685a12b1`
tree `2c123369c1554fad67e63ad4ecc06f769cb67841`.

Main disposition remains:
`MAIN_CODE_REVIEW_PASS / E2_ACCEPTED / WAIT_COMBINED_E3_WITH_#373 / NO_MERGE`.

#373 and #374 currently have zero changed-file overlap. After E4 returns, preferred shared CODEX mission is one exact combined E3 for E+F.

### G / #365 R1
PR #375:
`8c47e7944f1e025992b203a45a5caaf146819e9c`
tree `5008fa501758552c7648607e7123633252df760c`.
T1 `36379540510` SUCCESS.

Main disposition:
`MAIN_CODE_REVIEW_PASS / R1_E2_ACCEPTED / INTEGRATION_DEFERRED_BEHIND_#373_#374 / NO_MERGE`.

R1 stable-reference semantics are accepted pending integration. Because #375 and #373 both edit EngineeringContracts.cs, #375 will recompose after E+F integration rather than invalidate #373 evidence now.
