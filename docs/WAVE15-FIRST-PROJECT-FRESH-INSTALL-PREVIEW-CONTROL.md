# Wave 15 — First-Project Fresh-Install Partial Preview Control

> GitHub live is the sole authority. This control prepares a partial product audit after the first six post-FC0A lanes complete. It does **not** activate a preview now.

CONTROL_BRANCH: coord/w15-fresh-install-preview-control

MAIN_ORDER_REV: 0001

STATE: PREPARED / NOT ACTIVE

PREVIEW_FAMILY: W15-FIRST-PROJECT-FRESH-INSTALL-PARTIAL-PREVIEW

## 0. Purpose

Wave 15 will run an early fresh-install / first-project product audit before the later complete-product/EEE acceptance preview.

This audit asks a different question from T1/T2/T3/T4:

> Can a user who starts from a genuinely fresh EliteSCADA installation discover the product, create a first SCADA application from zero and place it into a truthful working Runtime without relying on a prebuilt project, hidden demo state or internal implementation knowledge?

Green CI does **not** substitute for this audit.

The preview has two independent moments:

1. CODEX black-box fresh-install preview;
2. Product Owner human fresh-install preview.

The two journeys must remain independent until both first-project attempts are complete.

## 1. Entry condition

Do not activate this preview until Main revalidates and records one exact integration checkpoint where:

- DEV-EDITOR is integrated and its required integrated validation is accepted;
- DEV-SCRIPT-ENGINEERING is integrated and its required integrated validation is accepted;
- DEV-AUTHORITY-UX is integrated and its required integrated validation is accepted;
- DEV-LICENSING-UX is integrated and its required integrated validation is accepted;
- feature-lane broader T2 on the exact integration head is green/accepted;
- FND-05 is POST_MERGE_VALIDATED and VERIFIED/FROZEN;
- FND-07 is POST_MERGE_VALIDATED and VERIFIED/FROZEN;
- no known P0/P1 blocker makes a meaningful fresh-install journey invalid before it starts.

The exact preview base SHA/tree must be written here at activation.

This is expected around the first FC0-B-era integrated checkpoint, before later complete-product convergence and before the final EEE/T3/T4 preview.

## 2. Fresh-install environment contract

Each first-project moment uses its own clean environment.

A valid clean environment has:

- no imported .escadapkg;
- no EEE project;
- no precreated customer/project application;
- no hidden or synthetic Demo Project;
- no reused Screen, Popup, TAG, Script, Data Source or Engineering Working state from earlier tests;
- no prior project database state that teaches the journey;
- no browser/session state intentionally carried from the other preview moment;
- supported product first-run identity/authentication only; no test-only identity shortcut presented as normal user flow;
- licensing in the truthful state a new installation would normally present unless the preview explicitly reaches a later license-install step.

If environment preparation requires implementation-specific seeding that a real fresh installation would not have, that is itself an audit finding unless it is purely infrastructure needed to host the product.

## 3. Moment 1 — CODEX black-box preview

GATE_ID: W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01

MODE: USER-LIKE / BLACK-BOX / EXPLORATORY

### 3.1 CODEX user mission

CODEX receives a fresh environment and one high-level goal:

> You have just installed EliteSCADA. Explore the product using normal user-visible interfaces and create a small SCADA application from scratch that reaches a functional Runtime.

Do not give a click-by-click recipe, preselected object list or internal API path.

The navigation path chosen by CODEX is part of the evidence.

### 3.2 Black-box restrictions during the user journey

Until CODEX declares the exploratory first-project journey complete or blocked, it must not use:

- repository source code;
- internal control-plane documents to discover how a feature is implemented;
- database inspection;
- direct internal API calls solely to bypass the UI;
- shell/console mutation of product state;
- test fixture shortcuts;
- implementation logs as a substitute for user-visible behavior.

CODEX may use:
- the normal product UI;
- normal product-visible Help/manual surfaces;
- ordinary browser interaction available to a real user.

If CODEX cannot discover how to proceed without source/internal knowledge, that is a usability/product finding.

### 3.3 No correction during exploration

During the exploratory journey CODEX must not:
- edit code;
- patch CSS;
- change configuration to hide a product problem;
- add test data;
- fix the discovered issue.

It records observations and continues where a normal user reasonably could.

A hard blocker may end the first-project journey.

### 3.4 Evidence to capture

Record, without over-scripting:
- route taken from first load to first project;
- points of uncertainty;
- misleading/fictitious status;
- dead ends;
- actions that required guessing;
- errors and recovery;
- product Help used;
- whether a project could be created;
- whether basic Engineering entities could be authored;
- whether lifecycle to a Runtime-capable state was understandable;
- whether Runtime showed truthful project/application state;
- any security/licensing/session state that affected progress;
- screenshots/browser evidence where useful;
- approximate time/interaction cost for major milestones.

The aim is not speed benchmarking; timing helps identify friction.

### 3.5 Post-journey diagnostic phase

Only after the black-box first-project journey ends may CODEX inspect:
- repository source;
- logs;
- APIs;
- tests;
- controls;
- exact implementation.

Diagnostic work should correlate user-visible findings to likely causes.

CODEX still does not fix product code during this audit unless Main explicitly converts a finding into a correction order.

### 3.6 Independence / report embargo

CODEX must persist its detailed findings in a dedicated preview evidence artifact/branch.

Before the human first-project moment is complete:
- Main may know whether CODEX completed or was blocked;
- Main must not brief the Product Owner on detailed navigation findings, specific traps, exact fixes or recommended click paths;
- canonical handoffs/ledger should record only gate status, not detailed spoiler content.

This preserves the independence of the human first-contact audit.

## 4. Moment 2 — Product Owner human preview

GATE_ID: W15-FIRST-PROJECT-HUMAN-PREVIEW-01

MODE: HUMAN / FRESH-INSTALL / EXPLORATORY

Use a second clean environment independent from the CODEX environment.

The Product Owner should perform the same high-level mission without first reading the detailed CODEX report:

> Starting from a freshly installed EliteSCADA with no prepared project, create the first SCADA application from zero and make it work in Runtime using the product as a normal human user.

No click-by-click Main/CODEX coaching should be provided during this first journey.

If the Product Owner becomes blocked, the block itself is evidence. Assistance, if later requested, should be logged as the point where unaided product discovery ended.

## 5. Independence rule

The first-project human audit must not be trained by the first-project CODEX audit.

Before Human Preview completion:
- do not expose CODEX's detailed report to the Product Owner;
- do not convert CODEX findings into a human test checklist;
- do not preconfigure the second environment to bypass CODEX-discovered friction;
- do not reuse the CODEX-created project.

