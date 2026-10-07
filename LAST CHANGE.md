# LATEST COORDINATION CHANGE — 2026-10-06 22:31 BRT

Outgoing Main handoff recorded in:
`docs/WAVE15-MAIN-COORDINATOR-HANDOFF-2026-10-06-2231.md`.

Accepted product checkpoint remains:
`27a9347e3d6f87db799a1541de2a27484b8bda51`
from PR #550 / #549 S0 with T1 #929 (`513 passed / 0 failed / 0 skipped`).

Current execution:
- #554 S1 transient-event Runtime released, branch untouched at product checkpoint;
- #534 HA remains CODEX_LOCAL, backup branch `f4e4adf...`, not integration-ready;
- #552 Mitsubishi research accepted/closed;
- #553 Panasonic research accepted/closed;
- no industrial product branch active;
- only unrelated open PR at outgoing revalidation: #362.

GitHub live is the authority; revalidate before acting.

---
# PRODUCT-OWNER ACCEPTANCE / CURRENT INTEGRATION — 2026-10-06

> Supersedes older Wave 15 current-state and remaining-issue wording below. GitHub is authoritative.

## Accepted scope

- Product integration: `wave15/corrections-integration@25df20bf562acf6c3d78eb2e953f7010729b7c49` (merge PR #529).
- PR #529 T1 validation run `37492274954`: SUCCESS on its exact PR head; Dynamo inspector properties and Runtime parameter-kind corrections are integrated.
- Product Owner accepted and closed issues #484, #501 and #308 on 2026-10-06. The EliteSCADA-owned SVG factory/catalog is accepted without third-party SVG curation as a Wave 15 gate; the current Dynamo set is usable and accepted; further visual-layer polish is future work, not a Productization blocker.
- Also closed: #482 (visual library), #496 (visual utility objects/media), #500 (driver diagnostics convergence), #503 (responsive editor/Runtime) and #445 (historical playback).

## Remaining Wave 15 gate

- Post-merge CI run `37494810971` on exact SHA `25df20bf562acf6c3d78eb2e953f7010729b7c49`: SUCCESS (Web build, Backend build/test/runtime smoke, Chromium Wave 15 merge integration smoke). The separate full-browser E2E step was skipped by workflow conditions; do not claim that suite ran.
- Productization and final Preview remain active under #306 and #300; localization/help/manual remain open under #379/#424/#425.
- PR #362 remains open on the independent Preview workbench branch and is not part of the product integration PR set.
- Current product issues #484/#501/#308 are CLOSED by Product Owner acceptance; do not reopen or count future visual polish as a Wave 15 blocker.

## Owner-directed next coordinator: Home/Building automation

- The next coordinator should start the Home/Building automation-driver program from #472, using #475 as research input. This is an explicit Product Owner priority; the older “WAIT_CURRENT_LANES / planning only” wording in #472 is superseded.
- Owner decision trail: [#472 program reprioritization](https://github.com/brunolrogerio-collab/EliteSCADA/issues/472#issuecomment-6020829024), [#483 foundation gate](https://github.com/brunolrogerio-collab/EliteSCADA/issues/483#issuecomment-6020829446), [#475 research review](https://github.com/brunolrogerio-collab/EliteSCADA/issues/475#issuecomment-6020829995).
- Kickoff immediately with a live audit of #469 (reuse its Host Serial foundation), #483 (still OPEN: canonical TAG/DataSource identity, Equipment/Template independence and integration-catalog convergence), and #475 (still OPEN: review each dossier's GO/WAIT/REJECT and legal/hardware/test constraints).
- Close or narrowly revise the #483 prerequisites against current integration before starting protocol-specific implementation; then launch the first driver slice chosen from verified #475 evidence. Do not duplicate existing BACnet/IP support, invent a second TAG/runtime model, or claim unverified device compatibility.
- Wave 15 is not declared complete by this handoff: #306/#300/#379/#424/#425 remain tracked. Their remaining productization work must be kept visible while the owner-directed Home/Building stream begins.

---

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

# LATEST DELTA — 2026-10-03 — BATCH 3 CLOSED / NEXT BATCH ACTIVE / MAIN HANDOFF

> GitHub live remains the sole authority. This delta supersedes older current-state wording below when there is a conflict.

Accepted Batch-3 product checkpoint:
`9b596286a40b1649564075626e817cc61e32e7bc`

Batch-3 exact combined T1 #619 / run `37092144575`: SUCCESS.
Post-merge slim on the product checkpoint: Web SUCCESS / Backend SUCCESS / Chromium SUCCESS.

Roadmap consolidation then advanced integration documentation-only to:
`aca641501302edb04f32b83b04adf0726d7e8a96`

Active next batch released from that exact base:
- #482 Visual Library Catalog V1 — `work/w15-visual-library-catalog` — ahead 0 / behind 0 / no PR;
- #483 HOME/BUILDING H0 — `work/w15-home-building-h0` — ahead 0 / behind 0 / no PR;
- #484 Asset Curation Batch 1 — `content/w15-builtin-asset-curation-b1` — WIP head `055deb4df30eaf3e8af4d33fdc8739cf3d1b9df4`, tree `bca34cc2ea3b12124db703244ed715ebced9e0f9`, ahead 1 / behind 0, 86 draft assets, no PR/handoff yet.

Canonical full successor snapshot:
`docs/WAVE15-MAIN-COORDINATOR-HANDOFF-2026-10-03-NEXT-BATCH-ACTIVE.md`

Still blocked:
- SVG-first/core.svgSymbol V2 until #482 disposition;
- HOME/BUILDING H1-H6 until #483 disposition;
- every protocol-specific HOME/BUILDING driver;
- no CODEX mission now.

Permanent visual rule: PR #451 artwork is rejected and must not be reused.

Final Wave 15 closure still requires #379 final i18n, #424 Help, #425 Manual, #308 visual acceptance, #306 Productization/EEE and #300 final fresh Preview.

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

# LIVE CURRENT CHECKPOINT — 2026-09-27 — WAVE 15 FRESH-INSTALL PREVIEW ENVIRONMENT PREPARATION

> This section supersedes older current-state snapshots below when they conflict. Historical entries remain preserved. GitHub live remains the sole authority.

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


## Fresh-install Preview topology

The six-lane product phase is closed and the Preview has moved into environment-readiness work.

The Product Owner changed the Preview topology before either exploratory journey produced findings:

1. **ENV_A — CODEX local:** EliteSCADA runs locally in an isolated containerized audit environment on the CODEX machine.
2. **ENV_B — Product Owner human:** a fresh independent GitHub Codespace runs the same exact product bytes.
3. After both environments are READY, the two exploratory journeys may run independently/in parallel.
4. No DB, project, browser/session state or findings are shared.
5. Detailed CODEX findings remain embargoed from the Product Owner until the independent human journey is complete.

Canonical Preview control:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`.

## ENV_A — local CODEX harness

Harness branch:
`preview/w15-first-project-env-harness`.

Current reviewed candidate before the active correction:
- SHA `ec050e9bfda121805b1165860a4aeda0eb2582e8`;
- tree `0d6221540c3678a8b042c51082f7a2ed0a466fa2`;
- exact parent/product base `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

The branch differs from the product base only by Preview/devcontainer/Compose/startup/lifecycle infrastructure.

### Pause/resume requirement — materially proven

The Product Owner requires the local CODEX audit to survive CODEX usage limits and PC shutdown/restart.

Repository-controlled lifecycle semantics are:
`start | pause | resume | status | reset`.

On the accepted candidate CODEX proved:
- clean ENV_A reached RUNNING;
- first Local Administrator was created through the normal UI as a minimal product-owned persistence marker;
- no project was created;
- audit paused as `PAUSED_RESUMABLE`;
- Docker Desktop was fully restarted with `docker desktop restart --timeout 180`;
- Docker Desktop/Engine returned with a different Docker session and healthy engine;
- the exact same audit session resumed;
- the normal product UI still showed the persisted Administrator/no-project state;
- a second ordinary pause/resume cycle preserved the same state;
- explicit reset returned `NOT_STARTED` and removed the dedicated Preview containers/volumes.

Therefore the important user requirement **pause -> power/engine interruption -> resume same audit** is materially proven.

Evidence handoff:
Issue #305 comment `5856199560`.

## Current ENV_A blocker — dependency preparation incorrectly coupled to product reset

After the successful continuity proof and explicit reset, a fresh start attempted to reinstall runtime npm dependencies because the reset had also removed dependency volumes.

The fresh start stopped before product UI with:
`UNABLE_TO_VERIFY_LEAF_SIGNATURE`
while downloading `ws-8.21.3.tgz` from npm.

CODEX correctly did **not**:
- disable npm TLS;
- set `NODE_TLS_REJECT_UNAUTHORIZED=0`;
- change host/container trust;
- alter product source.

Classification:
`ENV_A_HARNESS_DEFECT / AUDIT_RESET_DEPENDENCY_BOOTSTRAP_COUPLING`.

Main decision:
a clean **product/audit reset** must be independent from **toolchain/dependency preparation**.

Required new boundary:
- preparation/tool caches may persist and must be tied to exact lock/project/harness provenance;
- product database, Administrator/project/application state and audit session remain explicitly resettable;
- after successful preparation, repeated `reset -> start` must not download npm/NuGet packages again;
- no TLS/certificate weakening is allowed.

Active CODEX route:
`ROUTE-SEQUENTIAL-CODEX-ENV-A-DEPENDENCY-BOUNDARY-FIX-71`.

Shared control rev 0086:
commit `574011f722bb2234c7a1586fc8d62739892f716a`.

Preview control rev 0011:
commit `44023a9cf9e29c38d9b0e62f72198fa89e242018`.

ENV_A remains:
`BLACKBOX_HOLD / NOT_READY`.

## ENV_B — Product Owner Codespace

Canonical runbook:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-CODESPACE-ENV-B-RUNBOOK.md`.

The current static design uses:
- Compose-backed EliteSCADA + TimescaleDB;
- `postStartCommand` for every Codespace start/resume;
- Web port `5173` automatically set to **PUBLIC**;
- API `5080` remains internal/private;
- database `5432` remains internal/private;
- no precreated Administrator/project/Demo/EEE state;
- after Codespace stop/resume, repository automation must restart the product and re-assert `5173:public` without Product Owner terminal/Ports-panel work.

ENV_B is not yet READY because a real fresh Codespace + stop/resume lifecycle still needs to be exercised.

The currently connected GitHub automation cannot create/start/stop a Codespace. Product Owner interaction should ultimately be limited to creating/opening the prepared Codespace; product startup and port visibility must be automatic.

## CODEX Stage 1 and Stage 2

Stage 1 remains the unspoiled black-box first-project journey:
`W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`.

It has **not started** because ENV_A is not READY.

A second post-journey verification stage has already been prepared:

`docs/WAVE15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION.md`
commit `ce7a4b9a26cc5cdf36d466712eef62ab061d978f`.

Stage 2:
`W15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION = PREPARED / NOT ACTIVE`.

It will only start after Stage 1 evidence is sealed and a derived checkpoint/copy of the CODEX-created project exists. It targets the Wave 14 -> Wave 15 regression contracts: Working/Active authority, truthful failure UX, Editor/schema selection stability, Script Engineering maturity, Server Script recovery, Runtime projection/popup persistence, Popup values, Trends, Authority/Licensing truth and FND-07 Neutral/Detach; FND-05 HA is explicitly not overclaimed from single-node ENV_A.

## Immediate next safe action

1. Wait for CODEX to return an exact replacement harness for `ENV_A dependency preparation vs reset`.
2. Main reviews exact diff/SHA/tree; product semantics must remain untouched.
3. Prove:
   - preparation/provenance;
   - clean start;
   - Administrator persistence through real Docker Desktop restart;
   - reset;
   - second fresh start with **no new npm/NuGet download**;
   - final reset -> `NOT_STARTED`.
4. Only then mark ENV_A READY and release Stage 1 on a new clean audit session.
5. In parallel, once the shared harness impact is known, continue ENV_B real Codespace create/start/stop/resume proof.
6. Keep Stage 2 PREPARED / NOT ACTIVE until Stage 1 is sealed.

Durable ledger:
Issue #305 comments `5856105046`, `5856118585`, `5856157684`, `5856199560`, `5856218697`.

---

# LAST CHANGE — EliteSCADA

**Date:** 2026-09-23 BRT  
**Operational state:** **WAVE 15 ACTIVE / FND-04 VERIFIED+FROZEN / FND-06 ACTIVE / FC0-A BLOCKED ONLY ON FND-06**

## Latest verified product checkpoint

Exact frozen Foundation product checkpoint:

`6c810647c9773a19b212d9c33694780141786ac7`

tree:

`1221ff55963052be4e924dd644efbaa65763f546`

This checkpoint includes FND-03, INFRA-CI-01A and FND-04. Exact FND-04 post-merge EliteSCADA CI #1562 / run `35913456486` completed SUCCESS:
- Web `107358858133` — SUCCESS;
- Backend build/test/smoke `107358858405` — SUCCESS;
- Chromium E2E `107359503423` — SUCCESS.

FND-04 is **VERIFIED/FROZEN**. Its downstream Script TAG reference contract may be consumed but not redefined.

## Current active work

FND-06 is the only active FC0-A blocker.

- order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- exact product base: `6c810647c9773a19b212d9c33694780141786ac7`
- work branch: `work/w15-fnd-06-visual-stability-foundation`
- dedicated control: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
- active control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`
- work branch remains untouched at the exact base and no candidate/PR exists yet.

The frozen FND-06 plan covers centralized known-legacy visual compatibility, Screen/Popup selection stability, canonical renderer/public-model authority, Runtime navigation persistence and Working-vs-Active separation. Full single-canvas WYSIWYG remains downstream DEV-EDITOR scope.

## FC0-A preparation

FC0-A remains **BLOCKED only on FND-06 VERIFIED/FROZEN**.

Prepared downstream release file:
`coord/w15-fnd06-control:docs/WAVE15-FC0-A-RELEASE-PREP.md`

Latest prep commit:
`dfae2377f0c6802da726650697040181c7b0f453`

Reserved, not-yet-active orders:
- `DEV-EDITOR-FC0A-01`
- `DEV-SCRIPT-ENGINEERING-FC0A-01`
- `DEV-AUTHORITY-UX-FC0A-01`
- `DEV-LICENSING-UX-FC0A-01`

No downstream product branch is created before Main records the final exact FC0-A SHA/tree.

## Later Foundation preparation

Read-only/source-audit preparation was completed without product mutation:

- FND-05 HA authority control:
  - branch `coord/w15-fnd05-control`
  - file `docs/WAVE15-FND05-CONTROL.md`
  - prep commit `66d0f0e53a9842c719cfe28ef40da3b7fbdd1f6f`
  - state `PREPARED / NOT ACTIVE`
- FND-07 installation detach control:
  - branch `coord/w15-fnd07-control`
  - file `docs/WAVE15-FND07-CONTROL.md`
  - prep commit `760e1cb57a1bdb1e146a6326bd55f713c7790add`
  - state `PREPARED / NOT ACTIVE`

Neither later Foundation is authorized to mutate product while FND-06 owns the sequential high-risk Foundation executor.

## Foundation current Main gate

- FND-04: **VERIFIED/FROZEN**
  - exact product checkpoint: `6c810647c9773a19b212d9c33694780141786ac7`
  - tree: `1221ff55963052be4e924dd644efbaa65763f546`
  - post-merge EliteSCADA CI `35913456486`: SUCCESS
  - Web / Backend+tests+smoke / Chromium: all SUCCESS
  - control freeze commit: `0453428521b28e951aa8e9d01742ee58b09f6690`
- FND-06: **ACTIVE / NOT INTEGRATED**
  - exact product base: `6c810647c9773a19b212d9c33694780141786ac7`
  - work branch: `work/w15-fnd-06-visual-stability-foundation`
  - control branch/file: `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md`
  - active control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`
  - order: `FND06-CODEX-WAIT-POSTMERGE-V4`
- FC0-A: **BLOCKED only on FND-06**
- downstream release package is prepared but not released at `docs/WAVE15-FC0-A-RELEASE-PREP.md` on the FND-06 control branch.



## FND-06 execution metadata correction

Before first product mutation, Main corrected the prepared FND-06 validation profile to the actual Wave 15 router vocabulary: `UI_EDITOR, RUNTIME_RENDERER`. The exact product seed also proves bare `status` is a persisted legacy identifier alongside `tank`, `value` and `dynamo`; it is not a current canonical `core.*` built-in and no guessed alias is authorized.

Active FND-06 order: `FND06-CODEX-WAIT-POSTMERGE-V4`.
Control commit: `35e1ae631b8471a66eb0c4042295d5b5628d61ec`.


## FC0-A collision guard

The prepared downstream release package now includes a Main-owned parallel-file collision map. Primary ownership is separated across Editor (`engineering/visual-editor/**`), Script Engineering (`engineering/scripts/**` + `python-editor/**`), Authority UX (`UserAdministration*`) and Licensing UX (`web/licensing/**` + `Scada.LicenseGenerator/**`). Shared shell/router/types/i18n/CI files are Main-coordinated hotspots, not free-for-all lane ownership.

Latest FC0-A prep commit: `dfae2377f0c6802da726650697040181c7b0f453`.


## Coordinator correction — shared CODEX routing and FC0-A audit

A stale FND-04 CODEX `WAIT` order could cause the reused sequential CODEX chat to stop even though FND-06 was active.

Main corrected this at the source:
- FND-04 control rev 0016: `ROUTE-SEQUENTIAL-CODEX-TO-FND06-12`
- commit: `cf19a9b0ce7efbf25d3d85acc2076a8c48148a9d`
- same sequential CODEX chat/lane is explicitly the FND-06 executor
- FND-06 control rev 0004: `FND06-CODEX-WAIT-POSTMERGE-V4`
- commit: `1a1488388fd67bf89380879b07437e1460170f18`

FC0-A sequencing was also tightened:
- FND-06 freeze no longer releases FC0-A directly;
- mandatory gate: `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`;
- audit control created at `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-CONTROL.md`, commit `0689a53b6cdcb569dd6c65a8326009dfcf2da9de`;
- FC0-A release prep updated at `fee82e7dd8eed8cd237e067317fedbe48d64cb97`;
- FND-05 control rev 0002 / `b995435594f9031a33df5674a0207016405a41d7`;
- FND-07 control rev 0002 / `503a89d2985db64c1d6666e40d1de06251027687`.

After FND-06, audit must relate Wave 15 implementation to final Wave 14 diagnostics, product premises/gaps and frozen contracts. It must also prove FND-05/FND-07 are non-breaking to FC0-A DEV-consumed contracts.

Only audit PASS releases the four FC0-A DEVs and permits FND-05/FND-07 to activate in parallel.


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


## Post-FND06 broad CI exposed generic PostgreSQL blocker

PR #337 merged at `624f2eca456310a2c6156538b3616a06e3be075f`.

Exact broad EliteSCADA CI #1563 / `35940661531`:
- Web — SUCCESS;
- Backend build/test/smoke — FAILURE in test;
- Chromium — skipped downstream.

Only identified failure:
`PostgreSqlVisualDynamicPersistenceTests.RevisionPersistence_PreservesVisualExpressionConditionAndAnalogFill`

Error:
`23505: duplicate key value violates unique constraint pg_namespace_nspname_index`
during `PostgreSqlEngineeringProjectStore.InitializeAsync`.

The store and failing test are byte-identical to the pre-FND06 checkpoint. PR #337 did not touch Persistence/PostgreSQL. Same PostgreSQL catalog-race signature was diagnosed in Wave 14.

Main did not rerun blindly.

Active generic correction:
- `INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`
- control `coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`
- control commit `358b067d9af6501b2945f311b5d2cd32cab64efa`
- exact base `624f2eca456310a2c6156538b3616a06e3be075f`
- work branch `work/w15-infra-ci-01b-postgresql-schema-init`.

FND-06 = INTEGRATED / MAIN-ACCEPTED / FREEZE BLOCKED BY GENERIC INFRA.
FC0-A audit remains PREPARED.


## Broad CI #1564 — PostgreSQL fixed; FND-06 E2E fixture leak remains

Exact run `35944510920` on `eb4563cf0060449b479c4335ef30a19ed65e35ab`:
- Web SUCCESS;
- Backend build/test/smoke SUCCESS;
- Chromium FAILURE: 636 passed / 1 failed.

The PostgreSQL failure that triggered INFRA-CI-01B is closed at the backend gate.

The sole Chromium failure is `runtime.spec.ts`, which saw an extra `fnd06-legacy-screen-*` created by the FND-06 mounted closeout test.

Root cause: JSON import/apply for Screens/Popups is upsert-only. Reapplying the pre-test export cannot delete a new fixture entity.

Active test-only correction:
- `FND06-CODEX-E2E-FIXTURE-ISOLATION-V6`
- work `work/w15-fnd06-e2e-fixture-isolation`
- FND-06 control rev 0010 / `b7b3ab914464a2da87d7b2175eb95a24d8a7db9b`.

No product semantic change is authorized.


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


## FC0-A audit completed by Main — corrections required

The Main Coordinator completed the mandatory post-FND06 Wave14->Wave15 audit on frozen product SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`.

Result:
`CHANGES_REQUIRED`

Confirmed blockers:
- W15-P1-01 Server Script throttle recovery;
- W15-P1-06 truthful Engineering fallback.

FND-05 remains additive/compatible and FND-07 compositional/compatible; neither currently forces a breaking change to FC0-A-consumed frozen contracts.

Audit report commit:
`463d357f8a9c11f774f5da59480e4a17a19569f5`.

First correction activated:
- `FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`;
- branch `work/w15-fc0a-p101-server-script-recovery`;
- exact base `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`;
- profile `SCRIPT_RUNTIME`;
- control `b8858d08a8516588db4be40d2da48ea266fe793e`;
- routing `a088c884d90bd0c2d86b844f74332306a80b7a7c`.

P1-06 remains queued. No FC0-A lane has been released.


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


## 2026-09-25 — Main coordinator forced handoff after FND-07 legacy-test audit

The outgoing Main Coordinator paused the shared CODEX route because CODEX is temporarily unavailable and persisted the exact transfer state in GitHub.

FND-07 PR #348 exact head at transfer:
`3ecc4a78080685b0556402d50190e09236d6d8fa`.

Latest exact-head T1:
`36094394912` — Classify/Common/Web/.NET green; Chromium red.

The audit removed several legacy/harness assumptions and exposed/fixed a real activation endpoint nested-Task defect. The remaining blocker is now a clean HTTP 422 from the real Published -> Active path after publish returns 200. Root cause remains open for the replacement Main.

Durable ledger:
- PR #348 comment `5831728502`;
- FND-07 control `101e2977cd28e0e7d6c470e61b3e2c5f5a7ffc2c`;
- shared route `8b3be7727b18888a5dd3c0e046f8926a78c3d7e7`;
- central board `3c24c683575ae0aaf87483c5b6ec36f95aa1878b`.