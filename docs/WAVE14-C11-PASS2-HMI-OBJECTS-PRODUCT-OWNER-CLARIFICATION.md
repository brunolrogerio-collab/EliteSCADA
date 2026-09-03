# W14-C11 — Product Owner Clarification: Configurable Trend / Alarm / Event HMI Objects

**Date:** 2026-09-03 BRT  
**Authority:** Product Owner clarification during C11 Pass 2  
**State:** REQUIREMENT CLARIFICATION / PRODUCT-GAP AUDIT ONLY / IMPLEMENTATION LOCKED  
**Frozen product-code authority:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`

> This document supplements `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`, especially the requirements for Screens, Popups, alarms, events, trends and history. It does not authorize C11 implementation or product-code changes from the C11 audit lane.

## 1. Product Owner clarification

The requirement is not satisfied merely because Trend, Alarm Center or Historical Data Browser exists as a global Runtime route, overlay or standalone component.

For a normal SCADA/HMI project, the Engineering user must be able to place the relevant operator objects **inside authored Screens and Popups**, configure them, persist that configuration through the normal project lifecycle and consume them in Active Runtime.

The minimum product expectation is therefore:

`Engineering palette/object -> configure canonical properties -> Save -> Publish -> Activate -> render inside Screen or Popup`

A DEMO-only React component, hard-coded route, special CSS/DOM insertion, hidden package JSON or private runtime wiring does not satisfy this requirement.

## 2. Common authoring contract

Trend, Alarm Browser and Event Browser must behave as first-class HMI visual objects rather than fixed application pages.

At minimum each object must support:

- insertion into a Screen;
- insertion into a Popup;
- normal X/Y/width/height visual composition semantics;
- persisted configuration in the canonical Engineering/package model;
- configuration through normal Engineering UX, preferably schema-driven Property Inspector contracts;
- Save/Publish/Activate/Runtime projection;
- deterministic behavior when multiple instances exist in the same project;
- configuration that can be copied/reused without private IDs or implementation details;
- Runtime authorization for any interactive action the object exposes;
- multilingual visible chrome according to the canonical `pt-BR`, `en`, `es` requirement.

Complex collection editors may use a purpose-built Engineering editor where a simple scalar Property Inspector is inadequate, but the resulting configuration must still be represented by canonical persisted schemas. A special editor must not become an alternate private storage/runtime path.

## 3. Configurable Trend object

### 3.1 Embeddable object

A Trend must be a visual object that can be placed and sized in both Screen and Popup.

A global Trend page can coexist with this capability, but a global page does **not** replace the embeddable visual-object requirement.

### 3.2 Multi-Pen requirement

The canonical Trend must support **multiple Pens in one chart**. A one-Pen-only viewer is insufficient for normal industrial comparison and for the canonical EEE application.

Typical EEE examples include:

- wet-well level + flow;
- discharge pressure + pump frequency;
- GMB01 current + GMB02 current;
- level + start/stop thresholds where represented as trendable references.

Each Pen must have independently persisted configuration sufficient for practical use, including at least:

- TAG/reference;
- display label/legend text;
- visible/hidden state;
- unit where applicable;
- line appearance such as color and thickness/style;
- axis assignment where multiple axes are supported;
- automatic or manual scale;
- manual minimum/maximum when manual scale is selected.

The Trend object itself must expose configuration sufficient for practical operator use, including at least:

- live, historical or combined operating mode as supported by the product architecture;
- displayed time window/range;
- refresh/update behavior;
- legend visibility;
- grid visibility;
- axes/scale presentation;
- cursor/value inspection where supported;
- quality/no-data presentation;
- historian query behavior through normal protected product APIs.

## 4. Configurable Alarm Browser object

An Alarm Browser must be insertable into Screen and Popup and must not be limited to the global Alarm Center/overlay.

Its canonical persisted configuration must support practical filtering/presentation. At minimum, where the corresponding product data exists:

- current vs historical alarm view/mode;
- active/inactive/returned state filters;
- acknowledged/unacknowledged filters;
- priority/severity filters;
- Area / Equipment / TAG scope;
- text/search filter;
- time range for historical consultation;
- configurable visible columns;
- column ordering and sort;
- row/page/result limit or equivalent bounded-query behavior.

If the object exposes operator actions such as acknowledgement or shelving, those actions must use existing backend capability/authorization contracts. Rendering a button is not authorization, because apparently software still needs reminders about this in 2026.

## 5. Configurable Event Browser object

A general Event Browser must likewise be insertable into Screen and Popup and have persisted configurable filters/presentation.

At minimum, once a first-class Event model exists, configuration should be able to express the relevant subset of:

- event type;
- event category;
- source/origin;
- Area / Equipment / TAG scope;
- user/operator when applicable;
- operation/command category when applicable;
- time range;
- text/search filter;
- configurable visible columns;
- column ordering and sort;
- row/page/result limit or equivalent bounded-query behavior.

The Event Browser must represent normal operational events honestly. Pump start/stop, mode transitions or similar process events must not be fabricated as alarms merely to populate a browser.

`C11-P2-EVT-01` remains the prerequisite product gap for this capability because the frozen product does not currently provide a first-class general operational Event definition/history model distinct from alarms/audit.

## 6. Pass 2 classifications added/refined by this clarification

### C11-P2-TREND-02 — Configurable embeddable Trend visual object

**Classification:** `PRODUCT GAP — FUNCTIONAL/RUNTIME UX`  
**Disposition:** `fix before C11`

Expected behavior: Engineering can insert/configure a Trend in Screen or Popup and persist it through Save/Publish/Activate.

Current frozen behavior: a Trend implementation/component exists, but no canonical first-class Screen/Popup Trend visual-object authoring contract was identified.

`C11-P2-TREND-01` remains the umbrella finding that process trend consumption is not exposed through a complete normal operator/authored product path. `TREND-02` makes the required HMI composition contract explicit.

### C11-P2-TREND-03 — Multi-Pen Trend configuration

**Classification:** `PRODUCT GAP — FUNCTIONAL`  
**Disposition:** `fix before C11`

Expected behavior: one Trend object can contain multiple independently configured Pens.

Current frozen behavior: the audited `BasicTrendViewer` path is one-Pen-oriented and does not satisfy the canonical multi-signal comparison requirement.

### C11-P2-BROWSER-01 — Configurable embeddable Alarm Browser visual object

**Classification:** `PRODUCT GAP — FUNCTIONAL/RUNTIME UX`  
**Disposition:** `fix before C11`

Expected behavior: Engineering can insert an Alarm Browser in Screen or Popup and persist filters, scope, columns and presentation. Existing global Alarm Center functionality is useful but does not satisfy the embeddable-object contract by itself.

### C11-P2-BROWSER-02 — Configurable embeddable Event Browser visual object

**Classification:** `PRODUCT GAP — FUNCTIONAL/RUNTIME UX`  
**Disposition:** `fix before C11`

Expected behavior: Engineering can insert an Event Browser in Screen or Popup and configure type/category/source/scope/time/presentation filters.

Dependency: implementation is downstream of or coordinated with `C11-P2-EVT-01`, because a browser cannot repair the absence of the underlying first-class operational Event model.

## 7. Coordinator / Development Lead implications

These requirements belong to the **product correction gate before C11**, not to DEMO implementation.

Recommended correction planning should preserve the following boundaries:

1. **Operational Event model/history/query** — solve `EVT-01` as product infrastructure.
2. **Canonical Trend visual-object contract** — embeddable Screen/Popup object with persisted configuration.
3. **Multi-Pen Trend model/editor/runtime** — multiple independently configured Pens per object.
4. **Canonical Alarm Browser visual object** — persisted filters/columns/scope and protected Runtime actions.
5. **Canonical Event Browser visual object** — persisted filters/columns/scope using the first-class Event infrastructure.
6. Validate all objects through real Engineering `Save -> Publish -> Activate -> Runtime`, including multiple instances and placement inside both Screen and Popup.

The exact package split is Coordinator/Development Lead authority. C11 must not implement these corrections on `wave14/c11-pass2-product-gap-audit`.

## 8. C11 release consequence

This clarification does not change the current Pass 2 recommendation:

`KEEP C11 IMPLEMENTATION LOCKED`

It strengthens the reason for the lock: Trend and alarm/event consultation must be available as configurable reusable HMI composition objects, not merely as standalone product pages, before the canonical EEE DEMO is allowed to paper over those product gaps.
