# CURRENT MAIN CHECKPOINT — Z2M L3 AUDIT PASS / PANASONIC ACTIVE — 2026-10-09 (BRT)

> GitHub live is the only authority. This checkpoint supersedes earlier active-state snapshots. [Main ledger #305](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305).

## Integration result

Integration branch `wave15/corrections-integration` is at **`6f90db93829983ffad2eb24c777eb2b7d320cdd3`**.

- Foundation PR #576 merged first at `cb2e20bfcf71889236408765f481a1f4c45c6868`; S5 PR #574 merged second at `6f90db93829983ffad2eb24c777eb2b7d320cdd3`. Both were explicitly authorized and are closed/merged.
- Automatic post-merge CI #1707 / [run 37934164605](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/37934164605) passed Web, Backend build/test/runtime smoke and Chromium on the exact integration head.

## Current protocol lanes

- **Zigbee2MQTT #573 / PR #575:** HEAD `175b1f2ce5ed6876424e70ff53cd5a6b6e14a84d`, tree `494c1f60f501569066ddd4a0729fb538472aa5fa`. Exact normal DRIVER_PROTOCOL T1 #994 / [37943437466](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/37943437466) passed: 1,282 passed / 1 skipped / 0 failed; classifier/Common/gate PASS, Web/Chromium/HA two-process profile-skipped. [Main audit PASS](https://github.com/brunolrogerio-collab/EliteSCADA/pull/575#issuecomment-6083110187); PR is OPEN / READY / mergeable / unmerged. A1/A2 and canonical L3 are accepted. L2 remains bounded `SKIP_WITH_REASON`; no Z2M interoperability claim. **No merge authorization for #575 has been issued.**
- **Panasonic #570 / PR #572:** unchanged HEAD `6ef9008066c41f94e1eeb95a727776472c918571`; the prior T1 #976 / `37819834781` is RED on the pre-foundation candidate. [Main release](https://github.com/brunolrogerio-collab/EliteSCADA/issues/570#issuecomment-6081480408) directs the lane to merge foundation commit `cb2e20bfcf71889236408765f481a1f4c45c6868` ordinarily into its current branch, complete `CanAcquireInputs` / `CanOwnExternalEffects` separation and canonical #560 proof, publish, then run exact-head DRIVER_PROTOCOL T1. No newer DEV checkpoint is visible.

## Chat actions

| DEV chat | Action |
| --- | --- |
| Panasonic | **SIGA in the Panasonic DEV chat** — lane branch is unchanged; continue the already released L3 work. |
| Zigbee2MQTT | **WAIT** — code complete and Main audit passed; no further DEV work. PR #575 awaits its own explicit Product Owner merge authorization. |
| S5 | **WAIT** — PR #574 is integrated; issue #571 is closed. |
| Mitsubishi / S4 | **WAIT** — already integrated; no active assignment. |

Physical driver L4 is `DEFERRED / NOT RUN` for every driver until after Wave 16 and partner disclosure, once a stable EliteSCADA release has been installed on a computer for human validation. It is not a pre-merge gate; no physical compatibility claim is made.

Wave 15 remains open. No new driver lane is released; #306 Productization, #300 final Preview, #379 i18n, #424 Help and #425 Manual remain visible. Docs PR #569 remains docs-only and unmerged; current live issue/PR comments are operational authority.

Snapshot: 09/10/2026 — Hora: 11:38 BRT
---

# PRIOR MAIN AUDIT SNAPSHOT — STAGED-INPUT DEPENDENCY — 2026-10-08 (BRT)

> This snapshot supersedes mutable release/checkpoint claims below. GitHub live remains the sole authority; revalidate before action. [Current Main ledger #305](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305#issuecomment-6072038410).

Integration: `wave15/corrections-integration@cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`.
Three product assignments remain reserved; no fourth worker or queued driver is released.

| Work / report destination | Exact published HEAD | Live gate / Main disposition | Owner chat action |
| --- | --- | --- | --- |
| Foundation PR #576; Main #305 | `912b866b0df27173726a5d52a1bd308d3c18d6e0` | T1 37833944486 PASS; MAIN_AUDIT_PASS; unmerged, PO merge authorization pending | No new DEV chat |
| Panasonic #570 / PR #572 | `6ef9008066c41f94e1eeb95a727776472c918571` | T1 37819834781 RED, one candidate-readiness failure; foundation dependency | WAIT; SIGA after Main releases the integrated dependency |
| S5 #571 / PR #574 | `cbc3a8e5303db6fde30e31ba0a9104d418abe85c` | T1 37819447640 PASS; audit REQUEST_CHANGES | SIGA in S5 |
| Zigbee2MQTT #573 / PR #575 | `14fd0d4f03cb7245a8f0b78b73cf149e66571ce1` | T1 37828674889 PASS; audit REQUEST_CHANGES; L3 depends on foundation | SIGA in Z2M for independent corrections |
| Mitsubishi PR #567 / S4 PR #568 | already integrated | no new assignment | WAIT |

## Main-owned FND-STAGED-INPUT-01

[PR #576 Main audit](https://github.com/brunolrogerio-collab/EliteSCADA/pull/576#issuecomment-6071980262) accepts the separately isolated foundation at HEAD above / tree `e494cba4350798f33ecf0b0fb1fb5a6f8d28b7d4`; 1 ahead / 0 behind the assigned base. Exact-head T1: Core 530 passed; Drivers 1,250 passed / 1 skip / 0 failed.

The accepted shared PRODUCT/SHARED_HOTSPOT diagnosis is a readiness cycle: candidate acquisition was gated until after commit, but readiness is required before commit. The bounded correction permits canonical host-started acquisition into the private Active-eligible candidate cache while process writes and external event forwarding remain fenced. HA Standby stays Stopped with zero connection/poll/subscription/write. Existing resource handover, rollback, cleanup and Runtime/request-token lifetime remain canonical.

Ownership is limited to the two shared services/coordinator files and coordinator regressions in #576. Protocol workers cannot modify shared Runtime/HA. Direct callers without a separate acquisition delegate retain the old effect-fence fallback.

**Integration order:** explicit PO authorization -> Main integrates #576 -> Main releases exact integrated dependency/base -> narrow Panasonic/Z2M acquisition-vs-write consumer changes and canonical activation/#560 proof -> exact-head normal T1 -> final Main audit -> explicitly authorized product integration.

No silent consumption of the unmerged foundation. The unpublished combined local 33-test overlay is T0 only; it does not replace published-head #572/#575 evidence. SIGA is not merge authorization.

## Current worker correction requests

- [Panasonic Main disposition](https://github.com/brunolrogerio-collab/EliteSCADA/issues/570#issuecomment-6072006526): current T1 is 1,271 passed / 1 skip / 1 failed; keep the real activation defect visible. L0/L1 focused evidence exists. After dependency release prove canonical persistence/activation, both transports, restart and #560. Refresh the stale validation doc: the DEV has installed SDK 10.0.400 and recorded 23-pass/1-fail T0.
- [S5 audit](https://github.com/brunolrogerio-collab/EliteSCADA/pull/574#issuecomment-6071989348): invalid successful execute responses must be Unknown; add real Screen/Popup browser interaction evidence; prevent asynchronous typed-prompt ownership from stranding another action. Keep Version 2 Rich action / legacy Version 1 / Engineering v23. S5 is independent of #576. The Program.cs exception is only its two DI registrations and one endpoint mapping.
- [Z2M audit](https://github.com/brunolrogerio-collab/EliteSCADA/pull/575#issuecomment-6071998103): cached observations predating an operation cannot confirm a newer read/write; readiness must reflect bridge offline/invalid inventory. Correct these owned items now while L3 awaits the foundation. Preserve external user-operated Z2M, canonical Boolean/Double TAGs, protected credentials, stable IEEE identity and the two additive registration exceptions.

Main accepted each named driver's bounded L2 SKIP_WITH_REASON for this code PR only. It is not PASS or software/physical interoperability. Physical L4 remains human-only after Wave 16, partner disclosure and stable installation, and is not a code merge blocker.

Tests validate product. Batch coherent corrections -> minimum focused evidence -> normal exact-head T1. No unchanged-head rerun or redundant broad/full CI. Checkpoint is a savepoint. Issues/PRs carry decisions; separate chats require their own SIGA.

The #573 issue-body replacement was rejected by auto-review as a stale-overwrite risk. The body was preserved; the accepted [append-only owning-issue decision](https://github.com/brunolrogerio-collab/EliteSCADA/issues/573#issuecomment-6072015230) and PR audit carry the disposition.

KNX/Native Zigbee/DALI gateway/Z-Wave/Matter remain queued. Wave 15 is not closed; final #379/#424/#425 -> #306 -> #300 remains ahead. #362 remains closed without merge. Documentation PR #569 stays docs-only and unmerged; until authorized integration, live issues/comments override older integrated docs.

Snapshot: 08/10/2026 — Hora: 21:55 BRT

---

# PRIOR RELEASE SNAPSHOT — EXTERNAL ZIGBEE2MQTT V1 — 2026-10-08 (BRT)

> This release supersedes the older two-worker / Q2-QUEUED snapshot below.
> GitHub live is the authority. Current exact scope and authorization: [#573](https://github.com/brunolrogerio-collab/EliteSCADA/issues/573). Main release: [#305](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305#issuecomment-6065413847).

Integration checkpoint: `wave15/corrections-integration@cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`.

**Current product worker slots: 3 / 3**
- Panasonic #570: ACTIVE; PR #572 OPEN. At this snapshot HEAD is `40a7711685ee0e9abeb6154e66211575a6e7fba4`; latest exact-head T1 #37816222003 FAILED. Earlier missing batch-type and xUnit analyzer issues were corrected; do not repeat those old fixes blindly. DEV continues triage/correction/publication/T1 within its lane. Revalidate live HEAD/run before acting.
- S5 #571: ACTIVE / UNBLOCKED after Main's narrow ExecuteRichCommand/action-Version-2 decision.
- **Zigbee2MQTT #573: ACTIVE / UNBLOCKED / EXTERNAL_MQTT_STATE_V1 / NO_MERGE.** Product DriverType `zigbee2mqtt.bridge`; DEV-ID DEV-HOME-ZIGBEE2MQTT-BRIDGE-V1.

## Exact Z2M assignment

- Branch: `work/home-zigbee2mqtt-bridge-v1`, created by DEV from the exact integration SHA above after live revalidation.
- PR target: `wave15/corrections-integration`.
- Validation profile: DRIVER_PROTOCOL plus inferred risk floor.
- Checkpoints/handoff: #573; code/tests/exact-head T1: lane PR; shared/Main decisions: #305.
- Scope: separately user-operated Z2M over existing MQTT transport; sanitized bridge/device inventory; selected canonical Equipment/TAG/Capability discovery; bounded stateful Boolean/Double TAG mapping; availability/reconnect/report/readback truth; PointRead/#500/#560.
- No native radio, managed/bundled Z2M, commissioning/group/scene mutation, transient-event TAG, Rich/S6 expansion, new dependency/schema/security authority.
- Publication to this repo/branch and PR/comment reporting are explicitly authorized. IMPLEMENT -> focused TEST -> COMMIT/PUBLISH -> CHECKPOINT -> CONTINUE. No additional per-checkpoint owner authorization.
- Two bounded additive registration hooks are delegated in #573: CommunicationDriverRuntimeComposition and EngineeringDriverCatalogApi. Put the tooling factory in a new isolated file. Main owns ordered reconciliation with Panasonic; no unmerged-lane consumption or unrelated shared-file edits.
- Missing local SDK is local NOT_RUN/ENVIRONMENT; actual exact-head GitHub T1 is still required. No owner credential/token request, broad CI or unchanged-head reassurance rerun.

The third slot is now occupied. KNX/Native Zigbee/DALI gateway/Z-Wave/Matter remain queued; no fourth product lane. Stable docs/research preparation is not a speculative new product release.

All physical L4 remains human validation after Wave 16, partner disclosure and stable installation. It is not a code merge gate. Applicable intermediate evidence, exact-head T1, Main audit and explicit Product Owner merge authorization remain required.

Owner chat actions: **paste the supplied bootstrap in a new Z2M DEV chat to start #573**; Panasonic SIGA for live CI correction; S5 SIGA to consume/continue its resolved contract; Mitsubishi/S4 WAIT. SIGA wakes only the receiving chat.

Docs PR #569 stays OPEN / DRAFT / NO_MERGE_AUTHORIZATION. Live #305/#573 carry the operational release while docs integration is pending.

---

## Previous coordination snapshot (superseded where the release above advances it)

# CHAT WORK ASSIGNMENTS — EliteSCADA

## Current Wave 15 execution board — 2026-10-08 (BRT)

> GitHub live is the sole authority. Revalidate refs, issue/PR state and exact-head Actions before acting.
> This board supersedes the historical Wave 10 assignments retained below.
> Current Main queue and dependency decisions: [#305 comment 6064847156](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305#issuecomment-6064847156).

Wave: W15 / selected Home-Building + industrial expansion.
Main issue: #305.
Integration target: `wave15/corrections-integration`.
Current product checkpoint: `cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`.
Worker limit: up to three independent product slices; do not fill unused slots speculatively.

### ACTIVE — Panasonic

- DEV-ID/chat: DEV-INDUSTRIAL-PANASONIC-MEWTOCOL-V1.
- CurrentTask/report destination: #570. Accepted research: #553; industrial parent: #551.
- Branch: `work/industrial-panasonic-mewtocol-driver-v1`.
- BaseSHA and current published HEAD at this snapshot: `cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`; 0 ahead / 0 behind; no lane PR/T1 yet.
- StartCondition: satisfied by the Product Owner's exact lane release; Mitsubishi PR #567 is merged.
- Objective/AllowedScope: bounded native MEWTOCOL-COM TCP + Host Serial, own codec/session, canonical TAG read/write, Engineering/PointRead/#500 diagnostics and #560 lifecycle.
- DependsOn: integrated Driver SDK/#469/#500/#560; not S5/S6.
- ParallelSafeWith: S5 isolated HMI caller files. Shared SDK/catalog/DI/HA/security/schema/workflow changes are reserved to Main.
- ValidationMatrix: focused L0/L1/L2 disposition/L3, DRIVER_PROTOCOL normal exact-head T1, Main audit.
- CompletionCriteria: published coherent source tree, applicable intermediate evidence, exact-head T1 and accepted audit. Physical L4 is deferred.
- AfterCompletion: handoff #570; wait for explicit Product Owner merge disposition. DEV does not merge.
- NextQueuedTask: none released to this chat. A queue entry is not permission.
- Owner wake action: SIGA in the Panasonic DEV chat when its turn ends.

### ACTIVE — S5 HMI Rich Command

- DEV-ID/chat: DEV-DRIVER-INTERACTION-S5-HMI.
- CurrentTask/report destination: #571; architecture input #546.
- Branch: `work/driver-interaction-s5-hmi-rich-command`.
- BaseSHA and current published HEAD at this snapshot: `cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`; 0 ahead / 0 behind; no lane PR/T1 yet.
- DependsOn: integrated S0/S2/S3; S4 predecessor is merged.
- StartCondition: satisfied; Main resolved BLOCKED-CONTRACT in [#571 comment 6064796076](https://github.com/brunolrogerio-collab/EliteSCADA/issues/571#issuecomment-6064796076).
- Objective/AllowedScope: bounded HMI Screen/Popup caller, Active definition-driven typed form, existing human CommandExecute/Audit, truthful outcomes.
- Main-owned exception: distinct ExecuteRichCommand + CommandId + explicit action Version 2. Legacy visual/Dynamo/actions remain Version 1; scada.engineering stays v23. The exact shared-file exception and compatibility/roundtrip/restart tests are in #571.
- ForbiddenScope: legacy command reinterpretation/fallback, parameter presets, browser driver methods, second authority, S0-S3/schema migration, HA internals, unrelated shared files and S7 expansion.
- ParallelSafeWith: Panasonic protocol implementation. S5 action-contract files are reserved to this explicit exception; unrelated central composition stays Main-owned.
- ValidationMatrix: focused Core action-contract/roundtrip + API authority + actual HMI tests; FOUNDATION_LIFECYCLE, RUNTIME_RENDERER, UI_EDITOR, AUTHORITY_CORE plus inferred router floor; exact-head T1; Main audit.
- CompletionCriteria: published Screen/Popup caller and rejection-before-dispatch evidence, normal T1 and accepted audit.
- AfterCompletion: handoff #571; wait for explicit Product Owner merge disposition. DEV does not merge.
- Owner wake action: SIGA now in the S5 DEV chat to consume the resolved blocker.

### Completed / parked chats

- S4 #565 / PR #568: CLOSED / MERGED; chat WAIT.
- Mitsubishi #566 / PR #567: code MERGED; issue open only for deferred L4; chat WAIT.
- Shelly, ESPHome and HAB: built; reuse accepted #560 lifecycle evidence.
- Third product worker: UNASSIGNED.
- Docs PR #569: OPEN / DRAFT / NO_MERGE_AUTHORIZATION. This board is published on that docs branch until integration is authorized; live #305/#570/#571 remain operational authority.

### QUEUED — next releases, not ACTIVE

Priority order: KNX/IP -> external Zigbee2MQTT -> Native Zigbee -> DALI gateway -> Z-Wave JS -> Matter.
Priority is not a technical serial dependency. Each future release must supply its own DEV/chat, issue, branch, exact BaseSHA, scope/locks, validation profiles and StartCondition.

| Queue | Required release preparation | Parallel opportunity |
| --- | --- | --- |
| Q1 KNX/IP (#539) | No-fee redistribution scope; pinned Falcon feasibility on net10/Windows/Linux/container; Secure/DPT/Protected Material/L2 boundary. | Independent of Panasonic and S5 for ordinary TAGs; can overlap external Z2M. |
| Q2 external Z2M (#541) | User-managed MQTT boundary; protected credentials; IEEE identity; selected import; retained/stale/write/report semantics. | No managed sidecar/controller prerequisite; ordinary state can overlap S5. Events use integrated S1/S3/S4. |
| Q3 Native Zigbee (#541) | Reuse integrated #543; pinned Node/herdsman/converters + transitive license inventory; owned ZigbeeCoordinator lease; protected key/backup adapter. | After accepted Z2M-first rollout priority; can overlap Z-Wave in isolated adapters/resources. |
| Q4 DALI gateway (#539) | Verified gateway map/profile; existing Modbus TCP first, BACnet fallback; semantic Equipment/Capability projection. | Profile/content work can overlap Q1/Q2. KNX/DALI variant alone depends on KNX. No native DALI driver. |
| Q5 Z-Wave JS (#542) | Reuse #543; pinned server/core/schema; ZWaveController lease; local control plane; S0/S2 keys; sleeping write truth; NVM/recovery and LR risk disposition. | Independent of KNX/DALI/Native; parallel only without shared supervisor/resource/lockfile edits. |
| Q6 Matter (#542) | Linux/Wi-Fi first; pinned sidecar; current security/platform review; protected fabric persistence/restore. Thread later via external Border Router. | Independent of Zigbee/Z-Wave; later priority because more stack/release gates. |

S6 full protocol adoption is queued after accepted S5 for complete Script/HMI proof; assign one owner per real protocol adapter. Event-only automation already has S0/S1/S3/S4. S7 portability expansion is conditional, not speculative.

Suggested next batch: KNX feasibility/driver + external Z2M; DALI profile/content may use a third slot after exact release. Subsequent batch: Native Zigbee + Z-Wave, then gated Matter. Do not launch a fourth product worker.

### Productization queue

#379/#425 Phase 1 and Help PR #439 are already integrated. Do not restart them.
Stable-surface inventory/glossary/Help/manual deltas can proceed with separate file ownership; avoid S5's changing editor files.
Final accepted feature set -> #379 locale sweep -> synchronized #424/#425 final deltas -> #306 EEE Simulation + real-Modbus/provenance/PREVIEW-READY -> #300 fresh exact-SHA Preview -> Wave 15 acceptance -> Wave 16 #408.

### Deferred future / gates

- No paid vendor entitlement direct drivers now: Tuya Cloud direct and Intelbras GDI/Mibo direct are outside W15.
- Direct BTHome/Bluetooth remains future until qualification/fee applicability is settled.
- Certification/logo/restricted-mark claims remain future. External Z2M is retained; GPL is not a paid-license blocker. Managed/bundled Z2M is a separate packaging/compliance choice.
- All physical L4 is human validation after Wave 16 + partner disclosure + stable installation on a computer. It does not block code integration after intermediate tests/T1/Main audit and explicit merge authorization. No hardware claim before physical evidence.
- Every new ICommunicationDriver passes #560. HA = High Availability/redundancy; HAB = Home Assistant Bridge.
- SIGA wakes only its receiving chat and does not authorize merge. Checkpoints are savepoints; continue inside an ACTIVE lane. New coding starts only after an exact ACTIVE release.

---

## Historical assignment board — 2026-08-30 (superseded for current work)

# CHAT WORK ASSIGNMENTS — EliteSCADA

Date: 2026-08-30 (BRT)  
Stage: **DRIVER CONVERGENCE — ACTIVE / COMMON LAB MERGED / WAVE 11 DEFERRED**  
Integration owner: **Coordinator**  
Shared issue: **#174**  
Integration branch: `coordination/driver-convergence-v3`  
Draft PR: **#175**

Start a new Coordinator chat with `PROJECT GOAL.md`, `LAST CHANGE.md` and `docs/COORDINATOR-HANDOFF.md`, then re-read live GitHub state.

## Current priority

Wave 10 is CLOSED / MERGED / POST-MAIN GREEN. The common seven-peer interoperability lab is MERGED. Current priority is shared Driver convergence plus the remaining protocol-owned product L2/fix gates.

Operational authority is live branch/PR `head_sha` + exact Actions evidence + current coordination docs. Long-lived PR bodies can be historically useful but stale.

## Coordinator — Driver Convergence v3

Issue: #174  
Branch: `coordination/driver-convergence-v3`  
PR: #175  
Exact audited head: `06c7d408c76926bf5d37dfec4be20ea6044f52b1`  
Exact normal CI: **#895 GREEN**

### Implemented on #175

- fail-closed Driver module registry keyed by stable DriverType;
- common runtime planner/factory registry;
- protocol-neutral Data Source readiness contract;
- scoped host-owned protected-material resolver/lease seam;
- focused shared-contract tests;
- partial `CommunicationTagBinding` / `TagPhysicalValueTransform` / Engineering DTO scaffold.

### Immediate Coordinator task — COMPLETE v15 BINDING

The v15 scaffold is **not yet end-to-end functional**:

- `EngineeringExchangeService.CurrentSchemaVersion` remains 14;
- TAG Preview does not invoke `CommunicationTagBindingEngineeringValidator`;
- TAG Apply drops `dto.CommunicationBinding`;
- preview materialization also omits the rich binding;
- TAG CSV fidelity is missing;
- no complete JSON/CSV/Preview/Apply/re-export/package/revision/PostgreSQL v15 regression exists.

Next Coordinator must complete this slice before adapting MQTT:

1. bump canonical Engineering schema to 15 with <=v14 compatibility;
2. wire binding validation into Preview;
3. preserve binding through Apply/materialization/export;
4. enforce `Address == CommunicationBinding.PortableAddress`;
5. implement CSV fidelity where applicable;
6. prove JSON/CSV/package/revision/PostgreSQL round-trip;
7. fail closed on malformed binding/plaintext protected material;
8. preserve `TagValueSelector` and ADR-007 transform-before-selection semantics;
9. exact-head CI.

The old `coordination/driver-convergence-mainline-v2` is **reference-only / obsolete as a merge source**.

## Common interoperability lab — MERGED

PR #173 merge: `a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`  
Validated functional head: `3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`  
Interop Lab Smoke #42: GREEN  
EliteSCADA CI #886: GREEN after rerun of unrelated Modbus timing failures on unchanged SHA.

Common test peers on main: MQTT, CIP, OPC UA, IEC-104, DNP3, Siemens S7 and BACnet/IP. Peer health is not Driver product acceptance.

## Driver assignments

### D10 MQTT — READY FOR COORDINATOR CONVERGENCE

Branch `driver10/mqtt`; audited head `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`; Draft #128; exact CI #865 GREEN.

Broad live evidence: Mosquitto + HiveMQ, MQTT 5/3.1.1, QoS/retained, TLS/auth, negative security, persistent broker restart, live freshness recovery.

Worker standby for targeted defects only. After v15 is complete, D10 is the first shared integration candidate.

### D6 IEC-104 — READY FOR COORDINATOR CONVERGENCE

Branch `driver6/iec-60870-5-104`; head `d597ef5ed1885b63dcd0b3568287bc1e34330bee`; Draft #146; CI #798 GREEN.

Accepted independent lib60870 L2 evidence: former validation PR #168, smoke #7 GREEN, 13/13. #168 is now closed unmerged as completed evidence.

### D5 Allen-Bradley CIP — READY FOR COORDINATOR CONVERGENCE

Branch `driver5/allen-bradley-cip`; head `18ff6dc989a65c1f8b006f83c08d8394a5510914`; Draft #111; CI #785 GREEN.

Accepted independent CIP L2 evidence: former validation PR #165 / smoke #6 GREEN. #165 is closed unmerged as completed evidence.

### D9 OPC UA — ACTIVE PRODUCT-PATH L2

Branch `driver9/opc-ua`; head `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6`; Draft #169; CI #869 GREEN.

Next worker gate: actual Driver 9 session/read/write/subscription against common open62541, server loss/reconnect/resubscription, SourceTimestamp/ServerTimestamp preservation, dedicated L2 and exact normal CI. Secure/custom datatype evidence follows first green slice.

### D7 DNP3 — ACTIVE CANONICAL TYPE FIX

Branch `driver7/dnp3`; head `ac0dd6944f53d19447f3353addd404c02da7249c`; Draft #108; CI #697 GREEN.

Validation PR #167 remains OPEN. Real defect: configured `TagDataType.Int32` G30V1 value 4242 reaches canonical cache as `Double`. Worker must preserve configured canonical type, add regression and rerun #167. Never weaken the assertion.

### D8 Siemens S7 — ACTIVE PRODUCT-PATH L2

Branch `driver8/siemens-s7-iso`; head `0c37b922b44f591ebd143470abf3ebaa6b4bffae`; Draft #135; CI #789 GREEN.

Next gate against common python-snap7: ISO/S7 session, negotiated PDU, deterministic DB reads, write/readback, PDU-aware multi-read and peer restart/reconnect.

### D4 BACnet/IP — ACTIVE PRODUCT-PATH L2

Branch `driver4/bacnet`; head `de3357750f79266e43588e7bb26d66093f8cf3d5`; Draft #109; CI #860 GREEN.

Next gate against common BACpypes: Who-Is/I-Am, RP/RPM, WP/readback, COV and route loss/re-resolution/recovery. Priority/relinquish and BBMD/FDR follow when peer topology supports them.

## Convergence order

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

D9/D7/D8/D4 enter shared integration after their active worker gates close.

## Repository hygiene at handoff

Wave 10 issues #149-#152 are closed completed.

Validation-only PRs closed unmerged after evidence acceptance/supersession: #148, #160, #161, #162, #163, #164, #165, #166, #168.

Keep open: #175, Driver handoff PRs #108/#109/#111/#128/#135/#146/#169, and active DNP3 validation #167.

## Shared locks

- Engineering Preview/Apply/revisions/package fidelity is mandatory.
- No plaintext credentials/private keys/tokens in Engineering/package data.
- Stable TAG bit identity is `TagId + TagValueSelector`; `.NN` is display/authoring only.
- ADR-007 physical byte/word transform occurs before typed decode/bit selection.
- No Driver-to-Driver calls or bypass of TAG/cache/event architecture.
- Runtime readiness is Data Source/protocol readiness, not every point Good.
- L0/L1/L2/L3/L4, normal CI, licensing and conformance are separate claims.
- Never weaken a test to improve status.
- Wave 11 remains deferred until Driver convergence closes or priority is explicitly changed.

## Required worker handoff

Every worker handoff reports exact branch/head, delivered scope, changed files, exact CI/L2 evidence, limitations/risks, shared decisions needing Coordinator action, and confirmation that unassigned shared contracts were not redefined.