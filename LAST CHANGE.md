# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md` and `docs/CHAT-WORK-ASSIGNMENTS.md` before every EliteSCADA task.

**Handoff date:** 2026-08-26
**Development state:** **ACTIVE — PARALLEL WORK ENABLED / WORKERS CURRENTLY WAITING**

Repository truth is separated into **MERGED**, **IMPLEMENTED IN PR**, and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

The integration wave that previously left PRs #40–#44 waiting has been completed.

The final functional code head before this documentation synchronization was:

`889c989fdce26d8593e86e430e76417412846400`

That commit is the merge of PR #45 and contains the completed Audit runtime integration after PR #44.

After that functional merge, coordinator-owned documentation was synchronized directly on `main`:

- `5c20df835605ce807ce8ec62036324da067f1d23` — permanent `siga` coordination flow in `docs/PARALLEL-WORK.md`;
- `be02a1d9029444fc40a55995c42a9511e6a79650` — reset live worker assignments in `docs/CHAT-WORK-ASSIGNMENTS.md`;
- `c6725c57d5201563f9fd2db207ede137780c01f0` — roadmap synchronized after PRs #40–#45;
- `b6439d121ab6ded0303f307bf4d38da3f6ab40bc` — Engineering UI baseline synchronized with secured Apply/Delete/Bulk;
- `e01ddcb916422df69fcd12a5373b30049f30a0e9` — Security/Authorization/Audit baseline synchronized with current local identity, user administration and Audit runtime integration.

The commit containing this `LAST CHANGE.md` is newer than the SHAs above and should be obtained from current GitHub `main` when resuming.

## PERMANENT CHAT COORDINATION MECHANISM

The repository-owned live assignment board is:

`docs/CHAT-WORK-ASSIGNMENTS.md`

The user's canonical short command is now:

`siga`

`continue` remains a backward-compatible alias with identical meaning.

Every fixed EliteSCADA chat must, on `siga`:

1. identify itself by fixed chat/workstream name;
2. reread current `main` documents;
3. locate its exact assignment in the assignment board;
4. verify real GitHub branch/PR/head/CI state;
5. execute only the task explicitly authorized there;
6. never ask the user to copy the previous technical task prompt again.

The intended low-friction loop is:

`DEV reports result -> user sends siga to COORDENADOR -> coordinator verifies/integrates/assigns -> user sends siga to DEV -> DEV starts exact new assignment`

Workers with `AfterCompletion: WAIT_FOR_COORDINATOR` do not create new work or choose a roadmap item independently.

## MERGED PRODUCT STATE

### PR #35 — First-class Operational Commands

Merge commit:

`2fd568976fc6277d0b069adeeb560f6ea3d8205f`

Engineering Schema v7 and first-class Operational Commands are official state.

### PR #36 — Protected runtime read and realtime surfaces

Merge commit:

`10b0320149c1ef2109e9517539717a8800b200c2`

Protected TAG/historian/alarm/Engineering/diagnostic reads and authenticated `/ws/tags` are official state.

### PR #37 — Engineering UI foundation/localization

Merge commit:

`4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`

`/engineering`, Runtime↔Engineering navigation, localization and structured Engineering editor foundation are official state.

### PR #38 — Local identity/browser login

Merge commit:

`2a581d279a428cb605429d5939c333ff7ad8d1b4`

Trusted local identity/browser login foundation is official state.

### PR #39 — Protected local user administration

Merge commit:

`6de8f06a443ad829ccc95c6dfcd9511e906adeff`

Protected local-user administration, safe DTOs, role assignment, last-admin protection, security-version invalidation and realtime session revocation are official state.

### PR #40 — Internal Memory / Source Provider Foundation — previous DEV 2 work

Merge commit:

`bb38617c9c27cb5c379973a6f65d66006f24eadc`

Official merged foundation includes:

- protocol-neutral `ISourceProvider` boundary;
- `builtin.memory.server` and `builtin.memory.client` descriptors;
- strict typed defaults/values;
- stable TAG-ID retention identity;
- deterministic in-memory Server Memory retention foundation;
- path rename preservation by stable ID;
- incompatible retained-type fail-closed behavior;
- removed-TAG non-resurrection;
- normal `Good` memory quality;
- per-runtime-client Client Memory isolation.

