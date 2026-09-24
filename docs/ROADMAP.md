# EliteSCADA Roadmap — Wave 15

**Status date:** 2026-09-24 (BRT)  
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

## Live current gate — 2026-09-24

This section supersedes older execution-status prose below when describing the **current** Wave 15 gate. Historical slice descriptions remain useful as execution record but are not current authorization.

- FND-01 — **VERIFIED/FROZEN**.
- FND-02 incl. AUTH-04 — **VERIFIED/FROZEN**.
- FND-03 — **VERIFIED/FROZEN**.
- FND-04 — **VERIFIED/FROZEN**.
- FND-06 — **VERIFIED/FROZEN** at `560ac9d80cc7e854f2513559dc6afb28cfb4aee3` / tree `674019fbbc21001a2d68deb853c2c0b293e0a5cb`.
- FND-08 — **VERIFIED/FROZEN**.
- FND-05 — **PREPARED / HOLD**.
- FND-07 — **PREPARED / HOLD**.
- post-FND06 FC0-A audit — **CHANGES_REQUIRED**.
- second deep pass — **NO NEW PRE-FC0A BLOCKER IDENTIFIED**.
- third targeted gap pass — **NO NEW PRE-FC0A BLOCKER IDENTIFIED**.
- confirmed pre-FC0A blockers originated as **W15-P1-01** and **W15-P1-06**, but Main intentionally consolidated the known FC0-A correction backlog into PR #340.
- active correction path: `FC0A-CONSOLIDATED-CORRECTION-PACKAGE-V3` on `work/w15-fc0a-consolidated-corrections`; PR #340 is OPEN / V2 IN PROGRESS at exact head `6f19029b52641d5644fe0cf7365c119f7f87fa9a`.
- natural Wave 15 T1 on that checkpoint, run `36005490966`, is SUCCESS; remaining V2 reconciliation/validation is still in progress.
- DEV-EDITOR, DEV-SCRIPT-ENGINEERING, DEV-AUTHORITY-UX, DEV-LICENSING-UX, FND-05 and FND-07 remain **PREPARED / HOLD** until Main records `FC0A_RELEASE_APPROVED` on the exact integrated checkpoint.

Authoritative third-pass report:
`coord/w15-fnd06-control:docs/WAVE15-FC0A-POST-FND06-AUDIT-THIRD-PASS.md`.

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
