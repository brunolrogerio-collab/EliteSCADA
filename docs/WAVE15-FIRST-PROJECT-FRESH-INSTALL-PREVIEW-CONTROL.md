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


## 23. ENV_A READY — black-box still held for ENV_B readiness

MAIN_ORDER_REV: 0013

STATE: ENV_A_READY / ENV_B_WAIT_REAL_CODESPACE / BLACKBOX_HOLD

Main reviewed CODEX final readiness handoff in Issue #305 comment `5856722029` against exact accepted harness:
- branch: `preview/w15-first-project-env-harness`;
- SHA: `bb451fa6e07982ac12384895f6097d5833761d16`;
- tree: `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Main disposition:
`ENV_A -> READY / CLEAN / RESUMABLE`.

Accepted evidence on exact SHA:
- preparation provenance: READY;
- fresh start reached true first-run UI;
- only minimal Local Administrator was created through normal UI;
- no project/import/Demo state was created;
- pause -> `PAUSED_RESUMABLE`;
- real Docker Desktop restart succeeded;
- same audit session resumed;
- same Administrator/no-project product state survived;
- second ordinary pause/resume cycle passed;
- reset destroyed product/audit state while preserving `PREPARATION=READY`;
- second fresh start used prepared dependencies with `--no-build --pull never`;
- no `npm ci`, `npm install`, `dotnet restore` or restore log match occurred during the second start;
- second fresh start reached healthy product UI;
- final reset returned `NOT_STARTED / PREPARATION=READY` with no product/audit containers/volumes;
- black-box Stage 1 and Stage 2 did not start.

This closes the ENV_A infrastructure gate.

### 23.1 Black-box release discipline

ENV_A readiness does **not** self-start the CODEX black-box journey.

Per the parallel-independent topology, Main keeps Stage 1 on HOLD until ENV_B has one real Codespace lifecycle proof and is also READY. This ensures both auditors can begin from independently prepared clean environments without one auditor's findings influencing the other.

Current CODEX state after this disposition:
`HOLD / ENV_A_READY / WAIT_ENV_B_READY / NO_ACTIVE_EXPLORATORY_MISSION`.

### 23.2 Dedicated Product Owner Codespace branch

Main created an exact Codespace audit branch directly from the accepted ENV_A/ENV_B harness SHA:
`preview/w15-first-project-env-b-codespace`

Exact branch creation point:
`bb451fa6e07982ac12384895f6097d5833761d16`.

This branch is intended to remain the Product Owner Codespace source for the real ENV_B lifecycle proof so the Codespace does not depend on a moving harness branch.

No product semantics differ from the accepted harness candidate at branch creation.

### 23.3 ENV_B next gate

ENV_B remains:
`WAIT_REAL_CODESPACE_CREATE_START_RESUME_PROOF / HUMAN_PREVIEW_NOT_RELEASED`.

The next safe action is to create/open a **fresh GitHub Codespace from branch**:
`preview/w15-first-project-env-b-codespace`.

The repository-controlled devcontainer must then prove automatically:
1. exact checkout SHA remains the accepted Codespace branch point unless a deliberate Main-only documentation/infrastructure delta is authorized;
2. TimescaleDB/API/Web become healthy without manual terminal startup;
3. true first-run Administrator/no-project state;
4. port 5173 becomes PUBLIC automatically;
5. 5080/5432 are not public;
6. ordinary browser access reaches EliteSCADA;
7. after Codespace stop/suspend -> resume, product startup recovers automatically and 5173 is re-asserted PUBLIC;
8. product state continuity is preserved;
9. no manual Ports-panel or terminal recovery is required.

Only after ENV_B is READY may Main release both independent exploratory gates.

### 23.4 Stage 2

`W15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION` remains `PREPARED / NOT ACTIVE`.


## 24. ENV_B one-click Codespace launch prepared

MAIN_ORDER_REV: 0014

STATE: ENV_A_READY / ENV_B_READY_FOR_PRODUCT_OWNER_CONFIRMATION / BLACKBOX_HOLD

Main prepared the real Product Owner Codespace creation handoff without moving the exact ENV_B audit branch.

Canonical launch document:
`docs/WAVE15-ENV-B-CODESPACE-LAUNCH.md`

Creation commit:
`b33e74d2c9983353a145a3ab0c00948183e65fd1`.

Official GitHub Codespaces deep link:
`https://codespaces.new/brunolrogerio-collab/EliteSCADA/tree/preview/w15-first-project-env-b-codespace`