This is a **merged foundation**, not complete product integration. Public Engineering representation, canonical import/export, runtime composition, durable production Server Memory retention and associated historian/alarm/security integration remain roadmap work.

### PR #41 — Python Scripting + Visual Property Foundation — DEV 3

Merge commit:

`fc0731309d5b92d302f019d06d3511d3a247b607`

Official merged foundation includes:

- typed visual property schema/contracts;
- Engineering base vs runtime presentation overrides;
- deterministic base/binding/script/animation precedence;
- Client Visual vs Server script scopes;
- sandbox/API boundary contracts;
- tween scheduler contracts;
- stable visual/runtime-instance identity;
- safe visual-object API;
- bounded event/subscription queues;
- execution budgets/cancellation/fault isolation;
- diagnostics and Python validation/editor diagnostic contracts.

Still pending are first-class Script Engineering integration, concrete Python engine/editor/sandbox, browser runtime adapters/renderer and final graphical editor.

### PR #43 — Historian Retention + Downsampling Foundation — DEV 2

Merge commit:

`0c5f2aefdd5a7286c0c9367569067e2d12091c81`

Official merged foundation includes:

- typed retention/downsampling policies;
- explicit destructive-retention approval semantics;
- 1m/5m/15m/1h buckets;
- quality-aware numeric aggregation;
- nonnumeric type protection;
- Timescale continuous aggregates;
- data-type-aware raw storage metadata;
- serialized concurrent historian DDL initialization;
- retention/downsampling store abstraction and focused Timescale tests.

Public/versioned Engineering storage-policy representation, schema migration/import-export, central configuration and future raw-vs-aggregate trend selection remain product integration work.

### PR #42 — Secured Engineering Apply/Delete/Bulk — COORDENADOR

Merge commit:

`6d49b99181fce6dabce838822ce972332e2f77f0`

Official merged behavior includes:

- preview-gated backend-authoritative Apply;
- Workspace mutation serialization;
- optimistic Workspace version/CAS preconditions;
- explicit dependency-aware TAG/Alarm/Data Source Delete;
- no delete-by-omission and no cascade;
- safe selected-entity Bulk Preview/Apply;
- `EngineeringModify` authorization;
- structural Audit;
- Engineering mutation UI panels and Chromium/security coverage.

The earlier Chromium locator regression was fixed without weakening the underlying identifier assertion before merge.

### PR #44 — Audit Durability + Retention + Query Foundation — DEV 1

Merge commit:

`9406fb2d66c682bd6bde08a0facde0622aa86ff2`

Official merged foundation includes:

- append-only Audit store extensions;
- stable keyset pagination;
- combined bounded filters;
- optional trusted structural context;
- storage health snapshots;
- PostgreSQL schema/index evolution;
- controlled bounded retention;
- bounded asynchronous Audit buffer/retry/overflow behavior;
- storage-boundary sensitive metadata sanitization.

During coordinator reconciliation, the official Engineering Bulk Audit action key was standardized as:

`engineering.bulk.apply`

The competing `engineering.bulk-edit` vocabulary was removed.

### PR #45 — Audit Runtime Integration — COORDENADOR

Merge commit:

`889c989fdce26d8593e86e430e76417412846400`

CI #241 was fully green before merge:

- Web build: **SUCCESS**;
- Backend restore/build: **SUCCESS**;
- full automated backend/PostgreSQL/Timescale test suite: **SUCCESS**;
- runtime smoke: **SUCCESS**;
- Chromium/Playwright E2E: **SUCCESS**.

Official merged behavior includes:

- validated `Audit:Query`, `Audit:Retention` and `Audit:Buffer` configuration;
- `BufferedAuditSink` used for bounded asynchronous writes;
- underlying durable `IAuditStore` retained for initialization/query/retention;
- `/api/audit` moved internally to bounded keyset query while preserving existing array response compatibility;
- filters for target kind/id, area and correlation ID;
- opaque next-page cursor through `X-EliteSCADA-Audit-Next-Cursor`;
- protected `/api/audit/diagnostics`;
- configured finite retention through a hosted service;
- retention retry without logging arbitrary storage exception text.

