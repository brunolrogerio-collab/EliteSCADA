# W14-C18 — DEV Handoff — Embeddable Alarm + Event Browser HMI Objects

**State:** **RELEASED / IMPLEMENTATION AUTHORIZED**  
**Coordinator branch:** `wave14/corrections-integration`  
**Integration PR:** `#212` — DRAFT / DO NOT MERGE TO `main`  
**Package branch:** `wave14/c18-hmi-alarm-event-browsers`  
**Exact authorized development base:** `1dcd80a4df448ced3a228d3f5b9057fa26ef547c`  
**C11 implementation:** **LOCKED**

GitHub is the official development memory. Revalidate live refs before changing code.

## 1. Important current-base rule

The C18 branch already exists at the exact authorized green base above.

The Coordinator later composed C16 into `wave14/corrections-integration` at product-code commit `607a60d0e930fc7080e09c0689c306c040c4ace6`, but that combined head is currently **RED in Wave11 Runtime #266 / `33869678407`** while the other four gates are green.

Therefore:

- **do not rebase C18 onto `607a60d0...`;**
- **do not rebase C18 onto later documentation-only coordinator commits merely because they are newer;**
- continue development from `1dcd80a4...`, the last fully combined-green product authority;
- target `wave14/corrections-integration` with the package PR;
- the Coordinator owns later C16/C18 composition.

The current C16 composition failure is outside C18 scope unless C18 independently reproduces a real shared-contract defect. Do not "fix" C16 from C18.

## 2. Why C18 exists

C11 Pass 2 established that global Runtime routes/overlays do not satisfy the canonical HMI-object requirement for Alarm and Event browsing.

C18 closes:

- `C11-P2-BROWSER-01` — configurable embeddable Alarm Browser;
- `C11-P2-BROWSER-02` — configurable embeddable Event Browser;
- `C11-P2-I18N-HIST-01` — related Historical/Browser visible chrome that remains English-only.

Normal Engineering must allow:

`Engineering palette/object -> configure canonical properties -> Save -> Publish -> Activate -> render inside Screen or Popup`

No hidden package editing, DEMO-only React page, DOM/CSS injection, private runtime wiring or historical DEMO path counts as acceptance.

## 3. Release prerequisites already satisfied

C18 depends on:

1. C14 First-Class Operational Events;
2. C15 Embeddable Multi-Pen Trend HMI Object as the accepted first-class visual-object pattern.

Both are accepted/integrated.

Accepted corrected C15 candidate:

`3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`

Exact combined green C18 base:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Validation on that exact base:

- EliteSCADA CI #1337 / `33838725814` — SUCCESS;
- Wave 11 Active HMI Runtime #265 / `33838725796` — SUCCESS;
- Preview Licensing CI #287 / `33838725824` — SUCCESS;
- L3 Seven-Driver Lab #242 / `33838725850` — SUCCESS;
- Interop Lab Smoke #164 / `33838725805` — SUCCESS.

## 4. Mandatory reading

Before changing code, read/revalidate:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. this file;
5. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
6. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
7. C11 consolidated audit and HMI-object clarification on `wave14/c11-pass2-product-gap-audit`;
8. `docs/WAVE14-C14-OPERATIONAL-EVENTS.md`;
9. `docs/WAVE14-C15-HMI-TREND-MULTIPEN.md`;
10. `docs/CI-VALIDATION-POLICY.md`;
11. live issue #211 and draft PR #212.

If copied text conflicts with live GitHub, GitHub wins.

## 5. Architecture authority

Preserve:

- backend canonical authority;
- backend-side authorization;
- host-owned fail-closed licensing;
- no Preview-only bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identities;
- Active revision as Runtime project authority;
- lifecycle `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- accepted canonical visual-object schema / Property Inspector / Runtime rendering pattern from C05/C07/C15;
- C14 Operational Event semantics distinct from Alarm and Audit;
- pt-BR / en / es for affected visible chrome.

Do not hard-code DEMO behavior or EEE-specific browsing logic.

## 6. Alarm Browser required surface

Alarm Browser must be a first-class visual object insertable into both Screen and Popup.

Persist practical canonical configuration, including where supported by existing alarm contracts:

- current versus historical source/view;
- active/inactive/returned filtering;
- acknowledged/unacknowledged filtering;
- severity;
- Area / Equipment / TAG filtering;
- text search;
- time range;
- visible columns;
- sort;
- bounded result limit/page size or equivalent query control.

Interactive alarm operations such as ACK, shelve or unshelve must use backend-authorized product endpoints/contracts. Visible HMI controls never replace backend authorization. No direct client mutation of alarm state.

## 7. Event Browser required surface

Event Browser must also be a first-class Screen/Popup visual object.

It consumes the accepted C14 Operational Event model and protected query path. It must not reinterpret ordinary operational events as alarms merely to reuse alarm UI.

Persisted filtering/presentation should include, where supported by C14:

- event type/category;
- source;
- Area / Equipment / TAG;
- user/operator;
- operation/command;
- time range;
- text search;
- visible columns;
- sort;
- bounded result limit/page size or equivalent query control.

Operational Event remains distinct from Audit history.

## 8. Common first-class visual-object contract

Both objects require:

- canonical visual object identities;
- insertion from normal Engineering UI;
- X/Y/width/height composition through accepted visual contracts;
- persisted canonical configuration;
- schema-driven Property Inspector wherever practical;
- deterministic multiple independent instances;
- stable reusable configuration without private runtime IDs;
- Active Runtime rendering from persisted Active revision;
- loading, empty, no-data and backend-failure states;
- no hidden JSON/package edits for normal use.

Reuse C15 infrastructure only where it is genuinely common. Do not copy Trend-specific semantics into browser objects.

## 9. Historical/i18n ownership

C18 owns related visible Historical/Browser chrome from the C11 gap.

Affected visible strings must exist in:

- `pt-BR`;
- `en`;
- `es`.

Do not translate persisted technical identifiers, TAG paths, canonical enum wire values, IDs or backend keys.

## 10. Backend/query rules

Reuse protected backend query APIs and extend them only when a real generic product capability is missing.

If normal Alarm/Event Browser filtering or pagination is unavailable, implement the missing generic backend capability within this bounded package and document it. Do not fetch unbounded history and hide the problem behind client-side filtering.

Authorization remains backend-side for protected history and alarm state-changing actions.

## 11. Explicit non-scope

C18 does not own:

- redesign of C14 Event model/storage except a narrow proven integration defect;
- C15 Trend behavior/Multi-Pen;
- C16 Operational Command, Startup/Home or Popup X/Y;
- C16 combined Wave11 composition failure;
- EEE Simulation physics;
- DEMO-specific process screens;
- physical Modbus PLC mapping;
- Preview/Codespaces infrastructure;
- Wave13 packaging/signing.

## 12. Required acceptance coverage

Exact candidate HEAD must pass:

- EliteSCADA CI;
- Wave 11 Active HMI Runtime;
- Preview Licensing CI;
- L3 Seven-Driver Lab;
- Interop Lab Smoke;
- package-specific browser tests proving authored Screen and Popup instances.

Acceptance must prove real lifecycle:

1. insert Alarm Browser through normal Engineering;
2. insert Event Browser through normal Engineering;
3. configure filters/presentation;
4. Save;
5. Publish;
6. Activate;
7. render/open them in Active Runtime Screen/Popup;
8. observe canonical alarm/event data;
9. prove at least two independent instances with different persisted configurations;
10. exercise authorized alarm interaction and denied interaction where applicable;
11. prove pt-BR/en/es visible chrome;
12. restore shared Wave11 fixture state if modified.

Diagnose failures before rerunning. Do not weaken tests, authorization, event/alarm semantics or visual-object contracts to manufacture green.

## 13. Delivery boundary

Package PR must target:

`wave14/corrections-integration`

Never `main`.

PR #212 remains Coordinator-owned and DRAFT.

At delivery report:

- branch `wave14/c18-hmi-alarm-event-browsers`;
- base `1dcd80a4df448ced3a228d3f5b9057fa26ef547c`;
- candidate SHA;
- changed subsystems/files;
- exact workflow run IDs;
- architecture decisions;
- known limitations.

## 14. Release marker

**C18 RELEASED / IMPLEMENTATION AUTHORIZED**

Exact authorized base:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

C11 remains locked until all pre-DEMO corrections converge, a new exact product freeze is established, affected C11 findings are revalidated, and the Coordinator explicitly releases C11 implementation.
