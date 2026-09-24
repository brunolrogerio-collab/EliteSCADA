# Wave 15 — FC0-A Post-FND06 Foundation Closure Audit Result

`AUDIT_ID: FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

`AUDIT_OWNER: MAIN_COORDINATOR`

`AUDIT_RESULT: CHANGES_REQUIRED`

`EXACT_PRODUCT_SHA: 560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

`EXACT_PRODUCT_TREE: 674019fbbc21001a2d68deb853c2c0b293e0a5cb`

`FINAL_BROAD: 35953557122 / EliteSCADA CI #1565 / SUCCESS`

## 1. Executive conclusion

FND-06 is VERIFIED/FROZEN and its visual/runtime Foundation contract is accepted.

FC0-A is **not releasable yet**.

The post-FND06 Wave14->Wave15 audit confirms two unresolved material Wave 15 blockers:

1. **W15-P1-01 — Server Script recovery / throttle**
   - current runtime can enter a permanent throttled latch after repeated timeout/failure;
   - no bounded automatic cooldown/half-open/probe recovery path exists;
   - no production caller automatically clears the latch;
   - this is a Foundation/runtime blocker, not a DEV-SCRIPT-ENGINEERING authoring task.

2. **W15-P1-06 — truthful Engineering fallback**
   - the Engineering shell may render synthetic `Demo Project` while no public model snapshot is loaded;
   - no-model workspace fallbacks may present `unsaved` / `clean` as if authoritative;
   - this is a shared Engineering shell product blocker, not a DEV-EDITOR feature task.

No current FND-05 or FND-07 design decision requires breaking a frozen contract consumed by FC0-A DEVs.

Therefore:

`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — CHANGES_REQUIRED`

## 2. Exact checkpoint and CI

Final frozen FND-06 product checkpoint:

- SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`
- Web: SUCCESS
- Backend build/test/smoke: SUCCESS
- Chromium end-to-end: SUCCESS

Before FND-06 freeze Main compared the checkpoint to the integration coordination head and found only coordination-document divergence, no product/infra delta.

## 3. Wave14 -> Wave15 closure matrix

| Item | Audit status | Evidence / owner | Release impact |
| --- | --- | --- | --- |
| W15-P0-01 Working identity/bootstrap | CLOSED_FOUNDATION | FND-01 frozen lifecycle/bootstrap contract | none |
| W15-P1-01 Server Script recovery | **BLOCKED_FOUNDATION** | `ScriptFailureThrottle` latches; `ProcessNextAsync` returns Throttled while latched; only explicit `ResetThrottle()`; no automatic recovery caller found | **blocks FC0-A** |
| W15-P1-02 legacy visual compatibility | CLOSED_FOUNDATION | FND-06 known legacy `tank/value/dynamo/status` compatibility + unknown fail-closed | none |
| W15-P1-03 / A7 selection stability | CLOSED_FOUNDATION | mounted Screen/Popup legacy selection regressions accepted; final broad green | none |
| W15-P1-04 projection/navigation persistence | CLOSED_FOUNDATION | same-Active retry preserves navigation/Popup; real Active identity change resets deliberately | none |
| W15-P1-05 Script Engineering maturity | READY_FOR_DOWNSTREAM_DEV_AFTER_BLOCKERS | FND-04 readable TAG/stable identity contract frozen; authoring UX remains DEV-SCRIPT-ENGINEERING | downstream only after blockers close |
| W15-P1-06 truthful error/fallback UX | **BLOCKED_PRODUCT / SHARED_ENGINEERING_SHELL** | `EngineeringApp.tsx` may synthesize `Demo Project` when snapshot is null and no-model bar state can look authoritative | **blocks FC0-A** |
| W15-P2-01 Trends | DEFERRED_BOUNDED_WITH_EVIDENCE | retest after P1-01 recovery and stable runtime/forwarding | not a standalone FC0-A blocker |
| W15-P2-02 Popup live values | DEFERRED_BOUNDED_WITH_EVIDENCE | bounded freshness/live-value follow-up after upstream runtime corrections | not a standalone FC0-A blocker |
| W15-P2-03 shared header responsiveness | READY_FOR_DOWNSTREAM_SHARED_UX | #306 Productization/shared UI gate | downstream |
| W15-P2-04 account accessibility | READY_FOR_DOWNSTREAM_SHARED_UX | #306 accessibility/productization gate | downstream |
| W15-P2-05 Engineering navigation/scroll | READY_FOR_DEV_EDITOR | #303 / DEV-EDITOR | downstream |
| W15-P2-06 Engineering Lock footprint | READY_FOR_DOWNSTREAM_SHARED_UX | shared Engineering UX/productization; lock authority must not weaken | downstream |
| W15-P2-07 Templates/Equipment/Dynamos/Libraries | READY_FOR_VISUAL_QUALITY | #308 visual-quality/Library preview gate | downstream |
| W15-U-01 Codespaces/forwarding latency | DEFERRED_BOUNDED_WITH_EVIDENCE | no product transport patch without correlated browser/forwarding evidence | non-blocking |
| W15-U-02 Alarm timestamp semantics | DEFERRED_BOUNDED_WITH_EVIDENCE | reopen only with same-occurrence authority/timestamp evidence | non-blocking |
| RECHECK-SIM-PUMP-LEVEL | NOT_CONFIRMED_DEFECT | must not be reopened absent correlated reproduction | non-blocking |
| Preserved future requirements | DEFERRED_BY_EXPLICIT_PRODUCT_SCOPE | backup/restore, Historian admin, scaling, decimals, real PLC variant, signing | non-blocking for FC0-A |

## 4. Confirmed blocker A — W15-P1-01

Exact source at the audited checkpoint:

- `src/Scada.Engineering/VisualScripting/ScriptEventRuntimeFoundation.cs`
- `src/Scada.Engineering/VisualScripting/ScriptRuntimeExecutionCoordinator.cs`
- `src/Scada.Api/Runtime/ServerScriptRuntimeManager.cs`

Observed semantics:

- `ScriptFailureThrottle.Record` sets `_isThrottled=true` at threshold;
- a later successful status resets only consecutive-failure count if an execution could occur;
- once throttled, `ProcessNextAsync` returns `Throttled` before dequeue/execution;
- only explicit `ResetThrottle()` clears the latch;
- repository review found no production automatic reset/probe/cooldown path.

This exactly matches the Wave 14 confirmed failure mechanism and fails the Wave 15 recovery contract.

Required correction remains bounded:
- cooldown / half-open / single-probe style recovery or equivalent;
- no busy loop;
- bounded queue/coalescing;
- persistent failure visibly degraded;
- no stale writes after revision/authority changes;
- health/last-useful-execution/failure/probe truth exposed;
- preserve FND-04 TAG binding, sandbox, Authority and timeout/cancellation safety.

## 5. Confirmed blocker B — W15-P1-06

Exact source at the audited checkpoint:

- `web/scada-web/src/engineering/EngineeringApp.tsx`
- `web/scada-web/src/engineering/api.ts`

Observed semantics:

- `snapshot` is null during initial load and after failed load;
- sidebar project identity falls back to literal `Demo Project`;
- no-model workspace status can present `unsaved` / `clean`;
- HTTP response errors and transport rejection are not represented by a dedicated truthful Engineering bootstrap error contract.

This violates the Wave 15 rule that absent/unloaded public model must not masquerade as an authoritative project and that transport rejection must not become fictitious HTTP state.

Correction belongs to the shared Engineering shell and must be Main-coordinated, not absorbed opportunistically by DEV-EDITOR.

## 6. FND-05 compatibility decision

Audit result:

`FND-05 = ADDITIVE / COMPATIBLE WITH FROZEN FC0-A CONTRACTS`

The prepared FND-05 design may add:
- Cluster/Node/topology;
- effective-Active/fencing/epoch;
- lease replication/continuity;
- authenticated topology/readiness;
- additive redundancy entitlement/readiness only.

It may not redefine:
- FND-03 logical lease identity;
- requested/granted session class;
- Interactive/ViewOnly shared quota meaning;
- machine license trust/binding;
- transactional install/replace/remove;
- FND-04 TAG/source-binding semantics;
- FND-06 renderer/Working-vs-Active authority.

Existing FND-03 `RuntimeSessionLease` already contains topology hooks such as ServerNode/ClusterId, so HA continuity can be layered without invalidating the FC0-A consumer contract.

If implementation later requires semantic reinterpretation, FND-05 must stop with `BLOCKED-CONTRACT`.

## 7. FND-07 compatibility decision

Audit result:

`FND-07 = COMPOSITIONAL / COMPATIBLE WITH FROZEN FC0-A CONTRACTS`

FND-07 may compose:
- System Recovery;
- protected lifecycle detach;
- existing Authority replacement/admission;
- session invalidation;
- frozen FND-03 license keep/remove/replace;
- neutral bootstrap.

It may not redefine:
- FND-01 Working/Revisions/Published/Active;
- FND-02 capability/scope semantics;
- FND-03 license/session semantics;
- FND-04 Script identity;
- FND-06 visual authority.

If implementation later requires such semantic change, FND-07 must stop with `BLOCKED-CONTRACT`.

## 8. FC0-A release matrix

| Lane | Audit status | Reason |
| --- | --- | --- |
| DEV-EDITOR | HOLD | FND-06 contract is valid, but global FC0-A gate is blocked by P1-01 + shared-shell P1-06 |
| DEV-SCRIPT-ENGINEERING | HOLD | FND-04 contract is valid; P1-01 runtime Foundation defect must close first |
| DEV-AUTHORITY-UX | HOLD | contract-compatible, but common FC0-A checkpoint cannot release with confirmed P1 blockers |
| DEV-LICENSING-UX | HOLD | FND-03 contract valid; FND-05 additive guard accepted; common gate still blocked |
| FND-05 | HOLD | design compatible; may start only after blocker correction/re-audit per current sequencing |
| FND-07 | HOLD | design compatible; may start only after blocker correction/re-audit per current sequencing |

No row is `BLOCKED-CONTRACT`.

## 9. Required next sequence

1. Correct W15-P1-01 on isolated branch.
2. Main review + exact-head T1 + post-merge broad.
3. Correct W15-P1-06 on a separate isolated branch.
4. Main review + exact-head T1 + post-merge broad.
5. Re-run affected audit rows and exact checkpoint comparison.
6. If both blockers close and no new contract contradiction appears:
   - record FC0-A approved checkpoint;
   - release DEV-EDITOR;
   - release DEV-SCRIPT-ENGINEERING;
   - release DEV-AUTHORITY-UX;
   - release DEV-LICENSING-UX;
   - activate FND-05;
   - activate FND-07.

## 10. Final classification

`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — CHANGES_REQUIRED`

Reason: two confirmed material Wave 15 blockers remain. No FND-05/FND-07 breaking contract requirement is currently identified.