After both exploratory journeys complete, the report embargo ends.

## 6. Cross-audit comparison

Main then compares the two independent journeys.

Classify each finding as:
- BOTH — CODEX and human independently encountered the same issue;
- CODEX_ONLY;
- HUMAN_ONLY;
- PATH_DIVERGENCE — both succeeded/failed differently because they chose different workflows;
- NOT_REPRODUCED.

A BOTH finding is strong product evidence but severity still depends on impact.

Preserve concrete evidence rather than collapsing results into a score.

## 7. Second directed round after independent exploration

Only after both first-project journeys are complete may Main run a directed follow-up using the projects/environments created or controlled fresh equivalents.

### 7.1 Persistence and lifecycle

- restart browser/product;
- reopen persisted Working/project;
- verify Working/Revisions/Published/Active truth;
- verify Runtime still represents Active authority rather than draft state.

### 7.2 Authority / Runtime Session

- create/use another identity/role where supported;
- verify denied capabilities stay denied;
- exercise ViewOnly vs Interactive requested/granted state;
- verify user-visible fallback/reason truth.

### 7.3 FND-07 installation/application switching

Using Project A created during the preview:
- preflight detach;
- fence Project-A process effects;
- reach truthful neutral bootstrap;
- create/import/restore a Project B through supported flow;
- verify no hidden Demo project appears;
- verify no Project-A authority/content leaks into B;
- exercise B -> A again when the supported workflow allows it;
- verify license and Historian remain separate authorities according to frozen contracts.

This directed round may reveal downstream Installation UX work; that is expected and useful.

### 7.4 FND-05 HA — separate directed technical/user surface check

Do not contaminate the initial first-project journey with a split-brain/fencing script.

After the exploratory audit, if the user-facing HA surface is available:
- inspect Active/Standby/readiness truth;
- perform one supported manual break-before-make transfer;
- verify understandable role/state transition.

Deep fencing/split-brain correctness remains CODEX automated/adversarial evidence, not a manual preview burden.

## 8. Finding disposition

This is a partial product audit, not automatically a final release gate.

Main classifies findings.

### Blocking before further convergence

Examples:
- cannot create a first project;
- cannot reach a truthful Runtime;
- data loss/cross-project leakage;
- Authority/security bypass;
- fictitious project/Active identity;
- lifecycle corruption;
- unsafe HA/detach behavior.

### Route to downstream product lane

Examples:
- first-install/onboarding friction appropriate for DEV-INSTALLATION-UX;
- discoverability/Help gaps;
- non-blocking UX density;
- terminology/localization;
- workflow polish.

### Deferred evidence-bounded

Only when reproducibility/ownership is genuinely insufficient.

Every material finding must receive an owner or an explicit evidence-bounded disposition.

## 9. Preview outcome

Each moment receives a factual status:
- COMPLETE;
- BLOCKED_BY_PRODUCT;
- BLOCKED_BY_ENVIRONMENT;
- INVALID_ENVIRONMENT / RESET_REQUIRED.

After both moments and cross-audit comparison, Main records either:

W15-FIRST-PROJECT-FRESH-INSTALL-PARTIAL-PREVIEW -> ACCEPTABLE_FOR_NEXT_CONVERGENCE

or

W15-FIRST-PROJECT-FRESH-INSTALL-PARTIAL-PREVIEW -> CHANGES_REQUIRED

ACCEPTABLE_FOR_NEXT_CONVERGENCE does not mean final Wave 15 acceptance.

The later EEE/industrial complete-product audit, T3/T4 and final fresh Preview remain mandatory.

## 10. Relationship to EEE and final preview

The first-project fresh-install preview intentionally precedes the EEE v15 complete-product audit.

Use:
- fresh-install preview to test product birth, discoverability and first-project flow;
- EEE v15 to test industrial breadth/depth and representative integrated behavior;
- final T3/T4 + fresh Preview to test complete Wave 15 acceptance.

A prepared EEE project must never substitute for proving that a new user can create the first application from zero.

## 11. Activation rule

At activation Main must:
1. revalidate GitHub live;
2. record exact preview base SHA/tree;
3. verify the six post-FC0A lanes meet the entry condition;
4. prepare/reset two independent clean environments;
5. activate CODEX black-box preview first;
6. keep detailed CODEX findings embargoed from Product Owner;
7. activate Human Preview on a fresh second environment;
8. unseal/compare findings only after the human exploratory journey completes;
9. record remediation ownership in GitHub.

No preview is activated by this preparation document.


## 12. ACTIVATION — CODEX black-box first-project preview

MAIN_ORDER_REV: 0002

STATE: ACTIVE / CODEX BLACK-BOX FIRST

ACTIVE_GATE_ID: `W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`

Main activation revalidated the exact six-lane exit gate.

Exact preview product base:
- SHA: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`;
- tree: `5e5fce8ce87f31dfc11b83bb68ff86c67f9f0112`;
- accepted broader feature T2: `W15-FOUR-FEATURE-INTEGRATED-T2-01 -> PASS / ACCEPTED`;
- broad evidence: EliteSCADA CI #1584 / run `36290910850` / attempt 1 — SUCCESS across Backend build/full tests/Runtime smoke, Web build and Chromium end-to-end.

Coordination branch note:
`wave15/corrections-integration` subsequently advanced only by coordination documentation beyond this exact product SHA. Those documentation-only commits do not redefine the preview product bytes.

Entry condition at activation:
- Script Engineering: `T2_VERIFIED`;
- Editor: `T2_VERIFIED`;
- Authority UX: `INTEGRATED / VERIFIED_COMPLETE`;
- Licensing UX: `INTEGRATED / VERIFIED_COMPLETE`;
- FND-05: `VERIFIED / FROZEN`;
- FND-07: `VERIFIED / FROZEN`;
- no known blocking P0/P1 invalidates the first-project journey.

### Binding CODEX mission

Use a genuinely clean environment satisfying section 2. The user mission is intentionally high-level only:

> You have just installed EliteSCADA. Explore the product using normal user-visible interfaces and create a small SCADA application from scratch that reaches a functional Runtime.

During the exploratory journey CODEX MUST NOT use repository source, control-plane documents, database inspection, internal API shortcuts, shell/state mutation, test fixtures or implementation logs to discover/bypass the UI path.

No product/code correction is allowed during exploration. If blocked, record the product-visible blocker and end the black-box journey honestly.

After the journey is COMPLETE or BLOCKED, diagnostic source/log/API inspection is allowed only to correlate findings. Product mutation remains forbidden unless Main later issues a separate correction order.

Detailed findings MUST be persisted in a dedicated preview evidence artifact/branch and MUST remain embargoed from the Product Owner until the independent human first-project journey is completed. Main may surface only gate status (COMPLETE/BLOCKED/INVALID_ENVIRONMENT), not spoiler details.

### Environment validity gate

Before beginning user interaction, CODEX must verify only at the environment level that it is clean as defined in section 2. If a truthful clean environment cannot be obtained without implementation-specific product seeding, return `INVALID_ENVIRONMENT / RESET_REQUIRED` or `BLOCKED_BY_ENVIRONMENT`; do not silently weaken the fresh-install contract.

No Human Preview is active yet. It activates only after CODEX first-project exploration ends and a second independent clean environment is prepared.


## 13. Product Owner amendment — parallel independent environments

MAIN_ORDER_REV: 0003

STATE: ACTIVE / PRE-AUDIT ENVIRONMENT PREPARATION / PARALLEL INDEPENDENT EXECUTION AUTHORIZED

The Product Owner replaced the strictly sequential execution topology before either exploratory journey produced evidence.

Section 12's product base, T2 acceptance, clean-environment contract, black-box restrictions and findings embargo remain binding. Only the **environment/execution topology** changes.

### 13.1 Exact product authority

Both audits remain pinned to the same exact product bytes:
- product SHA: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`;
- product tree: `5e5fce8ce87f31dfc11b83bb68ff86c67f9f0112`;
- accepted T2: `W15-FOUR-FEATURE-INTEGRATED-T2-01 -> PASS / ACCEPTED`;
- broad evidence: EliteSCADA CI #1584 / `36290910850` SUCCESS.