This link preselects:
- repository `brunolrogerio-collab/EliteSCADA`;
- branch `preview/w15-first-project-env-b-codespace`.

The branch remains pinned at release point:
`bb451fa6e07982ac12384895f6097d5833761d16`
/tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`.

Expected Product Owner action is limited to opening the deep link and confirming **Create codespace**. Repository-controlled devcontainer/Compose/startup/port automation owns the rest.

Do not use `?quickstart=1` for this first ENV_B proof because that may resume a previously matching Codespace; the readiness gate requires a fresh Codespace.

ENV_B remains not READY until the first-open + real stop/resume evidence is accepted by Main.

Current gates:
- ENV_A: `READY / CLEAN / RESUMABLE`;
- ENV_B: `READY_FOR_PRODUCT_OWNER_CONFIRMATION / WAIT_REAL_CODESPACE_PROOF`;
- CODEX: HOLD;
- Stage 1: HOLD;
- Stage 2: PREPARED / NOT ACTIVE.


## 25. ENV_B first real Codespace creation in progress

MAIN_ORDER_REV: 0015

STATE: ENV_A_READY / ENV_B_FIRST_BOOT_IN_PROGRESS / BLACKBOX_HOLD

Product Owner initiated the first real GitHub Codespace creation from the dedicated ENV_B branch:
`preview/w15-first-project-env-b-codespace`.

Visual confirmation received from the Product Owner shows GitHub Codespaces in the remote setup/build phase (`Building codespace...`).

No readiness claim is made yet. Exact in-Codespace HEAD, devcontainer completion, service health and port visibility still require evidence after the build completes.

During this first boot the Product Owner must not:
- run manual startup commands;
- alter port visibility;
- seed Administrator/project/application state beyond a later Main-authorized continuity marker;
- restart/rebuild the Codespace before initial failure evidence is captured.

Expected automatic sequence after build:
1. repository devcontainer starts;
2. Compose starts TimescaleDB + EliteSCADA;
3. postStartCommand runs;
4. 5173 forwarding appears;
5. repository script re-asserts 5173 PUBLIC;
6. 5080/5432 remain non-public;
7. browser can open EliteSCADA first-run surface.

Next evidence gate after build completion:
- exact `git rev-parse HEAD`;
- Codespace/devcontainer startup completion;
- service health;
- Ports state for 5173/5080/5432;
- browser result on 5173;
- visible first-run/no-project product state.

Current state:
- ENV_A: `READY / CLEAN / RESUMABLE`;
- ENV_B: `FIRST_BOOT_IN_PROGRESS / NOT_READY`;
- CODEX Stage 1: HOLD;
- Human Preview: HOLD;
- Stage 2: PREPARED / NOT ACTIVE.


## 26. ENV_B first-open partial acceptance — product surface reached

MAIN_ORDER_REV: 0016

STATE: ENV_A_READY / ENV_B_FIRST_OPEN_PARTIAL_PASS / WAIT_PORT_VISIBILITY_AND_RESUME_PROOF / BLACKBOX_HOLD

Product Owner supplied visual evidence from the first real ENV_B Codespace boot.

Out-of-band screen-sharing note:
- a visible `Licença gratuita (uso não profissional)` banner belongs to AnyDesk remote-access software used by the Product Owner to access the computer;
- it is NOT EliteSCADA product UI and must be excluded from product findings/evidence classification.

GitHub live revalidation confirms the dedicated ENV_B branch still points to exact accepted harness SHA:
`bb451fa6e07982ac12384895f6097d5833761d16`.

First-open evidence accepted so far:
- GitHub Codespace successfully completed creation sufficiently to open the repository workspace;
- repository branch shown in the Codespace UI is the dedicated ENV_B Preview branch;
- EliteSCADA Web is reachable through a Codespaces forwarded `5173.app.github.dev` URL;
- the visible product surface is the genuine first-run `Bem-vindo ao EliteSCADA` flow;
- first Administrator creation form is available;
- no persisted project/application/import/Demo state is visible;
- no manual product startup command was reported by the Product Owner.

This is a **partial** ENV_B readiness pass only.

Still required before ENV_B READY:
1. inspect Ports state without changing it;
2. prove 5173 visibility is PUBLIC as required;
3. prove 5080 and 5432 are not PUBLIC;
4. capture exact in-Codespace HEAD if needed beyond branch-ref proof;
5. establish a minimal product-owned continuity marker at the correct readiness step;
6. normal Codespace stop/suspend;
7. resume same Codespace with no terminal recovery;
8. automatic DB/API/Web recovery;
9. automatic re-assertion of 5173 PUBLIC;
10. product-state continuity after resume.

Do not begin the real human first-project audit yet.

Current gates:
- ENV_A: `READY / CLEAN / RESUMABLE`;
- ENV_B: `FIRST_OPEN_PARTIAL_PASS / NOT_READY`;
- CODEX Stage 1: HOLD;
- Human Preview: HOLD;
- Stage 2: PREPARED / NOT ACTIVE.


## 27. ENV_B port visibility observation — Private but owner-accessible

MAIN_ORDER_REV: 0017

STATE: ENV_A_READY / ENV_B_FIRST_OPEN_PRODUCT_PASS / PORT_VISIBILITY_POLICY_MISMATCH / BLACKBOX_HOLD

Product Owner supplied the first live Ports-panel evidence from ENV_B.

Observed:
- EliteSCADA Web forwarding exists and the product opens successfully through the remote `5173.app.github.dev` URL;
- the Codespaces Ports panel reports the Web forwarded port as `Private`, not `Public`;
- this is consistent with GitHub Codespaces behavior: private forwarded ports remain browser-accessible to the authenticated Codespace creator, while public visibility removes the GitHub authentication requirement;
- therefore product/browser reachability is PASS, but the previously specified automatic-public visibility policy is NOT proven and currently mismatches the requested configuration.

Do not change the port manually yet. Preserve the live first-boot state for diagnosis.

This observation does not indicate an EliteSCADA product defect.

Possible infrastructure causes to distinguish before correction:
1. repository postStartCommand did not complete successfully;
2. the GitHub CLI inside the Codespace could not authorize the `gh codespace ports visibility` action with the runtime token;
3. organization/repository Codespaces policy disallows Public visibility;
4. visibility was set and subsequently reverted by a lifecycle/forwarding recreation event.

Main must distinguish these before changing the harness or asking Product Owner to modify the port manually.

Current gates:
- ENV_A: `READY / CLEAN / RESUMABLE`;
- ENV_B product first-open reachability: PASS;
- ENV_B port policy: `5173 PRIVATE / REQUESTED PUBLIC / DIAGNOSIS REQUIRED`;
- ENV_B overall: NOT READY;
- CODEX Stage 1: HOLD;
- Human Preview: HOLD;
- Stage 2: PREPARED / NOT ACTIVE.


## 28. ENV_B port visibility policy relaxed by Product Owner

MAIN_ORDER_REV: 0018

STATE: ENV_A_READY / ENV_B_PORT_VISIBILITY_NON_BLOCKING / WAIT_STOP_RESUME_PROOF / BLACKBOX_HOLD

The Product Owner explicitly chose not to spend further time on automatic 5173 Public visibility.

Live evidence shows:
- the Ports UI offers both Private and Public, so organization/repository policy permits Public visibility;
- the current 5173 forward is Private;
- the authenticated Product Owner can open EliteSCADA successfully in a normal browser through the Private forwarded URL;
- 5080 and 5432 are not exposed as public browser ports in the supplied evidence.

Main therefore reclassifies automatic `5173=Public` as a **non-blocking convenience requirement** for this Wave 15 human Preview.

This supersedes earlier wording that made Public visibility a readiness gate.

ENV_B may be accepted READY with 5173 remaining Private provided:
1. the Product Owner can open the forwarded Web URL normally while authenticated to GitHub;
2. 5080 and 5432 remain non-public;
3. product startup is automatic;
4. stop/suspend -> resume restores the product without terminal recovery;
5. the Product Owner can reopen the same Private forwarded Web URL after resume;
6. product-owned continuity state survives resume;
7. no hidden project/import/Demo state is seeded.

Do not spend CODEX/Main time repairing automatic Public visibility unless it later prevents the Product Owner from performing the audit.

Current next gate:
- create the minimal readiness continuity marker through the normal first-run UI;
- stop/suspend the Codespace normally;
- resume the same Codespace;
- verify automatic product recovery and browser accessibility;
- if PASS, Main may declare ENV_B READY.

Current states:
- ENV_A: `READY / CLEAN / RESUMABLE`;
- ENV_B: `FIRST_OPEN_PASS / PORT_PRIVATE_ACCEPTED / WAIT_STOP_RESUME_PROOF`;
- CODEX Stage 1: HOLD;
- Human Preview: HOLD;
- Stage 2: PREPARED / NOT ACTIVE.


## 29. ENV_B continuity marker created / Codespace stop in progress

MAIN_ORDER_REV: 0019

STATE: ENV_A_READY / ENV_B_CONTINUITY_PROOF_IN_PROGRESS / BLACKBOX_HOLD

Product Owner completed the minimal ENV_B readiness continuity marker through the normal EliteSCADA first-run UI:
- first Local Administrator created;
- no project created;
- no application/import/Demo/EEE state created;
- current product-owned continuity state is therefore the persisted Administrator plus the normal no-project surface.

This is the intended minimal marker for the Codespace stop/resume readiness proof and is not counted as the human first-project audit.

Product Owner then initiated a normal stop of the same Codespace and is waiting for it to reach the fully stopped state.

### Required resume proof

Once the Codespace is fully stopped, Product Owner must reopen **the same Codespace**.

During resume:
- do not run terminal startup commands;
- do not rebuild the container manually;
- do not change port visibility;
- do not create a project;
- do not repair product state manually.

Required observed result after normal resume:
1. Codespace/devcontainer returns to a usable state;
2. repository-controlled startup automatically restores TimescaleDB/API/Web;
3. the existing private 5173 forward remains browser-accessible to the authenticated Product Owner or is recreated automatically;
4. EliteSCADA opens without manual terminal recovery;
5. the first-run Administrator creation form does **not** reappear;
6. the product instead returns to the authenticated/post-bootstrap no-project state (e.g. `Criar novo projeto / No persisted project`);
7. no hidden project/import/Demo state appears;
8. no product-owned continuity state is lost.

If all pass, Main may record:
`ENV_B = READY / RESUMABLE`.

Then, and only then, Main may release the independent Product Owner human Preview and CODEX Stage 1 together from clean/isolated environments.

Current gates:
- ENV_A: `READY / CLEAN / RESUMABLE`;
- ENV_B: `CONTINUITY_MARKER_CREATED / CODESPACE_STOP_IN_PROGRESS / NOT_READY`;
- CODEX: HOLD;
- Stage 1: HOLD;
- Human Preview: HOLD;
- Stage 2: PREPARED / NOT ACTIVE.


## 30. ENV_B READY — parallel exploratory journeys released

MAIN_ORDER_REV: 0020

STATE: ENV_A_READY / ENV_B_READY / PARALLEL_EXPLORATORY_PREVIEW_ACTIVE

Product Owner completed the ENV_B stop/resume readiness proof on the same real Codespace created from:
`preview/w15-first-project-env-b-codespace`
at exact harness point:
`bb451fa6e07982ac12384895f6097d5833761d16` / tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`.

