# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — PARALLEL WORK ENABLED / NEXT WORKER WAVE ASSIGNED**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

The previous integration wave is complete. The last functional merge is PR #45:

`889c989fdce26d8593e86e430e76417412846400`

After that merge, coordinator-owned documentation synchronization advanced `main` through:

- `5c20df835605ce807ce8ec62036324da067f1d23` — permanent `siga` coordination flow;
- `be02a1d9029444fc40a55995c42a9511e6a79650` — reset worker assignments;
- `c6725c57d5201563f9fd2db207ede137780c01f0` — roadmap synchronization;
- `b6439d121ab6ded0303f307bf4d38da3f6ab40bc` — Engineering UI baseline synchronization;
- `e01ddcb916422df69fcd12a5373b30049f30a0e9` — Security/Audit baseline synchronization;
- `529b37259484542ff4f4a8bf6276088c56fd70a6` — closed integration-wave handoff;
- `d11ee06505cbd9c2f37672f2be028f77b49e6a75` — next parallel worker assignments recorded in `docs/CHAT-WORK-ASSIGNMENTS.md`.

The commit containing this `LAST CHANGE.md` is newer than the SHAs above and must be obtained from current GitHub `main` when resuming.

At assignment time there were **no open Pull Requests**. Historical feature branches remain in the repository but are not active assignments.

## PERMANENT CHAT COORDINATION MECHANISM

The live assignment board is:

`docs/CHAT-WORK-ASSIGNMENTS.md`

Canonical short command:

`siga`

`continue` is an equivalent alias.

Every fixed EliteSCADA chat must reread current `main`, locate its exact assignment and verify real branch/PR/head/CI state before acting. Workers with `AfterCompletion: WAIT_FOR_COORDINATOR` do not select their own next task.

## MERGED PRODUCT STATE

### PR #35 — First-class Operational Commands

Merge: `2fd568976fc6277d0b069adeeb560f6ea3d8205f`.

Engineering Schema v7 and first-class Operational Commands are official state.

### PR #36 — Protected runtime read and realtime surfaces

Merge: `10b0320149c1ef2109e9517539717a8800b200c2`.

Protected TAG/historian/alarm/Engineering/diagnostic reads and authenticated `/ws/tags` are official state.

### PR #37 — Engineering UI foundation/localization

Merge: `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`.

`/engineering`, Runtime↔Engineering navigation, localization and structured Engineering editors are official state.

### PR #38 — Local identity/browser login

Merge: `2a581d279a428cb605429d5939c333ff7ad8d1b4`.

Trusted local identity/browser login foundation is official state.

### PR #39 — Protected local user administration

Merge: `6de8f06a443ad829ccc95c6dfcd9511e906adeff`.

Protected local-user administration, safe DTOs, role assignment, last-admin protection and session invalidation are official state.

### PR #40 — Internal Memory / Source Provider Foundation

Merge: `bb38617c9c27cb5c379973a6f65d66006f24eadc`.

Official merged foundation includes the protocol-neutral Source Provider boundary, `builtin.memory.server`, `builtin.memory.client`, typed values/defaults, stable TAG-ID retention identity, in-memory Server Memory retention semantics and per-client Client Memory isolation.

This is a **foundation**, not complete product integration. Public Engineering representation, import/export/schema migration, durable production retention, runtime composition and related historian/alarm/security behavior remain incomplete.

### PR #41 — Python Scripting + Visual Property Foundation

Merge: `fc0731309d5b92d302f019d06d3511d3a247b607`.

Official merged foundation includes typed visual properties, runtime overrides, script scopes, sandbox/API boundaries, tween contracts, runtime instances, bounded event queues, execution budgets and diagnostics.

First-class Script Engineering integration and later concrete editor/sandbox/runtime implementation remain incomplete.

### PR #42 — Secured Engineering Apply/Delete/Bulk

Merge: `6d49b99181fce6dabce838822ce972332e2f77f0`.

Preview-gated Apply, optimistic Workspace version/CAS, explicit dependency-aware Delete, bounded Bulk Preview/Apply, authorization, audit and Engineering UI mutation panels are official state.

### PR #43 — Historian Retention + Downsampling Foundation

Merge: `0c5f2aefdd5a7286c0c9367569067e2d12091c81`.

Typed retention/downsampling policies, 1m/5m/15m/1h aggregates, quality-aware aggregation and Timescale continuous-aggregate foundations are official state.

### PR #44 — Audit Durability + Retention + Query Foundation

Merge: `9406fb2d66c682bd6bde08a0facde0622aa86ff2`.

Append-only Audit store evolution, stable keyset pagination, bounded filters, storage health, retention and bounded asynchronous outage buffering are official state. The official Engineering Bulk Audit action key is `engineering.bulk.apply`.

