# Wave 15 — Main Coordinator Handoff

> Persistent operational handoff for changing the Main Coordinator chat.
>
> **GitHub live is the source of truth.** Every SHA, PR, issue, Actions run, dependency state and mergeability statement below is a snapshot and must be revalidated before acting.

**Snapshot date:** 2026-09-13 (BRT)  
**Wave:** 15 — complete product delivery  
**Integration branch:** `wave15/corrections-integration`  
**Snapshot integration SHA:** `bc68bf450f6efd42b90898ad0656bea9b7543f57`

## 1. First actions for a new Main Coordinator

Before deciding, assigning, reviewing, rerunning CI or merging anything:

1. read `PROJECT GOAL.md`;
2. read `LAST CHANGE.md`;
3. read this file and `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
4. inspect live issue #297 and execution issue #305;
5. inspect the currently active foundation/product issue and its latest comments;
6. re-fetch `wave15/corrections-integration` and record its exact SHA/tree;
7. re-fetch every active PR and its exact base/head/tree;
8. inspect exact-head CI/Actions evidence;
9. treat repository/CI reality as authoritative when chat memory or stale documentation disagrees.

Never continue from a remembered SHA merely because it appears in this document.

## 2. Main Coordinator operating model

The Main Coordinator owns the dependency graph, exact base SHA assignment, activation of work packages, review of Work/DEV handoffs, integration order, CI disposition, checkpoint/freeze records and escalation to the Product Owner.

State progression is:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

A dependency is consumable only when its required contract is `VERIFIED + FROZEN`. An open PR, a green focused test or a Work handoff alone does not freeze a dependency.

When the Product Owner says `siga`, continue the current safe coordination sequence until completion or a genuine blocker. `siga` does not authorize a protected merge to `main`.

Permanent repository rules:

- no direct mutation of `main`;
- no force push, destructive rebase or evidence deletion;
- one bounded mission = one isolated branch/PR;
- no DEV merges its own PR;
- red CI is diagnosed before rerun;
- tests/contracts are never weakened to manufacture green;
- no opportunistic cross-scope refactor;
- no generic defect is patched with an EEE-only workaround;
- Alarm, Operational Event and Audit remain separate authorities;
- Runtime/Active must not depend on `.escadalib` as runtime truth;
- Security, Identity, authentication/authorization, Engineering Lock, Licensing, lifecycle, package boundaries, Active Runtime, Historian semantics and Drivers may not be weakened to ease a feature.

## 3. Main Coordinator, Work and parallel DEVs

### Main Coordinator

Coordinates and reviews. It does not silently become a competing implementation lane when Work already owns the active foundation mission.

### Foundation Work / technical reviewer

The separate Work chat owns one ACTIVE foundation/high-risk mission at a time unless Main explicitly opens another. Work must continue independent implementation while GitHub Actions runs. CI is evidence, not a reason to sit idle.

Work may use Actions as a validation fallback when the local environment lacks Chromium/Playwright, PostgreSQL/service containers, Docker/native dependencies or another CI-provided runtime. A required test not executed is `PENDING`, never `PASS`.

Work stops for a genuine contract/code blocker, unsafe scope ambiguity/conflict or completion/handoff. When Work asks Main a coordination question, Main should read the live issue/PR thread, answer there and then report the decision to the Product Owner.

### Parallel GPT DEVs

Parallel implementation remains gated by frozen shared foundations. Initial concurrency after release should be deliberately bounded, normally no more than four active coding DEVs.

Every DEV mission must define: DEV-ID/parent issue, exact required base SHA, status, hard/soft dependencies, owned boundary, forbidden/shared-authority boundary, frozen contracts consumed, permitted outputs, deterministic tests, branch name, PR target `wave15/corrections-integration`, handoff prefix, exact commit/tree and CI evidence.

If a DEV discovers a frozen shared contract is insufficient, it must report `BLOCKED-CONTRACT`. It must not redesign the shared contract inside its downstream feature PR.

## 4. Foundation and release checkpoints

Foundation families:

- FND-01 — Working/lifecycle/bootstrap;
- FND-02 — Security Authority;
- FND-03 — Runtime Session Lease / Licensing v2;
- FND-04 — Server Script recovery/ownership;
- FND-05 — HA identity/topology/fencing;
- FND-06 — canonical renderer/visual stability;
- FND-07 — secure installation detach/neutral bootstrap;
- FND-08 — WAN/common timing;
- INFRA-CI-01 — profile-aware Wave 15 CI orchestration.

### FC0-A

Release Editor, Script Engineering, Authority UX and Licensing UX only after:

`FND-01 + FND-02 + FND-03 + FND-04 + FND-06 VERIFIED/FROZEN`

plus the common FND-08 timing contract frozen, INFRA-CI-01 ready/frozen and one exact integration checkpoint recorded.

### FC0-B

Add FND-05 and FND-07 to release EliteGO, Installation UX and bounded downstream HA work. F0 is complete only at FC0-B.

## 5. Current frozen foundation snapshot

### FND-01 — VERIFIED/FROZEN

Deterministic persisted Working bootstrap/catalog/load/assets/recovery; neutral Working on empty catalog; conflict/invalid explicit references fail closed; deterministic fallback ordering.

### FND-08 common timing — VERIFIED/FROZEN

`elitescada.timing-policy/v1`. No global timeout inflation; bounded GET retry; writes never blind-retry; timeout may mean unknown outcome; stale response cannot overwrite newer truth.

### FND-02 / AUTH-01 — VERIFIED/FROZEN

- `elitescada.authority-policy/v1` public capability IDs;
- legacy enum ordinals 0..10 preserved;
- `EngineeringView=11`, `HighAvailabilityObserve=12`, `HighAvailabilityTransfer=13`, `HighAvailabilityAdmin=14`;
- Engineering View and Modify independent;
- HA capabilities vocabulary-only and deny-by-default at this slice;
- unknown/numeric capability input fails closed;
- role names do not grant privilege;
- multi-role composition additive/deterministic;
- capability-first TAG authorization; role lists may only narrow;
- `CommandExecute` remains distinct from `ProcessValueWrite`.

### FND-02 / AUTH-02 — VERIFIED/FROZEN

- Engineering schema 18 stable hierarchy/scope-node Guid identities;
- non-null scoped grants require stable ScopeNodeId; null means global only;
- malformed/stale/ambiguous scopes fail closed;
- explicit parent/descendant semantics, never string-prefix inheritance;
- stable TAG/screen/command/equipment bindings;
- display rename does not alter authorization;
- legacy textual scope migration only when exact/deterministic.

## 6. ACTIVE snapshot — W15-AUTH-03 / PR #314

**Revalidate live before acting.** Snapshot:

- PR #314: `W15 AUTH-03: persist canonical Security Authority`;
- base: `wave15/corrections-integration@bc68bf450f6efd42b90898ad0656bea9b7543f57`;
- branch: `work/w15-auth-03-security-authority-persistence`;
- head: `f8d56f8cb87ba0b51d33c58c7597a1466b4bd9c1`;
- status at handoff: OPEN / mergeable / NOT FROZEN.

AUTH-03 mission is the durable canonical Authority boundary:

- PostgreSQL Authority policy store with explicit version/CAS;
- deterministic first-init/legacy migration, no role-name inference;
- Authority-owned roles/scopes, Engineering sees versioned reference/projection instead of a second mutable owner;
- protected policy preview/apply administration;
- self-lockout/orphan and stale-concurrency protection;
- Authority backup v2 with authenticated encryption and exact identities+policy; v1 readable but policy-required;
- `.escadapkg` v3 / Engineering schema19 transition with deterministic legacy handling;
- stable audit identifiers and no secrets;
- preserve AUTH-01/AUTH-02 exactly.

Earlier contract blockers A/B/C were corrected and code review judged the contract clean before the latest CI-oriented commits. Current head additionally contains narrow CI fixture cleanup, smoke DB isolation and explicit API authorization DI construction. Review any new delta before accepting.

### Current validation evidence

Actions run `34766682415` on exact head `f8d56f8...`:

- Web build: PASS;
- backend restore/build: PASS;
- full .NET test stage: PASS;
- real PostgreSQL Authority persistence/CAS test: PASS (`PostgreSqlAuthorityPolicyStoreTests.PersistsPolicyAcrossRestartAndRejectsStaleCompareAndSwap`);
- backend job remains RED at Runtime smoke;
- a rerun of the existing failed backend job reproduced the same failure.

The current smoke failure is **not** the previously suspected lifecycle `ChangesPending` assertion. Execution reaches `/health`, Runtime diagnostics and `Runtime exposed 7 TAGs`. Diagnostics report TimescaleDB historian with `writtenSamples=7`. The next direct call to `/api/history/{id}` for `Demo.Tank01.Level` yields no sample at that point, causing `assert len(history) >= 1`. Later historical-query/security/lifecycle assertions are never reached.

Therefore the immediate task is a narrow historian-smoke diagnosis: timing/identity/query/routing or a stale smoke assumption. Do not weaken Historian or AUTH contracts. Do not call the entire PR green merely because the Authority PostgreSQL gate passed. If the failure is a stale/non-causal CI fixture, correct it in a bounded, evidence-backed way and revalidate the exact resulting head.

## 7. AUTH-04 is queued, not active

Do not activate AUTH-04 until AUTH-03 is integrated/verified/frozen and its FND-07/#304 dependency is ready.

Target contract includes installation/Authority generation, protected detach authorization, Runtime/Driver/Script/command fencing, session/token/lease invalidation, neutral bootstrap, rollback on failed transition, A->neutral->B isolation, Authority v2 backup recommendation and license independence.

## 8. GitHub Actions operating rule

All current workflows in `.github/workflows` support `workflow_dispatch`, but a given ChatGPT GitHub connector may not expose the mutation to create a new manual dispatch. Distinguish repository capability from connector capability.

For an existing run, the connector may expose rerun of one job or all failed jobs. Rerun only after diagnosis.

If a new manual dispatch is required and Main lacks the dispatch operation, delegate to Work/Codex/CLI `gh workflow run` when available. Never create an empty commit, retarget a PR to `main` or mutate workflow code merely to make CI start.

Most current automatic triggers still reflect `main` or Wave 14 branches. Do not solve this by adding `wave15/corrections-integration` to all old workflows. INFRA-CI-01 is the intended fix.

Validation tiers:

- T0 — local focused evidence;
- T1 — Wave 15 DEV PR sanity/profile;
- T2 — integrated broader validation;
- T3 — checkpoint validation;
- T4 — final complete-product validation.

Seven-driver, browser and other heavy suites are causal/risk-based rather than automatically paid for by every small PR.

## 9. Wave 15 product scope

Wave 15 is the complete-product delivery wave. It includes material generic Wave 14 corrections, developer-functional Screen/Popup Editor, Script Engineering, configurable granular Authority, Licensing v2, EliteGO, HA/redundancy, installation detach/switch, WAN resilience, profile-aware CI, industrial visuals/library thumbnails, contextual Manual/Help, pt-BR/en/es, the representative EEE Sim/Real Modbus v15 system, complete integration and a fresh Codespaces Product Owner audit.

### EliteGO

Distinct companion application, Runtime-focused rather than another Engineering/admin browser. It consumes server-owned Active Runtime truth, public APIs/realtime, screens/assets/alarms/trends and capability-authorized writes. It stores/uses server A/B topology but performs no client-side HA election and owns no independent license authority.

### Licensing v2

Server-owned shared Web + EliteGO session accounting. User Authority answers what the identity may do; Runtime Session Class (`Interactive` or `View Only`) is a restrictive ceiling; commercial license answers how many logical sessions may be admitted. One logical session = one lease across transports/reconnect/failover, with no double counting. HA nodes remain machine-licensed with an explicit redundancy entitlement.

### Authority

Editable role templates plus custom roles, explicit stable capability IDs and stable hierarchy/scopes. Role display names have no security semantics. Backend is final authority. `CommandExecute` and `ProcessValueWrite` remain distinct.

### Editor

Reuse the canonical Runtime renderer with Engineering overlays. Design/Preview share the same viewport/renderer boundary. Working design state never becomes Active Runtime authority merely because it renders.

### Installation switch

Application, Security Authority, Historian/database and License are separate authorities even when the UI coordinates a safe Application+Authority detach. Recommend separate exports, fence process effects, invalidate prior sessions and return to secure neutral bootstrap. Never silently delete Historian. License keep/remove/replace is a separate transactional operation.

## 10. Permanent architecture boundaries

- public/versioned Engineering model is authoritative;
- stable IDs outrank mutable display names/paths;
- Runtime derives from persisted Active revision, not mutable Working;
- server owns global process truth;
- clients do not directly access Drivers, database or private Runtime internals;
- public integration is through supported API/realtime contracts;
- `.escadapkg`, Authority backup, Historian/database and License remain separate authority artifacts;
- credentials/private signing material/secrets never become plaintext Engineering/package content;
- protected material uses secure references/resolvers;
- protected actions are backend-authorized and auditable;
- Wave 13 #205/#207 stays paused until a separate Product Owner maturity decision;
- final Wave 15 acceptance requires an exact integrated candidate, fresh Codespaces Preview, real Product Owner browser exercise, correlated findings and bounded correction/recheck.

## 11. Product Owner interaction convention

Do not make the Product Owner babysit Work or GitHub Actions. Coordinate Work through the repository, keep Work moving while CI runs, and report only meaningful state changes/blockers.

When the Product Owner asks to continue, continue rather than repeatedly requesting permission for already-authorized safe coordination actions.

For control, end coordinator-facing messages with the local America/Sao_Paulo time in the form:

`Hora: HH:MM`

## 12. Authoritative issue map

- #297 — Wave 15 complete product delivery / global status;
- #305 — dependency graph, Foundation checkpoints, parallel DEV orchestration and CI coordination rules;
- #302 — Security Authority / FND-02;
- #301 — Runtime Session Lease / Licensing;
- #303 — Editor;
- #304 — installation detach/switch;
- #298 — EliteGO;
- #299 — HA/redundancy;
- #300 — final integration and fresh Preview.

Read latest comments, not only issue bodies. Issue bodies may contain creation-time sequencing superseded by later binding comments.

## 13. Immediate resume sequence from this snapshot

1. Revalidate integration SHA and PR #314 head.
2. Read newest #302 and #314 comments for Work messages after this handoff.
3. Diagnose the reproducible direct historian smoke failure on exact AUTH-03 head without broadening scope.
4. Review any resulting delta and exact-head validation.
5. Only when AUTH-03 has acceptable code + required evidence, merge it into `wave15/corrections-integration`, verify merge tree/parents and record INTEGRATED -> VERIFIED -> FROZEN evidence in #302/#297.
6. Do not start AUTH-04 early.
7. Continue remaining FC0-A foundation work and INFRA-CI-01 toward the exact checkpoint that releases the first parallel DEV batch.
