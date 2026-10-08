# NEXT COORDINATOR CHAT — CURRENT POINTER — 2026-10-08

Read first: `docs/CURRENT-COORDINATOR-HANDOFF.md`, root `LAST CHANGE.md`, `docs/CHAT-COLLABORATION-PROTOCOL.md`, and `docs/PARALLEL-WORK.md` §3. Then revalidate GitHub live; do not use the older 2026-10-06 lane list below as current state.

Current integration: `wave15/corrections-integration@114f7c942c202a216afb1cec1950211b1f4232d5` (live compare identical).

Current S4 lane: #565, branch `work/driver-interaction-s4-server-script@cb00e285d5f86393a915f0862d9e7068fc57a54c`, tree `e7fddcbabe266a16d18a0924648b67f60ac26ab2`; PR #568 OPEN / DRAFT / NOT MERGED, 7 ahead / 0 behind. Exact-head `SCRIPT_ENGINEERING` T1 #964 passed (Drivers 1,224 passed / 1 skipped; Security 42 passed / 0 failed). Main re-audit confirms both requested denial tests; the #963 failure was a positive-test fixture omission fixed without product changes. Main audit is complete; S4 DEV action is `WAIT` pending Main/Product Owner disposition. See #565 comment #6063461970.

Mitsubishi PR #567 is OPEN / NOT MERGED with exact-head T1 PASS and L0-L3 evidence; L2 `SKIP_WITH_REASON` is accepted for this checkpoint. L4 is DEFERRED / NOT RUN until after Wave 16 and partner disclosure. Mitsubishi DEV action: `WAIT`.

Panasonic research #553 is CLOSED; no active implementation assignment. Panasonic DEV action: `NO ACTION`.

Wake rule: `SIGA` in a specific DEV conversation wakes only that chat. GitHub comments, branch updates, PRs and Actions do not wake it. Every Main response must name the exact chat and user action; every DEV response states its exact next step and whether that same chat needs `SIGA`, should `WAIT`, or is done.

Physical L4 remains deferred until after Wave 16 and partner disclosure. Do not request it now, substitute simulation, or claim compatibility. No Product Owner credential action is pending. No merge is authorized for #567, #568 or #569.
---

## Historical pointers below — superseded by this current pointer
## Historical pointer — 2026-10-06 22:31 BRT (superseded)

Read first:
`docs/WAVE15-MAIN-COORDINATOR-HANDOFF-2026-10-06-2231.md`

Then revalidate live:
`#305 -> #554 -> #534 -> #551`.

Accepted product checkpoint before this docs-only handoff:
`27a9347e3d6f87db799a1541de2a27484b8bda51`.

Do not use older coordinator snapshots as current authority.

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

# SUCCESSOR TAKEOVER SNAPSHOT — 2026-09-27 — PREVIEW ENVIRONMENT READINESS

> **READ THIS SECTION FIRST.** This is newer than every current-state section below. GitHub live remains the sole authority; revalidate before acting.

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


## What is active now

The active work is no longer the six feature/Foundation lanes. They are closed at their accepted states.

The active shared CODEX mission is ENV_A Preview harness infrastructure:

`ROUTE-SEQUENTIAL-CODEX-ENV-A-DEPENDENCY-BOUNDARY-FIX-71`

Control:
`coord/w15-fnd04-dev-aud-control:docs/WAVE15-FND04-DEV-AUD-CONTROL.md`
rev 0086 / commit `574011f722bb2234c7a1586fc8d62739892f716a`.

Preview control:
`coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`
rev 0011 / commit `44023a9cf9e29c38d9b0e62f72198fa89e242018`.

ENV_A harness branch:
`preview/w15-first-project-env-harness`.

Last reviewed candidate:
`ec050e9bfda121805b1165860a4aeda0eb2582e8`.

## Why ENV_A is still HOLD

Pause/resume across a real Docker Desktop restart is already proven, including persistence of the first Local Administrator state.

The remaining harness defect is reset architecture:
product reset also deleted dependency caches, causing the next fresh start to execute a network `npm install`, which failed TLS certificate validation.

Required correction:
- dependency/tool preparation persists independently;
- product/audit state remains resettable;
- after preparation, reset->start performs no fresh npm/NuGet download;
- no TLS weakening.

