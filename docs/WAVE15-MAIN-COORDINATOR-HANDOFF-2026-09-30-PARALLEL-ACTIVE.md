# LIVE TAKEOVER DELTA — 2026-09-30 — MAIN REVALIDATION AFTER PARALLEL START

> This section supersedes the transfer-time "ahead 0 / no PR" snapshot below. GitHub live remains the sole authority.

## Integrated authority

- `wave15/corrections-integration@ed04cf7358ca00b34593970ea3c1ba1931b01014` remains the common base.
- EliteSCADA CI #1618 / `36716809347` remains **GLOBAL SUCCESS**.
- No new product merge has been accepted by Main during this takeover.

## Live parallel state observed by new Main

All branches below still descend directly from `ed04cf7358ca00b34593970ea3c1ba1931b01014` with behind 0 at takeover revalidation.

- P / #383 / PR #429: exact head `5cf703fa757ef02a2219115efdd59d6a422deef2`, 4 commits ahead; T1 #256 / `36730432841` IN_PROGRESS at checkpoint.
- HA-D1 / #421 / PR #427: exact head `ab6a7ab7f1d44f91de4c4f404fdabb0e248861db`, 2 commits ahead; T1 #250 / `36729567908` IN_PROGRESS at checkpoint.
- Installation UX / #422 / PR #428: branch advanced during takeover; latest revalidated head before this documentation update was `c7ac99a5ef4b5f79ab128273d69ef8c1587cb4ab`, 10 commits ahead. Revalidate exact PR head and T1 before any disposition because the DEV is still active.
- Visual Quality / #308: branch advanced to `9dd147bcd9ec4fe197e4cbfb77c104cdffe5488a`, 8 commits ahead at checkpoint; no open PR was present in the last PR scan.
- M1 Multilingual / #379 / PR #426: exact head `f631b8b993189eeb730770c9af7bb6285afef8c5`, 20 commits ahead; T1 #249 / `36729542797` IN_PROGRESS at checkpoint.
- Contextual Help / #424: branch advanced to `8c768d2ef304a90bb3d238289d5944a8cb5ac94e`, 1 commit ahead at checkpoint; no open PR was present in the last PR scan.
- Complete Manual / #425 / PR #430: exact head `e34381562e226ee1a5efb2aa257aa6c9f1ac5005`, 1 commit ahead; T1 #254 / `36730051304` FAILURE at checkpoint.

## Main audit dispositions

### M1 / PR #426 — CHANGES_REQUIRED / NO_MERGE

Main source review on exact head `f631b8b993189eeb730770c9af7bb6285afef8c5` found cross-locale defects in `web/scada-web/src/engineering/c04I18n.ts`:
- English resource used Spanish `Fuente de datos`;
- English OPC UA preview-result used Spanish `Vista previa`;
- Spanish resource used English `Data Source`;
- Spanish OPC UA preview-result used English `Preview`.

The current glossary regression did not exercise these C04 entries.

Correction order is durable on PR #426 comment `5913475114`:
- restore locale-native pt-BR/en/es values;
- add deterministic coverage for the affected C04 resources;
- preserve backend/API/stable identifiers and lane boundaries;
- return a new exact head/tree, exact-head T1 and representative browser language-switch evidence.

A green T1 on the reviewed defective head is not acceptance.

### HA-D1 / PR #427 — EVIDENCE_GATE / NO_MERGE

Main review found relevant bounded source/test coverage for:
- HMAC peer authentication/replay rejection;
- standby Authority-instance restart without authority grant;
- Runtime Session Lease tombstones/resurrection prevention;
- durable mirror reload fail-closed until live resynchronization.

However #421 acceptance explicitly requires dedicated **two independent EliteSCADA server process** evidence over the real peer transport. The reviewed PR tests are in-process. Exact-head T1 and dedicated two-process evidence remain mandatory before Main acceptance.

### Installation UX / PR #428 — REVIEW_ACTIVE / NO_MERGE

The branch consumes the frozen FND-07 detach/preflight authority rather than redesigning backend detach. Main observed bounded UI work for:
- current-state review;
- unsaved Working warning;
- separate Application and Authority backup/export guidance;
- explicit consequences;
- license Keep/Remove/Replace choice;
- neutral transition and Historian-preservation messaging.

Acceptance still requires revalidation of the moving exact head, exact-head T1, and the mounted A -> B -> A regression required by #422. Do not merge on mergeable=true alone.

### Manual / PR #430 — T1_RED / NO_MERGE

