# Wave 15 — FC0-A Post-FND06 Foundation Closure Audit Control

> GitHub live is the sole authority.
> This audit is a mandatory gate between FND-06 freeze and FC0-A release.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`AUDIT_ID: FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

`AUDIT_REV: 0004`

`STATE: PREPARED / WAIT_INFRA_CI_01B_GREEN_AND_FND06_FREEZE`

`MODE: READ_ONLY_CROSS_WAVE_FOUNDATION_AUDIT`

`AUDIT_EXECUTION: MAIN_EVIDENCE_MATRIX + INDEPENDENT_AUD_REVIEW`

`IMPLEMENTING_CODEX_SELF_AUDIT: FORBIDDEN`

`PREFERRED_AUD_LANE: reuse independent FND-04 AUD chat/lane if available, re-routed by Main to this control`

`RELEASE_EFFECT: BLOCKING`

## 1. Activation rule

Do not close or release FC0-A merely because FND-06 is integrated/green.

Main activates this audit only after:
1. FND-06 PR is merged;
2. exact post-merge validation on the FND-06 integration SHA is green;
3. Main declares FND-06 `VERIFIED/FROZEN`;
4. the exact integrated product SHA/tree is recorded.

Until this audit finishes `ACCEPTABLE / FC0A_RELEASE_APPROVED`:
- DEV-EDITOR remains blocked;
- DEV-SCRIPT-ENGINEERING remains blocked;
- DEV-AUTHORITY-UX remains blocked;
- DEV-LICENSING-UX remains blocked;
- FND-05 remains PREPARED / NOT ACTIVE;
- FND-07 remains PREPARED / NOT ACTIVE.

After audit PASS, the four FC0-A DEV lanes plus FND-05 and FND-07 may advance in parallel on isolated branches/orders from the exact approved checkpoint.

## 1A. Exact FND-06 merged checkpoint pending freeze

FND-06 has been merged but is **not yet frozen**.

- PR #337: MERGED
- candidate head: `2257f8f99b5e6deac80d64ed2cc0c43aa8dab1cc`
- candidate tree: `2ebb839a788bb4fad249877689c25ac1b18f6d74`
- integration merge SHA: `624f2eca456310a2c6156538b3616a06e3be075f`
- merge tree: `fb864fb954b0123e69db379cd6b3120349b43600`
- candidate natural T1: `35939646387` — SUCCESS
- exact post-merge broad CI: `35940661531` / EliteSCADA CI #1563 — FAILURE in generic PostgreSQL initialization; Web succeeded, Backend test failed on `23505 pg_namespace_nspname_index`, Chromium skipped.

Main review has accepted the mounted A7 closeout evidence:
- Screen selection mounted across `tank | value | dynamo | status`;
- Popup selection mounted for `value | status`;
- Inspector/Dynamic/Binding remain mounted;
- safe shared property edit preserves legacy-specific authored data;
- arbitrary unknown remains fail-closed/contained;
- no lifecycle/Authority/Licensing/Driver/Historian/schema scope leakage.

This audit remains PREPARED until post-merge CI is green and Main marks FND-06 VERIFIED/FROZEN.

## 1B. Generic infrastructure blocker discovered by post-merge CI

Exact run:
- `35940661531` / EliteSCADA CI #1563
- exact head: `624f2eca456310a2c6156538b3616a06e3be075f`
- Web: SUCCESS
- Backend build/test/smoke: FAILURE in Test
- Chromium: skipped downstream.

Only identified failed test:
`Scada.Persistence.PostgreSql.Tests.PostgreSqlVisualDynamicPersistenceTests.RevisionPersistence_PreservesVisualExpressionConditionAndAnalogFill`

Error:
`23505 / pg_namespace_nspname_index`
during `PostgreSqlEngineeringProjectStore.InitializeAsync` / `CREATE SCHEMA IF NOT EXISTS elitescada`.

Causality:
- failing store source blob is identical before/after FND-06;
- failing test blob is identical before/after FND-06;
- PR #337 did not modify Persistence/PostgreSQL;
- same catalog-race signature exists in Wave 14 history;
- current source still has several shared-schema initializers where advisory-lock acquisition and DDL are issued in the same SQL batch.

Classification:
`GENERIC_INFRASTRUCTURE_BLOCKER / NOT_FND06_CAUSAL`

No blind rerun is authorized.

Blocking correction:
- control: `coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`
- order: `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`
- exact base: `624f2eca456310a2c6156538b3616a06e3be075f`
- work branch: `work/w15-infra-ci-01b-postgresql-schema-init`
- control commit: `358b067d9af6501b2945f311b5d2cd32cab64efa`.

The FC0-A audit remains PREPARED until:
1. INFRA-CI-01B is reviewed/merged;
2. exact broad integration CI is green;
3. Main records FND-06 VERIFIED/FROZEN on the resulting exact checkpoint.

## 2. Audit purpose

This is not a CI-only gate. It must robustly answer:

1. Did Wave 15 actually implement/freeze the product premises required by the end of Wave 14 diagnostics?
2. Which Wave 14 confirmed findings are closed, which intentionally move to FC0-A DEV work, which remain bounded/deferred, and which still block release?
3. Did any Wave 15 Foundation fix create a hidden product gap or contradict another frozen contract?
4. Can prepared FND-05 and FND-07 be implemented without breaking contracts already frozen and consumed by FC0-A DEVs?
5. Is there a coherent exact checkpoint from which the four DEV lanes can safely branch without foreseeable Foundation invalidation?

## 2A. Independence rule

The CODEX that implements FND-06 may supply implementation evidence but may **not** be the sole auditor of this gate.

Required roles:
1. Main Coordinator builds/revalidates the cross-wave closure matrix and exact live state.
2. An independent READ_ONLY AUD reviews the exact post-FND06 checkpoint, the matrix, frozen-contract compatibility and release disposition.
3. Main Coordinator makes the final FC0-A release decision.

The existing independent FND-04 AUD chat/lane may be reused for this audit if Main rewrites its routing/current order to this audit control. Product Owner relay is not required.

## 2B. Prepared Main evidence matrix

Preparatory matrix:

`coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-EVIDENCE-MATRIX.md`

Latest prepared matrix commit:

`bcaa29602577cd1cc1262ff943506973b29eed17`

This matrix is not PASS. It pre-maps Wave14->Wave15 items and contract-risk hypotheses so the independent AUD can review exact evidence after FND-06 freezes.

## 3. Mandatory evidence sources

Audit must read/reconcile at minimum:

### Product intent / Wave 15
- `PROJECT GOAL.md`;
- `docs/ROADMAP.md`;
- `docs/WAVE15-CORRECTION-BACKLOG-FINAL.md`;
- `LAST CHANGE.md`;
- `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`;
- `docs/CURRENT-COORDINATOR-HANDOFF.md`;
- Issues #297, #301, #302, #303, #304, #305;
- exact live PRs/branches/CI for all integrated Foundation slices.

### End-of-Wave-14 diagnostic authority
At minimum reconcile the latest chronological evidence from Issue #286, including:
- comment `5628159172` — direct-Codespace chronology / confirmed P1 and uncertain transport/runtime findings;
- comment `5628311338` — preserved audit decision and correlation requirements;
- comment `5628760255` — Wave 14 diagnosis-only -> Wave 15 correction transfer contract;
- comment `5634503355` — A7 editor selection crash and A8 Script Engineering functional findings.

Also read the canonical Wave 14 diagnostic closure/acceptance documents referenced by those comments. Newer chronological evidence supersedes earlier conflicting classification.

### Frozen Wave 15 contracts
- FND-01 lifecycle/bootstrap;
- FND-02 / AUTH-04 Authority;
- FND-03 Runtime Session Lease / machine license v2 / admission / shared quota / lifecycle;
- FND-04 Script TAG reference-resolution;
- FND-06 visual/runtime stability after its exact freeze;
- FND-08 common timing;
- INFRA-CI-01A profile-aware validation.

### Future Foundation design
- `coord/w15-fnd05-control:docs/WAVE15-FND05-CONTROL.md`;
- `coord/w15-fnd07-control:docs/WAVE15-FND07-CONTROL.md`;
- `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md`;
- C25 distributed-runtime/System Recovery architecture where relevant.

## 3A. PR #337 pre-freeze evidence incorporated into this gate

Live candidate reviewed by Main:

- PR: `#337`
- base: `6c810647c9773a19b212d9c33694780141786ac7`
- candidate: `923543705378016090e7067b35954795a9591a57`
- tree: `5657cee7169a4e77370d416add4efcf07184d7c0`
- changed files: 9, all inside the FND-06 visual/editor/runtime allowlist
- natural Wave 15 T1: `35931139983` — SUCCESS
- profile: `UI_EDITOR, RUNTIME_RENDERER`
- PR state at this audit revision: OPEN / not frozen.