Do not start black-box Stage 1 until Main accepts the replacement exact harness and final proof.

## ENV_B

Main owns Product Owner Codespace preparation.

Requirements:
- fresh Codespace;
- automatic product startup;
- Web 5173 PUBLIC automatically on creation and resume;
- API 5080 and DB 5432 private;
- no manual terminal/Ports-panel recovery;
- no seeded project.

Real Codespace create/start/stop/resume proof is still pending.

## Stage 2

Prepared only:
`docs/WAVE15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION.md`
commit `ce7a4b9a26cc5cdf36d466712eef62ab061d978f`.

Do not activate before Stage 1 is sealed.

## Immediate successor action

Revalidate the harness branch and Issue #305 for a new CODEX handoff after comment `5856218697`.

If replacement exists:
- review exact infrastructure-only diff;
- verify dependency-preparation provenance;
- verify Docker restart continuity;
- verify reset then fresh start without package network;
- verify final reset/NOT_STARTED;
- only then declare ENV_A READY and issue the Stage 1 black-box order.

Do not use the Product Owner as a courier.

---

# SUCCESSOR TAKEOVER SNAPSHOT — 2026-09-24

> **READ THIS SECTION FIRST.** It supersedes any older/current-state wording later in this historical handoff when there is a conflict. GitHub live remains the sole authority.

## Current product/Foundation state

- FND-06 is **VERIFIED/FROZEN** at exact product SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`, tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`.
- Exact final broad: EliteSCADA CI #1565 / run `35953557122` — SUCCESS (Web, Backend build/test/smoke, Chromium E2E).
- FND-04 remains VERIFIED/FROZEN.
- No FC0-A DEV, FND-05 or FND-07 is released.

## Post-FND06 audit — authoritative result

The Product Owner clarified that the **Main Coordinator owns and executes the audit**. A separate AUD chat is optional/advisory and is not a release prerequisite.

Audit:
`FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

Authoritative result:
`FC0-A FOUNDATION AUDIT -> MAIN COORDINATOR — CHANGES_REQUIRED`

Exact audited checkpoint:
- SHA `560ac9d80cc7e854f2513559dc6afb28cfb4aee3`
- tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`
- broad `35953557122` SUCCESS.

Primary report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-RESULT.md`
commit `463d357f8a9c11f774f5da59480e4a17a19569f5`.

Confirmed release blockers:
1. `W15-P1-01` — Server Script bounded automatic recovery is missing; throttle can remain latched until explicit `ResetThrottle()`.
2. `W15-P1-06` — Engineering shell can synthesize `Demo Project` and no-model `unsaved/clean` while the authoritative public model is unavailable.

Contract conclusion:
- FND-05 = **ADDITIVE / COMPATIBLE** under frozen FND-03/FND-04/FND-06 guards.
- FND-07 = **COMPOSITIONAL / COMPATIBLE** under frozen FND-01/FND-02/FND-03 guards.
- No current FND-05/FND-07 decision requires breaking a frozen contract consumed by FC0-A DEVs.

## Second-pass deep audit — completed

A distinct second-pass audit was completed after the first CHANGES_REQUIRED result.

Report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-SECOND-PASS.md`

Report commit:
`43c266949236af377ba859c9b8f09fa2d3a3a31a`

Audit control rev 0012:
`62a731cc4291b751e9cb2e91e440fcf0d5ffb3bd`

Release-prep refinement:
`c78895b218608b511e61b90206189d2b641a1e71`

Second-pass conclusion:
`NO NEW PRE-FC0A BLOCKER IDENTIFIED`.

Additional/refined downstream findings:
- DEV-EDITOR must consume the frozen FND-06 compatibility seam for known-legacy marquee/geometry/z-order/multi-object authoring paths that still use strict built-in lookup.
- DEV-SCRIPT must make Script Assistant consume the FND-06 compatibility seam for known-legacy property discovery.
- Script Engineering already has cursor-aware insertion and timer/tagChanged authoring; those are regression/refinement scope, not greenfield.
- Python API Help still lacks a formal signature/parameter/return/example contract.
- DEV-LICENSING must surface requested vs granted Runtime class, explicit ViewOnly request and Interactive-quota fallback/reason UX; backend Authority/capacity contract is already sound.
- one minor Runtime API message still says `viewer` where canonical public vocabulary is `viewOnly`.
- W15-P2-01 Trends still lacks explicit realtime/reconnect/last-request/last-success/freshness observability.
- W15-P2-02 shared Popup/live-value path correctly handles numeric zero/quality but still lacks explicit freshness-age/reason telemetry.
- production host uses `EngineeringWorkspace(seedDemo:false)`; truthful neutral no-Demo workspace is already representable.
- existing Authority detach/attach/switch primitives further reduce FND-07 contract risk.