Observed after normal Codespace stop -> reopen:
- Codespace resumed successfully;
- repository-controlled product startup ran automatically in the terminal lifecycle;
- Product Owner did not manually start EliteSCADA;
- browser access to the forwarded Web surface worked again;
- the first Administrator setup flow did not return;
- the persisted Administrator continuity marker survived;
- product returned to the normal no-project / `Criar novo projeto` state;
- no project/import/Demo/EEE state appeared.

Main disposition:
`ENV_B = READY / RESUMABLE`.

5173 remains Private but authenticated-owner browser access is accepted as a non-blocking convenience deviation per rev 0018. 5080/5432 remain non-public in the accepted first-open evidence.

### 30.1 Human Preview released

Gate:
`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 -> ACTIVE`.

Human starting checkpoint:
- genuine Product Owner Codespace;
- Administrator already created solely as the ENV_B readiness continuity marker;
- **no project exists**;
- current product-visible surface is `Criar novo projeto`.

This readiness bootstrap does not count as the first-project audit itself. The Human Preview begins now from the first-project boundary.

Binding human mission remains high-level:
> Starting from EliteSCADA with no prepared project, create the first SCADA application from zero and make it work in Runtime using the product as a normal human user.

No Main/CODEX click-by-click coaching should be given during the unaided journey. If Product Owner becomes blocked and asks for help, record that point as the end of unaided discovery before providing assistance.

