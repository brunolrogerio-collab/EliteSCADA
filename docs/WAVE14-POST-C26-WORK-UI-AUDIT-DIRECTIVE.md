# Wave 14 Post-C26 — ChatGPT Work Real UI/Functional Audit Directive

Tracking issue: #289  
Applies after: accepted C26 product is integrated through the authorized route into corrected canonical C11 and a new post-C26 Preview is technically ready.

## 1. Authority and scope

**GitHub live remains the official memory and sole authority for implemented EliteSCADA state.**

Before executing any part of this gate, revalidate live:

- `PROJECT GOAL.md`;
- `LAST CHANGE.md`;
- the active C26 issue/PR state or its accepted successor state;
- branch and exact HEAD;
- exact-HEAD workflows/checks;
- canonical C11;
- integration and validation PRs;
- the current Preview candidate.

If this directive and GitHub live disagree about what is implemented, GitHub live wins.

This document adds a mandatory **post-C26 real-use quality gate**. It does **not** weaken or replace existing rules for security, authentication, authorization, Identity, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics, backend Active Revision Runtime authority, CI, integration, `main`, or validation-only PRs.

Do not execute this audit during C26. The current ordered C26 work must finish normally first.

## 2. Purpose

After C26 is complete, exact-SHA validated, accepted, and integrated through the authorized flow into the corrected canonical C11/EEE candidate, ChatGPT Work must **open and actually use EliteSCADA through the real browser Preview as a user**.

The purpose is to discover visual, functional and usability defects that can survive deterministic tests and green CI but become obvious during normal use of the product.

The Work audit complements, and never substitutes for:

- unit/integration tests;
- Playwright/browser regressions;
- specialized CI/L3 gates;
- static code review;
- architecture review;
- Product Owner homologation.

The following do **not** satisfy this gate by themselves:

- repository-only analysis;
- static code review;
- screenshots alone;
- Playwright alone;
- CI alone;
- another chat merely reading the repository.

## 3. Mandatory quality sequence

The intended quality path after C26 is:

```text
C26 COMPLETE
  -> C26 exact-SHA green
  -> C26 accepted
  -> C26 -> canonical C11 through authorized integration
  -> corrected canonical C11/EEE
  -> regenerate canonical .escadapkg
  -> checksum + provenance
  -> NEW post-C26 Preview
  -> technical CI/smoke green
  -> READY FOR WORK AUDIT
  -> ChatGPT Work real exploratory UI/functional audit
  -> coordinator triage + technical reproduction
  -> generic corrections + deterministic regressions
  -> new exact candidate
  -> optional targeted Work recheck when justified
  -> PRODUCT OWNER final homologation
```

Pre-C26 Preview #285 remains historical evidence and must not be repurposed as the post-C26 candidate.

The candidate must **not** proceed directly from post-C26 Preview technical green to final Product Owner homologation unless the Product Owner later explicitly cancels this Work audit requirement.

## 4. Practical Work budget and preparation principle

The practical continuous Work audit budget is approximately **40 minutes**.

The coordinator must therefore prepare everything that can reasonably be prepared before the Work session. Work time is primarily for:

- opening the real product;
- observing it;
- navigating it;
- interacting with it;
- reproducing suspicious behavior;
- collecting screenshots/evidence;
- recording structured findings.

Do not spend the main Work window on work the coordinator can perform beforehand, including:

- discovering which branch/candidate to use;
- reading long coordination histories;
- cloning/preparing the repository;
- dependency restore / `npm install`;
- compiling;
- starting PostgreSQL/TimescaleDB from scratch;
- discovering ports/proxy URLs;
- configuring the Preview;
- manually creating the EEE application;
- importing the package and manually performing initial Save/Publish/Activate solely to bootstrap the audit;
- discovering usernames/passwords or creating audit users.

## 5. Correct execution moment

Do not start Work while any C26 item remains incomplete or unvalidated.

Required predecessor state:

1. C26 complete;
2. exact C26 product/test SHA green through required gates;
3. required C26 acceptance recorded;
4. authorized C26 -> canonical C11 integration complete;
5. corrected canonical C11 exact state established;
6. canonical EEE regenerated;
7. new `.escadapkg` generated;
8. package SHA-256 generated;
9. provenance bound to the exact accepted heads;
10. a **new** post-C26 Preview created;
11. Preview technical CI/smoke green;
12. real environment confirmed functional;
13. only then enter `READY FOR WORK AUDIT`.

## 6. Mandatory prepared environment

Before declaring `READY FOR WORK AUDIT`, the post-C26 Preview should expose a live SCADA rather than an empty setup screen.

Required operational target:

- Preview online and accessible;
- EliteSCADA backend running;
- frontend running;
- PostgreSQL available;
- TimescaleDB/Historian available;
- HistoricalQuery correctly enabled/configured, including required safe cursor-key configuration;
- canonical `EliteSCADA-EEE-Demo` loaded;
- persisted Active Revision active and authoritative;
- Runtime functional;
- canonical simulation running;
- TAG values changing;
- useful Alarm/Event/Historian state available;
- Screens available;
- Popups available;
- Dynamos operating;
- Engineering accessible;
- Screen Editor accessible;
- Popup Editor accessible.

The Work session must not be consumed discovering that a backend, database, route or package bootstrap never started.

## 7. Dynamic simulation data

The canonical EEE simulation should produce enough changing process state to evaluate EliteSCADA as an industrial supervisory system, not merely as a static drawing.

Where supported by the canonical application, provide observable changes in representative:

- levels;
- pump states;
- Boolean states;
- analog values;
- alarms;
- operational events;
- historian samples;
- trends;
- Dynamo states;
- quality.

Do not fabricate Good quality when the source state is unavailable/bad merely to make the audit visually pleasant.

## 8. Preview endpoints

The coordinator must identify the exact browser endpoints before the Work handoff.

Prefer explicit candidate URLs such as:

```text
Preview:     https://<candidate-preview>/
Runtime:     https://<candidate-preview>/runtime
Engineering: https://<candidate-preview>/engineering
```

Use the actual equivalent routes when they differ.

Do not require Work to discover Ports, Codespaces forwarding URLs, proxies or internal API routes.

## 9. Authentication preparation

Prepare audit identities appropriate to the real product authority model, ideally covering at least:

- Administrator / Engineer;
- Operator;
- Viewer / View Only.

This enables evaluation of:

- full Engineering;
- interactive Runtime;
- read-only/view-only Runtime;
- capability projection and visible surface differences.

**Never commit passwords, tokens or equivalent secrets.**

Credentials must be delivered temporarily/securely at execution time using the approved environment mechanism. The repository handoff contains only non-secret authentication instructions.

## 10. Candidate-specific Work handoff

Only when the actual post-C26 candidate is technically ready, create:

`docs/WORK-UI-AUDIT-HANDOFF.md`

Do **not** create a fake READY handoff early with invented URLs, hashes or credentials.

The candidate-specific handoff must be deliberately short and contain only what Work needs to begin quickly:

```text
Product:
EliteSCADA

Candidate exact SHA:
<sha>

Preview:
<url>

Runtime:
<url>

Engineering:
<url>

Application:
EliteSCADA-EEE-Demo

Package SHA-256:
<hash>

State initial expected:
<short description>

Authentication:
<secure instruction without versioned secret>

Available areas:
<short description>

Known intentional limitations:
<only genuinely relevant limitations>

Do not modify:
<audit boundaries>

Audit result destination:
<where the original report/evidence must be returned or recorded>
```

Do not make this handoff another history book. Its purpose is to save Work context and minutes.

## 11. First Work pass: audit only

The first Work execution is an **audit**, not development.

During that pass Work must:

- open the real Preview in the browser;
- authenticate;
- use the actual product;
- navigate/interact normally;
- reproduce suspicious behavior;
- capture useful screenshots/evidence;
- write findings.

During that first pass Work must **not**:

- alter code;
- create patches;
- fix CSS;
- create commits;
- merge anything;
- deeply investigate implementation/source causes;
- wait for CI;
- turn the session into repository maintenance.

Source diagnosis and correction belong to the coordinator after the exploratory report.

## 12. Audit strategy

Use two complementary passes.

### 12.1 Free exploration first

Start as a normal user, without a long list of expected defects. Navigate and operate the product naturally and record anything that appears broken, confusing, misleading, visually defective or inappropriate for an industrial SCADA.

This free pass is deliberate. It reduces confirmation bias and prevents Work from merely checking boxes for already-known problems.

### 12.2 Directed verification second

After free exploration, deliberately review the principal Runtime and Engineering surfaces below.

## 13. Priority audit matrix

| Area | Primary evaluation |
| --- | --- |
| Runtime | composition, navigation, stability, current screen preservation |
| Screens | clipping, scaling, fit, scroll, framing |
| Popups | position, authored bounds, overlay, z-index, open/close behavior |
| Dynamos | states, bindings, rendering |
| TAG values | live update, quality, formatting |
| Fullscreen | composition, transitions and return |
| Alarm Browser | real use, state, messages and error/no-data semantics |
| Event Browser | real use and conceptual distinction from Alarm/Audit |
| Historian | query behavior, graph, no-data vs error semantics |
| Trends | live update and readability |
| Engineering | navigation, spacing, panel recovery, usable canvas |
| Screen Editor | practical basic authoring workflow |
| Popup Editor | practical basic authoring + Runtime equivalence |
| Properties | access, editability, persistence where applicable |
| Theme | contrast, editable/readonly/disabled/placeholder legibility |
| Localization | pt-BR/en/es when practical |
| Error UX | raw/internal technical detail exposed to operator/user |
| Responsive | representative viewport behavior |