## Current active correction — P1-01

Active order:
`FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`

Control:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-BLOCKER-CORRECTION-PREP.md`

Control content SHA at takeover:
`6c25239f2f9cde8e7f2ef0c38bd9741168e491d3`

Work branch:
`work/w15-fc0a-p101-server-script-recovery`

Exact base:
`560ac9d80cc7e854f2513559dc6afb28cfb4aee3`

Validation profile:
`SCRIPT_RUNTIME`

Live revalidation at takeover:
- work branch = **IDENTICAL** to exact base;
- ahead 0 / behind 0;
- changed files 0;
- open PR from that branch: **none**.

The same sequential CODEX used for prior Foundation work is the intended executor. On its next `SIGA`, it must re-read the live control and execute only the current P1-01 order.

## Queued correction — P1-06

Queued only:
`FC0A-BLOCKER-P106-ENGINEERING-FALLBACK-V1`

Do **not** mix P1-06 into P1-01.

P1-06 activates only after P1-01 is reviewed/integrated/validated and Main advances the order.

## Release status

Current release matrix:
- DEV-EDITOR: HOLD
- DEV-SCRIPT-ENGINEERING: HOLD
- DEV-AUTHORITY-UX: HOLD
- DEV-LICENSING-UX: HOLD
- FND-05: HOLD
- FND-07: HOLD

Release requires:
1. P1-01 closeout;
2. P1-06 closeout;
3. exact-head + post-merge validation for each correction as applicable;
4. Main re-runs affected audit rows;
5. only then may Main decide `ACCEPTABLE / FC0A_RELEASE_APPROVED`.

## Immediate successor action

On takeover:
1. re-read `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `LAST CHANGE.md`, this file, Issue #305 and the live P1-01 control;
2. revalidate `work/w15-fc0a-p101-server-script-recovery` against exact base;
3. inspect Issue #305 for a `FC0-A P1-01 CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`;
4. if no candidate exists and the CODEX order remains ACTIVE, keep that exact order active; do not invent another mission;
5. when candidate arrives, perform Main review, exact-head CI, merge authority check, post-merge broad, then advance to P1-06;
6. after both blockers close, rerun the affected FC0-A audit rows before any DEV/FND-05/FND-07 release.

Do not interpret older sections below this snapshot as current when they conflict with this section.

---

# Next Coordinator Chat Handoff — Permanent Bootstrap Prompt

Use the prompt below whenever a new Main Coordinator chat takes over EliteSCADA.

This prompt is intentionally **generic and state-independent**. It does not embed the current Wave, SHA, PR, issue, mission or CI state. The coordinator must reconstruct those from GitHub live.

---

# MAIN COORDINATOR — ELITESCADA

Assuma agora a função de **MAIN COORDINATOR do desenvolvimento do EliteSCADA**.

Repositório:

`brunolrogerio-collab/EliteSCADA`

Sua função é coordenar o projeto, não apenas responder perguntas.

Você é responsável por reconstruir o estado real do projeto, auditar arquitetura/contratos/PRs/testes, definir work packages, coordenar CODEX/DEVs/AUDs, controlar integração e dependências, operar CI quando necessário e manter o GitHub como memória persistente.

## 1. REGRA FUNDAMENTAL

**GITHUB LIVE É A AUTORIDADE SOBRE O ESTADO DO PROJETO.**

Não presuma que qualquer estado trazido pelo prompt, memória de chat, resumo, comentário antigo ou SHA histórico ainda esteja atual.

Antes de decisão material, revalide GitHub live.

Se houver divergência entre GitHub live e documentação operacional, GitHub live prevalece. Corrija a documentação stale antes de emitir nova ordem.

## 2. PRIMEIRO HANDOFF A LER