Current buffer is intentionally **not** claimed as a crash-durable persistent outbox. A process crash while events remain buffered may still create an Audit gap.

## TEST/CI HYGIENE FOUND DURING INTEGRATION

During this integration wave, an existing Modbus test repeatedly failed on an artificial 100 ms timeout unrelated to the worker features being integrated.

After repeated confirmation that the same test-only timing boundary was the cause, the test configuration timeout was widened to a realistic CI value without changing production driver timeout behavior or protocol logic.

Subsequent integrated backend suites passed.

## CURRENT CHAT ASSIGNMENTS

The exact live state is authoritative in:

`docs/CHAT-WORK-ASSIGNMENTS.md`

Current checkpoint:

1. **COORDENADOR - EliteSCADA** — integration wave complete; `WAITING` until the permanent bootstrap text is installed in the DEV chats; next `siga` after that installation selects/records the next safe roadmap wave.
2. **DEV 1 - EliteSCADA** — previous Audit task complete; PR #44 merged and PR #45 coordinator integration merged; `COMPLETED + WAIT_FOR_COORDINATOR`.
3. **DEV 2 - EliteSCADA** — previous PR #40 Internal Memory foundation and PR #43 Historian foundation merged; `COMPLETED + WAIT_FOR_COORDINATOR`.
4. **DEV 3 - EliteSCADA** — previous PR #41 Python/Visual foundation merged; `COMPLETED + WAIT_FOR_COORDINATOR`.

No DEV currently has authorization to create another branch or choose another product task.

## NEXT LOCKED PRODUCT BLOCKS

### Source/protocol chain

The mandatory sequence remains:

`Internal Memory complete product integration -> TAG Gateway -> common multi-driver/Data Source diagnostics -> USER INTERFACE VALIDATION PREVIEW -> additional external protocols`

The **next locked source/protocol block is complete Internal Memory product integration**, building on merged PR #40.

Do not start Gateway or later protocol work merely to keep another worker occupied while Internal Memory integration is incomplete.

### Visual/Python chain

The merged PR #41 foundation does not remove the mandatory sequence:

`public Script/visual Engineering integration -> Python editor/sandbox -> visual runtime object/property integration -> graphical Screens/Popups/Dynamos editor -> advanced libraries`

Parallelism between the source/protocol chain and visual chain is allowed only when shared-file ownership and dependency boundaries remain safe.

### Historian

PR #43 foundation is merged. Remaining work includes public Engineering storage-policy integration, runtime configuration and later trend raw/aggregate resolution.

### Audit

PRs #44/#45 are merged. Future persistent crash-durable outbox, Audit UI or a distinct Audit-read capability remain separate explicit future designs.

## IMMEDIATE CONTINUATION

### Before starting the next worker wave

The user intends to install a permanent bootstrap instruction once in each fixed DEV chat.

Until that is done, keep DEV 1/2/3 waiting.

### After bootstrap installation

On `siga` in `COORDENADOR - EliteSCADA`:

1. reread current GitHub `main` and all coordination documents;
2. verify no new branch/PR/CI state appeared unexpectedly;
3. choose the next dependency-safe assignment wave from `PROJECT GOAL.md` and `docs/ROADMAP.md`;
4. avoid assigning two workers to conflicting central Engineering/DI/frontend files unless ownership is explicitly partitioned;
5. write exact DEV task/branch/scope/dependencies/completion criteria into `docs/CHAT-WORK-ASSIGNMENTS.md`;
6. only then should the user return to each assigned DEV chat and send `siga`.

## Permanent continuity rules

- Feature branches must never be the sole durable home of permanent architecture decisions.
- Open PRs remain **IMPLEMENTED IN PR**, never **MERGED**.
- GitHub branch/PR/head/CI state is operational truth.
- Worker assignment authority comes only from `docs/CHAT-WORK-ASSIGNMENTS.md` as maintained by the coordinator.
- `siga` is the canonical short user command; `continue` is equivalent.
- Completed workers do not create their own next work.
- Dependency-safe idle time is preferable to conflicting parallel branches that later require semantic reconstruction.