### 30.2 CODEX Stage 1 released in parallel

Gate:
`W15-FIRST-PROJECT-CODEX-BLACKBOX-PREVIEW-01 -> ACTIVE`.

Exact environment authority:
- harness branch: `preview/w15-first-project-env-harness`;
- harness SHA: `bb451fa6e07982ac12384895f6097d5833761d16`;
- harness tree: `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`;
- ENV_A readiness: `READY / CLEAN / RESUMABLE`;
- expected starting state: `NOT_STARTED / PREPARATION=READY`.

Dedicated embargoed CODEX evidence branch created:
`preview/w15-first-project-codex-evidence`
from exact harness SHA `bb451fa...`.

CODEX must start a brand-new ENV_A audit session and then obey the original black-box restrictions.

Allowed infrastructure-only lifecycle operations during Stage 1:
- `start` once for the accepted clean session;
- `status` for coarse environment state;
- `pause` / `resume` solely to survive CODEX time-window or host interruption;
- no `reset` unless the environment is invalid and Main later authorizes abandoning the session.

These lifecycle commands are not product navigation shortcuts and must not inspect or mutate product DB/state directly.

Binding CODEX user mission:
> You have just installed EliteSCADA. Explore the product using normal user-visible interfaces and create a small SCADA application from scratch that reaches a functional Runtime.

