# CURRENT W15 RELEASE — EXTERNAL ZIGBEE2MQTT V1 — 2026-10-08 (BRT)

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
- Scope: separately user-operated Z2M over existing MQTT transport; sanitized bridge/device inventory; selected canonical Equipment/TAG/Capability discovery; bounded stateful Boolean/Float64 mapping; availability/reconnect/report/readback truth; PointRead/#500/#560.
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

# CURRENT MAIN COORDINATOR HANDOFF — 2026-10-08

> GitHub live is the only authority. Revalidate branch, SHA, issue/PR comments and exact-head Actions on every wake.
> Current execution plan: [#305 comment 6064847156](https://github.com/brunolrogerio-collab/EliteSCADA/issues/305#issuecomment-6064847156). Board: [CHAT-WORK-ASSIGNMENTS.md](CHAT-WORK-ASSIGNMENTS.md). Historical sections below are not current assignments.

## Current integration / completed lanes

- Integration: `wave15/corrections-integration@cfd4ea9a718c3aede93a53606b3e5fd178fae2e1`.
- S4 #565 / PR #568 CLOSED / MERGED at `d38d5825e546aa340a3bd3ab6b7a6e7a7b1d816b`; exact-head SCRIPT_ENGINEERING T1 #964 and Main re-audit accepted. Chat WAIT.
- Mitsubishi #566 / PR #567 MERGED at current integration SHA; exact-head DRIVER_PROTOCOL T1 #955 accepted, L2 SKIP_WITH_REASON accepted for checkpoint. #566 stays open for deferred L4; chat WAIT.
- #543 / PR #548 managed sidecar + typed Host Resource is merged. S0-S3 and S4 Event/Command foundations are integrated; do not reimplement old research deltas.
- Docs PR #569 is OPEN / DRAFT / NO_MERGE_AUTHORIZATION; revalidate its latest head/T1 before disposition.
- Wave 15 is not closed: #306/#300/#379/#424/#425 remain active closure trackers.

## Current ACTIVE parallel lanes

| Chat / issue | Branch / assigned base | Next authorized action |
| --- | --- | --- |
| DEV-INDUSTRIAL-PANASONIC-MEWTOCOL-V1 / #570 | `work/industrial-panasonic-mewtocol-driver-v1` from `cfd4ea9a718c3aede93a53606b3e5fd178fae2e1` | MEWTOCOL-COM TCP + Host Serial; focused intermediate evidence -> publish -> DRIVER_PROTOCOL exact-head T1 -> Main audit. |
| DEV-DRIVER-INTERACTION-S5-HMI / #571 | `work/driver-interaction-s5-hmi-rich-command` from the same base | Read the resolved contract, implement the HMI caller, focused Core/API/browser tests -> publish -> normal exact-head T1 -> Main audit. |

Both branches are still at their base, with no product commit/PR/T1 at this snapshot. Revalidate before claiming progress.

Main resolved S5's valid BLOCKED-CONTRACT in [#571 comment 6064796076](https://github.com/brunolrogerio-collab/EliteSCADA/issues/571#issuecomment-6064796076): a distinct ExecuteRichCommand kind, non-empty CommandId and explicit **action Version 2**. Legacy visual/Dynamo/actions stay Version 1; Engineering stays v23. No fallback, presets, new authority or S0-S3 change. The updated issue specifies the exact shared-file exception and compatibility/roundtrip/restart proof. No further owner decision is pending for that bounded unblock.

Panasonic is independent of S5/S6 for normal TAG read/write. At most three parallel product workers; third slot unassigned. Main owns shared contract/composition hooks and serializes changes to SDK/catalog/DI/schema/security/HA/lockfiles.

## Next QUEUED work

KNX/IP -> external Zigbee2MQTT -> Native Zigbee -> DALI gateway -> Z-Wave JS -> Matter is preferred priority, not a serial technical prerequisite chain.
KNX needs stack/platform/redistribution proof; Z2M uses an external user-managed MQTT boundary; Native/Z-Wave reuse #543 and add isolated owned-resource adapters/keys/restore; DALI is a gateway profile over existing drivers; Matter is the later Linux/Wi-Fi gated candidate.
S6 full Script/HMI-to-protocol acceptance follows S5; event-only S4 automation and ordinary TAG drivers have independent prerequisites. S7 only for demonstrated portability need.

No future coding issue/branch/BaseSHA is released by queue placement. Use the live plan/board for start conditions. No consumption of another unmerged lane.

Phase 1 #379/#425 and Help PR #439 are integrated. Stable doc/glossary deltas can overlap protocols with separate ownership. Final accepted features -> #379/#424/#425 -> #306 late EEE Simulation + real-Modbus/PREVIEW-READY -> #300 fresh Preview -> Wave 15 acceptance -> Wave 16 #408. Never merge historical closed/unmerged PR #362 wholesale.

Paid direct Tuya/Intelbras, unresolved direct BTHome/Bluetooth qualification, native DALI and certified/logo claims remain future scope. External Z2M is retained; GPL is not a paid-license blocker.

## Evidence / operating procedure

All driver L4 is human physical validation **after Wave 16, partner disclosure and a stable EliteSCADA installation on a computer**. It is not a code merge gate. Available intermediate tests, exact published-head T1, Main audit and explicit Product Owner merge authorization remain required. No hardware compatibility claim before physical evidence.
Every new ICommunicationDriver passes #560. HA = High Availability/redundancy; HAB = Home Assistant Bridge; STATE != EVENT != COMMAND.

SIGA wakes only the receiving chat, never another chat. GitHub preserves state but does not wake separate chats. Checkpoint = savepoint; continue within the ACTIVE lane. Do not ask the owner for Git credentials/tokens; use the authorized GitHub publication path, exact-tree checks, expected-head CAS and force=false. Local T0 is not GitHub T1. Classify RED and fix its actual owner. No redundant broad CI.

## Exact owner chat actions

- Panasonic: SIGA when the DEV chat is idle; report #570.
- S5: **SIGA now** to consume Main's resolved blocker; report #571.
- S4 and Mitsubishi: WAIT.
- Future Home/Building/docs chats: no new chat/start until exact ACTIVE release.
- Main: continue live audit/coordination. No merge authorized by this planning update.

---

## Historical coordinator snapshots (retained; superseded by the live pointer above)
## Historical pointer — 2026-10-06 22:31 BRT (superseded)

Canonical current handoff:
`docs/WAVE15-MAIN-COORDINATOR-HANDOFF-2026-10-06-2231.md`

Accepted product checkpoint:
`wave15/corrections-integration@27a9347e3d6f87db799a1541de2a27484b8bda51`

Current next product lane:
`#554 DRIVER-INTERACTION-S1` on `work/driver-interaction-s1-transient-runtime`, released but untouched at the accepted product checkpoint.

#534 remains isolated `CODEX_LOCAL` and must not be merged/rebased blindly.

Industrial research #552/#553 is accepted/closed; implementation order remains `MITSUBISHI_FIRST -> PANASONIC_SECOND`, with no product branch active.

GitHub live remains the only authority.

---
# CURRENT CONSOLIDATION — 2026-10-06

The current product integration is `wave15/corrections-integration@25df20bf562acf6c3d78eb2e953f7010729b7c49` after PR #529. Product Owner accepted and closed #484, #501 and #308: the first-party SVG factory/catalog and current usable Dynamo set meet Wave 15 scope; visual-layer refinements are future work. #482/#496/#500/#503/#445 are also closed. Remaining Wave 15 closure is productization/Preview (#306/#300), localization (#379), Help (#424), and Manual (#425). Post-merge CI #37494810971 completed successfully for Web, Backend/test/runtime smoke and Chromium integration smoke; full-browser E2E was skipped by workflow conditions. The only open PR is unrelated Preview infrastructure #362.

Read [the current consolidation handoff](WAVE15-TEMPORARY-COORDINATOR-HANDOFF-2026-10-04.md) for product details and root [`LAST CHANGE.md`](../LAST%20CHANGE.md) for exact SHA/CI evidence. The older coordinator snapshots below are historical.

## Next coordinator assignment — Home/Building drivers

The Product Owner has explicitly directed the incoming coordinator to begin Home/Building automation-driver development. Start with #472 and the research record #475; the old `WAIT_CURRENT_LANES / NO_IMPLEMENTATION_BRANCH` planning-only status in #472 is superseded. Before protocol-specific coding, revalidate/reuse #469 Host Serial and resolve #483's still-open canonical TAG/DataSource, Equipment/Template and catalog-convergence prerequisites against the current SHA. Review #475 dossier decisions (GO/WAIT/REJECT, licenses, hardware and test levels), select the first feasible driver slice, then launch a bounded implementation with exact acceptance evidence. Reuse existing BACnet/IP; do not create a parallel Runtime/TAG model. Track #306/#300/#379/#424/#425 separately; this assignment does not declare Wave 15 closed.

# CURRENT CONSOLIDATION — 2026-10-05

The current product target is `wave15/corrections-integration`. The owner explicitly authorized this temporary coordinator to integrate the non-visual product work and produce its post-merge CI. Old PARKED/NO_MERGE lane checkpoints below are historical, not current instructions.

Read [the current consolidation handoff](WAVE15-TEMPORARY-COORDINATOR-HANDOFF-2026-10-04.md) first. Dynamo artwork is **not accepted** and its redesign is deferred by the owner. The static SVG-library request was NOT deferred: the owner identified that the 2,208 factory SVGs were absent from the unified library and visual-editor insertion. The follow-up connects the actual catalog to both surfaces; access is distinct from visual/curation acceptance. Do not count generated previews as approved artwork.

The independent PR #362 targets the preview-workbench lane, not this product. Residential/home automation driver development is the successor coordinator's scope. Current changes close Developer HA defaults/license gating, database Engineering navigation/themes, security-policy refresh and the latest post-merge regressions. Final CI is recorded in the current handoff, not inferred from older green runs.

---

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

# LATEST DELTA — 2026-10-03 — NEXT BATCH ACTIVE / COORDINATOR ROTATION

> This section supersedes older current-state wording below when there is a conflict.
> GitHub live remains the only authority.

Canonical successor handoff:
`docs/WAVE15-MAIN-COORDINATOR-HANDOFF-2026-10-03-NEXT-BATCH-ACTIVE.md`

Accepted product checkpoint:
`9b596286a40b1649564075626e817cc61e32e7bc`

Release base of the active next batch:
`aca641501302edb04f32b83b04adf0726d7e8a96`

Current active lanes at outgoing revalidation:
- #482 `work/w15-visual-library-catalog` — ahead 0 / behind 0 / no PR;
- #483 `work/w15-home-building-h0` — ahead 0 / behind 0 / no PR;
- #484 `content/w15-builtin-asset-curation-b1` — WIP at `055deb4df30eaf3e8af4d33fdc8739cf3d1b9df4`, tree `bca34cc2ea3b12124db703244ed715ebced9e0f9`, ahead 1 / behind 0, 86 draft assets in the batch manifest, no PR/handoff yet.

CODEX is parked.
Protocol-driver fan-out remains blocked.
SVG-first/core.svgSymbol V2 waits #482.
HOME/BUILDING H1-H6 waits #483.
Rejected PR #451 remains permanently excluded.

Read the canonical successor handoff above and latest #305/#482/#483/#484 GitHub live state before any action.

---

# LATEST DELTA — 2026-10-02 — OUTGOING MAIN / CODEX PRODUCT AUTHORITY / CI HANDOFF

> This section supersedes older current-state statements below when they conflict.
> GitHub live remains the only authority.
>
> The outgoing coordinator was explicitly instructed **not to investigate the
> latest broad CI failure** because the conversation is being replaced. The next
> coordinator must diagnose that failure fresh.

## Product authority — mandatory rule

The current Wave 15 product authority is the product that the **Product Owner
personally validated locally with CODEX and CODEX delivered to GitHub**.

The earlier coordinator/parallel-DEV state became unreliable. After that, the
Product Owner and CODEX performed roughly a dozen further local product
corrections and validated the result interactively. Those delivered product
bytes are the current branch of truth for Wave 15.

Automated CI is now validating that product. It is not permission to redefine it.

Mandatory rule:

`CODEX-delivered product -> tests validate product`

not:

`failing test -> change product until test passes`.

If a test is stale/brittle/obsolete relative to the accepted product, fix the
test. If a test proves a genuine product defect, record a blocker/finding and
wait for explicit Product Owner authorization before changing product bytes.

## Live state at outgoing handoff

Revalidated immediately before this handoff:

- integration:
  `wave15/corrections-integration@9ff26a3dfeb3a6b4962a8b9120c2e8f575c01878`
- current recovery/validation carrier:
  PR #450 — `feat(visual-editor): surface properties and simulation controls`
- PR #450:
  `OPEN / DRAFT / NO_MERGE`
- exact head:
  `b56516a4aa4e6683a45d929f5f9207eb308da66b`
- base:
  `9ff26a3dfeb3a6b4962a8b9120c2e8f575c01878`
- exact-head T1 #383 / run `37005646674`:
  **SUCCESS**
- broad EliteSCADA CI #1644 / run `37005642135`:
  **FAILURE**

The outgoing coordinator intentionally did **not** inspect CI #1644 logs after
the Product Owner reported the failure. Do not inherit a guessed diagnosis.
Inspect the run fresh in the next coordinator chat.

No merge is authorized. PR #450 must remain DRAFT until the Product Owner
explicitly changes the instruction.

## What Main changed during CI reconciliation

There was one coordinator mistake during this session: Main briefly changed
three product files while interpreting CI failures. The Product Owner clarified
that product must remain untouched for test validation. Main then restored
those files exactly to the CODEX-delivered bytes and recorded the correction.

After restoration, the coordinator limited changes to:
- E2E/integration test contracts;
- test expectations/selectors/fixtures/timeouts;
- temporary CI workflow plumbing needed to run broad validation on the recovery
  branch.

Before continuing, the next coordinator should compare the current branch
against the restored CODEX-product checkpoint and confirm that post-restoration
deltas remain test/CI-only.

## Remaining Wave 15 work

Reconstruct each item against GitHub live. Do not blindly trust old open PRs or
old coordinator messages.

### 1. Current CI validation

First diagnose broad CI #1644 on exact head `b56516a4...`.

Rules:
- no product changes merely to get green;
- no blind broad rerun;
- correct stale tests only after proving accepted product behavior;
- real product defect -> blocker/finding, not unauthorized patch;
- **NO MERGE**.

### 2. HA-D2 — issue #423 — mandatory

HA-D2 remains a Wave 15 requirement.

It must cover:
- conservative automatic failover;
- failback;
- fencing / epoch / reference authority;
- never promote solely because communication was lost;
- no duplicate writes/commands;
- no duplicate Server Scripts;
- no duplicate Alarm evaluation;
- no duplicate Historian ingestion;
- no duplicate Operational Events;
- Runtime Session continuity without double seat;
- Web Runtime re-discovery of effective Active using public product contracts;
- adversarial two-process evidence.

**Product Owner clarification added 2026-10-02:** HA-D2 must also ship an
Engineering/admin interface for HA operations. It is not enough to have only the
backend algorithm.

That surface must allow authorized users to:
- configure HA topology / peer identity and supported node settings;
- inspect local/peer role, effective Active, health and topology;
- see actionable degraded/blocked/split diagnostics;
- issue controlled privileged switchover/failback/role-transition commands where
  the authority contract permits;
- confirm authority-changing actions;
- never bypass fencing/epoch/reference rules from the UI.

Issue #423 comment `5952748508` records this requirement.

EliteGO application remains deferred and is not an HA-D2 gate.

### 3. Remote database — issue #366 — mandatory

Remote database use still must be developed/closed in Wave 15 using #366 as the
architecture authority.

Required product outcome:
- local managed PostgreSQL/TimescaleDB remains the normal/default profile;
- Engineering/admin UI can configure a supported remote DB endpoint;
- test connection / compatibility before cutover;
- supported migrate/copy flow;
- primary DB plus optional Historian override under the accepted topology;
- maintenance boundary;
- verify/readiness;
- atomic switch;
- rollback/recovery;
- local DB/data preserved unless explicit purge;
- credentials handled as deployment secrets;
- remote DB lifecycle remains external and is never silently uninstalled;
- HA consumes DB topology and does not use a shared DB as an authority bypass.

Issue #366 comment `5952749151` records this requirement.

### 4. Historical Playback

The explicit historical read-only past-state/playback contract remains to be
closed and validated. Reuse the integrated Historian/Data Query/Historical Time
Range foundation. Do not invent a second Historian API.

### 5. Visual Quality / Dynamos / Library — #308

The built-ins and later CODEX product work must be revalidated in the real
product. Close only with actual editor/runtime evidence:
- preview quality;
- no clipping/truncation;
- 2D;
- dimensional/front 3D;
- High Performance;
- insertion matches preview;
- Runtime uses the same canonical representation.

### 6. Help / i18n / Manual

After product behavior stabilizes:
- Help final convergence, including HA, Installation, remote DB and Historical
  behavior;
- final pt-BR / en / es convergence on changed surfaces;
- complete Manual #425, including final workflows, troubleshooting, screenshots
  and examples.

### 7. Productization #306

Late-stage only:
- EEE Simulation instructional project v15;
- EEE real Modbus variant;
- obey `docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`;
- do not invent PLC scaling/write semantics;
- produce normal `.escadapkg`;
- checksum/provenance;
- final Help/manual/i18n;
- declare PREVIEW-READY only when the actual final product is ready.

### 8. Final Preview #300

After PREVIEW-READY:
- fresh environment on the exact final SHA;
- do not reuse stale Codespace/runtime state;
- Product Owner human audit;
- real project creation and use across Engineering, TAG/Data Source,
  Screens/Popups, Scripts, Dynamos, Historian/Trend/Playback,
  Security/Authority, Licensing, Installation, HA, restart/reconnect/recovery;
- resolve residuals;
- only then decide the external-evaluator candidate.

## Wave 16 boundary

Do not divert Wave 15 into installer implementation.

Wave 16 remains the release-factory/installer wave. Its planning already
includes:
- controlled installer/package for project supporters/evaluators;
- commercial-expansion-ready architecture without making W16 a public
  commercial launch;
- Windows/Linux profile architecture;
- CEF Desktop Host;
- local/remote server modes;
- local/remote DB deployment model;
- licensing/EULA/compliance/SBOM;
- first Wave 16 operational gate to exclude coordination/development material
  from distributable packages and preserve that process in future handoffs.

## First actions for the next coordinator

1. Read latest #305 comments, especially outgoing handoff comment
   `5952754472`.
2. Read this newest section completely.
3. Revalidate integration ref and PR #450 exact head.
4. Confirm PR #450 remains DRAFT and unmerged.
5. Inspect broad CI #1644 / run `37005642135` fresh.
6. Do not use the outgoing coordinator's older CI hypotheses as diagnosis.
7. Keep CODEX-delivered product bytes frozen while classifying CI.
8. Then rebuild the Wave 15 closeout plan from:
   `issue requirement -> current product bytes -> current tests -> residual`.
9. Explicitly retain HA-D2 UI/config/commands and remote DB #366 in the Wave 15
   remaining scope.
10. Do not merge unless the Product Owner later authorizes it.

Disposition:

`OUTGOING_HANDOFF / CODEX_PRODUCT_AUTHORITY / PR450_DRAFT_NO_MERGE / T1_383_GREEN / BROAD_1644_RED_UNINVESTIGATED / HA_D2_UI_REQUIRED / REMOTE_DB_REQUIRED`

---

# LATEST DELTA — 2026-10-01 — COORDINATOR RECOVERY / WAVE 15 CHECKPOINT

> Revalidated against GitHub live on 2026-10-01 (America/Sao_Paulo). This
> section supersedes older integration/merge-status statements below. Keep the
> remaining Wave 15 scope open until the coordinator records lane dispositions;
> the stabilization merge is not a declaration that Wave 15 is complete.
>
> Validation scope for this documentation-only handoff: `DOCS_I18N_HELP`.

## Live integration state

The recovery candidate was merged by PR [#443](https://github.com/brunolrogerio-collab/EliteSCADA/pull/443)
into `wave15/corrections-integration`:

- candidate head: `6a6c7990dd7e83a78eb3290fbdb3dfc87c8f23ab`;
- merge commit / current integration ref at verification:
  `9a24e3b5ca40bad86918cad64c9c098ea930a747`;
- PR T1 #341: SUCCESS on the exact candidate;
- broad candidate CI #1632: SUCCESS (backend build/tests/runtime smoke, Web,
  Chromium E2E);
- post-merge CI #1633: SUCCESS on exact merge commit `9a24e3b5...`.

This closes the stabilization checkpoint that the older 2026-09-30 text below
still described as temporary and red. The prior red CI runs #1622/#1623 were
superseded by the exact candidate and post-merge successes; do not carry their
failure status forward.

PR #443 consolidated:
- the tested recovery/Engineering and localized E2E corrections from this
  branch;
- HA-D1 continuation PR #434 (exact-head T1 green);
- Installation UX continuation PR #436 (exact-head T1 green);
- the already-integrated Dynamo/Historical baseline.

The source continuation PRs #434 and #436 may still appear open as drafts even
though their accepted changes are now present through #443. Revalidate their
current GitHub status before closing or changing them; the coordinator should
avoid duplicate merges. Help PR #439 remains a separate open candidate with
green T1 and clean merge state at last live check; #418 remains conflicting.

## Work completed from 2026-09-30 to 2026-10-01

The commits on the recovery branch were merged by #443. In addition to the
coordination recovery and HA/Installation integration, the candidate includes
the broad Engineering/editor refinements, image upload/import support, visual
editor interaction and numeric-entry behavior, and updated E2E contracts. The
latest source commit was `6a6c7990` and the full exact-head and post-merge CI
results above are green.

Two follow-on documentation outcomes are retained in this checkout:

- The reference research for a future professional redesign of the 72 native
  dynamos is recorded in “Dynamo artwork research handoff” below. Opto 22 and
  Wikimedia links are visual/standards references only; third-party file
  licensing and attribution must be checked individually before reuse.
- Elipse E3 trial imports have been removed from automatic project seeding,
  while their converter, normalized drawings, group/z-order/fill/polygon/arc/
  Bezier handling and explicit-import guidance are retained in
  [`docs/E3-DYNAMO-IMPORT-CONVERTER.md`](E3-DYNAMO-IMPORT-CONVERTER.md).
- A local Docker Compose package now builds this accepted source version
  (including the removal of seeded E3 trial dynamos) and runs Web, API and
  TimescaleDB as one restartable stack. It uses loopback-only host ports
  18080/15080/15432 and a persistent named database volume. The detailed
  first-run, backup/persistence and stop/start steps are in
  [`docs/LOCAL-DOCKER-STABLE.md`](LOCAL-DOCKER-STABLE.md). Live validation on
  2026-10-01: all three services healthy; Web `/healthz` and API `/health`
  returned 200 before and after a full Compose restart. The container's
  licensing identity is supplied with a unique stable `ELITESCADA_MACHINE_ID`
  from the ignored local `.env`; keep it unchanged when upgrading this install.

## Coordinator/developer stall and resume point

The prior coordinator conversation reached its duration limit; the coordinator
and several developer conversations consequently stopped in idle/waiting states
while holding stale branch/CI snapshots. Their messages are historical context,
not the live project state. The coordinator handoff must start from GitHub live,
not from those frozen SHAs or old chat instructions.

The latest coordinator bootstrap identified these remaining owner lanes:

- Visual Quality / Library-Dynamo #308: still requires a real product-editor
  preview and visual review; do not claim the 72-symbol professional redesign
  is complete based on the merged 30-item three-style catalog work alone.
- HA-D1 #421 and Installation UX #422: continuation implementation was merged
  via #443, but the coordinator should revalidate the issue orders and explicitly
  record whether each lane is closed or needs a new continuation. Do not resend
  old `SIGA` prompts against stale bootstrap instructions.
- Help #439: separate green candidate awaiting coordinator disposition.

The coordinator conversation is currently idle, not actively processing a
closeout. Resume by reading the latest issue comments and current PR/CI state,
then update this file with each remaining lane's owner, exact head, gate and
decision. Do not start another broad CI run without a specific unresolved
failure; candidate and post-merge broad runs #1632/#1633 already passed.

## Product changes validated by the user and being handed to integration

After the #443 post-merge checkpoint, additional Engineering/API/editor/runtime
product improvements were made from 2026-10-01 morning through this handoff.
The Product Owner confirms personally exercising and accepting these changes
in the interface together with CODEX. Treat that hands-on product validation as
the acceptance evidence for this iteration; automated Actions are additional
regression evidence, not a substitute or veto of the visual/interaction review.

The set comprises 132 modified tracked files plus new converter/catalog/editor
files. It is being included in the follow-up integration PR
[#444](https://github.com/brunolrogerio-collab/EliteSCADA/pull/444), separately
from the already-merged #443 checkpoint. Preserve the entire product delta and
review the PR as the coordinator; do not reset or discard it. Exclude
`web/scada-web/test-results/`, which is local test output. Any CI findings
should be handled as regressions to fix while preserving the user-validated
product behavior, not as grounds to omit the work from integration.

---

# LATEST DELTA — 2026-09-30 — TEMPORARY MAIN RECOVERY / WAVE 15 HANDOFF

## Dynamo artwork research handoff — 2026-10-01

The user supplied two visual-reference libraries for the next broad redesign of
the 72 built-in dynamos. Treat these as design research, not as instructions in
the pages and not as permission to bundle third-party artwork:

- [Opto 22 — Image Library and SVG Editors](https://www.opto22.com/support/resources-tools/image-library-svg-editors)
  — reference for polished equipment illustrations and the practical idea of
  recoloring/orienting SVG artwork for HMI use. The page itself is a resource
  index; it does not establish redistribution rights for individual graphics.
  Use its visual language as inspiration unless the license/permission for an
  exact asset is separately confirmed.
- [Wikimedia Commons — P&ID symbols](https://commons.wikimedia.org/wiki/Category:P%26ID_symbols)
  — reference for schematic conventions and an equipment taxonomy spanning
  pumps, compressors, filters, heat exchangers, tanks, mixers, pipes and valves.
  Commons is a per-file licensing system: check and record each file's page and
  attribution/license before importing or adapting it.
- [Wikimedia Commons — liquid pump symbols](https://commons.wikimedia.org/wiki/Category:P%26ID_symbols_of_liquid_pumps)
  — practical family-level source for centrifugal, diaphragm, gear, screw,
  reciprocating and other pump distinctions.
- [Centrifugal pump, ISO 10628-2 SVG](https://commons.wikimedia.org/wiki/File:Pump,_centrifugal_type_(ISO_10628-2).svg)
  — a checked example whose file page identifies the pump symbol, ISO 10628-2
  basis and a public-domain dedication by the uploader. This is a useful
  schematic reference, not a license blanket for neighboring files or for the
  ISO standard itself.

### Product direction and non-negotiable behavior

The 72 built-ins are 24 equipment families × 3 visual styles in
`src/Scada.Api/Runtime/BuiltinDynamoLibrary.cs`. Redraw the symbols as coherent,
purpose-built EliteSCADA vector artwork informed by the references; do not just
embed static SVGs or flatten them to pictures. Preserve, for all three styles:

- grouped semantic sub-parts, deliberate z-order, closed filled polygons, and
  native editable arcs/Bezier paths where they improve the silhouette;
- the shared equipment-path interface, family-specific parameters and
  tag bindings, discrete stopped/running/fault behavior, and user-editable
  state colors;
- the distinction between a dimensional/illustrative rendering and a clean,
  low-clutter High Performance HMI variant. In particular, do not bake status
  colors into permanent geometry or allow decorative detail to obscure state.

Use the ISO/P&ID references for recognizable equipment anatomy and connection
layout, not as a mandate to make every screen symbol a monochrome P&ID glyph.
For each family, establish a clean centerline, balanced proportions, aligned
ports/shafts, sensible silhouette, and a clear outline/fill hierarchy. Keep
geometry in the canonical visual-object tree so engineering can recolor,
animate, bind properties, and edit individual parts. If a third-party SVG is
actually incorporated, preserve its source URL, creator, per-file license,
attribution and any adaptation notes in project metadata/handoff.

Do not claim a visual redesign complete solely because all 72 definitions
build. Validate representative pump, valve, motor, tank, instrument and
substation families in all three styles in the actual editor/runtime, then
review the full 72-card library for clipping, balance, overlap and readability.
The broad visual pass is still an implementation task until that review passes.

## Elipse E3 import catalog cleanup — 2026-10-01

The trial Elipse E3 dynamos are no longer part of first-project/default workspace
seeding. Existing working snapshots are cleaned selectively on checkout by
`importedDynamoLibrary=true` / `assetOrigin=elipse-e3-import`; native built-ins
and user-authored dynamos are retained. The E3 converter and its normalized
source drawings are intentionally preserved but are not auto-imported. See
[`docs/E3-DYNAMO-IMPORT-CONVERTER.md`](E3-DYNAMO-IMPORT-CONVERTER.md) for the
retained CSV-to-vector pipeline, group/Z-order/fill/curve handling, and the
explicit-import guardrails.

> GitHub live state is authoritative. Temporary Main is actively integrating the lanes below; this is not a handoff-ready or globally green checkpoint yet.

## Integration baseline and broad CI

Current integration base:
`wave15/corrections-integration@af24924ebcedec15e498457895e949bad6365832`
(tree `e35a7c1c4648a3251071c0621808f6c1106044d7`).

Temporary integration candidate is pushed on `coord/w15-recovery-handoff-2026-09-30`.
Latest exact HEAD: revalidate the live ref before any merge; it has not been merged into the integration branch.

Merged immediately before this checkpoint:
- PR #442 Historical mounted-test reconciliation: merge `e75c0faca5d09d161670ab58f14cebffcb3e7ca2`;
- PR #441 built-in Dynamo visual styles: merge `af24924ebcedec15e498457895e949bad6365832`.

Broad CI #1623 / run `36772159732` on exact integration HEAD `af24924...`:
- Backend build/test/smoke: SUCCESS;
- Web build: SUCCESS;
- Chromium E2E: FAILURE after 31.5 minutes; overall run took 35m10s;
- Playwright reported 717 tests, 688 passed, 23 failed, 6 did not run. It uses one worker; later progress numbers exceed 717 because retries are counted in the log.

This is not a regression proven against PR #441: the preceding broad CI #1622 / run `36758425452` was already red on parent integration `1f53f3e52dc2da82823480e31c980fca447d742a`, before #441 merged. The report exposed stale C04/Engineering expectations and selectors in pt-BR (Engineering/Engenharia, Data Sources/Fontes de dados, `Validar preview` vs. the current Preview control, Path/Caminho, and a script-search label). Candidate updates align these tests to current accessible names or stable `data-testid` controls. The two Historian cases failed because the explicit sample was seeded near suite startup, but the broad suite reached these tests about 19 minutes later; both queried the default 15-minute window and got zero records. Candidate now gives these read-only contract assertions an explicit 24-hour query window. The product's 15-minute default is unchanged. Focused/full broad Chromium validation of these candidate changes is still pending.

Installation UX exact-head T1 run `36783465631` on `be98503bc1f9483a8c8b8a7054315a1629fa2534` failed although Web build passed:
- .NET: `EngineeringRuntimeCommunicationDiagnosticsTests.ActiveRuntime_ExposesEngineeringDataSourceIdentityAndIndependentFailureRecovery` hit `EndOfStreamException` during a write immediately after reconnect. The test observed Healthy before a successful post-disconnect read. Added a deterministic post-disconnect read gate (commit `2f0121a1` on #436; same fix is in candidate `0839b6e8`); the focused local test passed five consecutive repetitions.
- Chromium: `runtime.spec.ts` expected `securityRoleCount = 1`, but AUTH-03 correctly yields zero project-local Authority-owned role rows. Corrected to zero (commit `2f0121a1` on #436; same fix is in candidate `0839b6e8`). That run had 70 passed and 1 failed.
- New exact-head #436 T1 run `36784595152` on SHA `2f0121a1956dae5a0ac7b268ba83f9c6672c2d7c` is fully SUCCESS: Focused .NET, focused Chromium (71/71), Web build, Common T1 sanity, final Wave 15 T1 gate. This supersedes the failed prior run for #436 only; the broad integration CI remains red.

Do not increase Playwright workers as a quick CI-speed fix: the suite shares mutable Engineering state, so parallelism could create cross-test contamination. First isolate the remaining failing tests and improve test-state isolation. Avoid repeatedly launching the full 700+ E2E suite; use targeted tests while repairing, then run the broad gate once.

## Engineering editor recovery candidate

Temporary coordinator's candidate includes the W15 visual-editor UX/theme refinements plus a 30-second timeout on Engineering JSON reads, Installation UX #436, and HA-D1 #434. Built-in Dynamo visual styles from #441 are already part of the integration base. Candidate also fixes System Recovery's authority admission to resolve modern package role references from the restored canonical Authority and reject stale/mismatched references.

Local evidence on the candidate:
- `npm run build`: PASS;
- targeted `visual-editor-workspace.spec.ts` Chromium test: PASS;
- C04 search/address and Driver-resource focused browser scenarios: PASS after updating the stale labels/selectors;
- focused recovery-authority and service tests: 13 PASS; passive-runtime package semantics: 1 PASS;
- `git diff --check`: PASS;
- full Chromium E2E remains unverified; local-auth is still intermittently timing out in the custom local harness, so exact GitHub T1 evidence remains authoritative;
- broad integration CI remains RED as recorded above; do not describe this branch as globally verified until CI is repaired.

The local untracked `web/scada-web/test-results/` directory is preserved and excluded from the recovery commit.

## Lane disposition

- HA-D1 PR #434 remains DRAFT at `7b5b0dd3d0bd35a22fd90293f9e7ef6674ba4a27`; T1 run `36780671722` is fully SUCCESS (Focused .NET, focused Chromium, HA two-process, web build, Common, final T1). It is in the temporary integration candidate but not merged to `wave15/corrections-integration`.
- Installation UX PR #436 remains DRAFT at `2f0121a1956dae5a0ac7b268ba83f9c6672c2d7c`; exact-head T1 run `36784595152` is fully SUCCESS (71/71 Chromium). Recovery-authority version rebind and active Runtime restoration are covered.
- Built-in Dynamo PR #441 is already integrated in base `af24924...`.
- Help closeout PR #439 remains open/mergeable at `715676ada46a178c7edd34fc2f964c954382e8a9`; T1 run `36758570399` is SUCCESS. It is a separate ready candidate, not included in this editor recovery branch.
- PR #418 is CONFLICTING; PR #362 remains a separate preview harness review and is not authorized by this handoff for merge.

## Next coordinator actions

1. #434 and #436 exact-head T1s are green. Finalize the candidate's focused E2E evidence for the localized selectors and 24-hour Historian fixture window, then open a single current-base integration PR.
2. Run the broad gate once on that exact candidate. The previous broad E2E took 31.5 minutes and had 23 failures/6 not run; do not claim green until the rerun is fully green and report/artifacts reviewed.
3. Only after the exact-base candidate CI is green should the validated changes be merged into `wave15/corrections-integration` and post-merge evidence captured.
4. Do not hand off to a new coordinator until HA-D1, Installation UX, Dynamo baseline, Engineering/API candidate, and final broad CI evidence are recorded in GitHub.

---

# LATEST DELTA — 2026-09-27 — CHAT HANDOFF / #362 REVIEW PENDING / CONTAINER-NATIVE CORE DIRECTION

> This delta supersedes older current-state wording below when there is a conflict. GitHub live remains the sole authority.

## Product authority

Accepted Wave 15 product checkpoint remains:
`1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`
(tree `5e5fce8ce87f31dfc11b83bb68ff86c67f9f0112`).

Do not confuse later coordination/docs commits with accepted product bytes.

## ENV_A canonical harness

Accepted repaired harness remains:
`preview/w15-first-project-env-harness@50a4451aa122f7f9fd0af98173c184f6623a147b`
(tree `5efeafb08725a90ce0c3df689b8d613f165bb569`).

## #360 local operator candidate delivered — review pending

CODEX delivered PR #362:
- title: `feat(preview): add safe local workbench operator`;
- branch: `preview/w15-vs-local-runner`;
- exact HEAD: `13cb1fcaa3091085515e4d84a3d3e894db541f5f`;
- base: `preview/w15-first-project-env-harness@50a4451...`;
- PR: OPEN / mergeable / NOT MERGED.

Changed paths:
- `docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md`;
- `docs/LOCAL-FIRST-PROJECT-PREVIEW-HARNESS.md`;
- `docs/VISUAL-STUDIO-AI-LOCAL-ELITESCADA-BOOTSTRAP.md`;
- `scripts/preview/elite-local.ps1`;
- `scripts/preview/local-audit.ps1`;
- `scripts/preview/test-local-operator.ps1`.

CODEX reports static/operator/dependency-identity regressions PASS.

Important:
**full lifecycle proof on exact HEAD `13cb1f...` is NOT_TESTED** because Main has not accepted this changed harness SHA for a new ENV_A session.

Shared CODEX control rev 0097:
`4147b108b74ec68082baa3b29c003ea4a515c1d5`.

State:
`WAIT_PENDING_MAIN_REVIEW / DO_NOT_CONTINUE_OR_MERGE`.

Full Visual Studio AI bootstrap copy:
Issue #305 comment `5858902468`.

Local-operations evidence handoff:
Issue #305 comment `5859122865`.

Mandatory next Main action:
1. revalidate PR #362 live exact head/base;
2. inspect all six changed files and compare with accepted harness `50a4451...`;
3. verify reset/persistence/accepted-SHA gating and infrastructure-only scope;
4. if acceptable, explicitly authorize exact `13cb1f...` for full lifecycle validation;
5. require full lifecycle proof before merge/promotion.

## Product Owner strategic architecture — containerized core

Product Owner clarified the desired long-term architecture:

**The containerized EliteSCADA Core should be the common product implementation. Platform/host architecture surrounds this same core to make it operational on each supported environment.**

Architecture owner:
- #363 — `ARCH-CONTAINER-FIRST — distribuição OCI canônica, multi-arch e perfis Windows/Linux/Edge`.

Canonical ADR:
- `docs/ADR-010-CONTAINER-NATIVE-DISTRIBUTION.md`;
- commit `c15a6f102945f40de51c39dfcf018554dd2eb14f`.

Stable Product Goal direction:
- commit `59fcab51ae927e3799c009bb559c60eb679ab932`.

Linux distribution reconciliation:
- `docs/LINUX-DEBIAN-DISTRIBUTION.md`;
- commit `0a9236c1a793e886595448f7592cf4385964e1cd`.

Preferred architecture candidate:

`canonical OCI core + external persistent state + host adapter + deployment capacity profile`.

Initial OCI architecture targets:
- `linux/amd64`;
- `linux/arm64`.

Host/platform adapters may provide:
- Windows service/host integration around a validated unattended OCI runtime;
- Linux systemd/OCI integration;
- industrial Edge/PLC container manager integration;
- future appliance/server integration.

Rules:
- no separate Lite/Edge product fork by default;
- same product contracts and `.escadapkg`;
- Edge capacity is an evidence-based deployment envelope, not a second product;
- commercial license entitlement is independent from physical hardware capacity;
- do not bind licensing to container ID, random hostname, veth/MAC or image digest;
- image recreate/update on the same authorized deployment host must not force license reissue;
- first constrained-Edge topology prefers external PostgreSQL/TimescaleDB;
- support is per homologated CPU/runtime/Driver/network/resource matrix;
- container-native does not mean literally every OS/device is automatically supported.

Windows native packaging remains preserved until an unattended industrially supportable Windows container-host spike proves OCI can safely replace it.

Current architecture disposition:
`CONTAINER-NATIVE PREFERRED ARCHITECTURE CANDIDATE / TECHNICAL VALIDATION REQUIRED / IMPLEMENTATION NOT YET AUTHORIZED`.

#360 must not widen into #363 implementation.

## Human Preview / correction state retained

Human Preview remains:
`BLOCKED_BY_PRODUCT / COMPLETE_FOR_FIRST_PASS`.

Material correction owners remain:
- #354 Demo/Runtime authority leakage;
- #355 Data Source Type -> TAG blocker;
- #356 Templates authoring discoverability;
- #359 Security/Authority remote/Codespace failure with remote-latency hypothesis;
- UX2 #357 / toolbox #358 / Editor #303 / Library-Dynamo #308.

Stage 2 remains prepared, not active.

## Coordinator transfer

Preview control rev 0031:
`297eb379b282ee3cebb9ce25aa30ac94026500ff`.

Durable ledger:
Issue #305 comment `5859163983`.

No product mutation is authorized by this handoff.

---

# LATEST DELTA — 2026-09-27 — CONTAINER-NATIVE DISTRIBUTION ARCHITECTURE CANDIDATE

> This delta supersedes older packaging/distribution strategic wording below when there is a conflict. GitHub live remains the sole authority.

Product Owner expanded the local-container work into a strategic distribution direction.

New owner:
- #363 — `ARCH-CONTAINER-FIRST — distribuição OCI canônica, multi-arch e perfis Windows/Linux/Edge`.

Canonical architecture ADR:
- `docs/ADR-010-CONTAINER-NATIVE-DISTRIBUTION.md`;
- commit `c15a6f102945f40de51c39dfcf018554dd2eb14f`.

Stable Product Goal direction:
- commit `59fcab51ae927e3799c009bb559c60eb679ab932`.

Linux distribution reconciliation:
- commit `0a9236c1a793e886595448f7592cf4385964e1cd`.

Preferred architecture candidate:

`same EliteSCADA product + canonical OCI image + external persistent state + host adapters + deployment capacity profiles`.

Initial OCI target architectures:
- `linux/amd64`;
- `linux/arm64`.

Key rules:
- do not create a separate Lite/Edge fork by default;
- Edge capacity limits are evidence-based hardware Deployment Capacity Profiles;
- commercial license entitlement and physical platform capacity remain independent;
- do not bind licenses to ephemeral container ID/hostname/veth MAC/image digest;
- container recreate/update on the same authorized host must not require license reissue;
- preferred first constrained-Edge topology is EliteSCADA OCI with external PostgreSQL/TimescaleDB;
- official support is per homologated OCI host/runtime/Driver/resource matrix;
- Windows native packaging remains preserved until an unattended industrially supportable Windows container-host strategy is proven.

Relationships:
- #360 collects local lifecycle/evidence only; no ADR-010 implementation in that mission;
- #361 owns common installed lifecycle/host-adapter semantics;
- #205/#207 must re-audit #363 when Windows packaging resumes;
- #306 must only document the container profile after it becomes technically accepted.

Preview control rev 0030:
`2f9d9512a2a71f78619a80a7f82c092b9ea0ad82`.

Ledger:
Issue #305 comment `5859084494`.

Disposition:
`CONTAINER-NATIVE PREFERRED ARCHITECTURE CANDIDATE / TECHNICAL VALIDATION REQUIRED / IMPLEMENTATION NOT YET AUTHORIZED`.

---

# LATEST DELTA — 2026-09-27 — LOCAL OPERATIONS EVIDENCE REQUIRED / INSTALLED SERVICE LIFECYCLE OPENED

> This delta supersedes older local-operator planning wording below when there is a conflict. GitHub live remains the sole authority.

Product Owner added two related operational requirements.

## Canonical local-running evidence

Before #360 completes, CODEX must create:

`docs/LOCAL-ELITESCADA-OPERATIONS-EVIDENCE.md`.

The file must preserve real local-operation evidence and failure modes for reuse by:
- the next fresh Preview;
- #307/#359 local-vs-Codespace diagnosis;
- Windows packaging #205/#207;
- future Linux packaging;
- installed-service design #361.

Required topics include lifecycle behavior, persistence/destructive boundaries, readiness/health, diagnostics/redaction, provenance-manifest loss, LF/CRLF dependency identity, PowerShell->Bash line endings, evidence/runtime worktree isolation, Docker restart/resume and installer implications.

Evidence labels:
`CONFIRMED | OBSERVED | HYPOTHESIS | NOT_TESTED`.

Shared CODEX control rev 0096:
`523458b51dc4a13045ed2efd07452a8fae68bca1`.

#360 requirement comment:
`5858872903`.

## Installed-service lifecycle

New issue:
- #361 — `INSTALL-OPS — lifecycle operacional como Windows Service e Linux systemd`.

Product Owner intent:
reuse the operational **semantics** now being proven locally for future installed administration, but not the Docker Preview implementation itself.

Future production targets:
- Windows Service / Service Control Manager;
- Linux systemd.

Normal start/stop/restart/status/diagnose must preserve persistent EliteSCADA state.
Pause/resume should be implemented only if a technical audit proves safe/meaningful.
Destructive reset/purge is not a normal service operation.

Packaging links:
- #205 comment `5858873311`;
- #306 comment `5858873683`.

Preview control rev 0029:
`0d5e6523fd49cb87ae13e4da5c902d71fa8bc644`.

Durable ledger:
Issue #305 comment `5858878923`.

---

# LATEST DELTA — 2026-09-27 — REPAIRED HARNESS ACCEPTED / VISUAL STUDIO LOCAL OPERATOR ACTIVE

> This delta supersedes older ENV_A harness current-state wording below when there is a conflict. GitHub live remains the sole authority.

CODEX final repaired-harness lifecycle proof was accepted from Issue #305 comment `5858036695`.

Accepted harness:
- SHA `50a4451aa122f7f9fd0af98173c184f6623a147b`;
- tree `5efeafb08725a90ce0c3df689b8d613f165bb569`;
- product base `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Canonical harness branch was fast-forwarded:
`preview/w15-first-project-env-harness -> 50a4451...`.

Accepted proof includes:
- dependency provenance stable across Windows LF/CRLF materialization;
- preparation READY;
- start/status stability;
- pause/resume;
- real Docker Desktop restart/resume;
- separate evidence worktree commit without invalidating runtime preparation;
- reset preserving dependency preparation;
- second fresh start without package install/restore;
- final NOT_STARTED / PREPARATION=READY.

## Visual Studio local operator

Product Owner authorized a reusable local container operator for native Visual Studio AI.

Dedicated branch:
`preview/w15-vs-local-runner`
created from exact accepted harness `50a4451...`.

Dedicated issue:
- #360 — `W15-LOCAL-OPS — operador local containerizado + bootstrap para IA do Visual Studio`.

Active CODEX route:
`ROUTE-SEQUENTIAL-CODEX-VS-LOCAL-OPERATOR-BOOTSTRAP-78`
commit `f1ec3ad28ceb0ce13a1d645e6ca84c356036301c`.

Preview control rev 0028:
`510291282cbdceca9bbdab806dd7f5b4524f55e9`.

Preferred operator:
`scripts/preview/elite-local.ps1`

Required commands:
`prepare | start | status | pause | resume | stop | restart | diagnose | reset`.

Default behavior preserves local database/project/workbench state. `reset` is explicitly destructive and must require confirmation.

Required native Visual Studio AI bootstrap:
`docs/VISUAL-STUDIO-AI-LOCAL-ELITESCADA-BOOTSTRAP.md`.

The bootstrap must let the Product Owner instruct Visual Studio AI in natural language to run, pause, stop, resume, restart and diagnose the local application without improvising Docker commands.

The local operator is also the preferred manual/AI-assisted surface for comparing local behavior against Codespace/remote findings under #307/#359.

This mission is infrastructure/docs only. Product corrections #354/#355/#359 and UX2 remain separate.

Ledger:
Issue #305 comment `5858162988`.

---

# LATEST DELTA — 2026-09-27 — SECURITY 402 REMOTE-LATENCY HYPOTHESIS ELEVATED

> This delta supersedes older #359 causal wording below when there is a conflict. GitHub live remains the sole authority.

Product Owner correlated the real Codespace Security/Authority failure with the established Wave 14 pattern where some browser/Engineering loads failed through the remote/forwarded path while local API/Vite remained healthy.

This is consistent with:
- Wave 14 A1 transport root `UNCERTAIN — BOUNDED`, with local API/Vite outage excluded in the captured window and forwarded-browser/static delivery left unresolved;
- #307 remote/WAN resilience contract.

#359 is now framed with the primary working hypothesis:

`REMOTE_PATH_LATENCY_OR_FORWARDING_EXPOSES_TOO-TIGHT_CLIENT_TIMING / ERROR_MAPPING`.

This is not yet the final root cause.

Required diagnostic order:
1. ENV_A local / normal latency;
2. ENV_A local / deterministic remote-like latency+jitter injection;
3. ENV_B/Codespace forwarded path with exact request/status/body/proxy/API correlation.

Interpretation:
- local normal failure -> generic Authority/product;
- local normal pass + injected-latency failure -> #307 generic remote/WAN resilience;
- local normal + injected-latency pass but Codespace failure -> forwarding/edge/environment remains likely.

Do not infer licensing/Authority semantics from numeric HTTP 402 alone.
Do not solve by globally increasing all timeouts.
Read/list operations may use bounded retry under #307; user/role mutations must not be blindly retried after ambiguous transport outcomes.

Stage2 V2-20 update:
`df057db7fe388d02f2a98266facfff8d726cb94c`.

Preview control rev 0027:
`a27eef269bd3a2618435a622d944bd7e7fc560a1`.

Ledger:
Issue #305 comment `5858066237`.

---

# LATEST DELTA — 2026-09-27 — SECURITY/AUTHORITY CODESPACE P1 ADDED

> This delta supersedes older Human Preview findings summaries below when there is a conflict. GitHub live remains the sole authority.

Additional Product Owner Human Preview finding:

- some Engineering surfaces failed to load with an observed HTTP `402` condition;
- Security/Authority administration remained unusable throughout the real Codespace journey;
- user creation and user editing could not be exercised.

Triage did not find an explicit intentional product `402 Payment Required` response path, so root cause remains bounded rather than inferred.

Classification:
`DEFECT / PREVIEW_SECURITY_ADMIN_UNAVAILABLE / ROOT_LAYER_UNCERTAIN_BOUNDED`.

New issue:
- #359 — `W15-PREVIEW-P1 — Security/Authority administration fails to load in Codespace with observed HTTP 402`.

Cross-linked owners:
- #302 Authority;
- #307 remote/WAN resilience.

Human findings:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-HUMAN-PREVIEW-FINDINGS.md`
commit `4199df90a7f8ece83217e28cdab3e6b4261f4be1`.

Stage2 now includes:
`V2-20 — Security/Authority mounted UI and HTTP 402 root isolation`
commit `202e9ccbac805294c24905017bf893ac9606d650`.

Preview control rev 0026:
`0c6d47a31abf3bff2ef29e913683e34bdd351c3a`.

Future acceptance requires exact network/status/body capture, local-vs-remote isolation, and mounted user create/edit/role-assignment verification.

The active repaired ENV_A harness final lifecycle validation remains uninterrupted.

---

# LATEST DELTA — 2026-09-27 — SECOND DEV UX WAVE PREPARED / ICON-FIRST EDITOR TOOLBOX

> This delta supersedes older post-Preview UX planning wording below when there is a conflict. GitHub live remains the sole authority.

The Product Owner decided to organize the remaining post-Preview usability work as a dedicated **second DEV UX wave** rather than scattered polish.

New umbrella:
- #357 — `W15-UX2 — segunda leva DEV UX pós-Preview: Engineering usability, authoring e visual workflow`.

New dedicated toolbox item:
- #358 — `W15-UX2-EDITOR — substituir toolbox textual por paleta compacta de ícones`.

## Binding toolbox decision

The current persistent text-button object toolbox consumes too much Editor workspace and is not sufficiently intuitive.

Target:
- icon-first compact palette by default;
- localized tooltip + accessible label per icon;
- keyboard reachable;
- clear active/insertion state;
- category grouping/flyouts where useful;
- canvas-space priority at representative desktop sizes;
- no object-schema/renderer fork;
- no proprietary SCADA/HMI icon copying.

#308 still owns actual Library/Dynamo visual preview before insertion; an icon opening the Library/Dynamo surface is not a substitute for preview.

#303 received the UX2 toolbox decision in comment `5858013095`.

Stage2 contract now includes:
`V2-19 — icon-first object toolbox`
via commit `fcf732cc4de95512291fe30bb2cd180c2c3a6375`.

Preview control rev 0025:
`f5bff65222f3c4adba85c12912c00b101422c20a`.

Planned post-harness correction order:
1. #354 P0 fresh-project Demo/Runtime authority;
2. #355 P1 Data Source Type -> TAG blocker;
3. bounded UX2 DEV slices (#303/#358/#308/#356) in parallel where safe;
4. integration;
5. directed Stage2 verification;
6. repeat fresh first-project Preview.

The currently active ENV_A harness final lifecycle validation remains uninterrupted.

Durable ledger:
Issue #305 comment `5858016758`.

---

# LATEST DELTA — 2026-09-27 — HUMAN PREVIEW BLOCKED / EMBARGO LIFTED / PRODUCT CORRECTIONS OPENED

> This delta supersedes older Human Preview / embargo wording below when there is a conflict. GitHub live remains the sole authority.

## Human Preview result

`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 = BLOCKED_BY_PRODUCT`.

Canonical Product Owner evidence:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-HUMAN-PREVIEW-FINDINGS.md`
commit `e413b3c85224cbf3f62bad6896738ec7ebe7810c`.

The Product Owner reported that the interface improved substantially, but could not create a trustworthy effective data-backed Runtime application.

Material findings:
- first-project Runtime contained unexpected Demo-like tank/pump/frequency/current content not intentionally created/imported and not readily reconcilable/deletable from Engineering;
- Data Source Type field did not expose a usable selectable list, blocking Data Source completion and TAG creation;
- Library/Dynamo preview still absent;
- Templates have no discoverable create/edit mechanism;
- Editor Property Inspector has light-on-light readability issues;
- text object visible content/rename path was not discoverable;
- rectangle/basic-shape fill/display color could not be changed reliably.

## Embargo lifted / cross-audit

The Human Preview reached a blocked disposition, so the CODEX findings embargo is now LIFTED for Main/CODEX cross-audit work.

Convergent evidence:
- CODEX independently observed Runtime `Demo · Estação Elevatória` while Engineering showed another project identity and zero TAGs/Data Sources;
- CODEX also observed Data Sources/TAGs controls not advancing usefully, supportive but not identical to the Product Owner's Type-selector failure.

## Correction owners

New issues:
- #354 — fresh first project leaks Demo Runtime content / Engineering authority mismatch;
- #355 — Data Source Type selector unusable blocks TAG creation;
- #356 — Templates create/edit workflow missing/not discoverable.

Existing issues updated:
- #303 — Human Preview Editor Properties/text/fill evidence;
- #308 — Human Preview Library/Dynamo preview evidence.

Preview control rev 0024:
`1d96f4262c4d2bf8ba7e383453b075f9e28540bd`.

Shared CODEX control rev 0093:
`9cc5489bb4d4f3d3f005df118fb38fa7bd3cdc24`.

## Stage2

Stage2 contract now includes V2-14..V2-18 for:
- hidden Demo Runtime leakage/authority;
- Data Source Type -> TAG -> Runtime path;
- Templates authoring CRUD/discoverability;
- Editor first-user property usability;
- Library/Dynamo visual previews.

Stage2 update:
`e96f1045a644a70e3f49c6aa0cdd1ebb88eab0da`.

Stage2 remains:
`PREPARED / NOT ACTIVE`.

## Current CODEX infrastructure work

Do not interrupt the current autonomous final lifecycle validation of:
`preview/w15-first-project-env-harness-repair@50a4451aa122f7f9fd0af98173c184f6623a147b`.

The repaired harness uses committed Git blob IDs for dependency identity so Windows LF/CRLF checkout differences do not invalidate preparation provenance.

CODEX should finish the full lifecycle proof before Main activates Stage2 or opens product correction execution.

## Current overall state

- Human Preview: `BLOCKED_BY_PRODUCT / COMPLETE_FOR_FIRST_PASS`;
- findings embargo: `LIFTED`;
- CODEX Stage1 attempt1: `INCONCLUSIVE / SEALED`;
- ENV_A repair lifecycle validation: ACTIVE;
- Stage2: `PREPARED / NOT ACTIVE`;
- Wave15 Preview acceptance: NOT ACHIEVED.

Durable ledger:
Issue #305 comment `5857997672`.

---

# LATEST DELTA — 2026-09-27 — ENV_A REPAIR CANDIDATE IN AUTONOMOUS FINAL VALIDATION

> This delta supersedes older ENV_A repair current-state wording below when there is a conflict. GitHub live remains the sole authority.

Human Preview remains:
`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 = ACTIVE`.

ENV_B remains:
`READY / RESUMABLE`.

CODEX Stage1 attempt 1 remains:
`INCONCLUSIVE / SEALED`.

Attempt-1 evidence/database state was preserved before old runtime cleanup. No product disposition was made from that attempt.

CODEX autonomous infrastructure recovery produced a repair candidate:

- branch: `preview/w15-first-project-env-harness-repair`;
- SHA: `50a4451aa122f7f9fd0af98173c184f6623a147b`;
- tree: `5efeafb08725a90ce0c3df689b8d613f165bb569`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Accepted root cause:
the old dependency provenance hashed working-tree bytes. A clean Windows checkout of the same exact Git tree could materialize LF vs CRLF differently, changing `inputsSha` and therefore the dependency key/manifest lookup. The repair uses committed Git blob IDs for dependency identity and normalizes embedded Bash text to LF before PowerShell->Bash execution.

Accepted static/regression evidence:
- same committed tree across LF/CRLF -> same dependency identity;
- committed dependency input change -> different identity;
- Bash CRLF/CR -> LF normalization regression PASS;
- preparation on repair candidate -> `PREPARATION=READY / STATE=NOT_STARTED`;
- compare remains Preview/harness-only.

Preview control rev 0023:
`5c0e57c6336e75a68769f35251cca245963b4801`.

Active shared CODEX route:
`ROUTE-SEQUENTIAL-CODEX-ENV-A-REPAIR-FINAL-LIFECYCLE-77`
commit `f7400e554907307506e634dd19b18a0bbb500ece`.

CODEX now continues autonomously through the complete final lifecycle proof:
- start/status stability;
- pause/resume;
- real Docker Desktop restart;
- separate evidence worktree commit while runtime dependency identity stays READY/stable;
- reset preserving preparation;
- second fresh start with no package install/restore;
- final reset/static/diff cleanliness.

CODEX should not return for ordinary recoverable harness failures; it should iterate safely and return only with a fully validated final candidate or a genuine blocker/preservation risk.

Do not promote the canonical harness branch yet. Main will promote after final proof review.

Stage2 remains:
`PREPARED / NOT ACTIVE`.

Durable ledger:
Issue #305 comment `5857900488`.

---

# LATEST DELTA — 2026-09-27 — CODEX AUTONOMOUS ENV_A INFRA RECOVERY

> This delta supersedes older CODEX current-state wording below when there is a conflict. GitHub live remains the sole authority.

Human Preview remains:
`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 = ACTIVE`.

ENV_B remains:
`READY / RESUMABLE`.

CODEX Stage1 attempt 1 remains:
`INCONCLUSIVE / SEALED`.

The attempt is not classified as a product defect and must not resume as an independent black-box journey in the same CODEX context.

Latest infrastructure reconciliation confirmed:
- exact accepted runtime worktree `bb451fa6e07982ac12384895f6097d5833761d16`;
- exact tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- sealed session `6f18039a-5e67-49bd-a566-2050cf775aee` still RUNNING;
- `PREPARATION=REQUIRED` because expected preparation provenance manifest is absent.

Product Owner authorized CODEX to continue useful work autonomously inside the ENV_A infrastructure/harness/evidence boundary.

Active shared route:
`ROUTE-SEQUENTIAL-CODEX-AUTONOMOUS-ENV-A-INFRA-RECOVERY-76`
commit `9b3c4b0170cb819a13bf096fefb10c483d024092`.

Preview control rev 0022:
`40125a09fd4444c64ca874c59dcb4269055940c1`.

CODEX may now:
- preserve/snapshot attempt1 state;
- pause/quiesce it safely;
- diagnose/fix preparation-manifest/provenance loss;
- create separate evidence worktrees;
- change Preview harness infrastructure;
- add regressions;
- prepare/revalidate dependencies after preservation;
- create/reset disposable validation sessions;
- restart Docker Desktop;
- iterate without returning for each small implementation decision.

Boundaries:
- no product source changes;
- no product-state DB repair shortcut;
- no auth/licensing/Authority weakening;
- no seeded project/Demo/EEE;
- no Product Owner observation consumption;
- no detailed finding disclosure;
- no new independent Stage1 black-box attempt in the contaminated CODEX context.

CODEX should return only with a fully validated replacement harness candidate or a genuine external blocker.

Stage2 remains:
`PREPARED / NOT ACTIVE`.

Durable ledger:
Issue #305 comment `5857659128`.

---

# LATEST DELTA — 2026-09-27 — HUMAN PREVIEW ACTIVE / CODEX STAGE1 ATTEMPT1 SEALED INCONCLUSIVE

> This delta supersedes older CODEX Stage1 current-state wording below when there is a conflict. GitHub live remains the sole authority.

## Human Preview

`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 = ACTIVE`.

ENV_B remains:
`READY / RESUMABLE`.

The Product Owner continues the first-project journey independently from the visible no-project boundary.

Detailed CODEX findings remain embargoed and must not influence the Human Preview.

## CODEX Stage1 attempt 1

Main reviewed the coarse handoff and embargoed evidence.

Disposition:
`STAGE1_ATTEMPT1 = INCONCLUSIVE / SEALED`.

This is not a product-defect disposition and not a completed independent black-box journey.

The attempt has already crossed into post-block diagnostic correlation, so the same CODEX context must not resume black-box exploration as though it were still unspoiled.

Existing ENV_A audit session is preserved; no reset is authorized.

Preview control rev 0021:
`95a632ba8fb2ef3db8caaff760e94be9953f4ef6`.

Shared CODEX route rev 0090:
`ROUTE-SEQUENTIAL-CODEX-STAGE1-ATTEMPT1-SEAL-RECONCILE-75`
commit `f37eebab556b44fa88f4a31ad1c3922568d10518`.

Current CODEX mission is infrastructure reconciliation only:
- restore runtime worktree to exact accepted harness if needed;
- verify same session / expected dependency preparation;
- pause the same session if reconciliation is exact;
- no product interaction;
- no prepare/start/resume/reset;
- no Stage1 retry;
- no Stage2.

Accepted harness remains:
`bb451fa6e07982ac12384895f6097d5833761d16`
/ tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`.

Embargoed evidence branch remains:
`preview/w15-first-project-codex-evidence`.

A valid future independent CODEX black-box retry requires a fresh CODEX context/agent that has not consumed attempt-1 diagnostic findings.

## Stage2

`W15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION = PREPARED / NOT ACTIVE`.

Main will consider Stage2 after the Human Preview reaches COMPLETE or BLOCKED and the embargo/cross-audit state can be reconciled.

## Immediate coordinator action

1. Let the Product Owner continue Human Preview unaided.
2. Wait for CODEX's coarse attempt1 seal/reconciliation handoff.
3. Do not expose embargoed CODEX findings to Product Owner.
4. Do not start a CODEX retry in the same contaminated context.
5. After Human Preview completion/block, lift embargo and perform cross-audit reconciliation before deciding retry/Stage2 sequencing.

Durable ledger:
Issue #305 comment `5857629860`.

---

# LATEST DELTA — 2026-09-27 — BOTH PREVIEW ENVIRONMENTS READY / PARALLEL EXPLORATION ACTIVE

> This delta supersedes older Preview-readiness wording below when there is a conflict. GitHub live remains the sole authority.

Main accepted the real ENV_B stop/resume proof and now has both independent Preview environments READY.

## ENV_A

`ENV_A = READY / CLEAN / RESUMABLE`

Exact harness:
- SHA `bb451fa6e07982ac12384895f6097d5833761d16`;
- tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

## ENV_B

`ENV_B = READY / RESUMABLE`

Real Codespace source:
`preview/w15-first-project-env-b-codespace`
at exact branch point `bb451fa6e07982ac12384895f6097d5833761d16`.

Accepted real lifecycle evidence:
- first Local Administrator created as readiness marker;
- no project created;
- same Codespace stopped normally;
- same Codespace reopened;
- repository-controlled startup restored EliteSCADA automatically;
- browser access returned without manual terminal recovery;
- Administrator marker persisted;
- product returned to normal no-project / create-project state;
- no hidden project/import/Demo/EEE state appeared.

5173 remains Private but owner-accessible; this was explicitly reclassified non-blocking by Product Owner. 5080/5432 remain non-public in accepted evidence.

## Parallel exploratory gates

Human Preview:
`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 -> ACTIVE`.

Human starting checkpoint:
Administrator already exists only as ENV_B readiness marker; no project is prepared. The human first-project audit begins from the visible `Criar novo projeto` boundary.

CODEX Stage 1:
`W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01 -> ACTIVE`.

Shared CODEX route:
`ROUTE-SEQUENTIAL-CODEX-FIRST-PROJECT-BLACKBOX-STAGE1-74`
commit `689a854638bfc0f26262cb86d1daaeee53b32456`.

Preview control rev 0020:
`965ceef9753bac16d488c064de5b9bcb1ed55333`.

Embargoed CODEX evidence branch:
`preview/w15-first-project-codex-evidence`.

## Independence / embargo

Detailed CODEX findings remain embargoed from Product Owner until the human first-project journey completes.

Product Owner findings are not fed to CODEX while its black-box journey is active.

No cross-audit comparison is permitted yet.

## Stage 2

`W15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION = PREPARED / NOT ACTIVE`.

Stage 2 starts only after CODEX Stage 1 is complete/sealed and its project/session state is checkpointed/derived according to the prepared Stage 2 contract.

## Immediate coordinator action

1. Allow Product Owner to perform the first-project journey unaided from `Criar novo projeto`.
2. Allow CODEX to execute Stage 1 independently on clean ENV_A.
3. If CODEX needs time interruption, it may use infrastructure-only pause/resume without reset.
4. Surface only coarse CODEX gate state while embargo is active.
5. When the Product Owner journey reaches COMPLETE or BLOCKED, end embargo and perform cross-audit comparison.
6. Only then consider Stage 2 activation.

Durable ledger:
Issue #305 comment `5857445127`.

---

# LATEST DELTA — 2026-09-27 — ENV_A READY / ENV_B REAL CODESPACE GATE

> This delta supersedes older ENV_A readiness wording below when they conflict. GitHub live remains the sole authority.

Main accepted the final ENV_A readiness proof on exact Preview harness:

- harness SHA: `bb451fa6e07982ac12384895f6097d5833761d16`;
- harness tree: `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Disposition:
`ENV_A = READY / CLEAN / RESUMABLE`.

Accepted proof includes:
- provenance-bound dependency preparation READY;
- true first-run UI;
- minimal Administrator created through normal UI;
- real Docker Desktop restart;
- same-session product-state continuity;
- second pause/resume cycle;
- reset preserving prepared dependencies while deleting product/audit state;
- second clean start with no npm/NuGet install/restore/bootstrap;
- final `NOT_STARTED / PREPARATION=READY`;
- no project created;
- Stage 1/Stage 2 not started.

Shared CODEX is now:
`HOLD / ENV_A_READY / WAIT_ENV_B_READY / NO_ACTIVE_EXPLORATORY_MISSION`.

Binding CODEX route:
`ROUTE-SEQUENTIAL-CODEX-ENV-A-READY-HOLD-FOR-ENV-B-73`
control commit `d3a2b643a1cd81bd2fbaf2f5197e4cd3769a361b`.

Preview control rev 0013:
`af6c911623e93ca868176c7b5597b04a9e303f0f`.

For ENV_B Main created a dedicated exact branch from the accepted harness:

`preview/w15-first-project-env-b-codespace`
at `bb451fa6e07982ac12384895f6097d5833761d16`.

ENV_B runbook updated:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-CODESPACE-ENV-B-RUNBOOK.md`
commit `1ccc1be58d6d7231da4800d47fe5d165d5e874c6`.

Current next gate:
create/open one **fresh Codespace** from `preview/w15-first-project-env-b-codespace` and prove:
- automatic API/Web/database startup;
- 5173 PUBLIC automatically;
- 5080/5432 not public;
- truthful first-run/no-project state;
- normal stop/resume recovers product automatically;
- 5173 is re-asserted PUBLIC after resume;
- no terminal/Ports-panel recovery needed.

Human Preview and CODEX Stage 1 remain HOLD until ENV_B is READY.

Stage 2 remains `PREPARED / NOT ACTIVE`.

---

# LATEST DELTA — 2026-09-27 — ENV_A REPLACEMENT ACCEPTED FOR FINAL READINESS PROOF

> This delta supersedes the ENV_A active-route/candidate wording in the checkpoint immediately below when they conflict. GitHub live remains the sole authority.

Main accepted the new exact Preview harness candidate for **final readiness proof only**:

- harness branch: `preview/w15-first-project-env-harness`;
- exact SHA: `bb451fa6e07982ac12384895f6097d5833761d16`;
- exact tree: `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- exact product base remains `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

The replacement separates dependency preparation from product/audit reset:
- explicit provenance-bound `prepare`;
- Node/NuGet dependency volumes are external, hash-keyed and provenance-labeled;
- tool image/version inputs are pinned;
- `start` / `resume` require prepared provenance and use `--no-build --pull never`;
- `reset` removes product/audit state while preserving prepared dependencies/tool image;
- stale/mismatched preparation fails closed.

Local HTTPS inspection is handled only by explicit opt-in during `prepare`:
- the selected certificate must already be a valid Windows trusted root;
- only its public PEM is mounted read-only into the preparation container;
- TLS verification remains enabled;
- no Windows/Docker/product trust store is changed;
- the root is not carried into product `start`/`resume`;
- thumbprint/DER SHA-256 are recorded in local preparation provenance.

Exact-candidate preparation already passed:
- tool image build: PASS;
- npm `ci`: PASS;
- dotnet restore: PASS;
- offline provenance/marker verification: PASS;
- second prepare: `READY_REUSED`;
- current preparation state at handoff: `NOT_STARTED / PREPARATION=READY`.

Binding shared CODEX route:
`ROUTE-SEQUENTIAL-CODEX-ENV-A-FINAL-EXACT-SHA-READINESS-PROOF-72`

Shared control rev 0087:
`7de960c87c7f09d4fed65f0c508cdc4b7a3988b0`.

Preview control rev 0012:
`08471efc2716fd5065bddb279a20d9f48c89e8b8`.

CODEX must now prove on exact `bb451fa...`:
fresh UI -> minimal Administrator -> pause -> real Docker Desktop restart -> same-session resume -> second pause/resume -> reset preserving `PREPARATION=READY` -> second clean start with **no npm/NuGet network/install/restore** -> final reset.

ENV_A remains:
`FINAL_READINESS_PROOF_ACTIVE / NOT_READY / BLACKBOX_HOLD`.

ENV_B remains:
`STATIC_ACCEPTED / WAIT_REAL_CODESPACE_PROOF / HUMAN_PREVIEW_NOT_RELEASED`.

ENV_A Stage 2 remains:
`PREPARED / NOT ACTIVE`.

Durable ledger: Issue #305 comment `5856654198`.

---

# LIVE WAVE 15 PREVIEW HANDOFF — 2026-09-27

> **READ THIS SECTION FIRST.** It supersedes older current-state wording later in this file when there is a conflict. GitHub live remains the sole authority.

GitHub live was revalidated before this documentation refresh.

- integration branch coordination tip before this refresh: `22fad82c3da98588d98051bd2ceb608da64ff8f3`;
- exact integrated **product checkpoint** remains `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`;
- exact product tree remains `5e5fce8ce87f31dfc11b83bb68ff86c67f9f0112`;
- the integration tip above is one coordination-document commit beyond the product checkpoint; no later product/test/workflow bytes redefine the Preview product base;
- broader feature T2 is formally accepted: `W15-FOUR-FEATURE-INTEGRATED-T2-01 -> PASS / ACCEPTED`;
- exact broad evidence: EliteSCADA CI #1584 / run `36290910850` — SUCCESS across Backend build/full tests/Runtime smoke, Web build and Chromium end-to-end.

Final six-lane disposition:
- Script Engineering: `T2_VERIFIED`;
- Editor: `T2_VERIFIED`;
- Authority UX: `INTEGRATED / VERIFIED_COMPLETE`;
- Licensing UX: `INTEGRATED / VERIFIED_COMPLETE`;
- FND-05: `VERIFIED / FROZEN`;
- FND-07: `VERIFIED / FROZEN`.


## Current phase

Wave 15 product/Foundation implementation is closed at the accepted product checkpoint. Current work is **fresh-install Preview environment readiness**, not feature development.

Preview topology:
- ENV_A = CODEX local isolated containerized audit environment;
- ENV_B = Product Owner fresh independent GitHub Codespace;
- exploratory journeys may run independently/in parallel only after their environments are READY;
- detailed CODEX findings remain embargoed until the Product Owner human journey completes.

## ENV_A binding state

Harness branch:
`preview/w15-first-project-env-harness`.

Last reviewed candidate:
`ec050e9bfda121805b1165860a4aeda0eb2582e8`
tree `0d6221540c3678a8b042c51082f7a2ed0a466fa2`.

Daemon continuity is proven:
first Administrator via UI -> pause -> real Docker Desktop restart -> resume same session/state -> second pause/resume -> reset.

Current blocker is harness-only:
`ENV_A_HARNESS_DEFECT / AUDIT_RESET_DEPENDENCY_BOOTSTRAP_COUPLING`.

After reset, dependency volumes were gone, so a fresh start attempted `npm install` and external TLS validation failed with `UNABLE_TO_VERIFY_LEAF_SIGNATURE`.

Active shared CODEX order:
`ROUTE-SEQUENTIAL-CODEX-ENV-A-DEPENDENCY-BOUNDARY-FIX-71`
rev 0086 / commit `574011f722bb2234c7a1586fc8d62739892f716a`.

Authoritative Preview control:
rev 0011 / commit `44023a9cf9e29c38d9b0e62f72198fa89e242018`.

Required fix:
separate provenance-bound dependency/tool preparation from destructive product/audit reset. No TLS weakening.

ENV_A:
`BLACKBOX_HOLD / NOT_READY`.

## ENV_B binding state

Runbook:
`docs/WAVE15-FIRST-PROJECT-CODESPACE-ENV-B-RUNBOOK.md`
on `coord/w15-fresh-install-preview-control`.

Required behavior:
- fresh Codespace;
- automatic product/database startup;
- `5173` PUBLIC automatically;
- `5080` + `5432` private/internal;
- same automatic recovery/public visibility after Codespace stop/resume;
- no pre-seeded first-project state.

ENV_B still requires a real Codespace lifecycle proof. Human Preview is not released.

## Prepared second CODEX verification stage

`docs/WAVE15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION.md`
commit `ce7a4b9a26cc5cdf36d466712eef62ab061d978f`.

State:
`PREPARED / NOT ACTIVE`.

It starts only after black-box Stage 1 is complete/sealed and a derived checkpoint of the CODEX-created project exists.

## Immediate coordinator action

1. Revalidate GitHub live.
2. Read the live shared CODEX control and Preview control.
3. Inspect whether CODEX returned a replacement harness for order `...DEPENDENCY-BOUNDARY-FIX-71`.
4. If yes, independently review exact diff/head/tree and evidence.
5. Do not release black-box Stage 1 until reset->fresh-start works without runtime dependency downloads and final clean state is proven.
6. Re-review ENV_B static impact if shared Compose/devcontainer/startup files changed.
7. Then execute real Codespace lifecycle proof.
8. Stage 2 remains PREPARED / NOT ACTIVE.

Do not resume historical lane routes. No feature DEV/FND mission is active.

---

# Current Coordinator Handoff — Wave 15

> GitHub live is the authority. Canonical operational handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.

## Stable Foundation state

- Wave 15 — ACTIVE.
- FND-01 — VERIFIED/FROZEN.
- FND-02 incl. AUTH-04 — VERIFIED/FROZEN.
- FND-08 common timing/WAN contract — VERIFIED/FROZEN.
- FND-03 global — VERIFIED/FROZEN.
- INFRA-CI-01A — VERIFIED/FROZEN.
- FND-04 Script TAG Reference Resolution — VERIFIED/FROZEN.
  - exact product checkpoint: `6c810647c9773a19b212d9c33694780141786ac7`
  - tree: `1221ff55963052be4e924dd644efbaa65763f546`
  - exact post-merge EliteSCADA CI `35913456486` — SUCCESS
  - Web `107358858133`, Backend/test/smoke `107358858405`, Chromium `107359503423` — SUCCESS
  - frozen control commit: `0453428521b28e951aa8e9d01742ee58b09f6690`

## Current active mission

FND-06 is **ACTIVE / NOT INTEGRATED** and is the only remaining FC0-A blocker.

- order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- exact product base: `6c810647c9773a19b212d9c33694780141786ac7`
- base tree: `1221ff55963052be4e924dd644efbaa65763f546`
- work branch: `work/w15-fnd-06-visual-stability-foundation`
- target: `wave15/corrections-integration`
- control branch/file: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
- active control commit: `8d1f415bc16b556e8133e6a1da1ab89881e7f189`
- validation profile: `UI_EDITOR, RUNTIME_RENDERER`

Latest revalidation: work branch is still identical to the exact product base; no FND-06 PR/candidate/handoff exists yet.

Frozen FND-06 scope:
- centralized known-legacy visual compatibility before strict schema consumers;
- Screen/Popup selection stability;
- canonical renderer/public-model single authority;
- selected Screen + Popup-stack persistence across retryable projection failure;
- deliberate reset on real Active identity change;
- Working-design vs Active Runtime authority separation.

Full single-canvas WYSIWYG remains downstream DEV-EDITOR scope.

## FC0-A preparation

FC0-A state: **PREPARED / NOT RELEASED / BLOCKED ON FND-06 + POST-FND06 FOUNDATION AUDIT**.

Prepared release file:
`coord/w15-fnd06-control:docs/WAVE15-FC0-A-RELEASE-PREP.md`

Latest prep commit:
`dfae2377f0c6802da726650697040181c7b0f453`

Reserved downstream orders:
- `DEV-EDITOR-FC0A-01`
- `DEV-SCRIPT-ENGINEERING-FC0A-01`
- `DEV-AUTHORITY-UX-FC0A-01`
- `DEV-LICENSING-UX-FC0A-01`

No downstream work branch is created until Main records the final exact `FC0_A_INTEGRATION_SHA` and tree after FND-06 freeze.

## Later Foundation controls — prepared only

### FND-05

- state: PREPARED / NOT ACTIVE
- reserved order: `FND05-CODEX-HA-AUTHORITY-V1`
- control: `coord/w15-fnd05-control:docs/WAVE15-FND05-CONTROL.md`
- prep commit: `66d0f0e53a9842c719cfe28ef40da3b7fbdd1f6f`

Prepared around one server-owned Cluster/Node/effective-Active/fencing contract, manual break-before-make transfer first, Runtime Session Lease continuity and no client/Driver election.

### FND-07

- state: PREPARED / NOT ACTIVE
- reserved order: `FND07-CODEX-DETACH-NEUTRAL-V1`
- control: `coord/w15-fnd07-control:docs/WAVE15-FND07-CONTROL.md`
- prep commit: `760e1cb57a1bdb1e146a6326bd55f713c7790add`

Prepared around secure populated-install Application+Authority detach, Runtime/process-effect fencing, old-session invalidation, neutral bootstrap, explicit license keep/remove/replace and no silent Historian deletion.

Neither later Foundation is authorized to mutate product while FND-06 owns the sequential high-risk Foundation executor.

## Current guards

- product changes only through isolated branch/PR; no direct feature writes to integration/main;
- exact-SHA evidence and natural Wave 15 T1 required;
- red CI diagnosed before rerun;
- downstream lanes consume frozen Foundation contracts and stop with `BLOCKED-CONTRACT` if a contract change is required;
- no force push/destructive rebase/evidence deletion;
- no Product Owner message relay between agents; agents read GitHub live control/ledger.

Primary ledger: Issue #305.


## FND-06 corrected execution metadata

- active order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- validation profile: `UI_EDITOR, RUNTIME_RENDERER`
- known legacy set: `tank | value | dynamo | status`
- `status` is compatibility-only unless a lossless migration is separately proven; no alias guessing
- control commit: `8d1f415bc16b556e8133e6a1da1ab89881e7f189`


## FC0-A collision guard

The prepared downstream release package now includes a Main-owned parallel-file collision map. Primary ownership is separated across Editor (`engineering/visual-editor/**`), Script Engineering (`engineering/scripts/**` + `python-editor/**`), Authority UX (`UserAdministration*`) and Licensing UX (`web/licensing/**` + `Scada.LicenseGenerator/**`). Shared shell/router/types/i18n/CI files are Main-coordinated hotspots, not free-for-all lane ownership.

Latest FC0-A prep commit: `dfae2377f0c6802da726650697040181c7b0f453`.


## Same sequential CODEX routing

The CODEX chat/lane that executed prior Foundation work including FND-04 is the active FND-06 executor.

- FND-04 old control no longer means executor WAIT.
- FND-04 control rev 0016 routes that same CODEX through `ROUTE-SEQUENTIAL-CODEX-TO-FND06-12`.
- routed destination: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
- active FND-06 order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- FND-06 control commit: `8d1f415bc16b556e8133e6a1da1ab89881e7f189`

On `SIGA`, that CODEX should execute FND-06, not report FND-04 frozen/wait.

## FC0-A release gate update

After FND-06 freeze, FC0-A still waits for `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`.

The audit must close the Wave14->Wave15 premise/gap matrix and prove FND-05/FND-07 are non-breaking to frozen contracts consumed by the first four DEVs.

Only after audit `ACCEPTABLE / FC0A_RELEASE_APPROVED` may Main activate:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05;
- FND-07.

FND-05 and FND-07 remain PREPARED / NOT ACTIVE until then.


## FND-06 Main review — mounted legacy closeout

Current PR #337 candidate:
- head `923543705378016090e7067b35954795a9591a57`
- tree `5657cee7169a4e77370d416add4efcf07184d7c0`
- natural T1 `35931139983` — SUCCESS
- 9 changed files, all inside FND-06 allowlist
- architecture/scope accepted by Main.

Remaining gate before merge:
- original Wave 14 A7 was a mounted Screen/Popup selection crash that blanked/poisoned Engineering;
- candidate currently proves model/helper compatibility but lacks the required mounted Screen + Popup persisted-legacy selection regression;
- active order: `FND06-CODEX-WAIT-POSTMERGE-V4`;
- control revision `0005`;
- control commit `8d1f415bc16b556e8133e6a1da1ab89881e7f189`;
- preferred delta: tests only; minimal production fix only if the mounted scenario exposes a remaining defect.

The same sequential CODEX lane remains the executor. No merge/freeze yet.


## PR #337 contract-risk snapshot for post-FND06 FC0-A gate

Post-FND06 audit control:
- audit: `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`
- audit rev: `0002`
- latest audit-control commit: `9e75fd836d03dc34619f72ba5056e3aa874cfdbb`

PR #337 pre-freeze evidence:
- candidate `923543705378016090e7067b35954795a9591a57`
- tree `5657cee7169a4e77370d416add4efcf07184d7c0`
- natural T1 `35931139983` SUCCESS
- no Security/Authority/Licensing/Script/lifecycle contract change in its 9-file visual/editor/runtime delta
- FND-06 still HOLD pending mounted A7 closeout; this is an evidence gate, not a known contract break.

Current FC0-A contract-risk classification:
- DEV-EDITOR: `LOW / GUARDED` — must consume frozen FND-06 compatibility/renderer authority; mounted A7 proof still pending.
- DEV-SCRIPT-ENGINEERING: `NONE IDENTIFIED / GUARDED` — FND-04 remains untouched; FND-05 may only fence execution authority around it.
- DEV-AUTHORITY-UX: `NONE IDENTIFIED / GUARDED` — FND-07 must compose FND-02/AUTH-04, not redefine it.
- DEV-LICENSING-UX: `LOW BUT MATERIAL RESIDUAL` — FND-05 redundancy entitlement/readiness must remain additive/backward-compatible to frozen FND-03 semantics.

FND-05 hard guard:
- control rev `0003`
- commit `85502eaaa3a27fc2c050396b3f40c3e08943ae37`
- existing FND-03 license validity/meaning, Interactive/ViewOnly/session quota semantics, machine binding and install/replace/remove behavior may not be reinterpreted by HA.
- if HA needs such a breaking change -> `BLOCKED-CONTRACT` before FC0-A DEV release.

FC0-A release prep snapshot commit:
`c0e4bd69c89c47efc7dc936a6ac9e61cbbbd8f96`.

This is pre-freeze risk assessment only; final release still requires exact FND-06 freeze + independent post-FND06 audit PASS.


## FND-06 merged checkpoint pending freeze

PR #337 is merged.

- candidate: `2257f8f99b5e6deac80d64ed2cc0c43aa8dab1cc`
- candidate tree: `2ebb839a788bb4fad249877689c25ac1b18f6d74`
- merge SHA: `624f2eca456310a2c6156538b3616a06e3be075f`
- merge tree: `fb864fb954b0123e69db379cd6b3120349b43600`
- candidate T1 `35939646387`: SUCCESS
- post-merge broad CI `35940661531` / #1563: PENDING/IN PROGRESS at this record
- FND-06 state: **INTEGRATED / POST-MERGE CI PENDING / NOT YET VERIFIED-FROZEN**
- CODEX: `FND06-CODEX-WAIT-POSTMERGE-V4 / NO_MUTATION`
- FND-06 control rev `0006`, commit `8d1f415bc16b556e8133e6a1da1ab89881e7f189`

Post-FND06 audit remains blocking and is preloaded with this exact checkpoint:
- `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`
- audit rev `0003`
- audit-control commit `a9ed4003acf8c71db035bc854a75f87cf97273fb`
- state `PREPARED / WAIT_FND06_POST_MERGE_CI_GREEN`

No FC0-A DEV, FND-05 or FND-07 release until FND-06 freezes and the independent audit returns `ACCEPTABLE / FC0A_RELEASE_APPROVED`.


## Current blocker — INFRA-CI-01B

FND-06 product work is merged and Main-accepted, but not frozen.

- FND-06 merge: `624f2eca456310a2c6156538b3616a06e3be075f`
- post-merge CI `35940661531`: FAILURE
- failure: PostgreSQL `23505 pg_namespace_nspname_index` during shared-schema initialization
- FND-06 causality: not established; failing source/test blobs unchanged by FND-06
- historical same-signature Wave 14 evidence exists
- no blind rerun authorized.

Active sequential CODEX mission:
- `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V2`
- control: `coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`
- control commit: `be7a2d875c24e07d023162e64c65acb0aebe9672`
- work: `work/w15-infra-ci-01b-postgresql-schema-init`
- exact base: `624f2eca456310a2c6156538b3616a06e3be075f`.

FND-06 control rev `0007` routes this same CODEX to INFRA-CI-01B.
FND-04 legacy routing control rev `0019` also routes directly to INFRA-CI-01B.

FC0-A and independent audit are not released until:
1. INFRA-CI-01B correction is reviewed/merged;
2. exact broad integration CI is green;
3. FND-06 is declared VERIFIED/FROZEN;
4. post-FND06 audit returns ACCEPTABLE / FC0A_RELEASE_APPROVED.


## INFRA-CI-01B V2 widened only for shared-schema DDL sequencing

Intermediate PR #338 head `97c665c8e4d62268336dfdef400f992c2f9d43cf` remains unmerged.

Full local concurrency RED exposed additional creators:
- PostgreSqlAuthorityPolicyStore
- PostgreSqlAuthorityLifecycleStore
- PostgreSqlRuntimeSessionLeaseStore

Current order is `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V2` at control commit `be7a2d875c24e07d023162e64c65acb0aebe9672`.

Only initialization lock sequencing is authorized in those files. No Authority, session, quota, admission, fencing or licensing semantics may change.


## Current active order — FND-06 E2E fixture isolation

Broad run `35944510920` proved INFRA-CI-01B backend correction green but exposed a test-only FND-06 fixture leak.

Active order:
`FND06-CODEX-E2E-FIXTURE-ISOLATION-V6`

Exact base:
`eb4563cf0060449b479c4335ef30a19ed65e35ab`

Work:
`work/w15-fnd06-e2e-fixture-isolation`

Only authorized mutation:
`web/scada-web/tests-e2e/fnd06-mounted-legacy-selection.spec.ts`

Do not weaken `runtime.spec.ts`, change product code, import semantics, Playwright workers/order/retries or CI workflow.

Required strategy: temporarily update existing canonical Screen/Popup identities, then restore those same identities in finally; prove no fnd06 fixture remains.

INFRA-CI-01B is merged/Main-accepted and requires no further mutation.
FC0-A audit remains PREPARED / blocked.


## Pre-audit finding — W15-P1-01 Server Script recovery

While preparing the mandatory post-FND06 FC0-A audit, Main revalidated the current Server Script runtime against the final Wave 14 backlog contract.

Current source still shows:
- `ScriptRuntimeExecutionCoordinator.ProcessNextAsync` returns `Throttled` while diagnostics `IsThrottled` is true;
- the only coordinator exit is explicit `ResetThrottle()`;
- repository search found no production automatic Server Script caller of `ResetThrottle()`;
- existing tests prove timeout -> throttled behavior, not bounded cooldown/half-open/probe recovery.

Wave 14/W15-P1-01 explicitly required bounded automatic recovery and rejected a permanent silent throttle latch.

Preliminary disposition:
`W15-P1-01 = PRELIMINARY BLOCKED_FOUNDATION`

This finding is independent of FND-06 and does not prevent FND-06 from becoming VERIFIED/FROZEN if exact broad CI #1565 is green. It **does** prevent immediate FC0-A release if the independent audit confirms it.

Audit evidence:
- `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-EVIDENCE.md`
- preliminary matrix commit `3483faf3aab4940791807b07b4453865629d9077`
- audit control rev 0008 / commit `18a0fcda2b354779cdf0f1ba4d829a838714b3d2`.

DEV-SCRIPT-ENGINEERING may not absorb this runtime/Foundation correction silently.


## Independent AUD routing prepared

The former FND-04 independent AUD lane is now pre-routed, but not activated, for the mandatory post-FND06 closure audit.

- current AUD order: `FC0A-AUD-WAIT-FND06-FINAL-BROAD-0012`
- state: `WAIT_FND06_FINAL_BROAD`
- final broad: `35953557122`
- provisional exact product checkpoint: `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- FND-04 AUD control rev 0023 / `1be8ef8cbdd555f3fa554e905faafab186c21dd3`
- next audit: `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

AUD must independently confirm/disprove Main's preliminary P1-01 Server Script recovery blocker after activation.


## Pre-audit finding — W15-P1-06 truthful Engineering fallback

Exact source inspected at the final FND-06 product checkpoint `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`:

`web/scada-web/src/engineering/EngineeringApp.tsx`

Observed:
- `snapshot` initializes as null while loading;
- failed load sets/keeps `snapshot=null` and renders an error/retry state;
- the sidebar project chip nevertheless renders `snapshot?.workspace.projectName ?? snapshot?.workspace.projectKey ?? 'Demo Project'`;
- therefore an absent/unloaded public model can still be displayed as `Demo Project`;
- no-model WorkspaceBar fallbacks can also present `unsaved` / `clean` despite no authoritative Working model.

This matches the Wave14 A6 / W15-P1-06 class that required truthful loading/unavailable/error identity rather than a fictitious Working project.

Preliminary disposition:
`W15-P1-06 = PRELIMINARY BLOCKED_PRODUCT / SHARED_ENGINEERING_SHELL`

This is not causal to FND-06 and does not block its freeze if broad #1565 is green. If independent audit confirms it, FC0-A remains blocked until a bounded shared-shell correction is integrated/revalidated.

Audit control rev 0009:
`888473cbf27e003d659ec3dd87de2f0f89240474`

Evidence matrix:
`a5260ca309742894ee48e8cce17d9175d67cda08`.


## Audit blocker correction preparation

Coordination-only preparation exists for the two preliminary blockers. It does **not** authorize product mutation:

`coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-BLOCKER-CORRECTION-PREP.md`

prep commit:
`5904924faf7dcc13ec42c495ff54fe6ef82ad005`

Prepared, inactive orders:
- `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`
- `FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`

Activation is forbidden until independent audit confirms the corresponding finding on the exact frozen post-FND06 checkpoint.

P1-01 plan preserves FND-04 Script TAG semantics, sandbox isolation, bounded queue/coalescing and Active revision safety while replacing a permanent latch only if confirmed.

P1-06 plan preserves lifecycle/Authority/FND-06 contracts while removing fictitious no-snapshot project/status state and distinguishing transport rejection from actual HTTP response only if confirmed.


## FND-06 final freeze / FC0-A audit active

FND-06 is now **VERIFIED/FROZEN**.

Exact product checkpoint:
- SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`;
- tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- broad `35953557122` / EliteSCADA CI #1565 — **SUCCESS**;
- Web SUCCESS;
- Backend build/test/smoke SUCCESS;
- Chromium end-to-end SUCCESS.

Main verified that divergence above the product checkpoint was coordination-doc-only before freeze.

FND-06 control:
- rev 0012;
- commit `44c372d8316733398d25f72f3331b12022ab6594`;
- CODEX order `FND06-CODEX-FROZEN-FINAL-08 / NO_MUTATION`.

Mandatory independent post-FND06 audit is now **ACTIVE**:
- audit `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`;
- audit rev 0010 / `72421abee84ac09415a045b7c86d554dba7dd187`;
- AUD lane rev 0024 / `0048a198a2c7d2ac40dd1a055c5bcc6346f30fe8`;
- order `FC0A-AUD-ACTIVE-POST-FND06-0013`.

Main preliminary P1-01 and P1-06 findings remain hypotheses until independent AUD disposition.

No FC0-A DEV, FND-05 or FND-07 release yet.


## Main-owned FC0-A audit result / active P1-01 correction

Audit owner is Main Coordinator.

Final result:
`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — CHANGES_REQUIRED`

Audit report:
- `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-RESULT.md`
- commit `463d357f8a9c11f774f5da59480e4a17a19569f5`.

Confirmed blockers:
1. W15-P1-01 Server Script bounded recovery missing;
2. W15-P1-06 truthful Engineering fallback missing.

FND-05 and FND-07 are contract-compatible under their current hard guards; no breaking contract delta is required now.

Active correction:
- order `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`;
- branch `work/w15-fc0a-p101-server-script-recovery`;
- exact base `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`;
- profile `SCRIPT_RUNTIME`;
- control commit `b8858d08a8516588db4be40d2da48ea266fe793e`;
- CODEX routing rev 0026 / `a088c884d90bd0c2d86b844f74332306a80b7a7c`.

Queued after P1-01:
`FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`.

DEV-EDITOR / DEV-SCRIPT-ENGINEERING / DEV-AUTHORITY-UX / DEV-LICENSING-UX / FND-05 / FND-07 remain HOLD.


## Post-FND06 audit — second-pass deep review

Main completed a distinct second-pass audit on the frozen baseline:
`560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`.

Report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-SECOND-PASS.md`

report commit:
`43c266949236af377ba859c9b8f09fa2d3a3a31a`

audit control rev 0012:
`62a731cc4291b751e9cb2e91e440fcf0d5ffb3bd`

release-prep refinement:
`c78895b218608b511e61b90206189d2b641a1e71`

Second-pass conclusion:
`NO NEW PRE-FC0A BLOCKER IDENTIFIED`.

The two confirmed release blockers remain:
- W15-P1-01 Server Script bounded recovery;
- W15-P1-06 truthful Engineering no-model/loading/error state.

New/refined downstream findings:
- DEV-EDITOR must consume FND-06 compatibility for known-legacy marquee/geometry/z-order/multi-object authoring paths that still use strict built-in lookup;
- DEV-SCRIPT must make Script Assistant consume the FND-06 compatibility seam for known-legacy property discovery;
- Script Engineering already has cursor-aware insertion and timer/tagChanged authoring, so those are regression/refinement scope, not greenfield;
- Python API Help still lacks a formal signature/parameter/return/example contract;
- DEV-LICENSING must surface requested vs granted Runtime class, explicit ViewOnly request and Interactive-quota fallback/reason UX; backend Authority/capacity contract is already sound;
- one minor Runtime API message still says `viewer` where canonical public vocabulary is `viewOnly`;
- W15-P2-01 Trends still lacks explicit realtime/reconnect/last-request/last-success/freshness observability;
- W15-P2-02 shared Popup/live-value path correctly handles numeric zero/quality but still lacks explicit freshness-age/reason telemetry.

Contract result strengthened:
- FND-05 remains ADDITIVE / COMPATIBLE;
- FND-07 remains COMPOSITIONAL / COMPATIBLE;
- production host uses `EngineeringWorkspace(seedDemo:false)`, so a truthful neutral no-Demo workspace is already representable;
- existing Authority detach/attach/switch primitives further reduce FND-07 contract risk.

No DEV/FND-05/FND-07 release occurs until P1-01 and P1-06 close and Main reruns the affected release rows.


## Main Coordinator takeover — third post-FND06 audit pass (2026-09-24)

Live takeover revalidated against GitHub after coordinator-chat rotation.

- frozen product checkpoint remains `560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`;
- post-FND06 FC0-A audit remains `CHANGES_REQUIRED`;
- third Main pass outcome: `NO NEW PRE-FC0A BLOCKER IDENTIFIED`;
- authoritative third-pass report: `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-THIRD-PASS.md`, commit `b144de748337045bf447a5142d825fa0beac1ff1`;
- audit control rev 0013: `0d27fe59d154c161418c996a4ea2df6fe59066e6`;
- confirmed release blockers remain only W15-P1-01 and W15-P1-06;
- active order remains `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1` on `work/w15-fc0a-p101-server-script-recovery`;
- work branch revalidated identical to exact base: 0 ahead / 0 behind / no changed files / no PR;
- P1-06 remains queued separately; it must not be mixed into P1-01;
- DEV-EDITOR, DEV-SCRIPT-ENGINEERING, DEV-AUTHORITY-UX, DEV-LICENSING-UX, FND-05 and FND-07 remain HOLD.

Third-pass refinements are downstream/pre-activation only: P2-03 shell responsiveness remains open; P2-04 account accessibility is substantially implemented but needs focused keyboard regression; P2-05/P2-06 remain Engineering layout-density residuals; P2-07 is partially implemented and should not rebuild the existing Dynamo insertion preview; Help routing is surface-level rather than Engineering-section-contextual; FND-07 gained an explicit Engineering Lock × detach/switch/neutral-bootstrap acceptance guard. FND-05 remains additive/compatible; FND-07 remains compositional/compatible.

FND control refreshes:
- FND-05 hold/activation metadata: `0c69dd307880b6afcd89f352cf76fef183087410`;
- FND-07 hold metadata + Lock/detach acceptance guard: `d0aba9e375a8330b7720b32f4defcdabf0ec12ae`.

No product code, product branch or release state was changed by this audit pass.


## Main decision — consolidated FC0-A correction package (2026-09-24)

Product Owner requested that all known FC0-A findings be corrected now where safely possible, with one larger CODEX delivery before the next Main review.

Active order:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V2`
- control: `coord/w15-fnd06-control:docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`
- control commit: `0ab4a2f9226a1f3710aa516dff9534d880023218`
- branch: `work/w15-fc0a-consolidated-corrections`
- exact base: `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- exact base tree: `674019fbbc21001a2d68deb853c2c0b293e0a5cb`
- one consolidated PR only after the package is complete and exact-head validation is green.

The former P1-01-only order/branch is superseded unused; it was identical to base with no PR at supersession.

The package includes the two mandatory blockers plus confirmed shared/downstream residuals that can be safely brought forward: shell responsiveness, account keyboard regression, Engineering scroll composition, Engineering Lock density, Trends/live-value freshness observability, contextual Help routing, canonical viewOnly wording, frozen FND-06 compatibility consumption in Editor/Script Assistant, structured Script API Help/representative recipe, Runtime Session/Licensing requested/granted UX, and Template/Equipment/Library inspection.

Evidence-bounded/speculative items and future Foundations remain outside the package. Frozen FND-01/02/03/04/06/08 semantics may not be redefined.

Sequential CODEX route updated to rev 0027 / commit `93d79a773d7571677a9f43e6f3a80ad21aa25612`.
No intermediate Main review is required; CODEX returns one final candidate handoff unless a real contract/base/environment blocker prevents safe completion.


## Main decision — six prepared parallel DEV chats / CODEX as sequential validator (2026-09-24)

Product Owner clarified that the previous `normally no more than four active coding DEVs` rule was a historical operational throttle for a different context and is **not** a permanent Wave 15 concurrency limit.

Prepared post-FC0A implementation chats:
- DEV-EDITOR;
- DEV-SCRIPT-ENGINEERING;
- DEV-AUTHORITY-UX;
- DEV-LICENSING-UX;
- FND-05 DEV;
- FND-07 DEV.

Canonical cross-lane coordination:
- branch: `coord/w15-parallel-dev-control`
- file: `docs/WAVE15-PARALLEL-DEV-CONTROL.md`
- prepared control commit: `87479d0bc3042435935ac1dbaa119d3a7ed72bd6`

All six are **PREPARED / BLOCKED** until Main records `FC0A_RELEASE_APPROVED` on an exact integrated SHA/tree. No work branch is created before that exact activation base is known.

Execution pipeline:
`normal DEV chat implements code -> Main reviews -> same DEV corrects material defects -> Main accepts exact candidate for CODEX -> sequential scarce CODEX writes/extends tests, runs focused/adversarial local validation and exact-head T1 -> Main finalizes integration`.

CODEX may make small validation-driven corrections that do not redesign the feature. Material product/design defects return to the owning DEV. Frozen-contract insufficiency returns to Main.

Feature DEV results stay in **separate PRs**; they are not combined into a raw monolithic implementation package. After individually accepted/T1-green merges, Main runs broader integrated T2 on the exact integration head.

FND-05/FND-07 also move to normal-chat DEV implementation:
- FND-05 prepared order: `FND05-DEV-HA-AUTHORITY-V1`; dedicated control rev 0004 / commit `f3f685cff16ebfef53db4aa69b061d7bac773829`.
- FND-07 prepared order: `FND07-DEV-DETACH-NEUTRAL-V1`; dedicated control rev 0003 / commit `ae92f5f1ec55464eea7f231c178d0a05dc04df13`.

Foundation validation remains stricter:
`DEV -> Main contract review -> CODEX adversarial/focused validation -> exact-head T1 -> Main merge -> post-merge validation -> VERIFIED/FROZEN`.

FND-05 additionally requires `CODEX_HA_ADVERSARIAL_GREEN`.

Release preparation was updated:
- `coord/w15-fnd06-control:docs/WAVE15-FC0-A-RELEASE-PREP.md`
- commit `349ed55e74b237c77dec64971a7f8dac3ee97c7e`.

ROADMAP current coordination update:
- commit `08b11987b78adee134de77906fd57b2af199e5fb`.

Current FC0-A product work remains PR #340 / V2 IN PROGRESS. This coordination preparation does not activate any downstream lane and does not alter the current CODEX mission.

Bootstrap texts for each lane will be generated on Product Owner request; the bootstrap is only onboarding convenience and GitHub live controls remain authoritative.


## Main decision — two-stage fresh-install partial preview (2026-09-24)

After the four feature DEV lanes are integrated/T2-verified and FND-05/FND-07 are independently VERIFIED/FROZEN, Main will run a partial first-project product audit before later EEE/complete-product acceptance.

Prepared control:
- branch: `coord/w15-fresh-install-preview-control`
- file: `docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`
- prepared commit: `8d209c8f0cacf5c1feb050632645c9537e1f09dc`

Prepared gates:
- `W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`
- `W15-FIRST-PROJECT-HUMAN-PREVIEW-01`

The two first-project journeys are independent.

CODEX moment:
- fresh installation / no project / no EEE / no hidden Demo project;
- user-like browser exploration;
- high-level objective only: create a first SCADA application from zero and reach a functional truthful Runtime;
- no source/control/database/internal API lookup during the black-box journey;
- no code correction during exploration;
- source/log/API diagnosis is allowed only after the journey completes or is blocked;
- detailed CODEX findings are persisted but not surfaced to Product Owner before the human preview.

Human moment:
- second independent clean environment;
- Product Owner repeats the same high-level first-project mission as a real user;
- no CODEX-created project;
- no detailed CODEX report/checklist before the unaided human journey completes;
- needing help or becoming blocked is itself audit evidence.

After both journeys:
- Main unseals and compares findings as BOTH / CODEX_ONLY / HUMAN_ONLY / PATH_DIVERGENCE / NOT_REPRODUCED;
- a directed follow-up may then cover restart/persistence, Authority/ViewOnly, FND-07 detach -> neutral bootstrap -> Project B -> B/A switching, and a separate HA user-surface/manual transfer check;
- material blockers are corrected/owned; non-blocking onboarding/usability gaps may feed later Installation UX/product convergence.

T1/T2/T3/T4 green evidence does not replace these audits.

Possible partial-preview dispositions:
- `ACCEPTABLE_FOR_NEXT_CONVERGENCE`
- `CHANGES_REQUIRED`

Neither is final Wave 15 acceptance. EEE v15, later T3/T4 and final fresh complete-product Preview remain mandatory.

Roadmap update: `57729b9db0ebcac2c748a9f6f0101eb48c5b625b`.
Six-lane control link update: `e130b5353c320e9c22f8e1f8f3a7e42a2dd6946c`.

This preparation does not activate the preview now and does not alter current PR #340 / CODEX V2 execution.


## Main review — PR #340 V2 complete / narrow V3 closeout active (2026-09-24)

Main reviewed final V2 candidate:
- PR #340;
- head `150b808140a5fdf80afd0c88d46ea80f83f630b2`;
- tree `c2bfbbfe705be64e5c1aac314f96bf8851a913b0`;
- 15 commits / 41 changed files;
- natural Wave 15 T1 `36033152318`: SUCCESS across classification, Common T1 sanity, Focused .NET, Focused Chromium, Web semantic build and final gate.

Disposition:
`FC0-A CONSOLIDATED V2 -> MAIN COORDINATOR — CHANGES_REQUIRED / NARROW V3`

Accepted V2 closures remain preserved. Two bounded groups remain before merge:
1. actual user-facing Runtime Session Class request/status surface for ViewOnly/Interactive requested-vs-granted/reason truth without FND-03 redesign;
2. missing R6 mounted evidence for shell no-overflow, Engineering independent scroll, compact Engineering Lock lifecycle and known-legacy advanced-authoring/unknown containment.

Binding control:
- `coord/w15-fnd06-control:docs/WAVE15-FC0A-CONSOLIDATED-CORRECTION-PACKAGE.md`
- order `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V4`
- section 12
- commit `6a13c88fd112fe94e9c87e1206d9840be75161eb`.

Sequential CODEX route:
- rev 0030
- `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CONSOLIDATED-V3-18`
- commit `7011f7e2a29f37c105657b1a56ceac62a6ef4d58`.

PR #340 Main review comment: `5819091955`.
Issue #305 ledger comment: `5819092476`.

No merge/freeze/release occurred. All downstream six lanes remain PREPARED/HOLD until FC0-A final acceptance.


## Main review — PR #340 V3 product accepted / final evidence-only closeout active (2026-09-24)

Exact V3:
- head `1efc16994ea7b11857b0a1aac7da1690276e6d07`
- tree `da022f95a0c2342188a34ffe6dc830acf84d873a`
- natural T1 `36036316797`: SUCCESS.

Main accepts the V3 Runtime Session Class product surface and shell compact-width product direction.

Merge remains blocked only on final test/evidence closure:
- explicit Engineering scroll-composition proof;
- compact Engineering Lock lifecycle proof including lock-now + clear;
- known-legacy advanced-authoring + arbitrary-unknown containment proof;
- focused execution of owner specs that natural T1 did not select.

The T1 Chromium job on V3 executed 16 tests but did not select the Runtime Session mounted spec, app-shell, Engineering Lock or legacy owner-model specs, so its green result is not used as proof for those rows.

Binding control:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V5`
- section 13
- commit `1485bd3d832a57b109d347e0b931b21334d56844`

CODEX route:
- rev 0031
- `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CONSOLIDATED-V4-19`
- commit `63afda347e798d5e45292161fab382911093ae42`

This is test/validation-only. No new feature scope is authorized.

PR #340 Main comment: `5819317792`.
Issue #305 ledger: `5819318386`.

No merge/freeze/release occurred. Six downstream lanes remain PREPARED/HOLD.


## Main review — PR #340 V4 tests accepted / final execution proof active (2026-09-24)

Exact V4 head `87eafb68e4fea7815a26ccffeb8a070fae6564c8` is test-only and natural T1 `36037962003` is green.

Main accepts the added scroll/Lock/legacy tests directionally, but the T1 Chromium log did not execute those owner specs. It selected only python-runtime-host, runtime, script-engineering-workspace-contract, visual-editor-workspace and local-auth bootstrap.

Merge therefore remains blocked solely on execution proof.

Binding control:
- `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V6`
- section 14
- commit `9990d84750be61c86322951255a67ff22fb4a29d`.

CODEX route:
- rev 0032
- `ROUTE-SEQUENTIAL-CODEX-TO-FC0A-CONSOLIDATED-V5-20`
- commit `96ccc7d6287db9889940b6a57ed54cee115a74e5`.

Required work is validation-only:
- support multiple profile-owned E2E specs in Wave 15 router;
- make UI_EDITOR execute app-shell, Engineering Lock and visual-editor owner specs in addition to workspace;
- make RUNTIME_RENDERER execute runtime-session owner spec in addition to runtime;
- unit-test router mapping;
- strengthen Lock backend-state transition and arbitrary-unknown containment assertions;
- final exact-head natural T1 must visibly execute these owner specs.

Required return:
`FC0-A CONSOLIDATED CODEX -> MAIN COORDINATOR — FINAL INTEGRATION HANDOFF V5`.

No merge/freeze/release yet. Six downstream lanes remain PREPARED/HOLD.


## FC0-A merged / release blocked by INFRA-CI-01C recurrence (2026-09-24)

FC0-A consolidated PR #340 is merged.

Exact accepted product checkpoint:
- SHA `d975174ae81ff7ed754585097240778a9862d965`
- tree `5ac06f47f1bede7a1b0c384c7b1d3c83730015ea`
- pre-merge Wave 15 T1 `36043296814`: SUCCESS
- required Chromium owner suite: 54 passed.

The exact post-merge push gate `EliteSCADA CI 36044280802` is red:
- Web build SUCCESS;
- Backend build SUCCESS;
- Backend Test FAILURE;
- smoke/Chromium skipped downstream.

Only identified failure:
`PostgreSqlEngineeringSchemaV15CommunicationBindingTests.PostgreSqlRevision_SavePreviewApply_RoundTripsCommunicationBinding`
with PostgreSQL
`23505 / pg_namespace_nspname_index`
on concurrent
`CREATE SCHEMA IF NOT EXISTS elitescada`.

Main proved the affected Engineering store, shared-schema lock helper, Timescale infrastructure, failing test and existing concurrency regression are byte-identical to frozen pre-FC0A `560ac9d...`. Classification:

`GENERIC_INFRASTRUCTURE_RECURRENCE / NOT_FC0A_PRODUCT_CAUSAL`.

Do not reopen accepted PR #340 product work and do not use a blind rerun as release evidence.

Active blocker:
- `coord/w15-infra-ci-01c-control:docs/WAVE15-INFRA-CI-01C-POSTGRES-SCHEMA-RACE-RECURRENCE-CONTROL.md`
- order `INFRA-CI-01C-POSTGRES-SCHEMA-RECURRENCE-V1`
- control commit `8cf15e123823f5aaae2c911f0f113e804c9dad0d`
- work branch `work/w15-infra-ci-01c-postgres-schema-recurrence`
- exact base `d975174ae81ff7ed754585097240778a9862d965`.

Sequential CODEX route:
- rev 0033
- `ROUTE-SEQUENTIAL-CODEX-TO-INFRA-CI-01C-21`
- commit `57dbe4a29ad69483d0a06f1bc0b8dc2f8f908d6b`.

Current state:
`FC0-A -> POST_MERGE_VALIDATION_BLOCKED_BY_INFRA`.

No `FC0A_RELEASE_APPROVED` yet. All six prepared DEV/FND lanes remain WAIT. Their work branches must not be created/activated from a stale checkpoint; after 01C is integrated and the exact new post-merge gate is green, Main will record the final release base and create/activate them.


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


## Post-review CI diagnosis and sequential CODEX queue (2026-09-24)

GitHub live revalidation after the first six-lane Main review pass:

- Script Engineering PR #344 remains the **active sequential CODEX mission** on exact accepted head `cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`.
- Editor PR #349 remains `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT` on `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`, T1 `36066874097` SUCCESS.
- Authority PR #346 remains in DEV correction. Its repaired-metadata T1 `36067636970` reached real evidence and found a candidate-causal Web compile defect: TS2345 in `AuthorityPolicyAdministration.logic.ts` caused by a generic number lookup against a literal-key capability Map. That defect is now part of `DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`; unchanged-head rerun is forbidden.
- Licensing PR #345 remains in DEV correction. T1 `36067628680` had Web/Chromium/Common green and .NET 692/693. The only failure, `Adapter_OutOfOrderIFrameFaultsBeforePublishingAsdu`, is non-causal to Licensing: the exact test, IEC-104 adapter and sequence-state blobs are unchanged from FC0-A.
- Main proved the IEC-104 failure is an asynchronous **test observation race**: the test waits only for `ProtocolErrors >= 1`, while the adapter increments that counter before `SignalSessionFailure` writes `IsConnected=false` and increments session failures.
- Separate test-infrastructure closeout prepared:
  `coord/w15-infra-ci-01d-control:docs/WAVE15-INFRA-CI-01D-IEC104-FAULT-OBSERVATION-RACE-CONTROL.md`,
  commit `b23cefeaf78a58ea17eaba8cf9a1566f3095ada4`.
- INFRA-CI-01D is **PREPARED ONLY**. No work branch exists yet and no mutation is authorized.
- Shared sequential CODEX plan after Script handoff:
  1. activate/close INFRA-CI-01D from the then-current integration HEAD;
  2. route CODEX to Editor #349.
- This queue is planning only. The active route remains Script until Main publishes a new binding route.

Shared CODEX control rev 0038:
`coord/w15-fnd04-dev-aud-control:docs/WAVE15-FND04-DEV-AUD-CONTROL.md`,
commit `163b476252ed1a2459730b716b7dd921470c5903`.

Central six-lane control rev 0004:
`coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`,
commit `59223cf41f96f5f7bdfc0e6e0c44f8299fe76031`.


## MAIN COORDINATOR CHAT TRANSFER — 2026-09-24

This coordinator chat is being replaced because the current chat runtime became unreliable. GitHub live remains the sole authority.

### Exact live checkpoint at transfer

- integration branch: `wave15/corrections-integration`
- integration HEAD observed at transfer: `3cdb13ed27b0529265b2e92785c09df09c96af3f`
- FC0-A released product base: `e3ed5138369c576549cb58a7aff9783792f322d3`
- FC0-A tree: `4e7627774fbfc111344e3d80fcb9d921eed8377e`
- release gate: EliteSCADA CI #1569 / `36060017969` / SUCCESS / Chromium 655 passed
- comparison from prior coordination target `e380e66f...` to transfer HEAD is documentation-only across the canonical handoff/roadmap files; no accepted post-FC0A lane product code has been integrated yet.

### Binding lane state at transfer

1. **DEV-SCRIPT-ENGINEERING / PR #344**
   - accepted candidate head `cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`
   - state: `MAIN_ACCEPTED_FOR_CODEX / DEV_WAIT`
   - active shared CODEX route: `ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`
   - no CODEX validation handoff had appeared at the last revalidation; new coordinator must re-check live before acting.

2. **DEV-EDITOR / PR #349**
   - head `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`
   - T1 `36066874097`: SUCCESS
   - state: `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`
   - must not preempt the currently active Script CODEX route unless Main deliberately reorders after live revalidation.

3. **DEV-AUTHORITY-UX / PR #346**
   - reviewed head `3986475b20e4a72969159bfaed003ad5d72626d4`
   - state: `DEV_CORRECTION`
   - order: `DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`
   - repaired-metadata T1 `36067636970` reached product evidence and found candidate-causal Web TS2345 compile errors in `AuthorityPolicyAdministration.logic.ts`.
   - do not rerun the unchanged old head as acceptance.

4. **DEV-LICENSING-UX / PR #345**
   - reviewed head `cdf572d644417fe83aee3003a3da3fe171d7ada3`
   - state: `DEV_CORRECTION`
   - order: `DEV-LICENSING-UX-STATUS-ENTITLEMENTS-02`
   - T1 `36067628680`: Web/Chromium/Common green; .NET 692/693.
   - only failure was diagnosed as shared IEC-104 test-observation race, not Licensing causal.
   - prepared infra closeout: `INFRA-CI-01D-IEC104-FAULT-OBSERVATION-RACE-V1`; it is PREPARED ONLY until Main explicitly activates it.

5. **FND-05 / PR #347**
   - reviewed head `8c2bd2724b7f17711d76c59919f68ed7037483a3`
   - T1 `36064607662`: SUCCESS
   - state: `DEV_CORRECTION / PEER_HANDOFF_BOUNDARY_REQUIRED`
   - current order: `FND05-DEV-PEER-HANDOFF-BOUNDARY-V2-01`
   - dedicated control rev 0006.
   - Main accepted the internal HA state-machine direction but requires a transport-neutral two-independent-node readiness/authority/lease handoff boundary before CODEX.
   - no network/consensus/automatic-failover scope was authorized.

6. **FND-07 / PR #348 (draft)**
   - reviewed head `ae11e42e8ad5e39b1e2c5a0068f81e4ec31653c6`
   - state: `DEV_CORRECTION / FRESH_INSTALL_NO_DEMO_TEST_CONTRACT`
   - current order: `FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`
   - dedicated control rev 0005.
   - T1 `36066422907` exposed a stale Demo-dependent E2E fixture; Wave 15 product truth is clean no-Demo fresh-install / neutral bootstrap.
   - do not restore hidden Demo product behavior merely to satisfy the old test.

### Sequential CODEX queue

Binding active route at transfer: Script Engineering #344.

Planned only, not yet activated:
1. after Script handoff, Main may activate INFRA-CI-01D to stabilize the shared IEC-104 test observation race;
2. then Editor #349 is the first already Main-accepted queued product candidate.

A new explicit shared CODEX route is required before the executor changes mission.

### Fresh-install preview remains downstream

The prepared two-stage first-project fresh-install partial preview remains **NOT ACTIVE**.

Entry still requires:
- four feature lanes integrated with required T2 acceptance;
- FND-05 and FND-07 post-merge validated and VERIFIED/FROZEN;
- no known blocking P0/P1 invalidating the journey.

Then:
1. CODEX black-box first-project journey on a clean environment;
2. Product Owner human first-project journey on an independent clean environment;
3. detailed CODEX findings remain embargoed from the Product Owner until the human journey ends;
4. Main compares both journeys afterward.

Prepared control:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`.

### Mandatory startup for the replacement coordinator

Before any merge, rerun, correction, CODEX reroute or new architecture decision:

1. read `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md` fully;
2. read `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md` fully;
3. read `docs/CURRENT-COORDINATOR-HANDOFF.md` and `LAST CHANGE.md`;
4. revalidate live integration HEAD, PRs #344-#349, their exact heads/mergeability/CI/comments, Issue #305, shared CODEX control, central six-lane control, and the dedicated controls of any lane being acted on;
5. if live GitHub differs from this transfer snapshot, GitHub live wins.

Do not treat the Product Owner as a courier between agents. Normal lane handoffs should be recovered directly from GitHub comments/control planes whenever available.


## REPLACEMENT MAIN TAKEOVER — LIVE REVALIDATION AFTER TRANSFER (2026-09-24)

GitHub live supersedes the transfer snapshot where lane heads advanced.

### Integration / product truth
- FC0-A released product checkpoint remains `e3ed5138369c576549cb58a7aff9783792f322d3` / tree `4e7627774fbfc111344e3d80fcb9d921eed8377e`.
- takeover integration HEAD before this documentation refresh was `3480ed03a6719aef38e4c1ced2f466aaa4fa10b3`.
- comparison FC0-A -> that takeover HEAD was 14 commits ahead / 0 behind and changed only the five canonical coordination/documentation files.
- therefore no post-FC0A feature/Foundation product code had been integrated at takeover.

### Live lane decisions
1. **Script Engineering / PR #344**
   - head `cf0ae2d1dcd2d63668b5b1c2c3590a5b6bb9bdaa`;
   - remains `MAIN_ACCEPTED_FOR_CODEX / ACTIVE_SHARED_CODEX_ROUTE / DEV_WAIT`;
   - no post-transfer CODEX validation handoff has appeared;
   - binding route remains `ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`.

2. **Editor / PR #349**
   - head `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`;
   - T1 `36066874097` SUCCESS;
   - remains `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`.

3. **Licensing UX / PR #345**
   - corrected head `f5d3212b9c114d3ad6e2239460172db0b3d568f8`;
   - tree `a3e87f2ef7beb15bc66d6980e12710eddcc30525`;
   - T1 `36071779912` SUCCESS;
   - Main accepted the corrected ESLIC1/ESLIC2 schema + signed Interactive/ViewOnly/HA entitlement projection;
   - state `MAIN_ACCEPTED_FOR_CODEX / QUEUED / DEV_WAIT`.

4. **Authority UX / PR #346**
   - corrected head `9fd2462f43c74b085e58be91dbec9ebdf18514c5`;
   - tree `bdd7ac76c3566aef81e249a9db91810674b7dfe8`;
   - stable-role-key correction direction accepted;
   - T1 `36071729747` FAILURE because Web semantic build reports nullable-baseline TS2345 at `AuthorityPolicyAdministration.tsx(341,48)` and `(345,38)`;
   - remains DEV correction under `DEV-AUTHORITY-UX-STABLE-ROLE-KEY-02`;
   - no unchanged-head rerun.

5. **FND-05 / PR #347**
   - corrected head `be9cf0f3f02aa1ba49cdb6b589abd5e1e723c845`;
   - tree `c9e94e84c078928ad690217484171fafa6390b46`;
   - T1 `36072325579` SUCCESS;
   - Main accepted the transport-neutral two-independent-service readiness/authority/lease handoff boundary directionally;
   - state `MAIN_ACCEPTED_FOR_CODEX_HA_ADVERSARIAL / QUEUED / DEV_WAIT`;
   - `CODEX_HA_ADVERSARIAL_GREEN` remains mandatory before integration.

6. **FND-07 / PR #348**
   - remains DRAFT at `ae11e42e8ad5e39b1e2c5a0068f81e4ec31653c6`;
   - remains `DEV_CORRECTION / FRESH_INSTALL_NO_DEMO_TEST_CONTRACT`;
   - no corrected post-transfer head has appeared.

### Sequential queue / infra
- INFRA-CI-01D remains `PREPARED / NO_MUTATION`; it is not automatically activated by the Licensing old-head IEC-104 diagnosis.
- Main will choose the next explicit CODEX route only after Script returns and after a fresh live revalidation.
- accepted/ready waiting work currently includes Editor #349, Licensing #345 and FND-05 #347 in addition to prepared INFRA-CI-01D; readiness does not itself change the route.
- fresh-install first-project preview remains PREPARED / NOT ACTIVE.

Central board rev 0005:
`coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`.

Dedicated control refreshes:
- Authority rev 0004 / `5e2804963ea919d9224ace341f279660804437ad`;
- Licensing rev 0004 / `a4a4098f7c65863bb5cc14a9b566f67aee9f7884`;
- FND-05 rev 0007 / `a3f69a286a62028b49b0809025c45b585ff698f8`.


## REPLACEMENT MAIN FOLLOW-UP — FND-07 CORRECTION RETURN (2026-09-24)

FND-07 advanced after the initial takeover revalidation:
- PR #348 remains DRAFT;
- corrected head `af7bf1ae51539975b3b3e8b40ea36472249ade2e`;
- tree `0947177b314dbcbaaebb3ae8ba65eb2fa3498c49`;
- delta is test-only in `web/scada-web/tests-e2e/local-auth.spec.ts`;
- fresh pre-project export now proves no hidden Demo/preconfigured Engineering content;
- first Administrator + first project + genuinely-empty-project assertions now execute past the prior blocker;
- natural T1 `36072685058` remains FAILURE only in focused Chromium because the historical fixture expects two Engineering security roles `developer + operator`, while the clean bootstrap endpoint truthfully returns one `developer` role and the same run reports `workspace.securityRoleCount == 1`.

Disposition:
`FND-07 -> DEV_CORRECTION / FRESH_INSTALL_NO_DEMO_TEST_CONTRACT`

Binding order remains:
`FND07-DEV-FRESH-INSTALL-NO-DEMO-E2E-01`

Dedicated control rev 0006:
`bc7e3f810b7534d1180196c1e768f4c92a1c60a5`.

Required delta remains test/harness-only unless new product evidence appears. Do not seed hidden Demo/operator state merely to satisfy the old expectation. No CODEX/merge/freeze authority.


## REPLACEMENT MAIN HANDOFF — FND-07 TEST AUDIT / CODEX UNAVAILABLE (2026-09-25)

GitHub live remains the sole authority.

Transfer snapshot:
- integration before these documentation-only handoff commits: `9895a01a662851505198b965fae2335e55fba6fa`;
- FND-07 PR #348: OPEN/DRAFT, mergeable;
- exact candidate head: `3ecc4a78080685b0556402d50190e09236d6d8fa`;
- exact-head T1: `36094394912`;
- Classify/Common/Web/focused .NET: SUCCESS;
- focused Chromium/final gate: FAILURE.

Main used the CODEX-unavailable interval for a legacy/stale-test audit. Durable audit details are in:
- PR #348 comment `5831728502`;
- FND-07 control rev 0022 / commit `101e2977cd28e0e7d6c470e61b3e2c5f5a7ffc2c`;
- shared CODEX control rev 0049 / commit `8b3be7727b18888a5dd3c0e046f8926a78c3d7e7`;
- central board transfer commit `3c24c683575ae0aaf87483c5b6ec36f95aa1878b`.

Closed audit findings include:
- deep-audit test compile/analyzer defects;
- removal of legacy hidden-Demo Playwright bootstrap;
- explicit INSTALLATION browser evidence;
- true persisted Working -> Published -> Active Runtime evidence instead of fallback Demo;
- deterministic Working cleanup between stateful E2E specs;
- stale Authority checkout test expectation;
- real `/published/activate` Minimal API nested-Task 500 fixed by awaiting `ActivatePublishedAsync`.

Remaining live blocker:
- explicit FND-07 fixture save/import succeeds;
- revision 2 publish returns HTTP 200;
- activation now reaches the real handler but returns HTTP 422 (`Activated=false`);
- root cause has not yet been diagnosed.

Disposition:
`FND-07 -> MAIN_DIAGNOSTIC / DEV_WAIT / CODEX_UNAVAILABLE / NO_MERGE`.

No blind rerun, no merge, no freeze. Replacement Main must revalidate live GitHub first and diagnose the exact-head 422 before issuing any new DEV/CODEX route.