A dedicated environment-only branch has been created directly from that product SHA:
`preview/w15-first-project-env-harness`.

At creation it is byte-identical to the accepted product checkpoint. It may receive only Preview/container/devcontainer/startup/reset/diagnostic infrastructure. Product semantics must not change on that branch.

### 13.2 Preparation phase is not the black-box journey

Before either audit starts, CODEX may inspect source/configuration/controls **only to build and validate the environment harness**. The black-box source/control/database/internal-API restrictions begin only after Main accepts the harness, resets both environments to clean state and explicitly marks each exploratory gate READY.

If getting a clean environment to boot requires a product-semantic change, hidden Demo/project seeding, auth/licensing bypass, or other behavior change, stop and return an environment/product blocker. Do not hide it in the harness.

### 13.3 Environment A — CODEX local container

CODEX will audit EliteSCADA locally on its machine using repository-owned Docker/Compose infrastructure.

Required direction:
- reuse the existing pinned Linux CI assets where practical: `ci/local/Dockerfile.linux-e2e`, `ci/local/docker-compose.yml`, `scripts/ci/*`;
- provide a clean application Preview mode, not merely a test runner;
- isolated disposable TimescaleDB/PostgreSQL volume/database;
- normal EliteSCADA API + Web startup from the accepted product bytes;
- browser-visible Web endpoint for user-like exploration;
- no precreated project, imported package, hidden Demo Engineering state or test-owned Working state;
- no automatic first Administrator/project creation that would bypass what a fresh user must discover through the product UI;
- deterministic reset that destroys only audit-environment state and browser/session state;
- environment/bootstrap diagnostics may exist outside the product UI but must not be used by CODEX to navigate the black-box journey.

After ENV_A is accepted and reset, CODEX executes `W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01` under the original black-box rules.

### 13.4 Environment B — Product Owner fresh Codespace

A second independent environment will be a **fresh GitHub Codespace** built from the same accepted environment harness and product bytes.

Codespace requirements:
- fresh Codespace, not a repaired/reused prior Preview;
- repository-controlled devcontainer/Compose/startup path;
- separate database/volume and separate browser/session state from CODEX;
- Web port 5173 exposed only through normal Codespaces forwarding and kept Private;
- API 5080 and database 5432 remain internal/private;
- no precreated project/EEE/imported package/hidden Demo Engineering state;
- no CODEX-created application/state copied into the Codespace;
- automatic environment startup may start dependencies/API/Web but may not complete the user's first Administrator/project/Engineering journey for them.

The existing `.devcontainer/devcontainer.json` is only a partial starting point. The historical `docs/CODESPACES-PREVIEW-RUNBOOK.md` is reference evidence, not permission to resurrect stale Wave 14 Demo/bootstrap behavior.

### 13.5 Parallel independence rule

Once Main records both environments READY on the same accepted harness/product checkpoint, the two exploratory journeys may execute **in parallel**.

No sequencing dependency remains between their start/end times.

Independence is enforced by isolation and information embargo:
- no shared DB/volume/project/session/browser profile;
- no shared audit-created files/state;
- no CODEX report/checklist/navigation hints exposed to Product Owner;
- Product Owner observations are not fed to CODEX during its black-box journey;
- Main may know only coarse gate state for coordination until both journeys end;
- detailed findings remain sealed until both exploratory journeys are COMPLETE/BLOCKED.

Parallel execution is therefore considered at least as independent as the former CODEX-first/human-second ordering and avoids one auditor waiting on the other.

### 13.6 Planned harness convergence

CODEX first prepares and validates `preview/w15-first-project-env-harness` as infrastructure-only.

After Main accepts one exact harness commit, Main should derive two clean audit refs/environments from that same harness commit:
- CODEX local audit environment;
- Product Owner Codespace audit environment.

The environment harness itself is not a product integration candidate and must not be merged into `wave15/corrections-integration` merely to run the Preview.

### 13.7 Current gate state

- `W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01`: RESERVED / WAIT_ENVIRONMENT_A_READY;
- `W15-FIRST-PROJECT-HUMAN-PREVIEW-01`: RESERVED / WAIT_ENVIRONMENT_B_READY;
- execution topology after readiness: `PARALLEL_INDEPENDENT`;
- findings embargo: ACTIVE.


## 14. Product Owner amendment — Human Codespace Web port public

MAIN_ORDER_REV: 0004

STATE: BINDING / ENVIRONMENT_B_NETWORK_EXPOSURE

The Product Owner explicitly requires the Human Preview Codespace Web entry to be easy to open directly in an ordinary browser.

### 14.1 Required exposure

For **Environment B — Product Owner fresh Codespace**:
- Web port `5173` MUST be forwarded and set to `PUBLIC` automatically;
- the resulting `https://<codespace>-5173.app.github.dev` URL must open directly without a separate GitHub forwarded-port authentication step;
- API `5080` remains internal/private and is consumed through the normal same-origin Web proxy;
- database `5432` remains internal/private and must not be forwarded publicly.