## 14. Screen Editor emphasis

Evaluate at minimum, according to what the product visibly exposes/supports:

- object selection;
- structure-tree selection;
- move;
- resize;
- copy/paste;
- undo/redo;
- group/ungroup;
- lock;
- alignment;
- distribution;
- z-order;
- zoom;
- pan;
- grid;
- snap;
- Properties;
- text editing;
- bindings;
- preview.

Rule: **a visibly enabled control must actually perform its documented action.**

An unsupported action must not masquerade as functional through an enabled but inert control.

## 15. Popup Editor emphasis

Evaluate:

- authored bounds visible during editing;
- selection;
- move;
- resize;
- Properties;
- preview;
- position;
- dimensions;
- composition with the underlying Screen;
- Engineering/Preview/Runtime semantic equivalence.

## 16. Representative viewports

When technically possible in the Work browser, cover at least:

- 1920 x 1080;
- 1366 x 768;
- 1024 x 720;
- fullscreen when time permits.

Do not spend the limited session sweeping dozens of resolutions.

## 17. Defect patterns to notice

The audit is not limited to this list, but explicitly watch for:

- clipping;
- unwanted internal scrollbars;
- tiny authoring canvas;
- large wasted areas;
- overlap;
- Popup mispositioning;
- inaccessible elements;
- truncated text;
- poor contrast;
- readonly/editable/disabled ambiguity;
- technical IDs exposed as visible UI;
- internal strings/resources exposed;
- raw HTTP/transport messages shown to users;
- buttons with no effect;
- enabled controls with no implemented action;
- incoherent visual state;
- confusing navigation;
- broken resize;
- broken fullscreen;
- Engineering/Runtime mismatch;
- Screen layout moving unexpectedly when Popup opens/closes;
- bad/unavailable data represented as Good;
- no-data confused with failure;
- failure confused with valid no-data.

## 18. Finding format

Every finding must receive a stable audit ID and structured record, for example:

```text
UI-007

Severity:
P1

Area:
Engineering / Screen Editor

Title:
Properties becomes unreachable after ...

Steps:
1. ...
2. ...
3. ...

Expected:
...

Observed:
...

Reproducibility:
3/3

Classification:
GENERIC PRODUCT

Evidence:
<screenshot / concise evidence reference>

Notes:
...
```

## 19. Severity

Use:

- **P0** — operational/safety/authority risk or real inability to use a critical function;
- **P1** — important functional defect or major operation/authoring break;
- **P2** — relevant usability/interface defect without blocking the principal function;
- **P3** — visual refinement or experience improvement.

## 20. Generic vs EEE-specific classification

Each finding must be classified as:

- `GENERIC PRODUCT`;
- `EEE-SPECIFIC`;
- `UNCERTAIN`.

A defect merely observed in the EEE application is **not automatically EEE-specific**.

Renderer, shell, editor, Runtime, Historian, Alarm/Event, layout, Authority projection, navigation and common lifecycle defects are normally candidates for generic platform classification until proven otherwise.

Never implement an EEE-specific workaround to hide a generic platform defect.

## 21. Visual evidence

Whenever practical:

- P0/P1 findings should include visual evidence;
- P2/P3 findings should include screenshots when they materially clarify the issue.

Evidence should make coordinator reproduction faster rather than requiring another full exploratory session.

## 22. Approximate 40-minute priority allocation

This is guidance, not a rigid timer:

- **0–3 min:** open Preview, authenticate, confirm candidate;
- **3–12 min:** Runtime / Screens / Popups / Dynamos;
- **12–18 min:** Alarm / Event / Historian / Trends;
- **18–31 min:** Engineering / Screen Editor / Properties;
- **31–35 min:** Popup Editor;
- **35–38 min:** representative viewports / fullscreen / targeted retests;
- **38–40 min:** consolidate report.

When a critical flow reveals a serious defect, prioritize reproduction/evidence over blindly following the clock allocation.

## 23. Coordinator actions after Work

The Work report is evidence, not automatic authority to patch.

After receiving it, the coordinator must:

1. preserve the original report intact;
2. deduplicate findings without rewriting away original evidence;
3. technically reproduce P0/P1 findings;
4. confirm `GENERIC PRODUCT` / `EEE-SPECIFIC` / `UNCERTAIN` classification;
5. investigate source cause in the code;
6. convert confirmed defects into controlled correction backlog;
7. create an appropriate correction package/stage when needed;
8. implement generic fixes first when the defect is generic;
9. add deterministic regression tests for confirmed defects;
10. run normal exact-SHA CI/specialized gates;
11. prepare a new exact candidate;
12. request a targeted Work recheck only when the cost is justified;
13. only then continue toward Product Owner final homologation.

## 24. Separation of responsibilities

### Coordinator / normal chats

Own:

- GitHub state;
- architecture;
- diagnosis;
- code;
- tests;
- CI;
- documentation;
- package/checksum/provenance;
- Preview preparation;
- environment readiness;
- corrections.

### ChatGPT Work

Owns during the first audit pass:

- real product use;
- browser navigation;
- interaction;
- exploratory visual/functional checking;
- UX reproduction;
- screenshots/evidence;
- structured report.

### Product Owner

Owns:

- final product judgement;
- human homologation;
- acceptance decisions and protected merge authorization where required.

## 25. Mandatory `READY FOR WORK AUDIT` gate

Before informing the Product Owner that Work can be started, explicitly provide and verify:

```text
READY FOR WORK AUDIT

Exact SHA:
<sha>

Package:
<canonical package identifier/path>

Package checksum:
<sha256>

Preview URL:
<url>

Runtime URL:
<url>

Engineering URL:
<url>

CI:
GREEN

EEE:
LOADED

Active Revision:
ACTIVE

Simulation:
RUNNING

Historian:
READY

HistoricalQuery:
READY

Alarm/Event:
READY

Audit users:
READY

WORK-UI-AUDIT-HANDOFF.md:
READY

Known setup blocker:
NONE
```

If any mandatory item is not true, fix or explicitly block the gate before consuming the Work session.

## 26. Deliverables to Product Owner at readiness

When truly ready, the coordinator must provide:

1. explicit `READY FOR WORK AUDIT` confirmation;
2. Preview URL;
3. Runtime and Engineering URLs;
4. secure authentication instruction without versioned secret;
5. exact candidate SHA;
6. package identity + SHA-256;
7. final candidate-specific prompt ready to paste into ChatGPT Work.

## 27. Candidate-specific Work prompt requirement

At readiness, generate the final prompt from the **actual exact candidate** and the short `docs/WORK-UI-AUDIT-HANDOFF.md`.

The prompt must tell Work that:

- it has approximately 40 useful minutes;
- it must use the real EliteSCADA through the browser;
- it should begin with free exploration, then directed verification;
- the first pass is audit-only;
- it must not modify code/commit/merge;
- it should prioritize actual interaction and defect discovery over repository analysis;
- findings must use IDs, severity, reproduction, expected/observed behavior, reproducibility, generic-vs-EEE classification and evidence;
- P0/P1 should include screenshots/evidence whenever possible;
- it must return a consolidated report for coordinator triage.

The final prompt should reference the short candidate-specific handoff, not require Work to reread the full coordination history or this entire directive.

## 28. Hard guardrails

This gate does not alter existing project boundaries:

- never modify `main` directly;
- PR #212 remains OPEN/DRAFT and requires a later separate explicit Product Owner authorization before merge to `main`;
- PR #288 MUST NEVER MERGE;
- PR #266 MUST NEVER MERGE;
- PR #287 remains C26 -> canonical C11 only;
- preserve pre-C26 Preview #285;
- no force push;
- no destructive rebase;
- no branch deletion;
- no unrelated cleanup;
- diagnose any red CI before rerun; never blind-rerun;
- never weaken tests or validation for green;
- never weaken security, authentication, authorization, Identity, Engineering Lock, Licensing, lifecycle, package, Drivers, Historian semantics or backend Active Revision Runtime authority;
- Runtime/Active remains self-contained and cannot depend on `.escadalib`;
- Alarm / Operational Event / Audit remain separate concepts/authorities;
- no EEE-specific workaround for a generic product defect;
- Wave13 #205/#207 remains paused under the existing approved sequence.

## 29. Coordination requirement

All active/future coordinator handoffs must point to this directive and issue #289 until the Work audit gate has been executed, triaged and its consequences resolved.

The coordinator must continue to revalidate GitHub live before every material decision. This directive is stable policy; mutable candidate SHAs, Preview URLs, checks and blockers belong in `LAST CHANGE.md`, the active issue/PRs and the candidate-specific `docs/WORK-UI-AUDIT-HANDOFF.md`.