T1 #254 / `36730051304` is FAILURE at this checkpoint. Main must inspect the causal failure before acceptance or rerun; no blind rerun.

## Downstream locks retained

- Historical Playback remains blocked by #383/query/time-range acceptance.
- HA-D2 / #423 remains blocked by HA-D1.
- EEE Simulation + real Modbus remain deliberately late immediately before final #300 Preview.
- Shared CODEX remains PARKED / PRESERVE_REMAINING_CAPACITY. Do not send SIGA merely because DEVs are active.
- No additional broad product-code concurrency is released while these first handoffs/PRs are being audited.

## Documentation authority note

The 2026-09-27 `CURRENT-COORDINATOR-HANDOFF.md`, `LAST CHANGE.md` and older `WAVE15-MAIN-COORDINATOR-HANDOFF.md` on the product integration branch are historical snapshots, not current execution authority. Current coordination state lives on this control branch and in #305/#378 plus owning issues/PRs. Do not mutate the product integration baseline merely to copy coordination-only status.

---

# EliteSCADA Wave 15 — MAIN COORDINATOR HANDOFF — 2026-09-30 — PARALLEL ACTIVE

**Repository:** `brunolrogerio-collab/EliteSCADA`

**Fundamental authority:** GitHub live is the sole authority. Revalidate live before every merge, rerun, branch release, contract change, CODEX mission or architecture decision.

This file is the canonical successor takeover snapshot created at the end of the 2026-09-30 Main Coordinator chat. It supersedes older current-state wording when there is a conflict.

## 1. Exact integrated product checkpoint

Current integration:
- branch: `wave15/corrections-integration`;
- exact HEAD: `ed04cf7358ca00b34593970ea3c1ba1931b01014`;
- exact tree: `b94c329ebf2eba1665d4120cd27bf8b841766911`.

Broad validation:
- EliteSCADA CI #1618;
- run: `36716809347`;
- exact head: `ed04cf7358ca00b34593970ea3c1ba1931b01014`;
- conclusion: **GLOBAL SUCCESS**;
- Web build: SUCCESS;
- Backend build/test/smoke: SUCCESS;
- Chromium end-to-end: SUCCESS.

Integrated immediately before this checkpoint:
- Script Authoring R2 PR #419 -> merge `bbb7513fef50bee847ec5cab86e4f8b24f91985b`;
- TAG-D PR #420 -> merge `ed04cf7358ca00b34593970ea3c1ba1931b01014`.

Script Authoring accepted exact candidate before merge:
- `b84a421b05f015d0009a218f4278ce1fa778fc55`;
- T1 #242 / `36711552620`: SUCCESS.

TAG-D accepted exact candidate before merge:
- `a671e65467eef7eb84a78d4b13125d0b375c44a4`;
- T1 #245 / `36714380148`: SUCCESS.

## 2. Binding Product Owner scope correction

The following is mandatory:

- **EliteGO as a separate application/client is DEFERRED INDEFINITELY.**
- EliteGO is not a Wave 15 or Wave 16 exit gate.
- **Redundancy / HA remains mandatory Wave 15 scope.**
- HA is not an EliteGO-only feature. Current Web Runtime/operations and future Runtime clients consume the same server-authoritative HA contracts.
- Wave 15 must finish all reusable **EliteSCADA-side foundations required by a future EliteGO**, including public Runtime projection/rendering, backend Authority/effective capabilities, client-neutral Runtime Session Lease/licensing, reconnect-safe logical identity, HA topology/effective-Active discovery, truthful stale/reconnect/freshness semantics, topology-neutral package boundaries and no direct client Driver/database/process authority.
- Already integrated work must not be redeveloped:
  - Authority UX #346;
  - Licensing UX + License Generator v2 #345;
  - FND-05 HA authority/manual-transfer #347;
  - FND-07 detach/neutral-bootstrap #348 plus closeouts #352/#353.
- Original Wave 15 work no longer waits behind a blanket correction-phase barrier. Disjoint work may run in parallel from an exact green common base.
- **EEE Simulation instructional project + EEE real-Modbus variant remain deliberately late**, finalized immediately before the last complete-product Preview so they exercise the final accepted product.

Canonical scope issues:
- #297 Wave 15 complete EliteSCADA delivery;
- #298 EliteGO deferred indefinitely;
- #299 HA/Redundancy;
- #300 final integration/Preview;
- #305 execution DAG;
- #306 productization.

## 3. Current active common-base batch

All lanes below were created from exact common base:
`ed04cf7358ca00b34593970ea3c1ba1931b01014`