This amendment supersedes the previous requirement that 5173 remain Private for this specific Product Owner audit environment.

### 14.2 Public visibility must survive resume/reopen

GitHub Codespaces currently defaults forwarded ports to private, and GitHub documents that a public forwarded port can revert to private when the port is removed/re-added or when the Codespace is restarted.

Therefore, merely making 5173 public once by hand is NOT accepted.

The repository-controlled Environment B lifecycle must re-assert `5173:public` automatically on initial startup and after Codespace stop/suspend -> resume/reopen.

The implementation may use the supported GitHub CLI visibility operation or another repository-controlled supported mechanism, but it must verify the real running Codespace result rather than assume a devcontainer label/forward declaration changes visibility.

Expected supported CLI semantics where available:
`gh codespace ports visibility 5173:public -c <codespace-name>`

If executed inside the Codespace, derive the current Codespace identity from supported environment/runtime context and verify that authentication/permissions permit the operation. Do not hardcode a Codespace name.

### 14.3 Readiness proof

ENV_B is not READY until evidence shows:
1. fresh Codespace starts from the accepted harness;
2. EliteSCADA Web becomes healthy on 5173;
3. 5173 visibility is PUBLIC without Product Owner manual configuration;
4. ordinary browser access to the public forwarded URL reaches the EliteSCADA product surface;
5. after stop/suspend and resume/reopen, EliteSCADA restarts automatically AND 5173 is restored to PUBLIC automatically;
6. 5080 and 5432 are not publicly exposed;
7. no first-project/application state is pre-seeded to obtain this result.

Security note: public Codespaces forwarding means anyone who obtains the URL can reach the forwarded Web endpoint without GitHub authentication. EliteSCADA's own authentication/authorization must remain intact; do not weaken it for Preview convenience.


## 15. Product Owner amendment — CODEX local audit pause/resume

MAIN_ORDER_REV: 0005

STATE: BINDING / ENVIRONMENT_A_RESUMABLE_AUDIT

The Product Owner requires the local CODEX audit to survive CODEX usage-window limits and host-PC shutdown/restart. The audit is therefore a **persistent resumable session**, not a single continuous process.

### 15.1 Required operator contract

The Environment A harness MUST expose one repository-controlled command surface with at least:

- `start` — create/start a brand-new clean audit session only when no resumable session exists;
- `pause` — gracefully checkpoint and stop the audit runtime while preserving all legitimate audit state/evidence required to continue later;
- `resume` — restore the same paused audit session and continue from its persisted state without resetting/reseeding the product;
- `status` — report session ID, lifecycle state, exact product/harness SHA, container/service health and whether the session is resumable;
- `reset` — explicitly destroy the local audit session and all disposable product state so a new clean `start` can be performed. This action must never happen implicitly during pause/resume.

A preferred interface is a single idempotent entry point such as:
`scripts/preview/local-audit.sh start|pause|resume|status|reset`

The exact file/name may differ if CODEX has a better repository-consistent design, but the semantics above are binding.

### 15.2 Persistence boundary

Pause/resume MUST preserve, outside ephemeral process lifetime:
- the disposable PostgreSQL/TimescaleDB audit volume containing the user-created application state;
- the local audit session identifier and exact product/harness provenance;
- audit evidence already collected by CODEX (notes/timestamps/screenshots/traces as applicable);
- browser/session state when technically practical and safe, or enough ordinary product-visible state to re-authenticate and continue without reconstructing hidden state;
- the last explicit audit checkpoint/status needed for CODEX to resume its own work without re-reading spoiler-producing implementation internals.

Persistent audit state must live in a named Docker volume and/or host-mounted ignored directory dedicated to Environment A. It must not depend on the running container filesystem alone.

Pause MUST stop compute/processes but MUST NOT delete the database volume, audit evidence, or user-created project.

### 15.3 Resume semantics

`resume` must be safe after:
- an explicit prior `pause`;
- CODEX session expiration;
- PC shutdown/reboot after a clean pause;
- unexpected host interruption, to the extent Docker state remains recoverable.

On resume the harness must:
1. revalidate the repository worktree/harness identity expected by the saved audit session;
2. refuse to silently resume against different product bytes;
3. restart required database/API/Web/browser tooling;
4. preserve the existing user-created product state;
5. verify health before returning READY;
6. emit a concise machine-readable/human-readable resume checkpoint so CODEX knows where its own audit left off.

If saved product/harness provenance does not match the current checkout, return a clear `RESUME_BLOCKED_VERSION_MISMATCH` (or equivalent) rather than migrating/resetting automatically.

### 15.4 Black-box integrity

The resumability mechanism is infrastructure only. It MUST NOT:
- seed or repair the first project;
- directly mutate product DB rows/state to advance the journey;
- inject navigation hints based on source/control knowledge;
- turn CODEX's prior detailed findings into a scripted checklist;
- use reset/recreate as a substitute for continuing the same audit session.

The persisted audit checkpoint may say where CODEX itself stopped (for example current visible product area and whether a step was COMPLETE/BLOCKED), but must not contain implementation-derived shortcuts for the next black-box interaction.

### 15.5 Evidence/embargo

Detailed CODEX findings remain embargoed from the Product Owner even across pauses.

Pause must flush/retain detailed evidence to the dedicated CODEX evidence surface. The Product Owner-facing coordination surface may expose only coarse state such as:
- NOT_STARTED;
- RUNNING;
- PAUSED_RESUMABLE;
- RESUMED;
- COMPLETE;
- BLOCKED_BY_PRODUCT;
- BLOCKED_BY_ENVIRONMENT;
- INVALID_ENVIRONMENT.

### 15.6 Readiness proof

ENV_A is not READY until CODEX proves at least:
1. clean `start` produces a fresh no-project product state;
2. CODEX can create some ordinary product-visible state sufficient to distinguish the session from fresh state;
3. `pause` stops the audit runtime without deleting that state;
4. host/container processes can be restarted and `resume` restores the same audit session/state;
5. a second pause/resume cycle is idempotent;
6. `status` distinguishes RUNNING vs PAUSED_RESUMABLE accurately;
7. `reset` is separately explicit/destructive and returns the next `start` to true fresh-install state;
8. no product/source semantics were changed merely to enable pause/resume.

The actual first-project exploratory audit starts only after Main accepts this proof and marks ENV_A READY.


## 16. Main acceptance — harness candidate for readiness proof only

MAIN_ORDER_REV: 0006

STATE: HARNESS_CANDIDATE_ACCEPTED_FOR_READINESS_PROOF_ONLY / BLACKBOX_HOLD

