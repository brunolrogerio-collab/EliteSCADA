# EliteSCADA Roadmap — Wave 15

**Status date:** 2026-09-13 (BRT)  
**Active direction:** **WAVE 15 FOUNDATION-FIRST COMPLETE PRODUCT DELIVERY**  
**Integration:** `wave15/corrections-integration`  
**Global issue:** #297  
**Foundation / dependency / parallel DEV orchestration:** #305

Authoritative stable product intent: root `PROJECT GOAL.md`.  
Mutable operational snapshot: root `LAST CHANGE.md`.  
Detailed Main handoff: `docs/WAVE15-MAIN-COORDINATOR-HANDOFF.md`.  
Current pointer: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Copy-ready coordinator rotation prompt: `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`.

> GitHub live always wins for exact branch/SHA/PR/CI state. Historical Wave 14/Test Preview documents remain evidence, not current sequencing authority.

## Product objective

Wave 15 is the complete-product convergence wave. It must combine the already accepted platform foundation with the remaining developer/operator/customer-visible product work rather than optimize for isolated issue closure.

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

Wave 15 deliberately freezes cross-cutting contracts before opening many parallel feature lanes.

Foundation families:

- **FND-01** — Working/lifecycle/bootstrap;
- **FND-02** — Security Authority;
- **FND-03** — Runtime Session Lease / Licensing v2;
- **FND-04** — Server Script recovery/ownership;
- **FND-05** — HA identity/topology/fencing;
- **FND-06** — canonical renderer/visual stability;
- **FND-07** — secure installation detach/neutral bootstrap;
- **FND-08** — WAN/common timing;
- **INFRA-CI-01** — profile-aware Wave 15 CI orchestration.

State model:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

A downstream feature may consume a shared contract only after the required slice is `VERIFIED + FROZEN`.

## Current frozen snapshot

- FND-01 — **VERIFIED/FROZEN**;
- FND-08 common timing contract — **VERIFIED/FROZEN**;
- FND-02 AUTH-01 capability vocabulary/enforcement — **VERIFIED/FROZEN**;
- FND-02 AUTH-02 stable hierarchy/scope identities — **VERIFIED/FROZEN**;
- FND-02 overall — **NOT FROZEN** while required AUTH-03/remaining slices are incomplete.

Exact evidence/SHA belongs in `LAST CHANGE.md` and live issues, not in this roadmap.

## Current active path — FND-02 Authority

### AUTH-03 — ACTIVE

PR #314 owns durable canonical Authority persistence/admin/portable backup v2 and Engineering/package ownership transition.

Required contract includes PostgreSQL version/CAS, deterministic bootstrap/migration, protected administration, self-lockout/orphan protection, strict stable capability/scope identities, Authority backup v2, `.escadapkg` v3 / Engineering schema19 and no second mutable Engineering policy owner.

At this roadmap snapshot the product/Authority test suite and real PostgreSQL Authority persistence gate pass, while a Runtime smoke still fails in a direct Historian read after samples have reportedly been written. Diagnose that narrow discrepancy before AUTH-03 freeze; do not weaken Historian or Authority contracts.

### AUTH-04 — QUEUED / NOT ACTIVE

Starts only after AUTH-03 is integrated/verified/frozen and FND-07/#304 dependency is ready. Owns coordinated safe Authority detach/switch, generation fencing, old-session invalidation, neutral bootstrap, A->neutral->B isolation and restore semantics.

## FC0 checkpoints

### FC0-A — first parallel feature release

Require:

`FND-01 + FND-02 + FND-03 + FND-04 + FND-06 VERIFIED/FROZEN`

plus:

- common FND-08 timing contract frozen;
- INFRA-CI-01 ready/frozen;
- one exact integration checkpoint.

FC0-A releases bounded parallel work for:

- Editor;
- Script Engineering;
- Authority UX;
- Licensing UX.

Start with controlled concurrency, normally no more than four active coding DEVs.

### FC0-B — full foundation release

Add:

- FND-05 HA;
- FND-07 installation detach.

FC0-B releases:

- EliteGO;
- Installation UX;
- downstream HA implementation that consumes frozen HA contracts.

F0 is complete only at FC0-B.

## Parallel DEV model after release

Every DEV mission uses one isolated branch and one PR to `wave15/corrections-integration` with an exact base SHA and explicit owned/forbidden boundaries.

A DEV may not:

- write directly to integration/main;
- merge its own PR;
- silently alter a frozen shared contract;
- borrow green CI from another SHA.

If a frozen contract is insufficient, report `BLOCKED-CONTRACT`; Main/Foundation owns the delta before downstream work resumes.

## CI roadmap — INFRA-CI-01

All current workflows support `workflow_dispatch`, but many automatic triggers still reflect `main` or Wave 14 branches. Do not solve that by wiring every heavy workflow to every Wave 15 PR.

Target tier model:

- **T0** — local focused validation;
- **T1** — DEV PR sanity/profile;
- **T2** — integrated broader validation;
- **T3** — exact integration checkpoint;
- **T4** — final complete-product validation.

Seven-Driver, browser, Licensing, HMI and other heavy suites run when risk/ownership/profile justifies them and at broader checkpoints.

A ChatGPT connector lacking the operation to create a new `workflow_dispatch` is a tool limitation, not repository limitation. Use Work/Codex/CLI dispatch when available; existing-run reruns are separate operations and require diagnosis before use.

## Product contracts that guide downstream work

### Security Authority

Roles are editable templates/custom roles with explicit capabilities and stable scope hierarchy. Role names never grant privilege. Authority controls what an identity may do; backend enforces it. `CommandExecute` remains distinct from `ProcessValueWrite`.

### Licensing / Runtime Session Class

Authority permissions, Session Class and commercial license quotas are separate layers. Session Class can only restrict Authority. One logical runtime session owns one lease across transports/reconnect/failover; Web and EliteGO share server-owned quotas.

### EliteGO

Separate runtime-focused companion app consuming canonical Active through public APIs/realtime. It does not own HA election or an independent licensing authority.

### Editor

Use the canonical Runtime renderer with Engineering overlays. Design/Preview share renderer semantics; Working design never silently becomes Active Runtime truth.

### Installation switch

Application package, Authority backup, Historian/database and License remain separate authorities even when one UX coordinates a safe detach. Fence process effects, invalidate old sessions and never silently delete Historian or leak project-A identities into project B.

### WAN/timing

No global timeout inflation; GET retry bounded and safe; writes never blind-retry; timeout can be unknown outcome; stale responses cannot overwrite newer state.

## Final Wave 15 acceptance path

```text
Foundation slices frozen
  -> FC0-A checkpoint
  -> bounded parallel Editor/Script/Authority UX/Licensing UX
  -> remaining FND-05/FND-07
  -> FC0-B checkpoint
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