During the exploratory journey CODEX MUST NOT use repository source, control-plane implementation detail, database inspection, direct internal API shortcuts, implementation logs, test fixtures or shell/state mutation to discover how to proceed.

If blocked, stop the user journey honestly and classify `BLOCKED_BY_PRODUCT` or `BLOCKED_BY_ENVIRONMENT` as applicable.

After the user journey is COMPLETE or BLOCKED, CODEX may enter diagnostic correlation and inspect source/log/API/tests, but must not fix product code.

Detailed findings must go only to the embargoed CODEX evidence branch and must not be copied into Product Owner-facing control/handoff text while the human journey is active.

### 30.3 Embargo and independence

While Human Preview is active:
- Main may know only coarse CODEX gate state: RUNNING / PAUSED_RESUMABLE / COMPLETE / BLOCKED;
- do not expose detailed CODEX findings, navigation traps, recommended clicks or diagnostic causes to Product Owner;
- do not feed Product Owner findings to CODEX;
- do not alter either environment in response to the other auditor's findings;
- no cross-audit comparison yet.

The embargo ends only after the Product Owner human first-project journey reaches COMPLETE or BLOCKED.

### 30.4 Stage 2 remains held

`W15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION = PREPARED / NOT ACTIVE`.

It may activate only after Stage 1 is sealed and its state is checkpointed/derived according to the Stage 2 contract.

Current gates:
- ENV_A: `READY / STAGE1_ACTIVE`;
- ENV_B: `READY / HUMAN_PREVIEW_ACTIVE`;
- CODEX Stage 1: ACTIVE;
- Human Preview: ACTIVE;
- Stage 2: PREPARED / NOT ACTIVE;
- findings embargo: ACTIVE.


## 31. CODEX Stage 1 attempt 1 sealed inconclusive

MAIN_ORDER_REV: 0021

STATE: HUMAN_PREVIEW_ACTIVE / CODEX_STAGE1_ATTEMPT1_SEAL_AND_RECONCILE / EMBARGO_ACTIVE

Main reviewed the coarse CODEX handoff from Issue #305 comment `5857611740` together with the embargoed evidence branch, without exposing detailed findings to the Product Owner.

