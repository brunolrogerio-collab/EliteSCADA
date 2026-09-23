# Wave 15 — FC0-A Post-FND06 Foundation Closure Audit Control

> GitHub live is the sole authority.
> This audit is a mandatory gate between FND-06 freeze and FC0-A release.

`CONTROL_BRANCH: coord/w15-fnd06-control`

`AUDIT_ID: FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

`STATE: PREPARED / WAIT_FND06_VERIFIED_FROZEN`

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
- FND-05 compatibility = additive/non-breaking;
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