Main accepts this candidate as evidence for:
- one centralized Engineering compatibility boundary for known persisted legacy `tank | value | dynamo | status`;
- no guessed canonical alias for bare `status`;
- arbitrary unknown `vendor.unknown-x` remains fail-closed/contained;
- Inspector/Binding/Dynamic model paths consume the same compatibility boundary;
- Runtime retains selected Screen/open Popup across retryable same-identity projection failure;
- genuine Active identity change deliberately resets Screen/Popup navigation;
- `CanonicalVisualRenderer` remains the visual artwork authority;
- no Security/Authority/Licensing/Driver/Historian/lifecycle/schema contract change was introduced by PR #337.

Main does **not** yet accept PR #337 as FND-06 freeze evidence because the original Wave 14 A7 defect was a mounted Screen/Popup selection crash. The active closeout order requires mounted browser proof before merge/freeze:

`FND06-CODEX-MOUNTED-LEGACY-CLOSE-V3`

Therefore this gate currently distinguishes:

- **contract status:** no breaking FND-06 contract change identified in PR #337;
- **evidence status:** incomplete until mounted A7 closeout + exact post-merge CI;
- **release status:** HOLD.

The final audit must replace this candidate snapshot with the exact merged/frozen FND-06 SHA/tree and post-merge CI evidence.