At this handoff, fresh live revalidation shows **all seven branches are still identical / ahead 0 / behind 0 and have no open PR**. Therefore no DEV bytes from this batch have been written yet.

### P — Historical Time Range / #383

State:
`ACTIVE / R2_C_HISTORICAL_TIME_RANGE / NO_MERGE`

Branch:
`work/w15-r2-historical-time-range`

Order:
`R2C-P-HISTORICAL-TIME-RANGE-01`

Canonical release comment:
#383 / `5912107649`.

Owns:
- shared Live / Historical Relative / Historical Absolute range semantics;
- Basic Trend + canonical multi-pen Trend + Historical Browser convergence;
- local-display -> UTC query authority;
- Runtime/session range state that does not dirty Engineering;
- bounded query behavior through existing Data Query authority.

Does **not** own Historical Playback.

Bootstrap delivered to Product Owner.

### HA-D1 / #421

State:
`ACTIVE / W15_HA_D1_PEER_REPLICATION / NO_MERGE`

Branch:
`work/w15-ha-d1-peer-replication`

Order:
`W15-HA-D1-PEER-REPLICATION-01`

Canonical release comment:
#421 / `5912108445`.

Consumes integrated FND-05 #347 and owns:
- authenticated real peer transport for the frozen envelopes;
- ReadyStandby convergence;
- truthful Runtime/TAG mirror;
- minimum durable revision/state replication;
- Runtime Session Lease continuity replication;
- reconnect/resync;
- two-process evidence.

Automatic failover/election is explicitly out of scope and belongs to #423 after HA-D1.

Bootstrap delivered to Product Owner.

### Installation UX / #422

State:
`ACTIVE / W15_INSTALLATION_UX / NO_MERGE`

Branch:
`work/w15-installation-ux`

Order:
`W15-INSTALLATION-UX-01`

Canonical release comment:
#422 / `5912109147`.

Consumes AUTH-04 / FND-03 / FND-07 and owns:
- current installation/project state summary;
- dirty Working warning;
- separate Application + Authority export recommendation;
- consequence confirmation;
- canonical detach progress/failure UX;
- neutral Create/Import/Restore;
- license keep/remove/replace UX using frozen lifecycle;
- A -> B -> A mounted regression;
- truthful Historian retention messaging.

Bootstrap delivered to Product Owner.

### Visual Quality / #308

State:
`ACTIVE / W15_VISUAL_QUALITY / NO_MERGE`

Branch:
`work/w15-visual-quality`

Order:
`W15-VISUAL-QUALITY-01`

Canonical release comment:
#308 / `5912106659`.

Owns:
- coherent professional original industrial symbol quality;
- representative valve/motor/pump/blower/tank/instrument/electrical/status objects;
- Library/Dynamo actual visual preview before insertion;
- truthful metadata/parameters/dependencies/provenance where available;
- contained malformed/legacy preview failure;
- preview -> explicit insertion identity/version consistency.

Does not own #385 portability/update lifecycle or EEE-specific product objects.

Bootstrap delivered to Product Owner.

## 4. Language / Help / Manual tracks

These were split out of #306 so Productization no longer hides the implementation work.

### M1 Multilingual / #379

State:
`ACTIVE_PHASE1 / NO_MERGE`

Branch:
`work/w15-multilingual-copy`

Order:
`W15-M1-MULTILINGUAL-01`

Canonical release comment:
#379 / `5912090775`.

Phase 1:
- freeze/use C-USER-COPY-I18N-01 glossary;
- inventory shared/component-local strings;
- remove internal coordination/implementation-brand leakage from stable surfaces;
- centralize common terminology;
- pt-BR/en/es resource parity;
- stable-surface accessibility/tooltips/errors.

Phase 2:
- final sweep after HA/Historical/Installation/new surfaces stabilize.

**Bootstrap was NOT yet delivered to the Product Owner at this coordinator transfer.**

### Contextual Help / #424

State:
`ACTIVE_PHASE1 / NO_MERGE`

Branch:
`work/w15-help-contextual`

Order:
`W15-HELP-PHASE1-01`

Canonical release comment:
#424 / `5912091420`.

Owns:
- existing installed/contextual Help architecture;
- topic inventory and coverage;
- task-oriented guidance;
- troubleshooting;
- contextual links;
- pt-BR/en/es semantic parity;
- broken-link/topic-id/coverage checks.

Final HA/Historical/Installation topics follow those surfaces.

### Complete Manual / #425