Exact accepted environment authority remains unchanged:
- harness SHA: `bb451fa6e07982ac12384895f6097d5833761d16`;
- harness tree: `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

### 31.1 Main disposition of CODEX Stage 1 attempt 1

The attempt is sealed as:
`INCONCLUSIVE / AUDIT_INTERACTION_AND_STATE_RECONCILIATION_REQUIRED`.

It is **not** classified as a product defect and it is **not** considered a completed independent black-box journey.

Reasons are retained only in the embargoed evidence surface. Product Owner-facing coordination must expose no navigation/detail spoilers while the Human Preview is active.

The existing attempt has already crossed into permitted post-block diagnostic correlation. Therefore the same CODEX context/session must not resume black-box exploration as if it were still unspoiled.

### 31.2 Preparation-state reconciliation

The audit session remains valuable evidence and must not be reset.

CODEX must reconcile only infrastructure/worktree identity:
1. stop all product interaction and diagnostic exploration;
2. preserve/push existing detailed evidence to `preview/w15-first-project-codex-evidence`;
3. use a separate Git worktree or connector-backed evidence path for future evidence commits so the runtime harness worktree is not moved onto the evidence branch;
4. inspect current runtime worktree branch/HEAD/tree using Git only;
5. if the runtime worktree is not the exact accepted harness SHA `bb451fa...`, restore that worktree to the exact accepted harness commit **without** resetting/recreating Docker product state;
6. do not run `prepare`, `start`, `resume` or `reset` during identity reconciliation;
7. run only `local-audit.ps1 status` after the exact harness checkout is restored;
8. expected reconciled preparation key is the accepted READY key `0d074f1bd0f1702a5313651791dd7ce6aa1996821f312ca45a7d6761f9b0794e`;
9. if status becomes `RUNNING / PREPARATION=READY`, immediately `pause` the same session with a coarse checkpoint indicating Stage 1 attempt 1 is sealed;
10. if status does not reconcile cleanly, return the exact coarse infrastructure state to Main and do nothing destructive.

No product-state mutation is authorized by this reconciliation.

### 31.3 Black-box retry policy

Because attempt 1 has already entered source/log/API diagnostic correlation, a valid independent black-box retry must use a **fresh CODEX context/agent** that has not consumed the embargoed attempt-1 findings.

Do not start such a retry yet while the Product Owner human journey remains active unless Main explicitly provisions a fresh CODEX context and new evidence surface.

Current shared CODEX state after reconciliation is intended to become:
`HOLD / STAGE1_ATTEMPT1_SEALED / WAIT_MAIN_RETRY_DECISION`.

### 31.4 Human Preview remains unchanged

`W15-FIRST-PROJECT-HUMAN-PREVIEW-01 = ACTIVE`.

Do not expose any CODEX detailed observations to the Product Owner. Do not change ENV_B in response to CODEX attempt 1.

### 31.5 Stage 2 remains held

`W15-ENV-A-CODEX-STAGE2-DIRECTED-VERIFICATION = PREPARED / NOT ACTIVE`.

Main will consider Stage 2 only after the human first-project journey reaches COMPLETE or BLOCKED and the embargo can be lifted/cross-audit state can be reconciled.

Current gates:
- ENV_A infrastructure: `READY / ATTEMPT1_SESSION_TO_BE_PAUSED`;
- CODEX Stage 1 attempt 1: `INCONCLUSIVE / SEALED_PENDING_INFRA_RECONCILIATION`;
- Human Preview: ACTIVE;
- findings embargo: ACTIVE;
- Stage 2: PREPARED / NOT ACTIVE.


## 32. CODEX autonomous ENV_A infrastructure recovery authorized

MAIN_ORDER_REV: 0022

STATE: HUMAN_PREVIEW_ACTIVE / CODEX_AUTONOMOUS_ENV_A_INFRA_RECOVERY / EMBARGO_ACTIVE

The Product Owner explicitly authorized CODEX to continue doing useful work without stopping for Main approval at every small infrastructure decision.

Main reviewed the latest reconciliation handoff in Issue #305 comment `5857641422`:
- runtime worktree is already exact and clean at accepted harness `bb451fa6e07982ac12384895f6097d5833761d16` / tree `650d30089596021cb1a564ee1d2f1abfc7d2b509`;
- existing attempt-1 session `6f18039a-5e67-49bd-a566-2050cf775aee` remains RUNNING;
- preparation reports REQUIRED because the provenance manifest is absent;
- no destructive/product action was taken during reconciliation.

The earlier attempt-1 product exploration remains sealed/inconclusive. It must not resume as an independent black-box attempt in this CODEX context.

### 32.1 Autonomous mission

CODEX is now authorized to act as **ENV_A infrastructure/audit-harness maintainer** until it reaches a stable repaired state or a genuine external blocker.

CODEX may decide the detailed implementation path without asking Main for each micro-step.

Goals, in priority order:
1. preserve all attempt-1 evidence and product/audit state before any destructive infrastructure operation;
2. quiesce/pause the existing attempt-1 runtime safely;
3. determine why the exact accepted harness now lacks its previously valid preparation manifest/provenance state;
4. correct the harness so evidence-branch operations, worktree switching, restart, pause/resume and evidence persistence cannot silently invalidate preparation state;
5. create automated regression coverage for the discovered harness failure mode;
6. validate the repaired harness through prepare/start/pause/resume/restart/reset cycles on disposable validation state;
7. leave a clean, deterministic ENV_A infrastructure candidate ready for a future fresh CODEX context or Stage 2;
8. preserve the sealed attempt-1 evidence independently from the runtime harness worktree.

### 32.2 Broadly authorized infrastructure actions

CODEX may, when needed:
- inspect harness source/scripts/configuration;
- inspect Docker images, labels, volumes, networks and container metadata;
- inspect gitignored Preview artifact/provenance files;
- inspect session/evidence manifests;
- inspect package preparation manifests and dependency markers;
- use source/log/API/test knowledge for **infrastructure diagnosis only**;
- create a separate Git worktree for embargoed evidence;
- commit/push harness-only changes on `preview/w15-first-project-env-harness`;
- add harness unit/static/integration regression checks;
- add a dedicated infrastructure command such as `repair-preparation`, `snapshot`, or equivalent if that is the cleanest design;
- regenerate preparation metadata from already-present immutable assets **only when their exact provenance can be cryptographically/label-verified**;
- run `prepare` after the attempt-1 session is safely quiesced/preserved and the harness contract supports doing so without touching product state;
- use package network during explicit preparation if required, under the already accepted TLS/trust rules;
- create disposable validation sessions after attempt-1 evidence is safely preserved;
- perform Docker Desktop restart tests;
- reset **disposable validation sessions** freely;
- update Preview harness documentation/evidence.

### 32.3 Attempt-1 preservation boundary

Before any destructive action against the existing attempt-1 product state, CODEX must create and verify a durable snapshot sufficient to preserve forensic value, including at minimum:
- current session manifest;
- exact harness/product identity;
- Docker volume/container metadata;
- persistent database/product-state snapshot or equivalent restorable backup;
- existing embargoed evidence branch HEAD/path references.

Prefer first attempting the normal harness `pause`; the current `pause` path does not require preparation READY and should preserve the database volume/session.

If normal `pause` fails for an infrastructure-only reason, CODEX may use the least invasive Docker-level quiesce necessary to preserve state, documenting that deviation. Do not edit product DB rows to manufacture a paused state.

After verified snapshot/preservation, CODEX may archive/reset the attempt-1 runtime **only if necessary to repair/validate the harness**. Attempt-1 remains sealed and must never be silently repurposed as a new black-box attempt.

### 32.4 Product boundary remains strict

CODEX MUST NOT:
- modify EliteSCADA product source or product tests;
- change product behavior to make the audit easier;
- alter auth/licensing/Authority semantics;
- mutate product DB rows as a repair shortcut;
- seed a project/Demo/EEE state for a pass;
- disclose attempt-1 detailed findings to Product Owner-facing controls;
- consume Product Owner human-audit observations;
- start a new independent black-box attempt in the same contaminated CODEX context.

If diagnosis uncovers a probable product defect, preserve it in embargoed evidence only and continue with infrastructure work where independent. Do not fix it.

### 32.5 Harness repair acceptance target

A replacement harness candidate is considered ready for Main review when CODEX can demonstrate, preferably in one comprehensive handoff:
- exact infrastructure-only diff from product base;
- attempt-1 evidence safely preserved/archived;
- preparation manifest/provenance failure root cause identified;
- regression test/check reproduces the old failure and passes after the fix;
- evidence commits no longer require moving/changing the runtime harness worktree;
- `prepare` reaches READY with exact provenance;
- disposable clean start reaches true first-run state;
- pause/resume works;
- Docker Desktop restart/resume works;
- reset preserves dependency preparation and removes product state;
- repeated status does not incorrectly flip READY -> REQUIRED;
- evidence worktree/branch commits do not invalidate runtime dependency identity;
- final disposable validation state is clean.

Do not stop merely because one approach fails. Choose another safe infrastructure approach within this contract.

### 32.6 Reporting cadence

CODEX should not return to Main for ordinary implementation choices or recoverable harness failures.

Return only when one of these is true:
- replacement harness candidate is fully validated and pushed;
- a genuine external blocker prevents further safe progress;
- preservation of attempt-1 evidence would be endangered by continuing;
- a required action would cross the product boundary above.

Detailed product observations remain embargoed.

### 32.7 Parallel human audit

Human Preview remains ACTIVE and unchanged.

No CODEX infrastructure repair should alter ENV_B or provide navigation guidance to the Product Owner.

Stage 2 remains `PREPARED / NOT ACTIVE`.


## 33. ENV_A repair candidate accepted for autonomous final lifecycle validation

MAIN_ORDER_REV: 0023

STATE: HUMAN_PREVIEW_ACTIVE / ENV_A_REPAIR_CANDIDATE_FINAL_VALIDATION / EMBARGO_ACTIVE

Main independently reviewed CODEX autonomous recovery handoff in Issue #305 comment `5857835461` and exact repair candidate:
- branch: `preview/w15-first-project-env-harness-repair`;
- SHA: `50a4451aa122f7f9fd0af98173c184f6623a147b`;
- tree: `5efeafb08725a90ce0c3df689b8d613f165bb569`;
- parent accepted harness: `bb451fa6e07982ac12384895f6097d5833761d16`;
- product base: `1f14a57491805a5d976bc9d0bf51393cf1b3ebcd`.

Compare vs product base remains Preview/harness-only. No EliteSCADA product source/test/workflow/auth/licensing/Authority/application-lifecycle semantics changed.

### 33.1 Root cause accepted

Main accepts the infrastructure root-cause model:

The previous dependency identity used hashes of dependency files as materialized in the working tree. On Windows, two clean checkouts of the **same exact Git commit/tree** may materialize text with different LF/CRLF bytes depending on checkout/configuration. That changed `inputsSha`, therefore changed the derived dependency key/manifest path even though Git HEAD/tree and dependency content authority had not changed. Result: a valid prepared environment could later report `PREPARATION=REQUIRED` with a different key and no matching manifest.

The repair changes dependency-input identity to committed Git blob IDs, aggregated deterministically, while the existing clean-worktree and exact harness SHA/tree guards remain authoritative.

This gives the desired property:
- same committed tree + different LF/CRLF materialization -> same dependency identity;
- changed committed dependency input -> different dependency identity.

A second Windows boundary was also identified: PowerShell here-string/embedded shell text may carry CRLF into Bash. The repair normalizes embedded Bash text to LF before base64 transport/execution.

### 33.2 Static/regression review accepted

Main accepts the candidate design for final validation:
- new `scripts/preview/PreviewDependencyIdentity.psm1` isolates committed dependency identity logic;
- dependency paths are repository-relative, normalized, sorted/unique and validated;
- provenance is derived from `HEAD:<path>` Git blobs rather than mutable checkout bytes;
- non-blob/missing committed inputs fail closed;
- `scripts/preview/test-dependency-identity.ps1` creates LF and CRLF clean clones of the same tree and proves identical dependency identity;
- the regression also proves a committed dependency change changes identity;
- the regression proves embedded Bash CRLF/CR is normalized to LF;
- preparation on the candidate reached `PREPARATION=READY / STATE=NOT_STARTED`;
- attempt-1 database snapshot was verified via disposable TimescaleDB restore before old runtime cleanup;
- sealed attempt-1 evidence remains preserved/embargoed.

This is an acceptance to run final lifecycle validation, not yet the final ENV_A harness promotion.

### 33.3 Autonomous final lifecycle proof — execute without further micro-approval

CODEX must continue autonomously on exact repair candidate `50a4451...` until the full validation below is complete or a genuine external blocker remains.

Required proof:
1. re-run the dependency identity/line-ending regression and record PASS;
2. `status = NOT_STARTED / PREPARATION=READY` on exact repair SHA/tree;
3. run a disposable `start` using Main-accepted repair SHA;
4. prove application stack health and true fresh first-run surface, without beginning a new black-box audit;
5. run repeated `status` checks and prove dependency key remains stable;
6. `pause` -> `PAUSED_RESUMABLE / PREPARATION=READY`;
7. `resume` -> same disposable session healthy;
8. perform a real Docker Desktop/engine restart;
9. after engine recovery, `status` then `resume` and prove same disposable session lifecycle remains valid;
10. while the runtime harness worktree remains fixed, make at least one harmless embargoed-evidence commit through a **separate worktree/path** and prove runtime `status` still reports the same PREPARATION=READY/dependency key;
11. pause/reset the disposable validation session;
12. prove `NOT_STARTED / PREPARATION=READY` and no product-state containers/volumes remain;
13. start a second disposable fresh session without re-running package preparation and prove no npm/NuGet install/restore is triggered;
14. final reset -> clean `NOT_STARTED / PREPARATION=READY`;
15. `git diff --check`, PowerShell parse/static checks and worktree cleanliness pass;
16. exact compare from product base remains harness-only.

No project creation is required for this validation. Do not turn it into a new black-box attempt.

### 33.4 Promotion target

If all proof passes, CODEX may push any final harness-only cleanup commit(s) on the repair branch and return one exact final SHA/tree.

Do **not** fast-forward or rewrite `preview/w15-first-project-env-harness` yourself. Main will promote the accepted final candidate after review.

### 33.5 Reporting discipline

Do not stop for recoverable test failures or small implementation choices. Iterate safely within the infrastructure boundary until the full proof passes.

Return only:
- fully validated final candidate; or
- genuine external blocker; or
- preservation boundary risk.

Human Preview remains ACTIVE and independent.
Detailed attempt-1 product observations remain embargoed.
Stage 2 remains `PREPARED / NOT ACTIVE`.