Main independently reviewed exact harness candidate:
- branch: `preview/w15-first-project-env-harness`;
- commit: `1df4dae293bcca59ee3191faf889058fecc973ee`;
- tree: `6efbf4b6fccd7750231c51d5f638d556707fdaec`;
- exact parent/product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Compare against the accepted product checkpoint contains exactly eight harness/infrastructure files:
- `.devcontainer/devcontainer.json`;
- `.gitattributes`;
- `ci/local/docker-compose.preview.local.yml`;
- `ci/local/docker-compose.preview.yml`;
- `docs/LOCAL-FIRST-PROJECT-PREVIEW-HARNESS.md`;
- `scripts/preview/codespaces-public-web-port.sh`;
- `scripts/preview/local-audit.ps1`;
- `scripts/preview/run-product-preview.sh`.

No product source, product test, workflow, identity, licensing, Authority, lifecycle or project-seeding semantics changed.

Main review accepts the candidate architecture for **readiness validation**, not yet for exploratory audit execution.

### 16.1 ENV_A validation authorization

CODEX is authorized to run the exact candidate locally with:
`scripts/preview/local-audit.ps1 start -AcceptedHarnessSha 1df4dae293bcca59ee3191faf889058fecc973ee`.

This validation session is disposable infrastructure proof and MUST NOT be counted as the black-box first-project audit.

To prove persistence/resumability, CODEX may create the **smallest supported product-owned state marker** required to distinguish the session from fresh install. It may use normal supported product/public contracts during this pre-audit validation phase. It MUST NOT mutate database rows directly, use a hidden fixture, import EEE/Demo, or perform exploratory UX/product evaluation.

Required ENV_A proof sequence:
1. exact candidate clean start;
2. prove no pre-seeded Administrator/project/application state from the harness;
3. create one minimal disposable product-owned persistence marker sufficient to prove continuity;
4. record its identity/value without exposing detailed product findings;
5. `pause` with a coarse checkpoint;
6. stop/restart the relevant Docker/host runtime as faithfully as the available local environment permits;
7. `resume` and prove the exact marker/session survived;
8. second `pause -> resume` cycle and prove idempotence;
9. verify `status` truthfully reports RUNNING / PAUSED_RESUMABLE and exact provenance;
10. explicitly `reset`;
11. prove `status = NOT_STARTED`, no dedicated preview containers/volumes remain, and a subsequent fresh start would have no state from the probe.

Detailed probe mechanics/evidence stay in CODEX evidence. Main only needs exact commands, session/provenance, pass/fail and proof that reset returned the environment to fresh state.

ENV_A remains:
`CANDIDATE_ACCEPTED_FOR_READINESS_PROOF / NOT_READY / BLACKBOX_NOT_RELEASED`.

### 16.2 ENV_B disposition

Static review accepts the intended Codespaces design shape:
- Compose-backed product/database;
- only 5173 forwarded;
- 5080/5432 ignored/not published;
- `postStartCommand` used so the visibility policy re-runs on container start/resume rather than only first creation;
- `codespaces-public-web-port.sh` dynamically uses `CODESPACE_NAME` and verifies live port state;
- supported GitHub CLI form `gh codespace ports visibility 5173:public --codespace <name>` is used.

However ENV_B cannot be declared READY from static/local proof. It still requires one real fresh Codespace plus one actual stop/resume cycle.

ENV_B remains:
`HARNESS_STATIC_ACCEPTED / WAIT_REAL_CODESPACE_PROOF / HUMAN_AUDIT_NOT_RELEASED`.

### 16.3 Gate discipline

Neither exploratory journey is released by this acceptance.

After CODEX returns the ENV_A lifecycle proof, Main may mark ENV_A READY if evidence is sufficient. ENV_B requires independent real Codespace proof before Human Preview starts.

The first-project CODEX audit must start only from a new clean post-validation session after the readiness probe has been explicitly reset.


## 17. ENV_A readiness blocker — bounded harness correction

MAIN_ORDER_REV: 0007

STATE: ENV_A_HARNESS_FIX_AUTHORIZED / BLACKBOX_HOLD

CODEX executed the exact accepted readiness probe on harness `1df4dae293bcca59ee3191faf889058fecc973ee` and correctly stopped on a harness-only lifecycle defect.

Observed blocker:
- product/database/Web/API became healthy;
- fresh state remained valid: Local Identity initial Administrator still required; no account/project/application/import/Demo was created;
- after Compose health succeeded, `scripts/preview/local-audit.ps1` failed in `Add-Transition` while assigning `lastTransitionMessage` to a `PSCustomObject` whose initial schema did not define that property;
- persisted session therefore remained `STARTING` and could not truthfully report resumable RUNNING state;
- CODEX used the authorized explicit `reset`, returning the environment to `NOT_STARTED` with no dedicated containers/volumes remaining.

Classification:
`HARNESS_DEFECT / POWERSHELL_SESSION_SCHEMA_MUTATION`.

This is not a product defect and does not reopen any product lane.

### 17.1 Bounded correction authorization

CODEX is authorized to change **only Preview harness infrastructure** on:
`preview/w15-first-project-env-harness`.

Required correction:
- make session transition metadata schema-safe and idempotent across newly-created and JSON-reloaded session objects;
- specifically, `Add-Transition` must never assume optional properties already exist before assigning them;
- preserve existing session/provenance/version-mismatch/reset semantics;
- do not weaken clean-worktree/exact-SHA/product-base guards;
- do not alter product source, tests, workflows, identity, licensing, Authority, lifecycle or project semantics.

A robust solution may initialize all optional schema fields at session creation and/or use explicit PowerShell property-add/update logic for missing optional fields. CODEX may choose the narrowest maintainable implementation.

### 17.2 Required harness regression proof before Main acceptance

Before publishing a replacement candidate, CODEX must prove at least:
1. PowerShell parse/static validation;
2. fresh `start` reaches truthful `RUNNING` and persists a manifest with the intended transition metadata;
3. `status` reports RUNNING without mutating/resetting state;
4. `pause -Checkpoint ...` reaches `PAUSED_RESUMABLE` and stores the checkpoint/message correctly;
5. `resume` reaches RUNNING/RESUMED correctly after JSON reload of the saved session;
6. repeated transition updates do not fail because optional properties are missing;
7. explicit `reset` returns `NOT_STARTED` and removes only the dedicated preview project/volumes while archiving evidence;
8. final branch diff from product base remains infrastructure-only.

This correction proof may remain blank-first-run infrastructure proof; do NOT create the continuity marker or begin exploratory product use until Main accepts the replacement exact harness SHA.

### 17.3 Gate state

