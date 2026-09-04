# W14-C18 — DEV Handoff — Embeddable Alarm + Event Browser HMI Objects

**State:** **RELEASED / IMPLEMENTATION AUTHORIZED**  
**Coordinator branch:** `wave14/corrections-integration`  
**Integration PR:** `#212` — DRAFT / DO NOT MERGE TO `main`  
**Package branch:** `wave14/c18-hmi-alarm-event-browsers`  
**Exact development base:** `1dcd80a4df448ced3a228d3f5b9057fa26ef547c`  
**C11 implementation:** **LOCKED**

GitHub is the official development memory. Revalidate live refs before changing code. The C18 branch was created by the Coordinator from the exact validated base above before later documentation-only commits advanced the integration branch. Do not rebase C18 onto a newer coordinator documentation SHA merely to make numbers look recent.

## 1. Why C18 exists

C11 Pass 2 established that global Runtime routes/overlays do not satisfy the canonical HMI-object requirement for Alarm and Event browsing.

C18 closes:

- `C11-P2-BROWSER-01` — configurable embeddable Alarm Browser;
- `C11-P2-BROWSER-02` — configurable embeddable Event Browser;
- `C11-P2-I18N-HIST-01` — affected Historical/Browser visible chrome that remains English-only.

The product must allow a normal Engineering user to place these objects inside authored Screens and Popups, configure them through canonical persisted properties, move them through `Save -> Publish -> Activate`, and render/use them in Active HMI Runtime.

## 2. Release prerequisites satisfied

C18 required both:

1. **C14 — First-Class Operational Events** accepted/integrated;
2. **C15 — Embeddable Multi-Pen Trend HMI Object** accepted/integrated so C18 can reuse the canonical first-class visual-object authoring/persistence/runtime pattern.

Both are satisfied.

Accepted corrected C15 package candidate:

`3a182b1963177e1c2c3bb5994fd87fa7cf2512f9`

Final combined post-C15 authority used as C18 base:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

Exact-head combined validation:

- EliteSCADA CI #1337 / `33838725814` — SUCCESS;
- Wave 11 Active HMI Runtime #265 / `33838725796` — SUCCESS;
- Preview Licensing CI #287 / `33838725824` — SUCCESS;
- L3 Seven-Driver Lab #242 / `33838725850` — SUCCESS;
- Interop Lab Smoke #164 / `33838725805` — SUCCESS.

The preceding Wave11 composition failure was diagnosed as a Coordinator test-project selector collision. The corrected selector ensures the generic lifecycle project matches only `active-runtime.spec.ts`, then C17 Memory, C15 Trend and the owner package execute in the intended dependency chain. Do not undo that isolation.

## 3. Mandatory reading

Before changing code, revalidate live GitHub and read:

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

If copied text conflicts with live GitHub, live GitHub wins.

## 4. Architecture authority

C18 must preserve:

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

## 5. Alarm Browser required product surface

Alarm Browser must be a first-class visual object insertable in both Screen and Popup.

It must support normal Engineering placement/sizing and persist practical configuration where canonical backend data exists, including at minimum:

- current versus historical view/source;
- active / inactive / returned state filtering where supported by alarm lifecycle;
- acknowledged / unacknowledged filtering;
- severity filtering;
- Area / Equipment / TAG filtering where canonical identities are available;
- text search;
- time range where applicable;
- visible columns;
- sort configuration;
- bounded result limit/page size or equivalent query control.

Interactive alarm operations such as ACK, shelve or unshelve must use backend-authorized product endpoints/contracts. A visible HMI control never substitutes backend authorization. No direct client mutation of alarm state is allowed.

## 6. Event Browser required product surface

Event Browser must also be a first-class visual object insertable in Screen and Popup.

It must consume the accepted C14 operational Event model and protected query path. It must not reinterpret ordinary operational events as alarms merely to reuse alarm UI.

Persisted configurable filtering/presentation should include, where available in the C14 contract:

- event type/category;
- source;
- Area / Equipment / TAG;
- user/operator;
- operation/command;
- time range;
- text search;
- visible columns;
- sort configuration;
- bounded result limit/page size or equivalent query control.

Operational Event remains semantically distinct from Audit history.

## 7. Common first-class HMI-object contract

Both objects must follow:

`Engineering palette/object -> configure canonical properties -> Save -> Publish -> Activate -> render inside Screen or Popup`

Required characteristics:

- canonical visual object identities;
- insertion from normal Engineering UI;
- X/Y/width/height composition through accepted visual contracts;
- persisted canonical configuration;
- schema-driven Property Inspector wherever practical;
- deterministic multiple independent instances;
- stable reusable configuration without private runtime IDs;
- Active Runtime rendering from persisted Active revision;
- visible loading, empty, no-data and backend-failure states;
- no hidden JSON/package editing for normal use;
- no DOM/CSS injection workaround;
- no DEMO-only React page counted as acceptance.

Reuse the accepted C15 visual-object pattern where it is genuinely common. Do not copy Trend-specific semantics into browsers.

## 8. Historical/i18n ownership

C18 owns the related visible Historical/Browser chrome gap identified by C11.

Any affected user-visible browser/table/filter/action strings must exist in:

- `pt-BR`;
- `en`;
- `es`.

Do not translate persisted technical identifiers, TAG paths, canonical enum wire values, IDs or backend contract keys.

## 9. Backend/query rules

Reuse protected backend query APIs and extend them only when a real generic product capability is missing.

If filtering/pagination needed for normal Alarm/Event Browser use is absent, implement the missing generic backend capability within this bounded package and document it. Do not fetch unbounded history and pretend client-side filtering is a scalable architecture.

Authorization remains backend-side for:

- protected historical/process information;
- alarm ACK/shelve/unshelve or equivalent state changes;
- any future interactive Event operation if one exists.

## 10. Explicit non-scope

C18 does not own:

- redesign of the accepted C14 Event model/storage except for a narrow proven integration defect;
- C15 Trend behavior or Multi-Pen implementation;
- C16 Operational Command, Startup/Home or Popup X/Y;
- EEE Simulation physics;
- DEMO-specific process screens;
- physical Modbus PLC mapping;
- Preview/Codespaces infrastructure;
- Wave13 packaging/signing.

Do not use C18 as an excuse to refactor unrelated shell/history surfaces.

## 11. Required acceptance coverage

Exact candidate HEAD must pass:

- EliteSCADA CI;
- Wave 11 Active HMI Runtime;
- Preview Licensing CI;
- L3 Seven-Driver Lab;
- Interop Lab Smoke;
- package-specific browser tests proving authored Screen and Popup instances.

Acceptance must prove real product lifecycle:

1. insert Alarm Browser through normal Engineering;
2. insert Event Browser through normal Engineering;
3. configure filters/presentation;
4. Save;
5. Publish;
6. Activate;
7. open Active Runtime Screen/Popup;
8. observe canonical alarm/event data;
9. prove at least two independent instances can hold different persisted configurations;
10. exercise authorized alarm interaction and denied interaction where applicable;
11. prove pt-BR/en/es visible chrome;
12. restore shared Wave11 fixture state if modified.

Diagnose failures before rerunning. Do not weaken tests, authorization, event/alarm semantics or visual-object contracts to manufacture green.

## 12. Integration boundary

Package PR must target:

`wave14/corrections-integration`

It must not merge directly to `main`.

PR #212 remains Coordinator-owned and DRAFT.

At delivery, report:

- branch `wave14/c18-hmi-alarm-event-browsers`;
- base `1dcd80a4df448ced3a228d3f5b9057fa26ef547c`;
- candidate SHA;
- changed subsystems/files;
- exact workflow run IDs;
- architecture decisions;
- known limitations.

## 13. Release marker

**C18 RELEASED / IMPLEMENTATION AUTHORIZED**

Exact authorized development base:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

C11 remains locked until all pre-DEMO corrections converge, a new exact product freeze is established, affected C11 findings are revalidated, and the Coordinator explicitly releases C11 implementation.