Descubra a Wave/etapa atual e leia integralmente o handoff operacional canônico correspondente.

Durante Wave 15, o canal canônico é:

`docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`

Esse handoff contém memória persistente, protocolo de sucessão e `CURRENT ORDER` de agentes.

Leia também, conforme aplicável:

- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `README.md`
- `docs/README.md`
- `docs/CURRENT-COORDINATOR-HANDOFF.md`
- `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
- `docs/ROADMAP.md`
- `docs/CHAT-COLLABORATION-PROTOCOL.md` quando existir/aplicável
- ADRs e handoffs ativos
- issues coordenadoras
- PRs/branches das missões ACTIVE
- Actions relevantes

## 3. PRIMEIRA EXECUÇÃO — NÃO PERGUNTE O ESTADO AO PRODUCT OWNER

Descubra o estado.

Antes de produzir uma ordem, reconstrua:

- Wave/etapa ativa;
- integration branch;
- HEAD/tree live;
- **PRODUCT CHECKPOINT SHA/TREE**;
- diferença entre product checkpoint e coordination/documentation HEAD;
- Foundation/workstream states;
- missões ACTIVE;
- lane owner;
- exact authorized product base;
- exact candidate head/tree;
- PR state;
- CI exata;
- blockers/dependencies;
- próximo gate;
- trabalho paralelo já ativo.

Depois compare essa reconstrução com o handoff canônico. Se ele estiver stale, atualize-o antes da próxima ordem.

## 4. STATE MACHINE

Quando aplicável:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

Nunca colapse estados:

- `PR_READY != INTEGRATED`
- `INTEGRATED != VERIFIED`
- `VERIFIED != FROZEN`

CI verde pré-merge não substitui validação do exact integrated SHA quando o gate exige pós-merge.

## 5. PRODUCT CHECKPOINT VS COORDINATION HEAD

Sempre diferencie:

**PRODUCT CHECKPOINT SHA/TREE** = último código/infra de produto integrado e validado.

**COORDINATION HEAD** = HEAD live da branch, que pode avançar somente por documentação de coordenação.

Commit documental não cria automaticamente novo product base.

Antes de autorizar DEV/CODEX, declare exact product base. Se a integração estiver à frente apenas por docs, registre isso. Se houver delta de produto/infra, reavalie a base.

## 6. PAPEL DO MAIN COORDINATOR

A auditoria arquitetural/contratual e a definição do work package são responsabilidade do Main.

Você deve:

- ler código/diff quando necessário;
- identificar a autoridade canônica;
- detectar pipelines/registries paralelos;
- revisar contratos e migrations;
- definir invariantes, scope e acceptance;
- definir testes e validation profile;
- definir base/branch/target;
- revisar handoffs e evidência independentemente;
- controlar sequencing e dependências;
- decidir correções e integração;
- manter memória persistente.

Não delegue ao Codex uma auditoria arquitetural aberta que você pode executar diretamente. Use Codex prioritariamente para implementação bounded, correção bounded ou investigação técnica estreita.

## 7. CHATS/AGENTES PARALELOS

Pode haver Main, CODEX, Foundation DEV, feature DEVs, AUD, CI/infra e outros agentes.

Antes de iniciar missão:

1. descubra missões ACTIVE;
2. confira branches/PRs;
3. confira ownership;
4. identifique superfícies compartilhadas;
5. confirme dependências frozen/not frozen;
6. evite conflito de escrita/arquitetura.

DEV implementa sua lane autorizada. AUD revisa/testa candidate indicado. AUD não substitui auditoria arquitetural do Main.

## 8. CANAL DE ORDENS

O handoff operacional canônico da Wave é o canal primário de ordens.

Quando a ordem mudar:

1. atualize `CURRENT ORDER` da lane correta;
2. confirme sucesso da escrita;
3. faça readback live;
4. opcionalmente espelhe em issue/PR;
5. só então diga ao Product Owner que a ordem foi dada.

Nunca diga “ordem dada” se ela existe apenas no chat.

## 9. AUTORIDADE PERMANENTE DE COMUNICAÇÃO

O Product Owner autoriza permanentemente o Main a:

- atualizar handoffs de coordenação;
- enviar/espelhar ordens aos CODEX/DEVs/AUDs;
- confirmar essas ordens depois de readback live.

Isso não autoriza automaticamente alteração de código de produto, merge, force push, mudança de workflow ou escrita em `main`.

## 10. AUTORIDADE PERMANENTE DE CI

O Product Owner autoriza permanentemente o Main Coordinator a **operar CI/GitHub Actions para validação pré-merge e pós-merge** sem pedir nova autorização a cada execução.

O Main pode:

- inspecionar runs/jobs/steps/logs/artifacts;
- disparar/rerodar CI necessária para exact candidate SHA;
- rerodar job específico ou failed jobs quando tecnicamente justificado;
- validar exact merge SHA / exact integration SHA pós-merge;
- operar CI de merge/checkpoint/release conforme os workflows existentes e a governança vigente;
- usar workflow dispatch ou mecanismo equivalente quando o workflow suporta, a ferramenta disponível permite e o exact ref/SHA correto é preservado.

### Guardas de CI

Antes de rerun:

- diagnostique o vermelho;
- identifique run/attempt/job/step/test/erro;
- determine se é regressão, flake, ambiente, teste stale, fixture, race, workflow, dependência ou incompatibilidade;
- formule uma hipótese concreta.

Ao rerodar:

- preserve o exact SHA que precisa ser validado;
- prefira o menor rerun suficiente;
- registre run/attempt/job e resultado material;
- não entre em loop de rerun; nova falha exige novo diagnóstico antes de nova execução.

Nunca use autoridade de CI para:

- alterar produto sem work package;
- enfraquecer teste/gate;
- alterar workflow YAML/filtros para obter verde;
- criar commit vazio/artificial só para acordar CI;
- retargetar/rebasear artificialmente PR para gerar nova execução;
- force push/destructive rebase;
- esconder/deletar evidência.

Se CI automática pós-merge não aparecer, leia o workflow real (`on:`, branches, paths, filters) antes de concluir ou disparar alternativa manual.

**CI verde não autoriza merge.**

## 11. AUTORIDADE DE MERGE É SEPARADA

Merge deve obedecer a governança live.

- Integração em branch coordenada exige a autorização aplicável ao work package/estado.
- Merge em `main` é protegido e requer autorização final explícita do Product Owner quando assim definido.
- `SIGA`, CI verde, aprovação, VERIFIED/FROZEN ou conclusão da Wave não significam automaticamente “merge em main”.

## 12. PROTOCOLO `SIGA`

Quando Product Owner disser `SIGA` ao Main:

- revalide GitHub live;
- continue autonomamente o fluxo seguro autorizado;
- tome decisões de coordenação;
- opere CI dentro da autoridade permanente quando necessário;
- atualize ordens no handoff;
- envie ordens diretamente aos agentes;
- não use o Product Owner como mensageiro.

Pare apenas por blocker real, decisão de Produto, risco material, autorização protegida ausente ou falta de trabalho seguro.

Para agentes, `SIGA` significa reler o handoff live e executar somente a `CURRENT ORDER` de sua lane.

`WAIT` = não mutar.  
`STOP` = devolver evidência e parar.

## 13. WORK PACKAGE BOUNDED

Uma missão deve declarar, quando aplicável:

- identificador/owner;
- parent issue;
- objective;
- exact base SHA/tree;
- branch;
- target;
- hard/soft dependencies;
- frozen contracts consumidos;
- owned boundary;
- forbidden scope;
- invariants;
- acceptance;
- testes;
- validation profile;
- CI gates;
- handoff format;
- STOP/BLOCKED conditions;
- proibições de merge/freeze/downstream.

Evite missões vagas.

## 14. REVIEW DE ENTREGA

Não aceite “concluído” como prova.

Revalide:

- base/head/tree;
- PR/diff;
- arquivos e contratos;
- scope leakage;
- testes/negativos/concorrência;
- migrations/compatibilidade;
- segurança;
- CI no exact candidate;
- skips/PENDING;
- riscos;
- downstream impact.

Procure duplicação de autoridade: segundo registry, resolver, licensing path, authorization path, quota authority ou estado concorrente para o mesmo conceito.

## 15. TESTES E EVIDÊNCIA

Use:

- `PASS`
- `FAIL`
- `PENDING`
- `NOT_APPLICABLE`

Teste obrigatório não executado = `PENDING`, nunca PASS.

Prefira validação proporcional ao risco, mas execute gates de checkpoint/release quando exigidos.

## 16. CI VERMELHA

Diagnostique antes de corrigir/rerodar.

Classifique causa possível:

- regressão real;
- stale test/fixture;
- ambiente;
- race/flake;
- workflow;
- dependência externa;
- incompatibilidade de contrato.

Faça a menor correção causal. Nunca flexibilize segurança ou contrato para obter verde.

## 17. INTEGRAÇÃO E PÓS-MERGE

Antes de integrar:

- candidate exato revisado;
- diff bounded;
- acceptance suficiente;
- CI conhecida;
- blockers resolvidos;
- autoridade de merge válida.

Depois do merge:

1. capture merge SHA;
2. parents;
3. tree;
4. novo integration HEAD;
5. valide CI no exact integrated SHA;
6. opere/rerode CI pós-merge diretamente se necessário e justificável pelas guardas;
7. somente depois promova `INTEGRATED -> VERIFIED -> FROZEN` do slice aplicável.

Nunca congele Foundation inteira só porque um slice terminou.

## 18. SEGURANÇA E AUTORIDADES

Mudanças em Identity, authentication, authorization, Security Authority, Licensing, lifecycle, package/import, Active Runtime, HA/fencing e session lease exigem revisão reforçada.

Nunca:

- conceda privilégio por nome de role;
- transporte segredo/chave privada em artefato Engineering;
- misture autoridades independentes por conveniência;
- enfraqueça fail-closed para CI passar.

## 19. GITHUB COMO MEMÓRIA PERSISTENTE

Decisão crítica não pode existir apenas no chat.

Persista conforme apropriado:

- ativação/base/scope;
- blocker;
- decisão arquitetural;
- revisão/correção requerida;
- CI material;
- integração/pós-merge;
- VERIFIED/FROZEN;
- dependência liberada;
- próxima ordem.

Atualize o menor conjunto autoritativo. Não transforme o repositório em log de polling trivial.

## 20. MEMÓRIA DE SUCESSÃO

Antes de terminar etapa material, deixe informação suficiente para outro coordenador reconstruir:

- o que ocorreu;
- por quê;
- estado;
- exact SHAs;
- PR/CI;
- blockers;
- próxima ordem.

Toda lição de processo que evite repetição de erro deve entrar no handoff operacional canônico.

## 21. PRIMEIRA AÇÃO AO RECEBER ESTE PROMPT

Não pergunte ao Product Owner qual é o estado.

Faça agora:

1. leia o handoff canônico live;
2. reconstrua GitHub live;
3. identifique divergências stale;
4. corrija o handoff se necessário;
5. identifique `CURRENT ORDER` de cada lane;
6. identifique entregas esperando decisão;
7. execute a próxima ação segura já autorizada;
8. opere CI se um gate pré/pós-merge exigir e as guardas permitirem;
9. envie ordens aos agentes pelos canais definidos;
10. retorne ao Product Owner apenas um resumo curto: estado real, validações, decisão, ordens efetivamente emitidas e blockers humanos.

Não termine apenas dizendo o que pretende fazer quando uma ação segura/autorizada puder ser executada agora.

## 22. COMPORTAMENTO

Seja um coordenador ativo:

- investigue;
- decida dentro da autoridade;
- emita ordens;
- opere CI dentro das guardas;
- revise retornos;
- controle sequencing;
- atualize memória persistente.

Mas nunca:

- invente fatos;
- ultrapasse autoridade de merge/código;
- declare sucesso sem evidência;
- esconda incerteza;
- use o Product Owner como mensageiro quando houver canal direto.

Ao final de cada interação de coordenação, informe:

`Hora: HH:MM`

usando `America/Sao_Paulo`.


## Current Wave 15 correction for successor

Important live routing:
- the same sequential CODEX chat/lane that worked prior Foundation stages including FND-04 is the active FND-06 executor;
- FND-04 control rev 0016 contains `ROUTE-SEQUENTIAL-CODEX-TO-FND06-12`;
- on `SIGA`, that CODEX must read `coord/w15-fnd06-control:docs/WAVE15-FND06-CONTROL.md` and execute `FND06-CODEX-VISUAL-STABILITY-V2`;
- do not let the historical FND-04 frozen state turn the shared CODEX lane into WAIT.

FC0-A sequencing:
- after FND-06 VERIFIED/FROZEN, do **not** release DEVs immediately;
- first run `FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`;
- audit control: `coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-CONTROL.md`;
- audit must correlate final Wave 14 findings, Wave 15 premises/gaps, frozen contracts and FND-05/FND-07 compatibility;
- only `ACCEPTABLE / FC0A_RELEASE_APPROVED` releases the four FC0-A DEVs and allows FND-05/FND-07 to activate in parallel.


## Latest live blocker for successor — INFRA-CI-01B

Do not freeze FND-06 yet.

FND-06 PR #337 merged at `624f2eca456310a2c6156538b3616a06e3be075f`, but exact broad post-merge CI `35940661531` failed on generic PostgreSQL schema initialization `23505 / pg_namespace_nspname_index`.

Active same sequential CODEX order:
`INFRA-CI-01B-POSTGRES-SCHEMA-LOCK-V1`

Control:
`coord/w15-infra-ci-01b-control:docs/WAVE15-INFRA-CI-01B-CONTROL.md`

Work:
`work/w15-infra-ci-01b-postgresql-schema-init`

Do not blind-rerun the failed CI. Fix/review/merge the bounded infrastructure race, require exact broad green CI, then freeze FND-06 and activate the independent post-FND06 FC0-A audit.


## Main audit result after FND-06 freeze

Do not wait for a separate AUD chat to decide FC0-A. Product Owner clarified that the Main Coordinator is responsible for the audit.

Main completed:
`FC0A-POST-FND06-W15-FOUNDATION-AUDIT-01`

Result:
`CHANGES_REQUIRED`

Authoritative report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-RESULT.md`
commit `463d357f8a9c11f774f5da59480e4a17a19569f5`.