- ENV_A: `HARNESS_FIX_AUTHORIZED / NOT_READY / BLACKBOX_NOT_RELEASED`;
- ENV_B: unchanged `HARNESS_STATIC_ACCEPTED / WAIT_REAL_CODESPACE_PROOF / HUMAN_AUDIT_NOT_RELEASED`;
- product checkpoint remains `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.


## 18. MAIN parallel ownership — ENV_B Codespace preparation

MAIN_ORDER_REV: 0008

STATE: ENV_B_MAIN_OWNED_PREPARATION / WAIT_SHARED_HARNESS_STABLE

The Product Owner authorized parallel preparation while CODEX continues ENV_A local-harness work.

Responsibility split:
- CODEX: ENV_A local container/readiness implementation and proof;
- MAIN COORDINATOR: ENV_B Codespace preparation, static review and real Codespace acceptance.

MAIN must not concurrently edit the shared harness implementation files while CODEX has an active bounded harness correction. ENV_B work is recorded control-side until the shared harness replacement SHA is reviewed and accepted.

Canonical ENV_B runbook:
`docs/WAVE15-FIRST-PROJECT-CODESPACE-ENV-B-RUNBOOK.md`

Runbook creation commit:
`04f3584baa72ffc5babf6bbe682ae2bad06be300`.

Current externally revalidated lifecycle facts incorporated into ENV_B acceptance:
- Dev Container `postStartCommand` executes on each successful container start and is part of environment resume semantics;
- GitHub Codespaces forwarded ports are private by default;
- a public forwarded port reverts to private after Codespace restart or remove/re-add;
- supported GitHub CLI visibility control is `gh codespace ports visibility <port>:public -c <codespace>`;
- `CODESPACE_NAME` and `CODESPACES=true` are supplied by Codespaces runtime;
- GitHub documentation explicitly warns that applications need to be restarted when a Codespace returns from inactivity.

Therefore the intended ENV_B design remains:
product/database start from Compose -> `postStartCommand` -> wait for 5173 forwarding -> re-assert 5173 PUBLIC -> verify 5080/5432 non-public -> browser readiness.

ENV_B remains:
`MAIN_PREPARING / STATIC_PATH_CONFIRMED / WAIT_EXACT_SHARED_HARNESS_SHA / HUMAN_PREVIEW_NOT_RELEASED`.

A real Codespace cannot be created/stopped/resumed through the currently available GitHub connector. Once the exact shared harness SHA is accepted, Product Owner interaction should be reduced to opening/creating the fresh Codespace; all startup and visibility configuration must be repository-controlled.


## 19. Replacement harness accepted — final ENV_A continuity proof

MAIN_ORDER_REV: 0009

STATE: REPLACEMENT_HARNESS_ACCEPTED_FOR_FINAL_READINESS_PROOF / BLACKBOX_HOLD

Main independently reviewed replacement harness:
- branch: `preview/w15-first-project-env-harness`;
- commit: `ec050e9bfda121805b1165860a4aeda0eb2582e8`;
- tree: `0d6221540c3678a8b042c51082f7a2ed0a466fa2`;
- parent harness: `1df4dae293bcca59ee3191faf889058fecc973ee`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Replacement delta vs parent is exactly one harness file:
`scripts/preview/local-audit.ps1`.

The fix introduces schema-safe add-or-update handling for optional session fields `lastTransitionMessage` and `lastCheckpoint`. No product/test/workflow/identity/licensing/Authority/project-seeding path changed.

CODEX regression evidence on the exact candidate is accepted for the blank first-run lifecycle layer:
- fresh start -> RUNNING;
- status -> RUNNING without manifest mutation;
- pause/checkpoint -> PAUSED_RESUMABLE;
- JSON-reloaded resume -> same session;
- two pause/resume cycles passed;
- repeated transition updates passed;
- reset -> NOT_STARTED with no dedicated preview containers/volumes.

This satisfies the harness wrapper regression but does not yet prove persisted product state across a host-level Docker restart.

### 19.1 Final ENV_A continuity proof authorized

CODEX is authorized to run one final disposable pre-audit readiness session using exact accepted harness:
`ec050e9bfda121805b1165860a4aeda0eb2582e8`.

Required sequence:
1. `start -AcceptedHarnessSha ec050e9bfda121805b1165860a4aeda0eb2582e8`;
2. prove fresh Local Identity/product state has no harness-seeded Administrator/project/application;
3. create the smallest supported product-owned persistence marker required for continuity proof. Prefer a minimal first-run Local Administrator identity only; do not create/import a project unless technically required to prove persistence;
4. verify the marker through normal supported product/public behavior;
5. `pause` and confirm `PAUSED_RESUMABLE`;
6. perform a **real Docker Desktop/daemon stop and restart**, not merely Compose stop/start, using a supported mechanism available on the host;
7. after Docker engine is healthy again, run `status` then `resume`;
8. prove the same session ID and same product-owned marker survived without reseed/migration;
9. perform a second normal `pause -> resume` cycle;
10. verify status/provenance;
11. explicit `reset`;
12. prove `NOT_STARTED`, no dedicated preview containers/volumes remain, and the marker no longer exists in a subsequent fresh environment boundary.

If an actual Docker Desktop/daemon restart is impossible because of host permissions/tooling, do not silently substitute Compose restart. Return `HOST_DAEMON_RESTART_PROOF_BLOCKED` with the exact limitation and strongest safe host-level test performed.

The purpose of step 6 is to validate the Product Owner requirement that a paused audit can survive PC shutdown/reboot. No actual OS reboot is required if the Docker engine itself can be fully stopped and restarted while named volumes persist.

This is still readiness proof, not black-box exploration. CODEX may use normal supported product/public contracts only to create/read the minimal marker; no source-guided UX evaluation, DB mutation, project fixture, EEE/Demo import or product correction.

ENV_A remains:
`FINAL_CONTINUITY_PROOF_AUTHORIZED / NOT_READY / BLACKBOX_NOT_RELEASED`.

### 19.2 Shared harness disposition for ENV_B

The same exact harness SHA `ec050e9bfda121805b1165860a4aeda0eb2582e8` is now the current shared candidate for Product Owner Codespace ENV_B.

The replacement touched only the local PowerShell lifecycle wrapper; Codespaces/devcontainer/Compose/public-port files are byte-identical to the previously static-reviewed candidate.

ENV_B therefore advances to:
`EXACT_SHARED_HARNESS_SELECTED / WAIT_REAL_CODESPACE_CREATE_START_RESUME_PROOF / HUMAN_PREVIEW_NOT_RELEASED`.

No real Codespace audit begins until the Codespace lifecycle gate is proven.


## 20. ENV_A CODEX Stage 2 directed verification prepared

MAIN_ORDER_REV: 0010

STATE: PREPARED / NOT ACTIVE / DOES_NOT_SUPERSEDE_CURRENT_ENV_A_READINESS_ORDER

A second ENV_A verification stage has been prepared from the Wave 14 diagnostic closure and final direct-CODEX handoffs, aligned with the accepted Wave 15 integrated lane premises.

Canonical contract:
`docs/WAVE15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION.md`

Creation commit:
`ce7a4b9a26cc5cdf36d466712eef62ab061d978f`.

Purpose:
- preserve Stage 1 as the unspoiled fresh-install/user-route black-box journey;
- after Stage 1 is sealed, derive a checkpoint/clone of the real CODEX-created project;
- use that realistic project as the body for directed/adversarial regression verification of Wave 14 transferred findings and Wave 15 integrated corrections;
- close or strengthen the formerly bounded A1/A4/A5a observations in local ENV_A where Codespaces forwarding is no longer the primary ambiguity;
- verify Editor, Script Engineering, runtime resilience, Authority/Licensing truth and FND-07 fresh/Neutral behavior without pretending single-node ENV_A proves FND-05 HA.

The prepared matrix is:
- V2-01 Working/Published/Active authority;
- V2-02 Engineering transport/error/fallback UX;
- V2-03 Screen/Popup selection + schema compatibility;
- V2-04 single-canvas Editor integration;
- V2-05 Script Engineering authoring/discovery;
- V2-06 Server Script bounded recovery/observability;
- V2-07 Runtime projection/navigation resilience;
- V2-08 Popup live values;
- V2-09 Trends/Historian/realtime separation;
- V2-10 Authority UX integrated behavior;
- V2-11 Licensing UX integrated behavior;
- V2-12 fresh-install Neutral/Detach boundary;
- V2-13 bounded FND-05 HA applicability.

Stage 2 has two modes:
- 2A: directed product-visible verification first;
- 2B: only after visible capture, diagnostic/adversarial correlation may use source, diagnostic/internal APIs, logs and controlled fault injection.

No product correction is authorized by this preparation.

Stage 2 may run after CODEX Stage 1 is complete/sealed even if the independent Product Owner ENV_B journey is still in progress, but all detailed CODEX Stage 1/Stage 2 findings remain embargoed from the Product Owner until the human journey ends.

Current binding shared CODEX mission remains the ENV_A readiness/continuity work already issued. Do not self-start Stage 2.

Gate:
`W15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION = PREPARED / NOT ACTIVE`.


## 21. ENV_A fresh-start dependency bootstrap blocker

MAIN_ORDER_REV: 0011

STATE: ENV_A_HARNESS_DEPENDENCY_BOUNDARY_FIX_AUTHORIZED / BLACKBOX_HOLD

CODEX completed the strongest part of the final continuity proof on exact harness `ec050e9bfda121805b1165860a4aeda0eb2582e8`:
- fresh session reached RUNNING;
- first Local Administrator was created through the normal UI as the minimal product-owned persistence marker;
- no project was created;
- session paused cleanly;
- Docker Desktop was fully restarted through supported `docker desktop restart --timeout 180`;
- Docker Desktop/Engine session changed and returned healthy;
- the exact same ENV_A session resumed;
- normal UI still showed the post-bootstrap no-project state, proving the Administrator/product-owned marker survived without reseed/import/migration;
- a second normal pause/resume cycle passed;
- explicit reset returned `NOT_STARTED` and removed all dedicated preview containers/volumes.

The final fresh-start absence check then failed before product startup because `run-product-preview.sh` attempted a new runtime `npm install` after reset and Node rejected the registry TLS chain with:
`UNABLE_TO_VERIFY_LEAF_SIGNATURE` while downloading `ws-8.21.3.tgz`.

CODEX correctly did not disable TLS verification or mutate host/container trust. The failed session was explicitly reset and ENV_A again ended `NOT_STARTED` with no dedicated preview resources.

Classification:
`ENV_A_HARNESS_DEFECT / AUDIT_RESET_DEPENDENCY_BOOTSTRAP_COUPLING`.

This is not a product defect. The Docker-daemon continuity requirement itself is now materially proven on the accepted candidate.

### 21.1 Architectural correction contract

A product/audit reset must not be the same thing as toolchain/dependency destruction.

The local Preview harness must separate:

**Preparation/toolchain state** — safe to persist across audit resets:
- pinned Linux runner image/toolchain;
- npm package cache and/or pre-resolved node_modules seed;
- NuGet package cache and/or pre-restored package seed;
- other immutable package-manager artifacts derived solely from repository lock/project files.

**Audit/product state** — MUST be reset explicitly:
- PostgreSQL/TimescaleDB audit database;
- runtime secrets/state specific to the audit session;
- Local Administrator/project/application data;
- browser/session state owned by the audit where applicable;
- current audit manifest/session identity.

Detailed audit evidence continues to be archived, not silently deleted.

### 21.2 Required operator semantics

CODEX may add an explicit repository-controlled preparation action such as:
`scripts/preview/local-audit.ps1 prepare`
(or a better equivalent), or may pre-bake dependencies into a dedicated Preview image/cache layer.

Binding behavior:
- network/package download belongs to preparation/build time, not to a clean product `start` after every reset;
- once preparation succeeds for an exact harness/product dependency set, repeated `reset -> start` cycles must not require a fresh registry/NuGet download;
- `reset` destroys product/audit state but preserves immutable dependency/tool caches;
- `start` must never silently repair a missing dependency boundary by weakening TLS/trust;
- exact repository lock/project files remain the package authority;
- cache/image provenance must be tied to the relevant lock/project hash or exact harness/product identity so stale dependencies cannot be reused silently.

### 21.3 Security boundary

Forbidden fixes:
- `npm strict-ssl=false`;
- `NODE_TLS_REJECT_UNAUTHORIZED=0`;
- disabling certificate validation in curl/npm/dotnet;
- automatically importing an unknown/untrusted host certificate;
- replacing pinned dependencies with floating versions;
- vendoring arbitrary machine-local dependency state into Git.

If a one-time preparation step itself cannot reach an external package registry because the host trust/network policy is invalid, fail explicitly as:
`ENVIRONMENT_PREP_BLOCKED_TLS`.

Do not hide that condition inside product startup.

### 21.4 Required proof on replacement candidate

Before Main acceptance, CODEX must return an exact replacement harness SHA/tree and prove:
1. branch diff from product base remains Preview/harness-only;
2. preparation succeeds or reuses a valid provenance-bound prepared layer;
3. clean `start` reaches the real first-run Administrator surface;
4. create the minimal Administrator marker through UI;
5. pause;
6. perform a real Docker Desktop/engine restart;
7. resume same session and prove marker continuity;
8. explicit `reset` removes product/audit state but preserves only dependency/tool preparation state;
9. a subsequent clean `start` reaches first-run Administrator/no-project state **without downloading npm/NuGet packages again**;
10. where technically possible, prove the post-reset start with external package network unavailable/blocked after preparation;
11. final explicit `reset` -> `NOT_STARTED` and no dedicated product-state containers/volumes;
12. no product semantics, auth, licensing, Authority or lifecycle were weakened.

The actual black-box Stage 1 remains HOLD until this exact replacement candidate is reviewed and ENV_A is declared READY.

### 21.5 Other gates

ENV_B remains pinned to the previously reviewed Codespaces implementation until Main reviews whether the replacement candidate touches shared Compose/devcontainer/startup files. No real Human Preview is released by this order.

ENV_A Stage 2 remains `PREPARED / NOT ACTIVE`.


## 22. Replacement dependency-boundary candidate accepted for final ENV_A proof

MAIN_ORDER_REV: 0012

STATE: EXACT_REPLACEMENT_ACCEPTED_FOR_FINAL_ENV_A_PROOF / BLACKBOX_HOLD

Main independently reviewed exact replacement harness:
- branch: `preview/w15-first-project-env-harness`;
- SHA: `bb451fa6e07982ac12384895f6097d5833761d16`;
- tree: `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Compare vs product base remains Preview/infrastructure-only. No product source, product test, workflow, auth, licensing, Authority or application-lifecycle semantic file changed.