## 4. Mandatory Wave14 -> Wave15 closure matrix

For every item below record:

`SOURCE -> W15 OWNER -> EXACT EVIDENCE -> STATUS -> RESIDUAL -> RELEASE IMPACT`

Allowed status:
- `CLOSED_FOUNDATION`;
- `READY_FOR_DOWNSTREAM_DEV`;
- `DEFERRED_BOUNDED_WITH_EVIDENCE`;
- `BLOCKED_FOUNDATION`;
- `BLOCKED_PRODUCT`.

Mandatory items:

1. W15-P0-01 — Working identity/bootstrap mismatch and truthful Working/Published/Active semantics.
2. W15-P1-01 — Server Script runtime recovery/observability/throttle behavior.
3. W15-P1-02 — known legacy visual compatibility.
4. W15-P1-03 / A7 — Screen/Popup object selection must not blank/poison Engineering.
5. W15-P1-04 — Runtime projection/navigation persistence through retryable failure and genuine Active authority changes.
6. W15-P1-05 / A8-001 / A8-002 — Script Engineering functional maturity:
   - readable TAG contract Foundation portion;
   - cursor-safe composable insertion;
   - API signatures/object/property addressing/examples;
   - scope/event-specific authoring and stale-field migration.
7. W15-P1-06 — truthful Engineering/SPA error/fallback UX; no fictitious Demo/HTTP state.
8. W15-P2-01 — Trends bounded follow-up.
9. W15-P2-02 — Popup live value/freshness bounded follow-up.
10. W15-P2-03..07 — shared UI/accessibility/navigation/templates residuals.
11. W15-U-01 — transport/forwarding latency remains evidence-bounded; no speculative product fix.
12. W15-U-02 — Alarm timestamp semantics remains evidence-bounded.
13. `RECHECK-SIM-PUMP-LEVEL` — must not be resurrected as confirmed Server Script defect without correlated reproduction.
14. preserved future requirements that were explicitly **not** Wave 14 defect conclusions:
    - protected whole-system backup/restore;
    - Historian Administration;
    - raw->engineering scaling;
    - decimal-place authoring;
    - real EEE Modbus/PLC variant;
    - Wave 13 signed-release work.