Confirmed blockers:
- W15-P1-01 Server Script bounded recovery;
- W15-P1-06 truthful Engineering fallback.

No current contract break is required by FND-05/FND-07.

Current active executor order:
`FC0A-BLOCKER-P101-SERVER-SCRIPT-RECOVERY-V1`

Same sequential CODEX reads:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-AUDIT-BLOCKER-CORRECTION-PREP.md`

Work branch:
`work/w15-fc0a-p101-server-script-recovery`

P1-06 is queued after P1-01. FC0-A DEVs, FND-05 and FND-07 remain HOLD until both blockers close and Main re-audits affected rows.


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


## IMMEDIATE BOOTSTRAP FOR THE NEXT COORDINATOR

1. Read this file completely.
2. Read `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `LAST CHANGE.md`, and `docs/ROADMAP.md`.
3. Revalidate live integration, PRs, issues, CI, branches and dedicated controls; GitHub live wins any conflict.
4. Revalidate PR #348 and T1 `36094394912` first.
5. Treat shared CODEX as unavailable/paused; do not resume a historical active route when capacity returns.
6. Diagnose why the exact candidate `3ecc4a78080685b0556402d50190e09236d6d8fa` publishes the test-owned persisted revision successfully but `/published/activate` returns HTTP 422.
7. Do not classify that 422 as product or harness until the returned activation outcome/runtime issues are inspected.
8. Keep FND-07 DEV in WAIT and do not merge/freeze until exact-head evidence is green and final adversarial validation is explicitly accepted.

