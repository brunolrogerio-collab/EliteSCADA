# CURRENT WAVE 15 LANE SEQUENCE — 2026-10-08 (BRT)

> This section supersedes all older current-status/driver-order sections below where they differ.
> GitHub live is authoritative; revalidate before action.
> Operational queue: [#305 comment 6064847156](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305#issuecomment-6064847156); execution board: [CHAT-WORK-ASSIGNMENTS.md](CHAT-WORK-ASSIGNMENTS.md).

Integration checkpoint: `wave15/corrections-integration@cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`.

## Current parallel batch

- **Panasonic #570 ACTIVE**: native MEWTOCOL-COM TCP + Host Serial; DRIVER_PROTOCOL. Mitsubishi-first prerequisite is satisfied by merged PR #567.
- **S5 HMI #571 ACTIVE**: Main resolved the Screen/Popup action discriminator blocker. Explicit ExecuteRichCommand/action Version 2 exception; legacy visual/Dynamo/actions stay Version 1; Engineering stays v23. Exact issue body is the contract authority.
- These two lanes are independent; ordinary PLC TAG read/write does not depend on S5/S6.
- At most three parallel product workers. Third slot remains unassigned until an exact release is ready.
- S4 #565/PR #568 and Mitsubishi PR #567 are merged; their chats WAIT. #566 remains open for deferred physical L4.
- Managed sidecar + typed Host Resource #543/PR #548 and Event/Command S0-S3/S4 are integrated. Old research WAIT-foundation language is historical.

## Next queue and dependencies

Preferred priority: **KNX/IP -> external Zigbee2MQTT -> Native Zigbee -> DALI gateway -> Z-Wave JS -> Matter**.
These remain QUEUED; this roadmap is not a new implementation release.

- KNX: settle pinned no-fee Falcon redistribution/platform feasibility, Secure/DPT/Protected Material and independent L2. Its ordinary TAG path can overlap external Z2M.
- External Z2M: user-managed MQTT, not bundled Z2M; IEEE identity, selected import, protected credentials and truthful report/readback. It needs no owned ZigbeeCoordinator. Events consume integrated S1/S3/S4.
- Native Zigbee: reuse #543, add a pinned protocol adapter, exclusive ZigbeeCoordinator and protected key/backup lifecycle. Z2M-first remains rollout priority. Native and Z-Wave may overlap after shared-path/resource separation.
- DALI: verified gateway profile over existing Modbus TCP or BACnet/IP, not a standalone native driver. Profile/content work may overlap earlier drivers. Only KNX/DALI variant depends on KNX.
- Z-Wave: reuse #543, ZWaveController, protected S0/S2 keys, local-only server API, software-tested NVM/recovery, sleeping-write truth and current LR risk disposition.
- Matter: Linux/Wi-Fi first per accepted #542, with pinned sidecar, current security/platform disposition and protected fabric restore; Thread later via external Border Router. It has no technical dependency on Zigbee/Z-Wave.
- S6: full Script/HMI-to-protocol proof after accepted S5; ordinary TAG construction and event-only S4 automation do not wait for HMI. Assign one owner per adapter. S7 library/fragment expansion only for a demonstrated requirement.

No lane may consume another unmerged DEV branch. Main owns common SDK/catalog/DI/schema/security/HA/lockfile arbitration.

## Productization / closure

Phase 1 already exists for #379/#425 and Help PR #439 is merged. Stable Help/manual/glossary inventory can overlap protocol work, with distinct file/Topic-ID ownership. Final locale/editor sweep follows accepted changing surfaces.

**Accepted selected W15 features -> #379 final pt-BR/en/es -> synchronized #424 Help / #425 Manual final deltas -> #306 late EEE Simulation + real-Modbus packages/provenance/PREVIEW-READY -> #300 fresh exact-SHA complete-product Preview -> Wave 15 acceptance -> Wave 16 #408.**

Do not reopen accepted visual-library/artwork work. PR #362 is CLOSED WITHOUT MERGE; historical installer evidence only, never wholesale integration.

## Scope exclusions / physical validation

- Tuya Cloud direct, Intelbras GDI/Mibo direct and unresolved direct BTHome/Bluetooth qualification remain outside W15.
- Native standalone DALI is not justified; use a gateway.
- Certified-product/logo/restricted-mark claims are future qualification.
- External Z2M remains a W15 candidate; GPL is not a paid entitlement. Managed/bundled Z2M is a separate packaging decision.
- **All physical driver L4 is human validation after Wave 16, partner disclosure and stable EliteSCADA installation on a computer. It is not a W15 code merge gate.** Intermediate tests, exact-head T1, Main audit and explicit Product Owner merge authorization remain required; no physical compatibility claim.
- #560 lifecycle is mandatory for every new driver. HA = High Availability/redundancy; HAB = Home Assistant Bridge.
- SIGA is chat wake, not new lane/merge authorization; CHECKPOINT = SAVEPOINT.

---

## Historical roadmap snapshots (retained; current status above wins)

# FINAL REVALIDATION CORRECTION — 2026-10-03

> Supersedes the active-lane status in earlier 2026-10-03 handoff text below.
> GitHub live remains authoritative.

Current integration after the first handoff-doc merge:
`e606d3f5dd50617fb5948372f909a5553dd97c36`

Active branches were released from:
`aca641501302edb04f32b83b04adf0726d7e8a96`

The resulting `behind 7` against current integration is documentation-only. Do not require DEV rebases merely for those handoff docs.

Final outgoing lane state:
- #482 `work/w15-visual-library-catalog`: still no implementation commit, no PR; `ahead 0 / behind 0` vs release base.
- #483 `work/w15-home-building-h0@9b2cea1293e8bdd0ab250a28446bd53cee8d8697`, tree `8fbd95cfeed9e95ca296957e1d5855d2eab78eb8`; one WIP commit touching `web/scada-web/src/engineering/types.ts`; no PR/handoff yet.
- #484 `content/w15-builtin-asset-curation-b1@055deb4df30eaf3e8af4d33fdc8739cf3d1b9df4`, tree `bca34cc2ea3b12124db703244ed715ebced9e0f9`; 86 draft assets in current batch manifest; no PR/handoff yet.

#483 and #484 are WIP, not Main-accepted.
CODEX remains parked.
Protocol-driver fan-out remains blocked.

---

# EliteSCADA Roadmap — Wave 15

## Product Owner acceptance update — 2026-10-06

This update supersedes the older current-gate and closure wording below. The product integration is `wave15/corrections-integration@25df20bf562acf6c3d78eb2e953f7010729b7c49` (PR #529 merged). Issues #484 (SVG curation), #501 (Dynamo catalog) and #308 (visual-quality gate) were closed by explicit Product Owner acceptance: the first-party SVG factory/catalog is sufficient without third-party SVGs; the current Dynamo catalog is usable; further visual polish is future work, not a Wave 15 gate. #482, #496, #500, #503 and #445 are also closed.

The post-merge CI run [#37494810971](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/37494810971) completed SUCCESS on that exact SHA: Web build, Backend build/test/runtime smoke, and Chromium Wave 15 merge integration smoke passed. The separate full-browser E2E step was skipped by workflow conditions. Remaining productization/closure work includes #306, final Preview #300, localization #379, contextual help #424 and manual #425. PR #362 remains a separate open Preview-workbench PR and does not block product integration.

For the precise mutable snapshot, see root [`LAST CHANGE.md`](../LAST%20CHANGE.md). Historical lane details below are retained as execution history, not current status.

### Next coordinator directive — Home/Building automation

The Product Owner wants the incoming coordinator to begin the Home/Building automation-driver program, not to reopen accepted Dynamo/artwork work. Start from #472 and review the research dossiers in #475. The old #472 state `WAIT_CURRENT_LANES / NO_IMPLEMENTATION_BRANCH` is superseded by this owner reprioritization. First revalidate and reuse #469 Host Serial; audit and close/revise the still-open #483 canonical Engineering/catalog prerequisites on the current integration; then choose the first protocol implementation from verified #475 GO/WAIT/REJECT evidence, with license, hardware and test strategy recorded. Do not duplicate existing BACnet/IP, and keep device compatibility claims at the level actually tested. Wave 15 productization issues #306/#300/#379/#424/#425 remain tracked in parallel; this transition is not a Wave 15 completion claim.

**Status date:** 2026-10-06 (BRT)
**Active direction:** **WAVE 15 FOUNDATION-FIRST COMPLETE PRODUCT DELIVERY**  
**Integration:** `wave15/corrections-integration`  
**Global issue:** #297  
**Foundation / dependency / parallel DEV orchestration:** #305

Authoritative stable product intent: root `PROJECT GOAL.md`.  
Mutable operational snapshot: root `LAST CHANGE.md`.  
**Live canonical Wave 15 Main Coordinator <-> Codex/Work handoff:** `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.  
**Short current combinator/pointer:** `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Generic coordinator rotation prompt: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`.

> GitHub live always wins for exact branch/SHA/PR/CI state. Historical Wave 14 documents remain evidence, not current sequencing authority. While Wave 15 is active, `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` is a live operational document, not a historical snapshot.

## Live current gate — 2026-10-03 — NEXT BATCH ACTIVE

This section supersedes the 2026-10-02 current-gate wording below for current sequencing only.

Accepted Batch-3 product checkpoint:
`9b596286a40b1649564075626e817cc61e32e7bc`

Post-merge slim evidence on that product SHA:
- Web SUCCESS;
- Backend build/test/smoke SUCCESS;
- Chromium SUCCESS.

Roadmap-only integration head used to release the next batch:
`aca641501302edb04f32b83b04adf0726d7e8a96`

Active bounded batch:
- #482 — Visual Library Catalog V1 — `work/w15-visual-library-catalog`;
- #483 — HOME/BUILDING H0 P0 convergence — `work/w15-home-building-h0`;
- #484 — Built-in Asset Curation Batch 1 — `content/w15-builtin-asset-curation-b1`.

Outgoing revalidation:
- #482: ahead 0 / behind 0 / no PR;
- #483: ahead 0 / behind 0 / no PR;
- #484: ahead 1 / behind 0 at `055deb4df30eaf3e8af4d33fdc8739cf3d1b9df4`, tree `bca34cc2ea3b12124db703244ed715ebced9e0f9`; manifest currently contains 86 draft assets; no PR/handoff yet.

Current concurrency guard:
- do not open V2 SVG-first/core.svgSymbol before #482 disposition;
- do not open HOME/BUILDING H1-H6 before #483 disposition;
- do not open protocol drivers yet;
- CODEX parked.

Canonical successor snapshot:
`docs/WAVE15-MAIN-COORDINATOR-HANDOFF-2026-10-03-NEXT-BATCH-ACTIVE.md`

## Live current gate — 2026-10-02

This section supersedes the older live-status sections below for **current sequencing only**. Historical foundation/FC0 material remains useful as execution record.

### Exact current integration / convergence

- current integration authority: `wave15/corrections-integration@056224d1498b217ccb9a7cf5d13e0a2eb27d27bb`;
- Main batch-3 convergence branch: `coord/w15-batch3-convergence`;
- validation PR: #480;
- convergence HEAD: `f6ed7d8f3ac24a54673885d8be5c10f0dbf03e3f`;
- composed exact candidates:
  - Historical Playback #470 @ `dd4263a565625072b5ecf1b53504443772c4db56`;
  - Asset Factory #474 @ `9942e36a8a261cb5591dd60177e3d01db1544b4a`;
  - Modbus Family / Host Serial #476/#469 @ `edab8a99b5058b6c971f3ffa07e1c77cd73772a0`.
- Asset Factory exact-head tooling validation: **26/26 PASS**, full build exit 0, catalog check valid.
- combined T1 #619 / `37092144575`: in progress at this roadmap update; classify/common/Web/.NET already green, Chromium/final gate still pending.
- do not release the next coding batch until #480 is accepted/merged and the post-merge slim gate is revalidated.

Canonical detailed remaining-plan note: #305 comment `5964985657`.

### Remaining Wave 15 — Stage 1: Visual Library / Asset product foundation

Owners:
- #473 — Visual Library / SVG-first;
- #474 — Asset Factory;
- #308 — visual-quality acceptance gate.

The rejected Dynamo artwork PR #451 remains permanently excluded. The accepted implementation path is now:

`#474 Asset Factory -> #473 Library Catalog UI -> #473 SVG-first authoring -> curated built-in asset expansion -> SVG-backed Dynamos/visuals -> #308 visual acceptance`

Required slices:

1. **Library Catalog Product UI**
   - EliteSCADA Built-in / Project-User / Associated Library separation;
   - hierarchical categories;
   - resource-kind filters;
   - tags/search;
   - preview and scalable browsing;
   - consume #474 manifest/taxonomy;
   - preserve `.escadalib` Associate != Import semantics.

2. **SVG-first authoring**
   - safe first-class vector object, preferred `core.svgSymbol` subject to contract audit;
   - canvas placement, resize/rotate/flip/opacity;
   - palette/fill/stroke overrides;
   - semantic paint slots;
   - canonical property/binding/animation integration;
   - no arbitrary SVG DOM/script authority.

3. **Built-in static asset content expansion**
   - large, curated, license/provenance-audited catalog;
   - preferred SVG;
   - Industrial, P&ID, Sanitation, Electrical, Process, Building, Residential, Navigation, UI/HMI, Indicators, Flow/Connectors and Generic/Layout;
   - navigation/action/indicator assets are first-class static system assets, not forced Dynamos.

4. **SVG-backed Dynamo composition**
   - high-quality vector artwork + canonical TAG/Command parameters;
   - primitive geometry remains available but is no longer mandatory for complex artwork.

### Remaining Wave 15 — Stage 2: HOME/BUILDING common foundation

Owner:
- #472.

Research authority:
- #475.

Preserve the single Runtime spine:

`Driver/Data Source -> TAG/Command/Equipment -> Cache/Event -> Historian/Alarm/Gateway/Scripts/Realtime`

Friendly projection:

`Location -> Equipment(Device) -> Capability -> TAG/Command`

Required foundation before protocol-specific automatic materialization:

- canonical frontend `DataSourceId` + `CommunicationBinding`;
- Memory ownership by stable DataSource ID;
- Equipment independent from mandatory Template/Faceplate;
- unified Driver product/module registration;
- Integration Catalog metadata;
- protected material / secret references;
- transient device-event semantics;
- protocol-neutral rich Action/Command only if required;
- physical topology `Site -> Building -> Floor -> Area/Room -> Zone`;
- protocol-neutral Device/Capability model;
- Host Resource generalization from the actual #469 Host Serial result;
- common LocalBridge lifecycle;
- Integrations/Locations/Devices UX;
- composite discovery candidates;
- privileged/audited pair/include/commission/remove/heal/address/restore boundary.

No second residential Runtime/TAG/Historian/Alarm/Gateway/automation engine.

### Remaining Wave 15 — Stage 3: HOME/BUILDING driver execution

Protocol DEV branches open only after the minimum shared foundation required by each one is frozen.

Planned families:

- Shelly RPC;
- ESPHome Native;
- Home Assistant Bridge;
- Zigbee2MQTT Bridge;
- Native Zigbee;
- DALI through selected documented gateway first;
- KNX/IP;
- Z-Wave JS Bridge;
- Matter Controller when maturity/certification gate is acceptable;
- BTHome receive-first after Bluetooth Host Resource.

Binding distinctions:

- Zigbee2MQTT and Native Zigbee are separate integrations;
- Zigbee2MQTT GPL code is not embedded/copied into the proprietary core;
- native DALI is not first release;
- native Z-Wave is not first release;
- Tuya/Intelbras remain explicit cloud/commercial decisions.

### Remaining Wave 15 — Stage 4: final product coherence

Do not restart already completed Phase-1 work.

- #379 — final pt-BR/en/es cross-product sweep after new surfaces stabilize;
- #424 — contextual Help recomposition/finalization;
- #425 — Manual final completion, screenshots and new workflows;
- #308 — final visual maturity acceptance through #473/#474 route.

### Remaining Wave 15 — Stage 5: Productization / Preview

#306 remains PREVIEW-READY aggregator:

- final localization/help/manual;
- accepted visual/library gate;
- selected HOME/BUILDING surfaces;
- EEE Simulation v15;
- EEE real-Modbus v15;
- package/checksum/provenance evidence;
- exact integrated validation.

Then #300 final fresh Preview:

1. CODEX black-box Preview when capacity permits;
2. independent Product Owner human Preview;
3. correlate findings;
4. concrete residual correction only;
5. exact candidate revalidation;
6. final Wave 15 acceptance.

EliteGO remains deferred after Wave 16 per Product Owner decision.

### Current scheduling guard

Immediate gate:

`PR #480 combined T1 -> Main acceptance -> merge -> slim post-merge -> new integration checkpoint`

No new implementation branch is released by this roadmap update.

After that checkpoint, Main may restore bounded parallelism only after path/contract ownership audit. Shared foundations precede protocol fan-out.

## Live current gate — 2026-09-24

This section supersedes older execution-status prose below when describing the **current** Wave 15 gate. Historical slice descriptions remain useful as execution record but are not current authorization.

- FC0-A — **RELEASE APPROVED** at product SHA `e3ed5138369c576549cb58a7aff9783792f322d3` / tree `4e7627774fbfc111344e3d80fcb9d921eed8377e`.
- release gate — EliteSCADA CI #1569 / `36060017969`: SUCCESS; Chromium 655 passed / 0 failed.
- FND-01 / FND-02 incl. AUTH-04 / FND-03 / FND-04 / FND-06 / FND-08 — **VERIFIED/FROZEN**.
- current integration HEAD at replacement-Main takeover — `3480ed03a6719aef38e4c1ced2f466aaa4fa10b3`; divergence from FC0-A is coordination/documentation-only across the five canonical handoff/roadmap files. No post-FC0A lane product code is integrated yet.
- DEV-SCRIPT-ENGINEERING PR #344 — `MAIN_ACCEPTED_FOR_CODEX / ACTIVE_SHARED_CODEX_ROUTE / DEV_WAIT` at `cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`.
- DEV-EDITOR PR #349 — `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT` at `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`; T1 `36066874097` SUCCESS.
- DEV-LICENSING-UX PR #345 — corrected head `f5d3212b9c114d3ad6e2239460172db0b3d568f8`; T1 `36071779912` SUCCESS; Main accepted for sequential CODEX queue.
- DEV-AUTHORITY-UX PR #346 — corrected head `9fd2462f43c74b085e58be91dbec9ebdf18514c5`; stable role-key direction accepted, but T1 `36071729747` is red on candidate-causal nullable-baseline TS2345; remains DEV correction under `DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`.
- FND-05 PR #347 — corrected head `be9cf0f3f02aa1ba49cdb6b589abd5e1e723c845`; T1 `36072325579` SUCCESS; transport-neutral two-independent-service peer handoff boundary Main-accepted; queued for mandatory `CODEX_HA_ADVERSARIAL_GREEN`.
- FND-07 PR #348 — DRAFT at corrected head `af7bf1ae51539975b3b3e8b40ea36472249ade2e`; no-Demo/empty-first-project fixture progressed, but T1 `36072685058` remains red on one stale historical `developer + operator` expectation while clean bootstrap truth returns one `developer`; remains DEV correction `FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`.
- INFRA-CI-01D — **PREPARED ONLY / NO MUTATION**; may activate only after the active Script CODEX handoff and a fresh Main decision from then-current integration HEAD.
- binding shared CODEX route remains `ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`. A new explicit route is required before changing mission.
- first-project fresh-install partial preview — **PREPARED / NOT ACTIVE**; entry still requires four feature lanes integrated/T2-verified plus FND-05/FND-07 independently VERIFIED/FROZEN and no journey-invalidating P0/P1.

Canonical live coordination board:
`coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`, rev 0006.

## Product objective

Wave 15 is the complete-product convergence wave. It combines the accepted platform foundation with the remaining developer/operator/customer-visible product work rather than optimizing for isolated issue closure.

Target product scope includes:

- material generic corrections inherited from Wave 14 homologation;
- developer-functional WYSIWYG Screen/Popup Editor;
- Script Engineering maturity;
- configurable granular Security Authority;
- Runtime Session Lease / Licensing v2;
- EliteGO companion runtime application;
- HA/redundancy;
- safe installation/application/Authority detach and project switching;
- WAN/timing resilience;
- profile-aware CI;
- industrial visuals/library/thumbnails;
- contextual Manual/Help;
- pt-BR/en/es product coherence;
- representative EEE Sim/Real Modbus v15 application;
- exact integration checkpointing;
- fresh Codespaces Product Owner audit and residual correction loop.

## Foundation-first execution

Foundation families:

- **FND-01** — Working/lifecycle/bootstrap;
- **FND-02** — Security Authority;
- **FND-03** — Runtime Session Lease / Licensing v2;
- **FND-04** — Server Script recovery/ownership + frozen Script TAG reference-resolution semantics;
- **FND-05** — HA identity/topology/fencing;
- **FND-06** — canonical renderer/visual stability;
- **FND-07** — secure installation detach/neutral bootstrap;
- **FND-08** — WAN/common timing;
- **INFRA-CI-01** — profile-aware Wave 15 CI orchestration.

State model:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

A downstream feature may consume a shared contract only after the required slice is `VERIFIED + FROZEN`.

## Current foundation snapshot

- FND-01 — **VERIFIED/FROZEN**.
- FND-02 Security Authority, including AUTH-04 — **VERIFIED/FROZEN**.
- FND-08 common timing — **VERIFIED/FROZEN**.
- FND-03 — **ACTIVE / NOT FROZEN**.
- FND-03 Slice 1 durable Runtime Session Lease identity/persistence — **INTEGRATED / VERIFIED** at integration checkpoint `456c66f4966ab5302831f642a39690ae3a3402a5`.
- FND-04 remains required for FC0-A and has a new binding readable-TAG-reference resolution exit criterion from #305 comment `5701881550`.
- FC0-A remains blocked.

Exact current SHA, branch/PR and CI details belong in `LAST CHANGE.md`, `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`, the short `docs/CURRENT-COORDINATOR-HANDOFF.md` pointer and live issues.

## Current active path — FND-03 Runtime Session Lease / Licensing v2

### Slice 1 — durable Runtime Session Lease identity/persistence

**INTEGRATED / VERIFIED.**

The integrated slice establishes durable logical Runtime Session Lease state without treating transport/socket count as licensed seats. Exact post-merge CI is recorded in #301 and `LAST CHANGE.md`.

FND-03 as a whole is not frozen merely because Slice 1 is green.

### Current authorized slice — machine-license v2 schema/codec

Latest Main Coordinator authorization: #301 comment `5699620231`.

Required base is the current Slice-1 integration checkpoint. Scope is limited to the signed machine-license schema/codec foundation:

- preserve ESLIC1 compatibility;
- add a signed machine-bound v2/ESLIC2 representation with explicit `viewOnlySeats`, `interactiveSeats` and `haRuntime` entitlement;
- preserve the existing signature/hardware-fingerprint/expiry trust path;
- do not infer Interactive entitlement from ESLIC1;
- expose the new entitlement information through narrow versioned/internal contracts;
- do not mix Runtime admission enforcement, quota calculation, license lifecycle, Generator UX, Installation UX, EliteGO UX or HA election/fencing into this slice.

The Product Owner reports Codex started this slice and was interrupted by the interaction limit before a completed handoff. Because unpublished local progress may exist, resume must inspect the prior worktree/session before recreating work. GitHub absence is not proof that the local implementation is empty.

### Remaining FND-03 work after the schema/codec slice

Still requires coordinator-approved slices for the rest of the frozen FND-03 contract, including as applicable:

- common server-side requested-class -> authoritative granted-class admission semantics;
- Authority intersection and View Only fail-closed enforcement;
- shared Web + EliteGO Interactive/View Only quota accounting;
- explicit fallback/rejection reasons;
- reconnect/REST/WebSocket multiplicity remaining one logical lease;
- transactional license inspect/verify/replace/remove primitives required by installation switching;
- deterministic compatibility and negative/concurrency tests.

Do not collapse these into the current schema/codec slice without an explicit scope change.

## FND-04 addition — readable Python TAG references are a Foundation contract

The Product Owner's W15-P1-05 requirement that normal generated Python use readable TAG paths instead of GUIDs exposed a shared contract gap: `tag_read` and `tag_write` do not currently share one frozen reference-resolution semantic, and plain path-only runtime resolution would be unsafe under TAG rename/path reuse.

Therefore #305 comment `5701881550` adds an explicit FND-04 exit criterion. Before FND-04 can freeze for DEV-SCRIPT-ENGINEERING, it must establish and test that:

- readable source references resolve through one supported read/write semantic;
- stable `TagId` remains the internal identity authority;
- Script Engineering retains enough stable binding evidence to detect identity drift;
- missing/ambiguous/stale references fail closed;
- rename/move or later reuse of an old path can never silently retarget a script to another TAG;
- selectors preserve the same stable-identity protection.

This does not move the Object Browser/autocomplete/cursor-insertion UX into Foundation. Those remain downstream DEV-SCRIPT-ENGINEERING responsibilities after the contract freezes.

## FC0 checkpoints

### FC0-A — first parallel feature release

Require:

`FND-01 + FND-02 + FND-03 + FND-04 + FND-06 VERIFIED/FROZEN`

plus:

- common FND-08 timing contract frozen;
- INFRA-CI-01 ready/frozen;
- one exact integration checkpoint.

FC0-A release now has an additional mandatory closure gate after FND-06:

`FND-06 VERIFIED/FROZEN -> FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01 -> FC0-A`

That audit must reconcile Wave 15 implementation against final Wave 14 diagnostic premises/gaps and prove prepared FND-05/FND-07 do not require breaking contracts already frozen for the first four DEV lanes.

Only `ACCEPTABLE / FC0A_RELEASE_APPROVED` releases bounded parallel work for:

- Editor;
- Script Engineering;
- Authority UX;
- Licensing UX.

At the same audited checkpoint FND-05 and FND-07 may also activate in parallel on isolated Foundation branches.

Parallel coding concurrency is set dynamically by Main according to ownership isolation, shared-hotspot risk, review capacity and the sequential CODEX validation queue. There is **no fixed four-DEV limit**; the prior limit was a historical operational throttle for a different context.

Until Main explicitly records FC0-A, these DEVs remain blocked even if an individual prerequisite PR happens to exist.

### FC0-B — full foundation release

Add:

- FND-05 HA;
- FND-07 installation detach.

FC0-B releases:

- EliteGO;
- Installation UX;
- explicitly delegated downstream HA implementation consuming frozen HA contracts.

F0 is complete only at FC0-B.

## Prepared six-lane post-FC0A coordination

The implementation model after `FC0A_RELEASE_APPROVED` is prepared for six normal ChatGPT DEV chats:

- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05 DEV;
- FND-07 DEV.

Canonical prepared control:

`coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`

Execution model:

`DEV code -> Main review -> same DEV correction if material -> Main accepts for CODEX -> sequential CODEX focused/adversarial validation + exact-head T1 -> Main integration`

Feature lanes keep independent branches/PRs and converge at integrated T2 after controlled merges.

FND-05/FND-07 also use normal DEV implementers, but each remains an independent Foundation gate: Main contract review -> CODEX validation -> T1 -> merge -> post-merge validation -> VERIFIED/FROZEN.

CODEX is a scarce sequential validation resource, not the default implementation owner for these six lanes.

## Parallel DEV model after release

Every DEV mission uses one isolated branch and one PR to `wave15/corrections-integration` with an exact base SHA and explicit owned/forbidden boundaries.

A DEV may not:

- write directly to integration/main;
- merge its own PR;
- silently alter a frozen shared contract;
- borrow green CI from another SHA.

If a frozen contract is insufficient, report `BLOCKED-CONTRACT`; Main/Foundation owns the delta before downstream work resumes.

## CI roadmap — INFRA-CI-01

Target validation tiers:

- **T0** — local focused evidence;
- **T1** — DEV PR sanity/profile;
- **T2** — integrated broader validation;
- **T3** — exact integration checkpoint;
- **T4** — final complete-product validation.

Seven-Driver, browser, Licensing, HMI and other heavy suites run when risk/ownership/profile justifies them and at broader checkpoints. Red CI is diagnosed before rerun. Required but unexecuted validation is `PENDING`, never `PASS`.

A connector lacking a `workflow_dispatch` mutation is a tool limitation, not repository capability. Do not mutate workflows, create empty commits or retarget PRs merely to wake CI.

## Product contracts that guide downstream work

### Security Authority

Roles are editable templates/custom roles with explicit capabilities and stable scope hierarchy. Role names never grant privilege. Authority controls what an identity may do; backend enforces it. `CommandExecute` remains distinct from `ProcessValueWrite`.

### Licensing / Runtime Session Class

Authority permissions, Runtime Session Class and commercial license quotas are separate layers. Session Class can only restrict Authority. One logical runtime session owns one lease across transports/reconnect/failover; Web and EliteGO share server-owned quotas.

### EliteGO

Separate runtime-focused companion app consuming canonical Active through public APIs/realtime. It does not own HA election or an independent licensing authority.

### Editor

Use the canonical Runtime renderer with Engineering overlays. Design/Preview share renderer semantics; Working design never silently becomes Active Runtime truth.

### Installation switch

Application package, Authority backup, Historian/database and License remain separate authorities even when one UX coordinates a safe detach. Fence process effects, invalidate old sessions and never silently delete Historian or leak project-A identities into project B.

### WAN/timing

No global timeout inflation; GET retry bounded and safe; writes never blind-retry; timeout can be unknown outcome; stale responses cannot overwrite newer state.

## First-project fresh-install partial preview

After the four FC0-A feature lanes are integrated/T2-verified and FND-05/FND-07 are independently VERIFIED/FROZEN, Wave 15 will run a partial product audit before later complete-product/EEE acceptance.

Prepared control:

`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`

Prepared gates:

- `W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`
- `W15-FIRST-PROJECT-HUMAN-PREVIEW-01`

The two first-project journeys are intentionally independent.

Moment 1:
- CODEX uses a truly fresh installation as a user;
- no prebuilt project/EEE/hidden Demo state;
- no source/internal-control lookup during exploration;
- no product correction during the black-box journey;
- detailed findings remain embargoed from the Product Owner until the human journey completes.

Moment 2:
- Product Owner repeats the first-project journey as a real human on a separate clean environment;
- no CODEX-created project or detailed CODEX findings are supplied beforehand;
- becoming blocked without assistance is valid product evidence.

High-level mission for both:

`start from a fresh installation -> discover the product -> create the first SCADA application from zero -> reach a truthful functional Runtime`

Only after both exploratory journeys complete does Main unseal the CODEX report, compare both paths and optionally run a directed second round covering restart/persistence, Authority/ViewOnly, FND-07 detach/neutral bootstrap/A<->B switching and a separate user-surface HA manual transfer check.

T1/T2/T3/T4 green evidence does not substitute for these product audits.

The partial preview may return `CHANGES_REQUIRED` or `ACCEPTABLE_FOR_NEXT_CONVERGENCE`; the latter is not final Wave 15 acceptance. EEE v15, later complete-product validation and the final fresh Preview remain required.

## Final Wave 15 acceptance path

```text
Foundation slices frozen
  -> FC0-A checkpoint
  -> bounded parallel Editor/Script/Authority UX/Licensing UX
  -> remaining FND-05/FND-07
  -> FC0-B checkpoint
  -> first-project fresh-install partial preview
       -> CODEX black-box journey
       -> independent Product Owner human journey
       -> compare / correct material findings
  -> EliteGO + Installation UX + downstream HA
  -> product convergence / industrial visuals / help / localization / EEE v15
  -> exact integrated candidate
  -> T3/T4 validation
  -> fresh Codespaces Preview
  -> real Product Owner browser audit
  -> evidence-correlated residual corrections
  -> exact revalidation
  -> final Wave 15 acceptance
```

Wave13 #205/#207 remains paused until a separate Product Owner decision. Do not silently reinsert signed-release work into the active path.

## Permanent execution guards

- no direct `main` mutation;
- no destructive history operations;
- no blind rerun;
- no weakened tests/contracts to get green;
- no generic defect hidden behind EEE-only workaround;
- Runtime/Active remains independent of `.escadalib`;
- Alarm, Operational Event and Audit remain distinct;
- credentials/secrets stay outside plaintext Engineering/package/audit;
- stable IDs outrank display names/paths;
- clients do not directly own process truth, DB or Driver internals;
- exact SHA/tree and evidence are required at integration/freeze boundaries.


## FC0-A post-merge gate — INFRA-CI-01C

FC0-A consolidated PR #340 is merged at product SHA
`d975174ae81ff7ed754585097240778a9862d965`.

Pre-merge exact-head T1 `36043296814` is green with 54 Chromium owner tests.

Release is **not approved yet** because exact post-merge EliteSCADA CI `36044280802` exposed a generic PostgreSQL shared-schema initialization recurrence:
`23505 / pg_namespace_nspname_index`.

The affected infrastructure/failing-test blobs are unchanged from pre-FC0A frozen product base, so PR #340 product work remains accepted.

Blocking correction:
`INFRA-CI-01C-POSTGRES-SCHEMA-RECURRENCE-V1`
on `work/w15-infra-ci-01c-postgres-schema-recurrence`, exact base `d975174a...`.

After 01C merge, exact broad post-merge green CI and final affected post-FND06 audit recheck are required before `FC0A_RELEASE_APPROVED` and six-lane activation.


## FC0-A post-merge Help E2E load blocker (2026-09-24)

INFRA-CI-01C PR #341 merged at `da0e64122f1e4d0293e027f45ef95021cd03c1a1`
(tree `a724e565f11121a43b97bf0c59683b414c72f48c`).

Exact broad `EliteSCADA CI 36047274028 / #1567`:
- Web SUCCESS;
- Backend build/test SUCCESS;
- Runtime smoke SUCCESS;
- Chromium FAILURE before test execution.

The PostgreSQL recurrence is closed.

The remaining deterministic blocker is the FC0-A-added
`contextual-help-routing.spec.ts` importing the React shell `AppNavigation.tsx`;
its transitive CSS import reaches the Node-side Playwright loader and fails with
`src/auth/auth.css: Unexpected token (1:0)`.

Classification:
`FC0A_POSTMERGE_E2E_TEST_LOAD_DEFECT / PR340_TEST_CAUSAL / PRODUCT_BEHAVIOR_NOT_SHOWN_DEFECTIVE`.

Active closeout:
- control: `coord/w15-fnd06-control:docs/WAVE15-FC0A-POSTMERGE-HELP-E2E-LOAD-CONTROL.md`;
- order: `FC0A-POSTMERGE-HELP-E2E-LOAD-V1`;
- branch: `work/w15-fc0a-postmerge-help-e2e-load`;
- exact base: `da0e6412...`;
- CODEX route rev 0034 / `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-HELP-E2E-22`.

FC0-A remains NOT RELEASED. Do not activate the six prepared downstream lanes until Main obtains a globally green exact post-merge broad CI and records `FC0A_RELEASE_APPROVED`.


## FC0-A broad #1568 — stale Canvas source-contract blocker (2026-09-24)

Current exact release candidate:
`1ab3550e1afb258e38caaa6de3f6547f481bbef7`
(tree `701f4591a294885b414284691b1051cf274c9707`).

EliteSCADA CI `36049229264 / #1568`:
- Web SUCCESS;
- Backend build/test SUCCESS and Runtime smoke SUCCESS after the single repository-authorized same-SHA retry of the known IEC-104 T2 timing transient;
- Chromium full suite: 654 passed / 1 failed.

The only Chromium failure is `visual-editor-canvas-source-contract.spec.ts`, whose unchanged historical source assertion still requires `getBuiltinVisualObjectSchema`. The accepted FC0-A product intentionally consumes FND-06's `getVisualSchemaForEngineering` compatibility seam instead. Functional Canvas tests pass.

Classification:
`STALE_SOURCE_CONTRACT_TEST / PRODUCT_BEHAVIOR_NOT_DEFECTIVE`.

Active closeout:
- `FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-V1`;
- control `coord/w15-fnd06-control:docs/WAVE15-FC0A-POSTMERGE-CANVAS-SOURCE-CONTRACT-CONTROL.md`;
- branch `work/w15-fc0a-postmerge-canvas-source-contract`;
- CODEX route rev 0035 / `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CANVAS-CONTRACT-23`.

FC0-A remains NOT RELEASED and the six post-FC0A lanes remain WAIT until a later exact broad gate is globally green.


## FC0-A RELEASE APPROVED / six implementation lanes ACTIVE (2026-09-24)

Main completed the final post-FND06 release audit.

Product release checkpoint:
- SHA `e3ed5138369c576549cb58a7aff9783792f322d3`
- tree `4e7627774fbfc111344e3d80fcb9d921eed8377e`
- exact broad gate `EliteSCADA CI #1569 / 36060017969`: SUCCESS
- Web build: SUCCESS
- Backend build/test: SUCCESS
- Runtime smoke: SUCCESS
- Chromium full suite: **655 passed / 0 failed**

Final audit:
`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — ACCEPTABLE / FC0A_RELEASE_APPROVED`
rev 0014.

All six implementation branches were created directly from the exact product release SHA and independently revalidated as identical before coding:
- `work/w15-dev-editor-single-canvas`
- `work/w15-dev-script-engineering`
- `work/w15-dev-authority-ux`
- `work/w15-dev-licensing-ux`
- `work/w15-fnd-05-ha-authority`
- `work/w15-fnd-07-detach-neutral`

All six lanes are now `ACTIVE_CODING / AUTHORIZED`.

Control state:
- central parallel control rev 0002;
- four feature controls rev 0002;
- FND-05 control rev 0005 / order `FND05-DEV-HA-AUTHORITY-V1`;
- FND-07 control rev 0004 / order `FND07-DEV-DETACH-NEUTRAL-V1`.

Sequential CODEX is no longer executing FC0-A closeouts:
- route rev 0036;
- `ROUTE-SEQUENTIAL-CODEX-WAIT-POST-FC0A-24`;
- state `WAIT_FOR_MAIN_ACCEPTED_CANDIDATE`.

Workflow:
`normal DEV implements -> Main reviews -> same DEV corrects if needed -> Main accepts -> sequential CODEX validates/tests/T1 -> Main integrates`.

Foundations:
`normal FND DEV implements -> Main contract review -> sequential CODEX adversarial validation -> T1 -> Main integration -> post-merge -> VERIFIED/FROZEN`.

Product release SHA remains `e3ed5138...` even if `wave15/corrections-integration` advances afterward through coordination-only documentation commits.

Ledger: Issue #305 comment `5822605281`.

After all four feature lanes reach integrated T2 verification and FND-05/FND-07 are independently VERIFIED/FROZEN, proceed to the already-defined two-moment fresh-install first-project partial preview: CODEX black-box first, then Product Owner human journey on a separate reset environment.


## Post-FC0A six-lane Main review snapshot (2026-09-24)

FC0-A release base remains:
`e3ed5138369c576549cb58a7aff9783792f322d3`
(tree `4e7627774fbfc111344e3d80fcb9d921eed8377e`), broad CI #1569 / `36060017969` SUCCESS / Chromium 655 passed.

Current downstream lane dispositions after Main's first candidate review pass:

- Script Engineering PR #344 — `MAIN_ACCEPTED_FOR_CODEX / DEV_WAIT`, exact accepted head `cf0ae2d1...`; shared sequential CODEX route `ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25` is active.
- Editor PR #349 — `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`, exact head `06eed31d...`, T1 `36066874097` SUCCESS; queued behind active Script validation.
- Authority UX PR #346 — `DEV_CORRECTION` under `DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`; persisted role keys must remain stable/truthful against user assignments.
- Licensing UX PR #345 — `DEV_CORRECTION` under `DEV-LICENSING-UX-STATUS-ENTITLEMENTS-02`; canonical Licensing status must expose ESLIC1/ESLIC2 schema truth and signed ESLIC2 session/HA entitlements.
- FND-05 PR #347 — `DEV_CORRECTION` under `FND05-DEV-PEER-HANDOFF-BOUNDARY-V2-01`; internal HA state machine is accepted directionally, but two independent services need a transport-neutral readiness/authority/lease handoff boundary before CODEX.
- FND-07 PR #348 — `DEV_CORRECTION` under `FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`; old local-auth E2E incorrectly depended on process Demo state, while Wave 15 requires clean no-Demo first-project/neutral bootstrap truth.

No feature/Foundation candidate is merged yet.

The integration target's post-release product baseline remains unchanged by these reviews; coordination/documentation advances do not authorize lane self-rebase or self-merge.

Central board:
`coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`, rev 0003.

The prepared two-stage first-project fresh-install preview remains downstream of four feature integrations/T2 plus FND-05/FND-07 VERIFIED/FROZEN.


### Live coordinator transfer gate — 2026-09-25

FND-07 remains the current Foundation closeout blocker. PR #348 is at `3ecc4a78080685b0556402d50190e09236d6d8fa`; T1 `36094394912` is green except focused Chromium. Legacy Demo/fallback test assumptions were removed and persisted activation is now exercised directly. Publish succeeds; activation returns HTTP 422 and must be diagnosed before merge/freeze.

Shared CODEX is paused/unavailable. No historical route may auto-resume; the replacement Main must publish a new explicit route after live revalidation and 422 diagnosis.
