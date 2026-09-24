# Wave 15 — FC0-A Post-FND06 Audit Evidence Matrix

> PRELIMINARY MAIN MATRIX — NOT AN AUDIT PASS.
> GitHub live is the authority. Independent AUD review is mandatory after FND-06 VERIFIED/FROZEN.

`AUDIT_ID: FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

`MATRIX_STATE: ACTIVE_MAIN_EVIDENCE / INDEPENDENT_AUD_REVIEW_REQUIRED`

`CURRENT_INTEGRATION_SHA: 560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

`CURRENT_TREE: 674019fbbc21001a2d68deb853c2c0b293e0a5cb`

`FINAL_BROAD_GATE: 35953557122 / EliteSCADA CI #1565 / SUCCESS`

## High-priority Wave14 -> Wave15 matrix

| Item | Preliminary status | Exact/current evidence | Residual / release impact |
| --- | --- | --- | --- |
| W15-P0-01 Working identity/bootstrap | CLOSED_FOUNDATION, pending independent reconfirmation | FND-01 VERIFIED/FROZEN at `b445ad5a9cdf7f920cf216ac66e42b6a65516aa2`; 69/69 integrated validation | No known release blocker |
| W15-P1-01 Server Script recovery/observability | **BLOCKED_FOUNDATION — preliminary Main finding** | current `ScriptRuntimeExecutionCoordinator` still hard-stops dispatch when `IsThrottled`; only explicit `ResetThrottle()`; no production automatic reset/recovery caller found | **Blocks FC0-A if independent audit confirms**; requires bounded Foundation correction before DEV release |
| W15-P1-02 legacy visual compatibility | CLOSED_FOUNDATION, pending independent reconfirmation | PR #337 + mounted V3 evidence; known `tank/value/dynamo/status`, unknown fail-closed | Final broad #1565 SUCCESS; independent audit remains required |
| W15-P1-03 / A7 selection stability | CLOSED_FOUNDATION, pending independent reconfirmation | mounted Screen/Popup selection closeout in PR #337; fixture isolation PR #339 prevents cross-spec state leak | Chromium on #1565 SUCCESS; independent audit remains required |
| W15-P1-04 projection/navigation persistence | CLOSED_FOUNDATION, pending independent reconfirmation | retryable same-identity Screen/Popup persistence + real Active identity reset regressions | Chromium on #1565 SUCCESS; independent audit remains required |
| W15-P1-05 / A8 Script Engineering maturity | SPLIT: Foundation identity contract closed; downstream authoring work remains | FND-04 readable TAG binding VERIFIED/FROZEN at `6c810647...`; #297 owns authoring UX | Cursor-safe insertion, API signatures/examples, event/scope authoring and UI recipe remain DEV-SCRIPT-ENGINEERING scope **after P1-01 Foundation blocker is closed** |
| W15-P1-06 Engineering/SPA truthful fallback UX | **BLOCKED_PRODUCT — preliminary Main finding** | exact `EngineeringApp.tsx` still renders `Demo Project` when `snapshot=null`, including loading/error states; no-model `WorkspaceBar` can present `unsaved/clean` fallbacks | **Blocks FC0-A if independent audit confirms**; bounded shared-shell correction required before parallel DEV release |
| W15-P2-01 Trends | DEFERRED_BOUNDED_WITH_EVIDENCE, pending audit | Wave14 uncertain/bounded; requires stable runtime/freshness retest | Does not block FC0-A unless audit finds a P1 mechanism |
| W15-P2-02 Popup live values | DEFERRED_BOUNDED_WITH_EVIDENCE, pending audit | Wave14 authoritative Good zero/binding evidence but missing browser freshness path | Downstream bounded runtime/UI follow-up unless audit escalates |
| W15-P2-03..07 shared UX/accessibility/navigation/templates | READY_FOR_DOWNSTREAM_DEV / DEFERRED by owner, pending audit | Wave15 backlog explicitly UI/usability scoped | Must have named downstream ownership; not Foundation by default |
| W15-U-01 forwarding latency | DEFERRED_BOUNDED_WITH_EVIDENCE | no product patch without correlated product-owned divergence | Non-blocking unless reproduced/correlated |
| W15-U-02 Alarm timestamp semantics | DEFERRED_BOUNDED_WITH_EVIDENCE | reopen only with same-occurrence authority/timestamp correlation | Non-blocking unless reproduced/correlated |
| RECHECK-SIM-PUMP-LEVEL | NOT A CONFIRMED DEFECT | Wave14 chronology says do not reopen absent correlated reproduction before restart | Must not be used to invent a Server Script fix |

## Preliminary contract-risk matrix for FC0-A lanes

| Lane | Current risk | Main note |
| --- | --- | --- |
| DEV-EDITOR | HOLD — shared-shell P1-06 + audit | FND-06 contract appears consumable, but confirmed Wave15 truthful Engineering-shell UX must be closed centrally before Editor starts on the same UI shell. |
| DEV-SCRIPT-ENGINEERING | **HOLD — upstream P1-01 Foundation blocker** | FND-04 TAG binding contract is frozen, but the runtime throttle-recovery defect is not safe to delegate to authoring UX. |
| DEV-AUTHORITY-UX | NONE IDENTIFIED / GUARDED | FND-07 must compose, not redefine, FND-02/AUTH-04. |
| DEV-LICENSING-UX | LOW BUT MATERIAL RESIDUAL | FND-05 HA entitlement/readiness must remain additive/backward-compatible to FND-03. |
| FND-05 | HOLD UNTIL AUDIT PASS | Prepared additive HA contract; must not rely on a permanently-stalled Server Script runtime for safe effective-Active ownership. |
| FND-07 | HOLD UNTIL AUDIT PASS | Prepared compositional detach contract; must fence Server Script effects using a sound runtime ownership/recovery contract. |

## Important sequencing consequence

Even if FND-06 freezes on broad #1565, this preliminary matrix currently does **not** support immediate FC0-A release.

The mandatory independent audit must first determine whether W15-P1-01 is still open on the exact frozen checkpoint. If confirmed open, Main must close it as a bounded Foundation correction, revalidate the exact integration state, and repeat the affected audit rows before releasing:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05;
- FND-07.

This matrix is append-only evidence and does not authorize product mutation by itself.


## P1-06 source note

Exact product source inspected:
`web/scada-web/src/engineering/EngineeringApp.tsx@560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

Observed:
- null snapshot during initial loading;
- null snapshot on failed fetch;
- unconditional sidebar fallback `'Demo Project'` while snapshot is null;
- no-model WorkspaceBar fallback can present `unsaved` and `clean`.

This is direct source evidence of the Wave14 A6/W15-P1-06 class and must be independently mounted/revalidated before audit disposition.


## Activation checkpoint

FND-06 was declared VERIFIED/FROZEN by Main after exact broad validation:

- product SHA: `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`;
- tree: `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- EliteSCADA CI #1565 / run `35953557122`: SUCCESS;
- Web: SUCCESS;
- Backend build/test/smoke: SUCCESS;
- Chromium end-to-end: SUCCESS.

Main verified pre-activation integration divergence above that product SHA was coordination-doc-only.

This matrix remains Main evidence, not an audit verdict. Independent AUD must confirm/reject every row.