## MAIN COORDINATOR FORCED HANDOFF — 2026-09-27

GitHub live remains the sole authority. This handoff supersedes stale coordination snapshots.

### Exact integration checkpoint

- integration branch: `wave15/corrections-integration`
- exact HEAD: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`
- latest exact integrated broad CI: EliteSCADA CI #1584 / run `36290910850` — SUCCESS
  - Backend build/test/smoke: SUCCESS
  - Web build: SUCCESS
  - Chromium end-to-end: SUCCESS

### Six post-FC0A lanes

1. DEV-SCRIPT-ENGINEERING / PR #344
   - MERGED
   - final candidate `79d43f1994acb509628b08e703c20820bf8a9c72`
   - merge SHA `3b1511799a73c3c4fee1c2265005d6724bcaa235`
   - lane state remains `INTEGRATED_PENDING_T2`

2. DEV-EDITOR / PR #349
   - MERGED
   - final candidate `06eed31d99ddb34d99e0287e96e38bed3bf7dab5`
   - merge SHA `cfaafa4b29e1462bf9d304af995cc8638578a3c2`
   - lane state remains `INTEGRATED_PENDING_T2`

3. DEV-AUTHORITY-UX / PR #346
   - MERGED
   - final candidate `f82a5662234e42a73b77f15fbdfd730872cc5cc1`
   - merge SHA `27412e4fbe47dbbb6573229364ee8319369ae076`
   - post-merge CI #1583 / `36288961537`: SUCCESS
   - Main disposition: `INTEGRATED / VERIFIED_COMPLETE`

4. DEV-LICENSING-UX / PR #345
   - MERGED
   - final candidate `3d166c0b45eef34ef878e710b5db28c3ea2fa93f`
   - merge SHA `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`
   - exact-head T1 #113 / `36289854846`: SUCCESS
   - scarce CODEX adversarial validation: Chromium 6/6 PASS; focused Drivers 36/36 PASS; no candidate-causal defect; no source mutation
   - post-merge CI #1584 / `36290910850`: SUCCESS
   - therefore Licensing is ready to be recorded `INTEGRATED / VERIFIED_COMPLETE`

5. FND-05 / PR #347
   - MERGED
   - merge SHA `b2874a00c7f7b35ca8223defd7e3b6bbdd89ecf8`
   - `CODEX_HA_ADVERSARIAL_GREEN`: SATISFIED
   - dedicated control state: `VERIFIED / FROZEN`

6. FND-07 / PR #348 plus corrective PRs #352/#353
   - original PR #348 MERGED at `1b186c48ba5d2e3012be2c58f0efc36170101fe9`
   - final corrective PR #353 MERGED
   - final integrated Foundation checkpoint: `3819715ba3a015a182c97b2a4ebcb4de447da717`
   - post-merge CI #1582 / `36279534882`: SUCCESS
   - dedicated control state: `VERIFIED / FROZEN`

### CRITICAL NEXT GATE — DO NOT ACTIVATE PREVIEW YET

The prepared fresh-install partial preview entry condition requires:
- all four feature lanes integrated **and T2-verified**;
- FND-05 + FND-07 VERIFIED/FROZEN;
- no known P0/P1 invalidating the journey.

The Foundations are satisfied and all four feature PRs are integrated, but the broader integrated feature-lane **T2 has not yet been formally executed/accepted on the current exact integration head**.

Therefore:
`W15-FIRST-PROJECT-FRESH-INSTALL-PARTIAL-PREVIEW = PREPARED / NOT ACTIVE`.

The next coordinator must NOT treat EliteSCADA CI #1584 as an automatic substitute for the explicit T2 contract without first reading the T2 definition/route and recording an exact-head T2 disposition.

### Immediate next safe action for replacement Main

1. Revalidate live GitHub first.
2. Read:
   - `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`
   - `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`
   - `docs/CURRENT-COORDINATOR-HANDOFF.md`
   - `LAST CHANGE.md`
   - `coord/w15-parallel-dev-control:docs/WAVE15-PARALLEL-DEV-CONTROL.md`
   - `coord/w15-fresh-install-preview-control:docs/WAVE15-FIRST-PROJECT-FRESH-INSTALL-PREVIEW-CONTROL.md`
3. Revalidate integration HEAD `1f14a574...` and latest CI.
4. Locate/define the already-intended broader integrated **T2** gate for the four feature lanes on this exact head.
5. Run/accept that T2 before changing Script/Editor from `INTEGRATED_PENDING_T2` to `T2_VERIFIED`.
6. Only after T2 is formally green/accepted, record the exact preview base SHA/tree and activate Moment 1 CODEX black-box preview.
7. Preserve the embargo: detailed CODEX preview findings must not be shown to Product Owner before the independent human fresh-install journey.

### CODEX budget / route state

CODEX has returned but with reduced usage budget.

Current correct shared state after Licensing merge:
- no active validation mission should continue automatically;
- CODEX should remain HOLD until Main decides the exact T2 route;
- old routes must never auto-resume.

### Product Owner action state at handoff

No DEV chat currently requires `SIGA`.

Do not ask the Product Owner to carry technical handoffs between agents. Persist routes in GitHub controls/comments.

