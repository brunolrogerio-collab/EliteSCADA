# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26  
**Last coordinator synchronization:** 2026-08-27

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH IN PR** = research/specification exists only in an open branch/PR and is not product implementation.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Interface product development and central UX integration

**Branch:** `main` + `feature/interface-product-development`

**Status:** `IN_PROGRESS`

**Objective:**

Prioritize the real product interface now that Internal Memory, TAG Gateway and common communication diagnostics are complete. Evolve EliteSCADA from technical proof surfaces into a coherent industrial application across Runtime, Engineering and Audit before returning to the provisional Windows validation build or additional drivers.

**AllowedScope:** coordinator-owned shared/central frontend shell/routing, `main.tsx`, `AppNavigation.tsx`, global/interface CSS, `EngineeringApp.tsx`, central localization/integration, browser tests, CI, assignment board, roadmap/handoff documentation and integration of worker-delivered isolated UI components.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no new production MQTT/OPC UA/BACnet/S7/Driver Module runtime during this block;
- no completion/handoff of the provisional Windows presentation package unless the product owner reprioritizes it;
- no frontend-only security decisions;
- no private Engineering truth;
- no production graphical Screen/Popup/Dynamo editor or Python engine/editor ahead of the locked Script/visual prerequisite chain.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`

**ObservedGitHubState:**

- Internal Memory: **MERGED / COMPLETE** through PR #49.
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55.
- Common communication diagnostics: **MERGED / COMPLETE** through PRs #56 and #57.
- PR #57 merge SHA: `c8190cc119a2e288834d619084396107103b2f56`; CI #350 and post-merge CI #351 green.
- Engineering Schema: **v9**.
- `integration/interface-validation-preview` exists with two unmerged preparatory commits touching `Program.cs` and `AppNavigation.tsx`; branch is intentionally **PARKED / NO PR / DO NOT MERGE YET**.
- active interface coordinator branch: `feature/interface-product-development`.
- PR #53 and PR #54 remain delivered research inputs and are not production implementations.

**Dependencies:**

- canonical Engineering remains authoritative;
- UI work may consume existing Runtime/Engineering/Audit APIs but must not bypass them;
- worker UI slices must remain isolated until coordinator integration;
- graphical HMI editor remains blocked by the Script/visual prerequisite chain;
- Windows validation packaging resumes after the interface reaches a more valuable user-testable state;
- additional drivers/protocols remain postponed.

**NextActions:**

1. replace floating/developer-like global navigation with a coherent EliteSCADA application shell;
2. normalize Runtime/Engineering/Audit visual language and route context;
3. integrate DEV 1 Engineering workspace ergonomics primitives;
4. integrate DEV 2 Runtime operational overview primitives;
5. integrate DEV 3 authenticated session/user-menu UX;
6. improve Engineering information architecture, search/navigation, scalable entity presentation and editor consistency;
7. improve Runtime operational context without pretending the future graphical HMI editor already exists;
8. extend Chromium E2E to the integrated UX and keep existing functional/security behavior green;
9. merge only reviewed current-head green interface slices;
10. return to the Windows validation package only after the interface development checkpoint is materially useful for product-owner feedback.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Engineering workspace/entity-browser ergonomics primitives

**Branch:** `feature/engineering-workspace-ux`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Build isolated reusable Engineering UI primitives that make large entity collections practical: compact searchable/filterable list/master-detail behavior suitable for TAGs, Data Sources, alarms and future Engineering entities. Deliver primitives only; coordinator owns wiring into `EngineeringApp.tsx` and central routing/localization.

**AllowedScope:**

- new files under `web/scada-web/src/engineering/**` specifically for reusable entity browser/workspace UI;
- component-local CSS under the same directory;
- focused frontend/E2E tests for the isolated component where practical;
- existing Engineering type imports may be consumed read-only.

**ForbiddenScope:**

- `main`;
- `web/scada-web/src/engineering/EngineeringApp.tsx`;
- `web/scada-web/src/main.tsx`;
- `AppNavigation.tsx` and global shell/routing;
- `api.ts`, central backend/API/DI, Engineering schema/contracts;
- graphical Screen/Popup/Dynamo editor implementation;
- Python/editor runtime;
- drivers/protocols, workflows or lockfiles.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- research PR #53 document as optional UX input.

**CompletionCriteria:**

1. reusable component supports search/filter and scalable selection/list navigation;
2. selected entity has a clear detail surface without forcing a huge always-expanded form;
3. keyboard/focus behavior is sane for desktop Engineering;
4. empty/loading/no-match states are explicit;
5. component does not create private Engineering state or mutate backend directly;
6. focused tests/Web build are green;
7. Draft PR opened as **IMPLEMENTED IN PR / NOT MERGED**, then stop.

**NextActions:** work only on `feature/engineering-workspace-ux`, deliver isolated primitives, open Draft PR, wait.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Runtime operational overview UI primitives

**Branch:** `feature/runtime-operations-ux`

**Status:** `ASSIGNED`

**PullRequest:** none yet

**Objective:**

Build an isolated Runtime operational overview component using existing protected EliteSCADA APIs. The component should complement the current process demo with platform-level operational context: runtime status, communication health, active alarms/TAG quality and Gateway/diagnostic summary where already available.

**AllowedScope:**

- new files under `web/scada-web/src/runtime/**` for the operational overview;
- component-local CSS;
- focused frontend/E2E tests and local types/helpers inside that runtime slice;
- read-only consumption of existing `/api` endpoints.

**ForbiddenScope:**

- `main`;
- `web/scada-web/src/main.tsx` and global `styles.css`;
- central navigation/shell;
- backend/API contract changes;
- Engineering schema/contracts;
- direct driver/device access;
- new drivers/protocols;
- graphical HMI editor;
- workflows/lockfiles/dependencies.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`

**CompletionCriteria:**

1. isolated runtime overview presents useful operational summary from existing APIs;
2. communication abnormalities are emphasized while healthy states remain visually quiet;
3. component handles loading/error/empty states cleanly;
4. no direct device/driver access or fabricated metrics;
5. focused tests/Web build are green;
6. Draft PR opened as **IMPLEMENTED IN PR / NOT MERGED**, then stop.

**NextActions:** work only on `feature/runtime-operations-ux`, deliver isolated component, open Draft PR, wait.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Authenticated session/user-menu UX primitive

**Branch:** `feature/session-ux`

**Status:** `ASSIGNED`

**PullRequest:** none yet for this task; research PR #54 remains separate and unchanged

**Objective:**

Build an isolated authenticated-session UI primitive using the existing `useAuth()` context. It should make current user identity, roles and logout discoverable in the future product shell without changing authentication semantics.

**AllowedScope:**

- new files under `web/scada-web/src/auth/**` for a `UserSessionMenu`-style component and component-local CSS;
- focused frontend tests;
- read-only use of the existing Auth context/profile/logout API.

**ForbiddenScope:**

- `main`;
- changing `AuthGate.tsx` authentication logic unless coordinator explicitly expands scope after review;
- global `main.tsx`, `AppNavigation.tsx` or shell routing;
- backend identity/JWT/security changes;
- Engineering schema/contracts;
- Python/editor runtime despite the older research PR;
- drivers/protocols/workflows/lockfiles.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/PARALLEL-WORK.md`

**CompletionCriteria:**

1. component clearly shows authenticated display name/username and role context without exposing tokens;
2. logout uses the existing trusted Auth context behavior;
3. keyboard/focus/menu behavior is usable;
4. unauthenticated/disabled-auth states degrade cleanly;
5. focused tests/Web build are green;
6. Draft PR opened as **IMPLEMENTED IN PR / NOT MERGED**, then stop.

**NextActions:** work only on `feature/session-ux`, deliver isolated component, open Draft PR, wait.

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