Audit must determine whether any confirmed P0/P1 item is neither closed nor safely owned by one of the released DEV lanes. Such an item blocks FC0-A.

## 5. Cross-Foundation invariant audit

At the exact post-FND06 integration checkpoint independently verify:

### Lifecycle
- Working != Published != Active;
- Working/editor rendering cannot silently become Runtime authority;
- cross-project activation remains fail closed;
- FND-06 renderer compatibility did not weaken lifecycle identity.

### Authority
- backend capability/scope semantics remain authoritative;
- role names do not grant privilege;
- Runtime Session Class remains a restrictive ceiling only;
- `CommandExecute` remains distinct from `ProcessValueWrite`.

### Licensing / Runtime Session Lease
- one logical session remains lease-based, not socket-count-based;
- requested vs granted class semantics remain;
- Web + future EliteGO use one shared quota authority;
- install/replace/remove remains transactional and fences obsolete lease authority;
- machine license remains outside `.escadapkg`.

### Script
- normal readable source remains bound to stable TagId;
- source-token membership vs canonical registry proof remains separated;
- read/write fail closed on stale/identity drift;
- Server Script sandbox has no second TAG/Authority authority.

### Visual/runtime
- one canonical renderer/public model authority;
- known legacy compatibility is bounded and truly unknown types remain contained;
- Screen/Popup selection stays mounted;
- retryable projection failure preserves valid navigation under unchanged Active identity;
- genuine Active identity change reinitializes navigation.

### Timing/CI
- no global timeout inflation or blind write retry;
- T1 profile declarations used by released lanes exist in the real router;
- exact SHA evidence is not borrowed from another candidate.

Any contradiction between two frozen contracts is `BLOCKED-CONTRACT`.

## 6. FND-05 compatibility audit — required before FC0-A release

Question: can FND-05 HA identity/topology/fencing be added without redefining contracts consumed by FC0-A DEV lanes?

### Frozen contracts FND-05 must preserve

#### FND-03 lease/licensing
Existing frozen lease identity already includes topology hooks such as `ServerNode` and `ClusterId`.

FND-05 may add replication/continuity/topology/fencing authority, but must not redefine:
- logical `SessionId` / `ClientInstanceId` ownership;
- requested vs granted Runtime Session Class;
- Authority ceiling semantics;
- shared Interactive/ViewOnly quota meaning;
- lease generation/authority-revision invalidation semantics;
- machine-license trust/entitlement semantics;
- transactional license install/replace/remove behavior.

Additive topology/fencing fields/endpoints are permitted if backward-compatible.

#### FND-04 Script
FND-05 may decide **whether this node is allowed to execute industrial side effects** through effective-Active/fencing authority.

It must not redefine:
- readable Script TAG binding persistence;
- source-token declaration membership;
- stable TagId identity;
- stale/identityDrift semantics;
- Script Engineering authoring contract.

Any HA execution fence must wrap/guard the existing Server Script runtime authority, not replace its TAG resolver.

#### FND-06 visual/runtime
FND-05 may expose topology/effective-Active state for later EliteGO/runtime status use, but must not create another renderer/public visual model or redefine Working-vs-Active visual authority.

### FND-05 audit disposition

Current prepared design is expected to be **ADDITIVE / COMPATIBLE**, because:
- the frozen RuntimeSessionLease already models `ServerNode` and `ClusterId`;
- C25 explicitly reserved ReadyStandby/epoch/fencing as later work;
- `.escadapkg` is already topology-neutral;
- HA policy is intended above Drivers and clients.

But this is only a release assumption until the post-FND06 audit revalidates exact code/contracts.

If FND-05 requires a breaking lease/license/Script/renderer semantic change:
`BLOCKED-CONTRACT -> FOUNDATION DELTA BEFORE FC0-A DEV RELEASE`.

## 7. FND-07 compatibility audit — required before FC0-A release

Question: can secure detach/neutral-bootstrap be implemented by composing frozen lifecycle/Authority/licensing primitives rather than redefining them?

FND-07 must preserve:

### FND-01 lifecycle
- explicit Working/Revisions/Published/Active authority;
- no silent Working->Active;
- package import/open remains Preview/Apply/lifecycle-aware;
- detach orchestrates existing authorities rather than inventing a hidden second project authority.