### PR #45 — Audit Runtime Integration

Merge: `889c989fdce26d8593e86e430e76417412846400`.

CI run #241 completed successfully with Web build, backend build/tests, PostgreSQL/Timescale coverage, runtime smoke and Chromium/Playwright E2E all green.

Official runtime integration includes configured Audit query/retention/buffer policies, `BufferedAuditSink`, protected diagnostics, bounded keyset queries/cursors and periodic retention.

The current Audit buffer is **not** a crash-durable persistent outbox.

## TEST/CI HYGIENE

During the previous integration wave, an existing Modbus test was found to use an artificial 100 ms test timeout. The test-only timing boundary was widened to a realistic CI value after repeated confirmation of the root cause. Production driver timeout behavior and Modbus protocol logic were not changed.

## CURRENT CHAT ASSIGNMENTS

The exact live state is authoritative in `docs/CHAT-WORK-ASSIGNMENTS.md`.

Current assigned wave:

1. **COORDENADOR - EliteSCADA** — coordinating the dependency-safe worker wave and owning shared integration.
2. **DEV 1 - EliteSCADA** — `ASSIGNED`: Audit UI and diagnostics client foundation on `feature/audit-ui`.
3. **DEV 2 - EliteSCADA** — `ASSIGNED`: Internal Memory Engineering + durable Server Memory product integration on `feature/internal-memory-product-integration`.
4. **DEV 3 - EliteSCADA** — `ASSIGNED`: Public Script Engineering integration foundation on `feature/script-engineering-integration`.

Shared-contract ownership for this wave is explicit:

- DEV 2 alone has the worker exception to modify `src/Scada.Engineering/Contracts/EngineeringContracts.cs`, and only for Internal Memory requirements;
- DEV 3 must keep Script Engineering work isolated from that central contract and report coordinator-owned canonical-schema hooks as `INTEGRATION REQUIRED`;
- DEV 1 must keep Audit UI isolated from central frontend routing/application-shell files and report the final route/navigation hook as `INTEGRATION REQUIRED`.

## LOCKED PRODUCT ORDER

### Source/protocol chain

The mandatory sequence remains:

`Internal Memory complete product integration -> TAG Gateway -> common multi-driver/Data Source diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

**Internal Memory remains the next locked source/protocol block.**

TAG Gateway is still **SPECIFIED / NOT IMPLEMENTED** and must not begin merely to keep a worker occupied while Internal Memory integration is incomplete.

### Visual/Python chain

The mandatory sequence remains:

`public Script/visual Engineering integration -> Python editor/sandbox -> visual runtime object/property integration -> graphical Screens/Popups/Dynamos editor -> advanced libraries`

DEV 3's current assignment advances only the first integration stage and deliberately does not start a concrete Python engine/editor or graphical editor.

### Historian

PR #43 foundation is merged. Public Engineering storage-policy integration, runtime configuration and raw-vs-aggregate trend resolution remain future work.

### Audit

PRs #44/#45 are merged. DEV 1 is now assigned the isolated Audit UI/client feature. A crash-durable persistent outbox and a distinct weaker Audit-read capability remain separate future designs and are not part of the current assignment.

## IMMEDIATE CONTINUATION

The three fixed DEV chats have explicit new assignments in `docs/CHAT-WORK-ASSIGNMENTS.md`.

The user may now send only `siga` in each DEV chat. Each DEV must reread current `main`, verify/create only its assigned branch, and execute only its recorded task.

Coordinator resume behavior on the next `siga`:

1. reread all mandatory documents from current `main`;
2. inspect active branches, open PRs, heads, ahead/behind state and CI;
3. review worker PR bodies and `INTEGRATION REQUIRED` sections;
4. prioritize DEV 2 Internal Memory central-contract reconciliation before adding DEV 3 Script entities to the canonical Engineering schema;
5. integrate DEV 1 route/navigation centrally only after its isolated Audit feature is reviewed;
6. merge only reviewed green work;
7. update assignments and roadmap/handoff documentation to actual merged state.

## Permanent continuity rules

- Feature branches must never be the sole durable home of permanent architecture decisions.
- Open PRs remain **IMPLEMENTED IN PR**, never **MERGED**.
- GitHub branch/PR/head/CI state is operational truth.
- Worker assignment authority comes only from `docs/CHAT-WORK-ASSIGNMENTS.md` as maintained by the coordinator.
- `siga` is the canonical short user command; `continue` is equivalent.
- Completed workers do not create their own next work.
- Dependency-safe idle time is preferable to conflicting parallel branches that later require semantic reconstruction.