### 22.1 Dependency boundary review

Main accepts the replacement architecture for final readiness proof:
- explicit `prepare` command owns package/network work;
- dependency identity is keyed to exact product base, harness SHA/tree and hashes of dependency inputs/lock/project files;
- Node/NuGet caches use external hash-keyed Docker volumes with provenance labels;
- the local Preview image is labeled with exact dependency provenance;
- `start` and `resume` require prepared provenance and execute Compose with `--no-build --pull never`;
- prepared startup verifies dependency markers before product launch;
- `reset` removes product/audit containers/network/database/runtime volumes but preserves provenance-bound dependency caches/tool image;
- evidence remains archived;
- stale/mismatched dependency state is fail-closed.

Toolchain image versions are pinned:
- Node 24.19.0;
- .NET SDK 10.0.400;
- Playwright 1.62.1;
with registry image digests pinned in the Preview runner Dockerfile.

### 22.2 Explicit local HTTPS-inspection root disposition

The earlier npm failure was traced to local Norton HTTPS inspection. The selected Norton root is already present in the Windows trusted-root store.

Main accepts the new opt-in mechanism **only for dependency preparation** because it:
- requires an explicit 40-hex thumbprint;
- reads only LocalMachine/CurrentUser Windows trusted-root stores;
- rejects absent, non-self-issued, non-CA or expired/not-yet-valid certificates;
- exports only the public certificate to a temporary PEM;
- mounts the PEM read-only only in the isolated preparation container;
- keeps TLS verification enabled through `NODE_EXTRA_CA_CERTS` / `SSL_CERT_FILE`;
- deletes the temporary PEM after preparation;
- records thumbprint + DER SHA-256 in local preparation provenance;
- does not modify Windows, Docker or product trust stores;
- does not copy the root into product `start`/`resume`;
- does not permit `TrustedRootThumbprint` on any command except `prepare`.

