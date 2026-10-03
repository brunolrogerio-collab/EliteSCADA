# FINAL REVALIDATION CORRECTION — 2026-10-03 — ACTIVE BRANCHES MOVED DURING HANDOFF

> This correction supersedes the active-lane status inside the 2026-10-03 rotation text below.
> GitHub live remains the only authority.

Current integration after the first documentation-only handoff merge:
`e606d3f5dd50617fb5948372f909a5553dd97c36`

Important: the active implementation/content branches were released from:
`aca641501302edb04f32b83b04adf0726d7e8a96`

The seven commits by which they are now behind current integration are coordination/documentation-only. Do NOT require a rebase merely to consume those handoff docs.

Final outgoing revalidation:

## #482 Visual Library Catalog
- branch: `work/w15-visual-library-catalog`
- relative to release base `aca64150...`: `ahead 0 / behind 0`
- relative to current integration `e606d3f5...`: `ahead 0 / behind 7`
- no implementation commit
- no PR

## #483 HOME/BUILDING H0
The branch moved while the handoff was being written.

- branch: `work/w15-home-building-h0`
- exact HEAD: `9b2cea1293e8bdd0ab250a28446bd53cee8d8697`
- exact tree: `8fbd95cfeed9e95ca296957e1d5855d2eab78eb8`
- commit: `fix(w15-h0): make tag datasource identity canonical`
- changed path in that commit: `web/scada-web/src/engineering/types.ts`
- relative to release base `aca64150...`: `ahead 1 / behind 0`
- relative to current integration `e606d3f5...`: `ahead 1 / behind 7`
- no PR
- no formal DEV handoff comment yet

Therefore #483 is ACTIVE/WIP. Do not treat the one published commit as an H0 handoff or as Main-accepted.

## #484 Asset Curation Batch 1
- branch: `content/w15-builtin-asset-curation-b1`
- exact HEAD: `055deb4df30eaf3e8af4d33fdc8739cf3d1b9df4`
- exact tree: `bca34cc2ea3b12124db703244ed715ebced9e0f9`
- commit: `content(assets): add Wave 15 Batch 1 SVG curation`
- relative to release base `aca64150...`: `ahead 1 / behind 0`
- relative to current integration `e606d3f5...`: `ahead 1 / behind 7`
- 88 changed files
- current batch manifest contains 86 draft assets
- no PR
- no formal DEV handoff yet

#482/#483/#484 remain the complete active batch.
CODEX remains PARKED.
Protocol fan-out remains BLOCKED.

---

# Wave 15 Main Coordinator Handoff — 2026-10-03 — NEXT BATCH ACTIVE

> GitHub live is the sole authority. Revalidate every exact SHA/branch/PR/CI state before acting.
> This document is a coordination snapshot. If GitHub live differs, GitHub wins.

## 1. Product authority

Accepted Batch-3 product checkpoint:

`9b596286a40b1649564075626e817cc61e32e7bc`

This checkpoint integrated:
- Historical Playback #470;
- Modbus Family / Host Serial #476/#469;
- Asset Factory #474.

Exact combined T1:
- Wave 15 T1 #619 / run `37092144575`: SUCCESS;
- classify: SUCCESS;
- common sanity: SUCCESS;
- Web: SUCCESS;
- focused .NET: SUCCESS;
- focused Chromium: SUCCESS;
- final T1 gate: SUCCESS.

Exact post-merge slim evidence on the product checkpoint:
- Web build: SUCCESS;
- Backend build, test and smoke: SUCCESS;
- Chromium integration gate: SUCCESS.

Asset Factory exact-head evidence accepted:
- 26/26 tests PASS;
- full build exit 0;
- catalog check valid;
- 10 seed assets;
- 90 taxonomy categories;
- no duplicates.

Roadmap consolidation was merged after the product checkpoint as documentation-only.

Release base of the currently active next batch:

`wave15/corrections-integration@aca641501302edb04f32b83b04adf0726d7e8a96`

Important distinction:
- accepted product bytes checkpoint: `9b596286...`;
- `aca64150...` adds the consolidated Wave 15 roadmap only.

This handoff itself is documentation-only and will move integration again if merged. Do not confuse that later docs SHA with a new product checkpoint.

## 2. Canonical remaining Wave 15 plan

Canonical detailed sequencing:
- #305 comment `5964985657`;
- `docs/ROADMAP.md`.

Remaining product route:

1. Visual Library / Asset product foundation
   - Library Catalog V1;
   - SVG-first authoring / preferred `core.svgSymbol` after V1;
   - large curated built-in asset catalog;
   - SVG-backed Dynamo composition;
   - final #308 visual acceptance.

2. HOME/BUILDING common foundation
   - H0 canonical identity/Equipment/driver-catalog convergence;
   - H1 Location topology;
   - H2 Device/Capability;
   - H3 Host Resource generalization;
   - H4 LocalBridge lifecycle;
   - H5 Integrations/Locations/Devices UX;
   - H6 composite discovery + separate privileged external-network mutation.

3. Protocol drivers only after minimum common contracts freeze
   - Shelly RPC;
   - ESPHome Native;
   - Home Assistant Bridge;
   - Zigbee2MQTT Bridge;
   - Native Zigbee;
   - DALI gateway first;
   - KNX/IP;
   - Z-Wave JS;
   - Matter when maturity/certification gate is acceptable;
   - BTHome after Bluetooth Host Resource.

4. Final coherence
   - #379 final pt-BR/en/es sweep;
   - #424 final Help;
   - #425 final Manual;
   - #308 visual-quality acceptance.

5. Productization / Preview
   - #306 EEE Simulation + real Modbus + final packaging/provenance/Help/manual/i18n;
   - #300 final fresh Preview and Product Owner audit.

EliteGO remains deferred after Wave 16 and is not a Wave 15 exit gate.

## 3. Current bounded batch

Main released exactly three parallel lanes from `aca641501302edb04f32b83b04adf0726d7e8a96`.

### #482 — Visual Library Catalog V1

Issue:
`#482 — W15-VISUAL-LIBRARY-CATALOG — scalable built-in/user/associated resource browser`

Branch:
`work/w15-visual-library-catalog`

Contract:
`C-VISUAL-LIBRARY-CATALOG-01`

Order:
`W15-VISUAL-LIBRARY-CATALOG-01`

At outgoing revalidation:
- `ahead 0 / behind 0` against release base;
- no changed files;
- no PR.

State:
`ACTIVE / V1_LIBRARY_CATALOG / NO_MERGE`

Owned scope:
- Built-in / Project-User / Associated Library source separation;
- hierarchical categories;
- resource-kind filters;
- tags/search;
- preview-before-insert;
- scalable browsing;
- consume #474 taxonomy/catalog;
- preserve `.escadalib` Associate != Import.

Forbidden:
- `core.svgSymbol`;
- renderer/property-schema;
- SVG paint slots/animation;
- HOME/BUILDING contracts;
- Dynamo artwork;
- rejected PR #451 bytes.

### #483 — HOME/BUILDING H0

Issue:
`#483 — W15-HOME-BUILDING-H0 — canonical identity, Equipment and driver-catalog convergence`

Branch:
`work/w15-home-building-h0`

Contract:
`C-HOME-BUILDING-H0-01`

Order:
`W15-HOME-BUILDING-H0-P0-01`

At outgoing revalidation:
- `ahead 0 / behind 0` against release base;
- no changed files;
- no PR.

State:
`ACTIVE / H0_P0_CONVERGENCE / NO_PROTOCOL_DRIVER / NO_MERGE`