### FND-02 Authority
- backend capability/scope evaluator remains authoritative;
- populated installation never reopens anonymous takeover;
- old Authority sessions are invalidated through existing protected authority/session mechanisms.

### FND-03 licensing
- license remains machine/install authority separate from Application/Authority artifacts;
- keep/remove/replace uses existing transactional licensing operations;
- invalid replacement preserves prior valid license;
- Runtime lease/authority fencing reuses frozen lifecycle semantics.

### FC0-A DEV contracts
FND-07 must not require DEV-AUTHORITY-UX or DEV-LICENSING-UX to redesign their frozen backend semantics.
A later Installation UX may orchestrate the new detach transaction, but the first four FC0-A lanes must remain valid.

Shared shell/router changes needed for neutral bootstrap are integration hotspots, not permission to redefine lifecycle/Authority/licensing contracts.

### FND-07 audit disposition

Current prepared design is expected to be **COMPOSITIONAL / COMPATIBLE**, because existing System Recovery, prospective Authority admission, atomic Authority replacement and licensing lifecycle primitives already exist.

If exact source audit shows neutral bootstrap requires changing the meaning of a frozen lifecycle, Authority or licensing contract:
`BLOCKED-CONTRACT -> FOUNDATION DELTA BEFORE FC0-A DEV RELEASE`.

## 7A. FC0-A DEV contract-risk matrix after PR #337

This is a **pre-freeze risk assessment**, not release approval.

| Lane | Frozen contracts consumed | PR #337 impact | FND-05/FND-07 future impact | Current contract risk | Release condition |
| --- | --- | --- | --- | --- | --- |
| DEV-EDITOR | FND-06 renderer/public model, known-legacy compatibility, Working-vs-Active separation | Directly affected in its future ownership surface, but PR #337 is Foundation-compatible and intentionally precedes DEV-EDITOR | FND-05 may add topology/status surfaces only; FND-07 may touch shared shell later, neither may redefine renderer/lifecycle | **LOW / GUARDED** — remaining issue is mounted A7 evidence, not a known contract break | FND-06 mounted closeout + merge + post-merge green + audit confirms DEV-EDITOR consumes, does not bypass, `getVisualSchemaForEngineering`/frozen compatibility boundary |
| DEV-SCRIPT-ENGINEERING | FND-04 readable source-binding/stable TagId contract | No product overlap in PR #337 | FND-05 may fence whether a node may execute side effects, but may not alter Script TAG resolution/source membership; FND-07 detach may fence Runtime but not authoring contract | **NONE IDENTIFIED / GUARDED** | Audit verifies FND-05 execution fencing wraps existing Server Script authority and FND-07 does not alter FND-04 semantics |
| DEV-AUTHORITY-UX | FND-02/AUTH-04 capability/scope/effective-permission contract | No overlap in PR #337 | FND-07 must orchestrate existing Authority replace/session invalidation without redefining evaluator semantics | **NONE IDENTIFIED / GUARDED** | FND-07 stays compositional; any change to capability/scope meaning or populated-install security = BLOCKED-CONTRACT |
| DEV-LICENSING-UX | FND-03 license v2, transactional install/replace/remove, Runtime Session Lease/shared quota semantics | No overlap in PR #337 | FND-07 reuses existing licensing lifecycle. FND-05 may need redundancy readiness/entitlement data | **LOW BUT MATERIAL RESIDUAL** — no current breaking change, but FND-05 redundancy entitlement is not yet a consolidated product contract in current code | Any HA redundancy entitlement must be **additive/backward-compatible** to frozen FND-03 schema/semantics. If FND-05 requires reinterpreting signed license fields, seat classes, quota meaning, machine binding or transaction semantics, block FC0-A and perform Foundation delta first |

### Risk interpretation

- `NONE IDENTIFIED / GUARDED` means no live design/code evidence currently requires a contract change; the audit still verifies the guard.
- `LOW / GUARDED` means integration/evidence/shared-surface risk exists, but no semantic contract break is currently identified.
- `LOW BUT MATERIAL RESIDUAL` means a future Foundation detail could become a contract issue unless constrained before activation.