This does not authorize arbitrary enterprise/local roots. Any different root requires the same explicit trusted-root checks and remains an environment preparation concern, not product trust policy.

### 22.3 Preparation evidence accepted

On exact SHA `bb451fa...` CODEX reports:
- pinned tool image build: PASS;
- explicit trusted-root preparation: PASS;
- `npm ci`: PASS;
- `dotnet restore`: PASS;
- offline marker/provenance verification: PASS;
- repeated `prepare`: `READY_REUSED`;
- `status`: `NOT_STARTED / PREPARATION=READY`;
- no product/audit containers active.

Preparation evidence is sufficient to proceed to final product/audit lifecycle proof, not sufficient by itself to declare ENV_A READY.

### 22.4 Final exact-SHA ENV_A readiness proof

CODEX is now authorized to execute the final disposable readiness proof on exact SHA `bb451fa6e07982ac12384895f6097d5833761d16`.

Required sequence:
1. verify `status = NOT_STARTED / PREPARATION=READY`;
2. `start -AcceptedHarnessSha bb451fa6e07982ac12384895f6097d5833761d16`;
3. verify normal UI is true fresh first-run Administrator state with no seeded project/import/Demo;
4. create only the minimal Local Administrator through normal UI;
5. verify post-bootstrap no-project state;
6. `pause` -> `PAUSED_RESUMABLE`;
7. perform a real Docker Desktop/engine restart;
8. verify Docker engine recovery, then `status` + `resume`;
9. prove same audit session and same Administrator/no-project product state survived;
10. second normal `pause -> resume` cycle;
11. explicit `reset`;
12. prove product/audit resources are removed while `PREPARATION=READY` remains;
13. perform a second clean `start` on the same accepted SHA;
14. prove the second start performs no npm/NuGet install/restore/download and reaches fresh first-run Administrator/no-project state;
15. if practical, establish that package-network access is unnecessary during step 13 (without changing product networking semantics);
16. final explicit `reset` -> `NOT_STARTED / PREPARATION=READY`, with no product/audit containers/volumes remaining.

Do not create a project during this proof. Do not begin black-box Stage 1. Do not begin Stage 2.

### 22.5 ENV_B impact review

This replacement changes shared `ci/local/Dockerfile.linux-e2e` but does not change `.devcontainer/devcontainer.json`, `ci/local/docker-compose.preview.yml` or `scripts/preview/codespaces-public-web-port.sh` relative to the previously reviewed Codespaces candidate.

The final image still exposes the same pinned Node/.NET/Playwright toolchain and has already booted the local product path. ENV_B static design therefore remains accepted but still requires real Codespace create/start/stop/resume proof after ENV_A replacement acceptance is complete.

Current gates:
- ENV_A: `FINAL_EXACT_SHA_READINESS_PROOF_AUTHORIZED / NOT_READY / BLACKBOX_HOLD`;
- ENV_B: `STATIC_ACCEPTED / WAIT_REAL_CODESPACE_PROOF / HUMAN_PREVIEW_NOT_RELEASED`;
- ENV_A Stage 2: `PREPARED / NOT ACTIVE`.