Owned H0 scope:
- canonical frontend TAG `DataSourceId + CommunicationBinding`;
- Memory ownership by stable DataSourceId with safe legacy fallback;
- Equipment creatable without mandatory Template/Faceplate;
- smallest coherent Driver product registration convergence;
- Integration Catalog metadata such as IntegrationDomain, ConnectionModel, external dependency/host requirement.

Explicitly deferred:
- Location hierarchy;
- Device/Capability;
- Host Resource generalization beyond current description needs;
- LocalBridge;
- protected-material provisioning UI/store;
- transient event runtime;
- Integrations/Locations/Devices UX;
- discovery materialization;
- every HOME/BUILDING protocol driver.

Research input:
- #475 accepted by Main as docs-only architecture input;
- do not merge research branch automatically;
- use actual integrated #469 Host Serial as implementation evidence.

### #484 — Built-in Asset Curation Batch 1

Issue:
`#484 — W15-ASSET-CURATION-B1 — built-in SVG content expansion and license curation`

Branch:
`content/w15-builtin-asset-curation-b1`

Contract:
`C-BUILTIN-ASSET-CURATION-01`

Order:
`W15-BUILTIN-ASSET-CURATION-BATCH1-01`

At outgoing revalidation this lane has STARTED.

Exact observed branch head:
`055deb4df30eaf3e8af4d33fdc8739cf3d1b9df4`

Exact tree:
`bca34cc2ea3b12124db703244ed715ebced9e0f9`

Commit:
`content(assets): add Wave 15 Batch 1 SVG curation`

Ancestry:
`ahead 1 / behind 0`

Observed delta:
- 88 changed files;
- content under `assets/sources/batch1/**`;
- batch manifest contains 86 assets;
- all sampled manifest entries are `draft`;
- provenance is declared `generated/original`;
- derivation note explicitly says no vendor/third-party/rejected #451 artwork copied.

No PR was open at outgoing revalidation.
No DEV handoff comment had been posted yet.
Therefore this is WORK IN PROGRESS, not Main-reviewed/accepted.

Do not merge or declare this batch accepted until the #484 DEV posts the required handoff and Main verifies:
- exact head/tree;
- category counts;
- provenance/license matrix;
- no rejected #451 reuse;
- Asset Factory test/build/check;
- duplicate report;
- visual-review limitations.

## 4. Concurrency boundaries