### Explicit mitigation for DEV-EDITOR

Once FND-06 freezes, DEV-EDITOR must:
- consume the centralized known-legacy compatibility boundary introduced by FND-06;
- not revert selection-dependent consumers to direct strict built-in lookup for known persisted legacy types;
- not create another visual schema registry;
- preserve arbitrary-unknown fail-closed behavior;
- treat full single-primary-canvas UX as downstream composition over the frozen renderer/compatibility contract.

### Explicit mitigation for DEV-LICENSING-UX / FND-05

FND-05 may add:
- optional/additive topology/fencing/readiness data;
- an additive redundancy-entitlement/readiness surface **only if** old FND-03 license v2 consumers remain semantically valid.

FND-05 may not:
- change the meaning of existing signed license fields;
- reinterpret Interactive/ViewOnly/session-class quota semantics;
- move licensing authority into HA clients/nodes;
- make a formerly valid FND-03 license invalid solely because a new non-required field is absent;
- require DEV-LICENSING-UX to rewrite its existing frozen license lifecycle contract.

If those constraints cannot be met:
`FND-05 -> BLOCKED-CONTRACT -> FOUNDATION DELTA BEFORE FC0-A RELEASE`.

### Preliminary compatibility hypothesis — must be revalidated on exact post-FND06 SHA

| Future Foundation | Preliminary classification | Reason | Breaking-change trigger |
| --- | --- | --- | --- |
| FND-05 | ADDITIVE / EXPECTED COMPATIBLE | Frozen FND-03 lease already contains ServerNode/ClusterId hooks; HA is intended above Drivers/clients; topology stays out of package; FND-04 source/binding semantics need not change | Any required redefinition of lease identity/class/quota/license semantics, Script TAG resolver, or canonical visual authority |
| FND-07 | COMPOSITIONAL / EXPECTED COMPATIBLE | Existing System Recovery, prospective Authority admission, atomic Authority replacement and FND-03 licensing lifecycle can be orchestrated | Any required redefinition of Working/Published/Active lifecycle, Authority capability/scope meaning, or transactional licensing semantics |

These are not PASS results. The independent audit must prove them on the exact post-FND06 checkpoint before release.

## 8. FC0-A release matrix

Audit must produce a table for:

- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05;
- FND-07.

For each:
- exact approved base SHA/tree;
- frozen contracts consumed;
- known residuals;
- shared-file hotspots;
- contract-break risk;
- status `RELEASE | HOLD | BLOCKED-CONTRACT`.

No row may be `RELEASE` if it is based on an assumed future contract rather than the exact audited checkpoint.

## 9. Audit outcome

### PASS

Required final prefix:

`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — ACCEPTABLE / FC0A_RELEASE_APPROVED`

PASS requires:
- no unresolved P0/P1 Foundation blocker;
- all Wave 14 confirmed findings mapped;
- all uncertain findings bounded with explicit future owner/gate;
- no contradiction among frozen Wave 15 contracts;
- FND-05 compatibility = additive/non-breaking, including additive/backward-compatible redundancy entitlement/readiness semantics;
- FND-07 compatibility = compositional/non-breaking;
- exact checkpoint SHA/tree + CI recorded;
- residual ledger explicit;
- six release rows classified.

Main may then:
1. record the exact FC0-A checkpoint;
2. activate the four DEV lanes;
3. activate FND-05 and FND-07 in parallel on isolated branches;
4. keep EliteGO/Installation UX/HA downstream blocked until their specific Foundations freeze.

### FAIL

Use one of:

`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — CHANGES_REQUIRED`

`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — BLOCKED-CONTRACT`

On failure:
- no FC0-A DEV release;
- no FND-05/FND-07 product activation if their design is the source of the contract blocker;
- Main creates the required Foundation delta first and repeats the audit on the new exact checkpoint.

## 10. Audit governance

- READ_ONLY unless Main explicitly creates a bounded audit-test order.
- No audit agent merges product.
- No audit agent weakens tests/contracts to obtain PASS.
- Evidence is exact-SHA and append-only.
- Product Owner is not a required messenger.
- GitHub live controls all release decisions.
