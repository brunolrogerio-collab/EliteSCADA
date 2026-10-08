# PARALLEL WORK — EliteSCADA

This file defines the permanent concurrent-work safety rules. The detailed Development Wave execution model is authoritative in `docs/DEVELOPMENT-WAVES.md` and must be read with this file. GitHub Actions consumption rules are defined in `docs/CI-USAGE-POLICY.md`.

## 1. Core ownership

Each worker chat owns exactly one ACTIVE assignment/branch at a time.

Workers:

- never alter `main`;
- never merge their own PR;
- never choose or broaden their own mission;
- never work in another DEV branch;
- obey AllowedScope/ForbiddenScope/ReservedFiles;
- stop at `WAIT_FOR_COORDINATOR` after delivery.

`COORDENADOR - EliteSCADA` owns assignments, cross-domain architecture, central composition, integration branches, merge ordering, official documentation and final integration CI.

## 2. Mandatory read protocol

Before any EliteSCADA action, every fixed chat reads current `main`:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/PARALLEL-WORK.md`;
5. `docs/DEVELOPMENT-WAVES.md`;
6. `docs/CHAT-WORK-ASSIGNMENTS.md`;
7. `docs/CI-USAGE-POLICY.md`;
8. every current `MustReadSpecific` document.

For product planning through first owner validation, coordinator and relevant workers also read `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

GitHub branch/PR/head/CI state is operational truth. Documentation is coordination truth and must be synchronized promptly when it lags.

## 3. Permanent `siga` / `continue`

When the user sends only `siga` or `continue`, the chat:

1. identifies its fixed role;
2. performs the mandatory read protocol;
3. locates its exact assignment;
4. verifies branch/PR/head/CI and wave state;
5. checks StartCondition/status;
6. continues only explicitly authorized work without asking the user to repeat old prompts.

Workers with delivered work and `WAIT_FOR_COORDINATOR` do not create new branches or start queued work. A `NextQueuedTask` is not authorization until promoted according to the board and `docs/DEVELOPMENT-WAVES.md`.

### 3.1 Chat wake is local

A `SIGA` / `continue` wakes only the chat that receives it. A `SIGA` sent to Main does not wake any DEV chat. GitHub comments, issue updates, branch pushes, PR creation and completed Actions runs are durable coordination records; they do not wake a separate ChatGPT conversation.

At the end of every Main response, list each active DEV conversation and give the exact user action for that chat: `SIGA` in that DEV chat, `WAIT`, or `NO ACTION`. Do not say only 'continue' without naming the receiving chat. A DEV must end each response with its lane state, exact next step, and whether the user should send `SIGA` in that same DEV chat or wait for Main.

`SIGA` resumes the existing authorized assignment after a live-state re-read. It is not merge authorization, an architecture decision, permission to expand scope, or permission to start a queued lane.

### 3.2 Reusable DEV Bootstrap block

Before sending a lane Bootstrap, Main fills in the exact receiving DEV chat, owning issue/PR and report destination, branch, current HEAD/tree, required base/merge-base, allowed and forbidden scope, reserved files, validation profile, acceptance tests, known gates, and the next already-authorized action. State explicitly which action requires a Main decision and whether the user should send `SIGA`, `WAIT`, or take `NO ACTION` in that exact DEV chat.

Copy this block into each lane-specific DEV Bootstrap and replace every bracketed field:

```text
ELITESCADA DEV OPERATING PROTOCOL

Repository: brunolrogerio-collab/EliteSCADA.
GitHub live is the sole authority for branch, commit, PR, issue and CI state.
Receiving DEV chat: [EXACT CHAT NAME].
One active assignment: [ISSUE / LANE]. Owning report destination: [ISSUE / PR / HANDOFF].
Branch: [BRANCH]. Current GitHub HEAD/tree: [SHA / TREE]. Required base and merge-base: [BASE SHA].
Allowed scope: [SCOPE]. Forbidden scope / reserved files: [DETAILS].
Validation profile: [PROFILE]. Required focused checks: [TESTS].
Current gates and evidence: [PASS / PENDING / DEFERRED / BLOCKED, WITH LINKS].
Next already-authorized action: [ONE CONCRETE STEP].
Stop and wait for Main only for: [ARCHITECTURAL / SHARED CONTRACT / SECURITY AUTHORITY / SCOPE / MERGE GATES].

On SIGA received in THIS DEV conversation:
1. Read the required current repository docs, including LAST CHANGE.md, docs/ROADMAP.md, docs/PARALLEL-WORK.md, docs/CHAT-COLLABORATION-PROTOCOL.md and docs/CI-USAGE-POLICY.md, plus this lane's MustReadSpecific files.
2. Revalidate the live issue, branch HEAD/tree, base/merge-base/compare, PR state, latest comments and Actions runs on the exact current SHA. GitHub live overrides this prompt, local memory, old comments and stale handoffs.
3. Continue the next action already authorized by this lane without asking the Product Owner to repeat it. SIGA resumes existing scope; it does not authorize a new scope, shared-contract change, PR-ready transition or merge.

Validation and publication:
- Local checks are T0 evidence. The lane PR must obtain the normal GitHub Actions T1 on the exact published SHA using the stated profile. Record SHA/tree, run and job links, counts, skips and failures in the owning PR/issue.
- Tests validate product. Classify each RED as PRODUCT, TEST_STALE, ENVIRONMENT, WORKFLOW/CI, SHARED_HOTSPOT or UNKNOWN before selecting a fix. Do not alter correct product to satisfy a stale test or weaken a valid assertion to get green.
- Run the minimum focused checks needed, then publish the exact candidate and open/update the lane PR to trigger or attach exact-head T1. Observe an already-running broad workflow; do not start a duplicate. Full CI is reserved for its normal gate, material risk, or post-integration flow.
- If HTTPS push fails or `gh` is unavailable, do not ask the Product Owner to configure credentials or send a token. Use the authorized connected GitHub Git Database/API path when available: read the live ref; create blobs/tree/commit with the current branch HEAD as parent; advance the ref with expected-head/CAS and `force=false`; then re-read GitHub and verify the commit/tree. Preserve the source tree and record local provenance SHA separately from the GitHub commit SHA.
- If this chat has no approved write path, report `BLOCKED_GIT_AUTH` to Main with the exact error, source SHA/tree, changed paths and local T0 evidence. Do not claim publication or T1. Continue lane work that does not depend on publication if safe; otherwise wait for Main's operational unblock. Never ask the Product Owner to resolve routine credential setup.
- Physical L4 scheduled after Wave 16 and partner disclosure is `DEFERRED / NOT RUN` now. Continue only the lane's available authorized gates. Do not substitute a simulator for physical L4 or claim compatibility before its evidence is recorded.

Boundaries and response format:
- Never merge your PR. A green T1 is not merge authorization. Merge requires separate explicit Product Owner authorization through Main.
- Do not expand scope, consume another lane, or change shared contracts, schema, dependencies, Security Authority or High Availability internals unless the lane explicitly permits it or Main authorizes the gate.
- End every response with lane status; exact GitHub SHA/tree/PR/T1 state; the exact next action; who acts next; and whether the user should send `SIGA` in THIS DEV chat, wait for Main, or take no action. Include the current local timestamp as `DD/MM/YYYY — Hora: HH:MM BRT`.
```

### 3.3 Publication and validation state labels

Use explicit states so a local result cannot be mistaken for GitHub validation:

- `LOCAL_T0_ONLY` — focused local checks passed; candidate not yet published or no exact-head T1.
- `PUBLISHED_T1_PENDING` — exact candidate is on GitHub and the lane PR / T1 is pending.
- `T1_FAILED_DIAGNOSIS_REQUIRED` — record failed job, test names, exact SHA and preliminary failure class; do not claim completion.
- `T1_PASS_MAIN_AUDIT_PENDING` — exact-head profile gate passed; Main audit remains.
- `WAIT_FOR_COORDINATOR` — a real Main decision or reserved-scope gate is required.
- `BLOCKED_GIT_AUTH` — authorized connection cannot publish; report to Main without asking the Product Owner for a token.

## 4. Development Waves

Parallel product development is organized into explicit waves with:

- one product checkpoint;
- immutable logical `WaveBaseSHA`;
- up to three parallel-safe worker slices;
- coordinator integration branch;
- reserved/shared ownership;
- validation matrix;
- objective wave gate.

The wave base is not invalidated merely by coordination/documentation-only commits.

During an active wave, avoid merging unrelated product/research/refactor work into `main`. Critical/security/CI-blocking/indispensable dependency fixes are exceptions.

Detailed rules, Definition of Ready/Done, queue semantics and review checkpoints are in `docs/DEVELOPMENT-WAVES.md`.

## 5. Integration Train

Workers prove `WaveBaseSHA + worker slice` on their own Draft PRs. They are not automatically required to reconcile individually with every unrelated newer `main` commit.

The coordinator integrates accepted slices into `integration/<wave>`, implements central hooks, reconciles the integrated composition with real `main` where needed and runs final complete CI there.

If a semantic conflict is isolated to one worker, the coordinator returns only that worker for targeted correction when appropriate.

Final wave quality remains strict: no wave merge without green integrated validation required by its matrix, normally including Web build, backend build/tests, runtime smoke and Chromium E2E.

## 6. GitHub Actions budget discipline

`docs/CI-USAGE-POLICY.md` governs CI frequency for both workers and coordinator.

When its mode is `CONSTRAINED`:

- workers batch coherent implementation changes and prefer focused validation during iteration;
- opening/updating a Draft PR does not mean every intermediate commit requires a full workflow matrix;
- unchanged-head reruns for reassurance are not allowed;
- a localized CI failure must be diagnosed and corrected before another expensive full run, except for demonstrably transient infrastructure failures;
- coordinator reuses valid exact-head evidence and reserves complete matrices for meaningful integration/final checkpoints;
- documentation-only `main` changes do not invalidate unchanged product CI evidence;
- final integrated Wave Definition of Done remains unchanged;
- if available Actions minutes cannot support the required final matrix, the correct state is `BLOCKED_BY_CI_BUDGET`, never a weakened merge.

CI economy changes **frequency**, not assertions, security, CAS, lifecycle, persistence, Runtime guards or final acceptance requirements.

## 7. Shared files reserved to coordinator

Unless an assignment grants a narrow explicit exception, workers do not modify:

- `PROJECT GOAL.md`;
- `LAST CHANGE.md`;
- `docs/ROADMAP.md`;
- `docs/PARALLEL-WORK.md`;
- `docs/DEVELOPMENT-WAVES.md`;
- `docs/CI-USAGE-POLICY.md`;
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
- `docs/CHAT-WORK-ASSIGNMENTS.md`;
- `.github/workflows/**`;
- central solution/orchestration/DI files;
- `src/Scada.Api/Program.cs`;
- central frontend routing/shell/composition files;
- lockfiles;
- canonical Engineering contract/schema files such as `src/Scada.Engineering/Contracts/EngineeringContracts.cs`.

Workers prefer isolated files/types and record required central changes in PR `INTEGRATION REQUIRED` notes.

## 8. Worker PR requirements

Draft PRs are opened early enough for event-driven reviews:

- Early Contract Review;
- Integration Review;
- Delivery Review.

Worker delivery requires focused tests/assigned CI, exact head evidence appropriate to the active CI budget mode, changed-domain description, no scope violation and a PR body separating:

- `IMPLEMENTED IN PR`;
- `INTEGRATION REQUIRED`;
- `SPECIFIED / NOT IMPLEMENTED`.

No permanent architectural decision may live only in a worker branch.

Under `CONSTRAINED` CI mode, a worker may reach coordinator review with focused evidence when the board/policy permits deferring the complete matrix to integration. This never authorizes merging a known-failing worker head or bypassing a specifically required worker validation.

## 9. Assignment authority and queue

Only the coordinator changes worker missions in `docs/CHAT-WORK-ASSIGNMENTS.md`.

Future work may be preplanned, using:

- `QUEUED`;
- `READY`;
- `ACTIVE`.

A worker starts only ACTIVE/explicitly authorized work whose StartCondition is satisfied. Queue preparation exists to reduce idle coordination, not to grant autonomy over roadmap selection.

## 10. Preferred specialization

Preferences, not rigid ownership:

- DEV 1: Engineering/configuration/lifecycle/editors/import-export;
- DEV 2: Runtime/TAGs/historian/source-runtime/operations;
- DEV 3: cross-product acceptance/security/session/Audit/UX quality;
- Coordinator: central contracts/schema/DI/routing/shell/composition/integration/merges/official docs.

Coordinator may redistribute work when dependency or parallel-safety analysis requires it.

## 11. Status vocabulary

Product/repository:

- `MERGED` — official `main` state;
- `IMPLEMENTED IN PR` — exists only in an open branch/PR;
- `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED` — architecture/evidence is official but no production capability is implied;
- `SPECIFIED / NOT IMPLEMENTED` — locked intent with no merged implementation.

Execution states may include `QUEUED`, `READY`, `ACTIVE`, `IN_PROGRESS`, `PR_OPEN`, `CI_FAILED`, `READY_FOR_COORDINATOR_REVIEW`, `INTEGRATION_REQUIRED`, `WAIT_FOR_COORDINATOR`, `BLOCKED`, `BLOCKED_BY_CI_BUDGET`, `MERGED` and `COMPLETED`.

Never describe an open branch as merged product state.

## 12. Document responsibilities

- `PROJECT GOAL.md` = long-lived architecture/product north;
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md` = locked first owner-validation product scope and ordered waves;
- `docs/ROADMAP.md` = macro current implementation order/status;
- `docs/DEVELOPMENT-WAVES.md` = permanent scheduling/integration model;
- `docs/PARALLEL-WORK.md` = concurrent safety/ownership rules;
- `docs/CI-USAGE-POLICY.md` = CI budget modes, evidence hierarchy and rerun discipline;
- `docs/CHAT-WORK-ASSIGNMENTS.md` = live execution board;
- `LAST CHANGE.md` = exact operational handoff;
- PR bodies = branch-local delivery evidence.

No one document replaces the others.