Current active batch is intentionally:
- 2 product DEVs (#482, #483);
- 1 content lane (#484).

Do not open more product lanes merely because capacity exists.

Still blocked:
- V2 SVG-first / `core.svgSymbol` waits the #482 disposition;
- V4 SVG-backed Dynamos waits V2;
- HOME/BUILDING H1-H6 waits H0 disposition;
- protocol driver fan-out remains blocked;
- no Zigbee/Z2M/DALI/Home Assistant/ESPHome/Shelly/Z-Wave/Matter DEV yet.

Before adding another lane:
1. revalidate all active branches;
2. audit shared-path/contract overlap;
3. prefer integrating/fixing current batch first.

## 5. Visual quality rule

#308 remains a final Wave 15 visual-quality gate.

Binding Product Owner decision:
- PR #451 is REJECTED;
- branch `work/w15-dynamo-artwork-r2` is CANCELLED;
- do not cherry-pick or reuse artwork, SVG, geometry, commits or design from that lane.

Future visual route:
`#474 Asset Factory -> #482 Library Catalog -> SVG-first -> curated assets -> SVG-backed Dynamos -> #308 acceptance`.

## 6. HOME/BUILDING architecture rules

Preserve one canonical Runtime spine:

`Driver/Data Source -> TAG/Command/Equipment -> Cache/Event -> Historian/Alarm/Gateway/Scripts/Realtime`

Friendly future UX is a projection:

`Location -> Equipment(Device) -> Capability -> TAG/Command`

No second residential Runtime/TAG/Historian/Alarm/Gateway/automation core.

Stable IDs outrank friendly key/path.
Clients do not directly own Driver/process truth.
Normal writes converge through canonical Runtime/TAG ownership.

Native Zigbee and Zigbee2MQTT are distinct integrations.
DALI gateway should precede native DALI.
Z-Wave JS should precede native Z-Wave.
Matter remains maturity/certification gated.

## 7. Final Wave 15 owners that must not be forgotten

### #379 — I18N
Phase 1 integrated.
Keep issue open for final cross-product pt-BR/en/es sweep after new surfaces stabilize.
Do not redo Phase 1.

### #424 — Help
Phase 1 exists and must not be restarted.
Final pass must cover the final product, including:
- Playback;
- Modbus RTU/TCP Server;
- Library/SVG;
- HOME/BUILDING;
- selected new drivers.

### #425 — Manual
Phase 1 architecture/stable chapters integrated.
Final pass still needs:
- new visual/library workflow;
- HOME/BUILDING;
- new drivers;
- final screenshots;
- EEE Simulation/real-Modbus walkthroughs.

### #306 — Productization
Final PREVIEW-READY aggregator:
- #308 visual acceptance;
- #379 final i18n;
- #424 final Help;
- #425 final Manual;
- EEE Simulation v15;
- EEE real-Modbus v15;
- checksums/provenance/package readiness;
- exact integrated validation.

### #300 — Final fresh Preview
Only after #306 PREVIEW-READY.
Fresh exact candidate, no stale project/demo/runtime state.
Product Owner human audit remains authoritative.

## 8. CI operating rule

Do not watch long CI continuously.

Use:

`LAUNCH -> OBSERVE MAX ~2 MIN -> STOP -> REVALIDATE FINAL RESULT LATER`

If red:
- diagnose;
- do not blind rerun;
- do not weaken tests/contracts to obtain green.

A required test that did not execute is PENDING, never PASS.

## 9. CODEX

CODEX is PARKED for the current batch.

Do not spend CODEX on routine implementation.

Reserve it for:
- difficult integration/adversarial validation;
- hard blockers;
- final Preview;
- visual generation/review when materially better than normal DEV.

## 10. Product Owner / chat coordination rule

Do not use the Product Owner as messenger between parallel DEV chats.

Main should read GitHub handoffs directly and write Main decisions/orders to the owning issue.

The Product Owner expects Main responses to clearly state:
- status of every active DEV/lane;
- CODEX status;
- exactly what action, if any, the Product Owner must take in each chat.

## 11. Immediate successor actions

On takeover:

1. Revalidate GitHub live; do not trust the SHAs in this file blindly.
2. Read:
   - this handoff;
   - `docs/ROADMAP.md`;
   - `docs/CURRENT-COORDINATOR-HANDOFF.md`;
   - latest #305 comments;
   - #482/#483/#484 latest comments/branches.
3. Confirm integration current HEAD and distinguish docs-only advancement from product bytes.
4. Revalidate:
   - #482 branch/PR;
   - #483 branch/PR;
   - #484 branch/PR/head.
5. Do NOT open SVG V2 or any protocol driver yet.
6. When a DEV hands off:
   - inspect exact diff and ownership;
   - verify required focused evidence/T1;
   - return to same DEV for material corrections;
   - only then integrate/converge.
7. Keep #451 permanently excluded.
8. Keep final #379/#424/#425/#306/#300 route visible.

## 12. Current user action matrix at handoff

- #482 Visual Library Catalog: chat should execute current order; Product Owner only needs to wait for handoff.
- #483 HOME/BUILDING H0: chat should execute current order; Product Owner only needs to wait for handoff.
- #484 Asset Curation: work is already published on branch but handoff not yet posted; Product Owner only needs to wait for the DEV to finish/hand off.
- CODEX: no action.
- Main Coordinator: revalidate GitHub and own integration/review.

State:

`NEXT_BATCH_ACTIVE / 2_PRODUCT_DEVS + 1_CONTENT_LANE / PROTOCOL_FANOUT_BLOCKED / CODEX_PARKED`
