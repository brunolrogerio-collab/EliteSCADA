# W14-C18 — DEV Handoff — Embeddable Alarm + Event Browser HMI Objects

**State:** **PREPARED / NOT RELEASED FOR IMPLEMENTATION**  
**Coordinator branch:** `wave14/corrections-integration`  
**Integration PR:** `#212` — DRAFT / DO NOT MERGE TO `main`  
**Package branch when released:** `wave14/c18-hmi-alarm-event-browsers`  
**Development base:** **TO BE PUBLISHED BY COORDINATOR AFTER C15 IS ACCEPTED + INTEGRATED**  
**C11 implementation:** **LOCKED**

GitHub is the official development memory. This handoff is deliberately prepared before release so the next DEV can start from an exact, bounded contract once the Coordinator publishes the post-C15 base. **Do not create implementation commits for C18 until the Coordinator changes this document/state or explicitly publishes the exact C18 base.**

## 1. Why C18 exists

C11 Pass 2 established that global Runtime routes/overlays do not satisfy the canonical HMI-object requirement for Alarm and Event browsing.

C18 closes:

- `C11-P2-BROWSER-01` — configurable embeddable Alarm Browser;
- `C11-P2-BROWSER-02` — configurable embeddable Event Browser;
- `C11-P2-I18N-HIST-01` — related Historical/Browser visible chrome that is still English-only within the affected surfaces.

The product must allow a normal Engineering user to place these objects inside authored Screens and Popups, configure them through canonical persisted properties, move them through `Save -> Publish -> Activate`, and render/use them in Active HMI Runtime.

## 2. Release prerequisites

C18 implementation remains blocked until both prerequisites below are satisfied:

1. **C14 — First-Class Operational Events** is accepted and integrated. This prerequisite is already satisfied. C18 must consume the accepted C14 Event model/query contract rather than creating a frontend-only event schema.
2. **C15 — Embeddable Multi-Pen Trend HMI Object** must be corrected, accepted and integrated. C18 must reuse the accepted first-class visual-object authoring/persistence/runtime pattern rather than inventing a parallel object system.

At the time this handoff was prepared, C15 candidate `2abee30a25577368999f16d52a0802e6eea473ca` remained under Coordinator `CHANGES REQUIRED` because its EliteSCADA CI Chromium lane was red. Therefore **no exact C18 development base is published yet**.

## 3. Mandatory reading when released

Before changing code, revalidate live GitHub and read:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. `docs/WAVE14-C11-PASS2-HMI-OBJECTS-PRODUCT-OWNER-CLARIFICATION.md` from the C11 audit branch if not yet intaken verbatim;
7. C14 handoff and accepted operational Event contracts;
8. final accepted C15 handoff/contracts;
9. `docs/CI-VALIDATION-POLICY.md`;
10. live issue #211 and draft PR #212.

If any copied SHA conflicts with live GitHub, live GitHub wins.

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
- canonical visual-object schema/Property Inspector/runtime rendering patterns accepted through C05/C07 and final C15;
- C14 first-class Event semantics distinct from Alarm and Audit;
- pt-BR / en / es for affected user-visible chrome.

Do not hard-code DEMO behavior or EEE-specific browsing logic.

## 5. Alarm Browser required product surface

Alarm Browser must be a first-class visual object insertable in:

- Screen;
- Popup.

It must support normal Engineering placement and sizing using the accepted canonical visual composition model and persist practical configuration where backend data exists, including at minimum:

- current vs historical mode/source;
- active / inactive / returned state filtering as supported by the canonical alarm lifecycle;
- acknowledged / unacknowledged filtering;
- severity filtering;
- Area / Equipment / TAG filtering where canonical identities are available;
- text search;
- time range where applicable;
- visible columns;
- sort configuration;
- result limit/page size or equivalent bounded query control.

Interactive alarm operations such as acknowledgement or shelving must use existing backend-authorized product endpoints/contracts. Rendering a button in HMI does not replace backend authorization.

No direct client mutation of alarm state is allowed.

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
- result limit/page size or equivalent bounded query control.

The browser must remain semantically distinct from Audit history. Audit is security/administrative evidence; Operational Event is process/operation history.

## 7. Common first-class HMI-object contract

Alarm Browser and Event Browser must follow the same canonical authoring contract expected by C11:

`Engineering palette/object -> configure canonical properties -> Save -> Publish -> Activate -> render inside Screen or Popup`

Required characteristics:

- first-class canonical visual object identities;
- insertion from normal Engineering UI;
- X/Y/width/height composition through accepted visual contracts;
- persisted canonical configuration;
- schema-driven Property Inspector wherever practical;
- deterministic multiple independent instances;
- stable reusable configuration without private runtime IDs;
- Active Runtime rendering from persisted Active revision;
- loading, empty, no-data and backend-failure states that remain visible and diagnosable;
- no hidden JSON/package editing required for normal use;
- no DOM/CSS injection workaround;
- no DEMO-only React page counted as acceptance.

## 8. Historical/i18n scope

C18 owns the related visible Historical/Browser chrome gap identified by C11.

Any user-visible browser/table/filter/action strings touched by this package must be available in:

- `pt-BR`;
- `en`;
- `es`.

Do not translate persisted technical identifiers, TAG paths, canonical enum wire values, IDs or backend contract keys.

## 9. Backend/query rules

C18 should reuse protected backend query APIs and extend them only when a real generic product capability is missing.

If backend filtering/pagination needed by normal Alarm/Event Browser use is genuinely absent, implement the missing **generic** backend capability in the bounded package and document the contract. Do not fetch unbounded history into the browser and pretend client-side filtering is a product architecture.

Authorization remains backend-side for:

- viewing protected historical/process information;
- alarm ACK/shelve/unshelve or equivalent state-changing actions;
- any future interactive Event operation if one exists.

## 10. Explicit non-scope

C18 does not own:

- C14 operational Event storage/model redesign unless a proven integration defect requires a narrow compatibility correction;
- C15 Trend behavior or Multi-Pen implementation;
- C16 Operational Command, Startup/Home or Popup X/Y;
- EEE Simulation physics;
- DEMO-specific process screens;
- physical Modbus PLC mapping;
- Preview/Codespaces infrastructure;
- Wave13 packaging/signing.

Do not use C18 as an excuse to refactor unrelated historical pages or shell navigation.

## 11. Validation required before Coordinator acceptance

Exact candidate HEAD must pass, at minimum:

- EliteSCADA CI;
- Wave 11 Active HMI Runtime;
- Preview Licensing CI when affected authorization/capability paths require it;
- L3 Seven-Driver Lab;
- Interop Lab Smoke;
- package-specific browser tests proving authored Screen and Popup instances.

Acceptance coverage must prove real product lifecycle, not only source-level assertions:

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
12. restore test project state if the shared Wave11 fixture is modified.

Diagnose failures before rerunning. Do not weaken meaningful tests, authorization, data semantics or visual-object contracts to manufacture green.

## 12. Integration boundary

Package PR, when created, must target:

`wave14/corrections-integration`

It must not merge directly to `main`.

PR #212 remains Coordinator-owned and DRAFT.

The Coordinator will provide the **exact post-C15 base SHA** when releasing C18. The DEV must branch from that exact SHA and report:

- branch name;
- base SHA;
- candidate SHA;
- changed subsystem/files;
- exact workflow run IDs;
- architecture decisions;
- known limitations.

## 13. Release marker

Until a Coordinator update replaces this section, the binding state is:

**C18 PREPARED BUT NOT RELEASED. NO IMPLEMENTATION AUTHORIZED.**
