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