State:
`ACTIVE_PHASE1 / DOCS-FOCUSED / NO_MERGE`

Branch:
`work/w15-manual-complete`

Order:
`W15-MANUAL-PHASE1-01`

Canonical release comment:
#425 / `5912092324`.

Owns the complete long-form EliteSCADA manual:
- user;
- engineering;
- administration;
- operations;
- troubleshooting;
- HA;
- Historian/Trend/Playback;
- Security/Authority;
- Licensing;
- Installation switching;
- Script Engineering;
- Library/Dynamos;
- backup/import/export/restore;
- public APIs/identifiers where appropriate.

Phase 1 writes stable chapters and full TOC now.
Final pass adds remaining feature chapters, final screenshots and EEE worked examples immediately before final Preview.

## 5. Downstream blocked/ordered work

### Historical Playback

Do not release until #383 P is accepted enough that Query + Time Range semantics are stable.

Playback must consume:
- Data Query foundation;
- Historical Time Range;
- Visual identity;
- Authority;
- Runtime mutation fencing.

Playback is large/risk-sensitive and should remain one primary vertical DEV rather than fragmented Runtime ownership.

### HA-D2 / #423

State:
`BLOCKED_BY_HA_D1 / NO_BOOTSTRAP`

Owns later:
- conservative automatic failover/failback;
- split-brain/ambiguous fail-closed behavior;
- stale epoch/fencing adversarial proof;
- no-duplicate Driver/write/Script/Alarm/Historian/Event effects matrix.

No automatic promotion merely because peer communication is lost.

### M1 Phase 2

Run after new product surfaces stabilize.

### EEE Simulation + EEE real Modbus

Remain deliberately late under #306:
- final Simulation instructional project;
- final real Modbus variant from authoritative mapping;
- final screenshots/walkthroughs;
- immediately before #300 final Preview.

## 6. Productization / Preview

#306 is now the acceptance aggregator, not the only implementation lane.

Consumes:
- #379 Multilingual;
- #424 Help;
- #425 Manual;
- #308 Visual Quality;
- final EEE Simulation;
- final EEE real Modbus;
- exact product metadata/docs;
- exact validation.

#300 remains the final complete-product Preview gate.

EliteGO executable/UI must not be reintroduced as a #300 entry gate.

HA **is** a #300 entry gate.

## 7. Shared CODEX

Shared CODEX remains:
`PARKED / PRESERVE_REMAINING_CAPACITY / NO_PRODUCT_MUTATION / NO_MERGE`

Do not send SIGA merely because DEVs are active.

Use CODEX capacity only when:
- a high-risk architecture/audit step genuinely requires it;
- final black-box Preview gate is reached;
- Main records a concrete mission.

## 8. Wave 16

Wave 16 Windows native/runtime installer architecture is documented, including certificate/signing direction, DB/runtime dependencies and packaging strategy.

It is **not current execution**.

Do not start Wave 16 product implementation until Wave 15 acceptance/transition is explicitly authorized.

Current product identity:
`EliteSCADA Alpha 0.15.2.1`

Approximate version convention:
`0.WAVE.REVISION.DELIVERY`.

## 9. Mandatory Main Coordinator behavior

The Product Owner expects every coordination response to end with exactly one clear table:

| Chat/lane | Status atual | Sua ação |

Include every relevant parallel DEV plus CODEX and state explicitly whether Product Owner should:
- Enviar SIGA;
- Aguardar;
- Não mexer;
- Ainda não abrir.

When the Product Owner says only `SIGA`:
1. revalidate GitHub live;
2. execute the next safe already-authorized action;
3. do not merely describe what could be done.

The Product Owner is not a messenger between agents. Put orders/handoffs in GitHub.

No merge merely because `mergeable=true`.
Audit exact base/head/diff/contracts/tests/T1 first.

## 10. Immediate successor actions

1. Read this file first.
2. Read:
   - `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
   - `docs/WAVE15-POST-E3-DEVELOPMENT-ROUTE.md`;
   - `docs/CURRENT-COORDINATOR-HANDOFF.md`;
   - `LAST CHANGE.md`;
   - latest #305 and #378 comments.
3. Reconstruct GitHub live independently.
4. Check whether any of #383/#421/#422/#308/#379/#424/#425 branches have advanced after this snapshot.
5. Audit any first DEV handoff rather than releasing more product-code concurrency blindly.
6. Keep #423 and Historical Playback blocked until their upstream dependencies are proven.
7. Preserve late EEE finalization and final Preview sequence.

GitHub live always overrides this snapshot.
