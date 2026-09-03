# W14-C11 — Pass 2 Session Checkpoint

**Date:** 2026-09-03 BRT  
**State:** PASS 2 CONSOLIDATED / IMPLEMENTATION LOCKED  
**Frozen product-code SHA:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`

This file is the concise cross-chat resume point for C11 Pass 2. The canonical consolidated audit is:

`docs/WAVE14-C11-PASS2-PRODUCT-GAP-AUDIT.md`

on branch:

`wave14/c11-pass2-product-gap-audit`

Detailed static/runtime evidence is retained in:

`docs/WAVE14-C11-PASS2-RUNTIME-AUDIT-EVIDENCE.md`

Product Owner clarification added after the initial Pass 2 consolidation for configurable HMI objects is retained in:

`docs/WAVE14-C11-PASS2-HMI-OBJECTS-PRODUCT-OWNER-CLARIFICATION.md`

That clarification is normative for correction planning and explicitly supplements the canonical DEMO requirements: Trend, Alarm Browser and Event Browser are required as configurable first-class visual objects insertable into authored Screens and Popups. Trend must support multiple Pens. Browser objects must support persisted filters/scope/columns/presentation rather than existing only as global Runtime routes/overlays.

The Coordinator-side authority/continuation handoff remains on:

`wave14/corrections-integration`

C11 implementation is **not authorized**.

## Frozen product and CI

Product-code authority remains:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Revalidated exact-SHA workflows:

- EliteSCADA CI #1273 — SUCCESS;
- Wave 11 Active HMI Runtime #203 — SUCCESS;
- Preview Licensing CI #225 — SUCCESS;
- L3 Seven-Driver Lab #180 — SUCCESS;
- Interop Lab Smoke #102 — SUCCESS.

PR #212 remains DRAFT/unmerged. Documentation-only audit commits do not supersede the frozen product SHA.

## Confirmed supported foundations

Pass 2 confirmed that the current product already has real canonical support for:

- backend-authoritative schema-driven Data Source forms;
- protocol-aware TAG address assistants;
- discoverable Server Memory and Client Memory Source types;
- typed/retentive Server Memory and client-local Client Memory;
- numeric `% Full` and canonical Analog Fill;
- reusable Dynamos with typed public properties/TAG references;
- contextual Popup open/close/context;
- safe authorized Runtime TAG writes;
- secure backend Operational Command execution;
- alarm activation/ACK/return/shelving;
- localized operator Alarm Center;
- historian capture from canonical TAG changes;
- durable alarm history;
- operator tabular historical-data consultation;
- project PNG/background assets;
- capability-driven Operator vs Engineering/Diagnostics separation;
- stable logical TAG identity decoupled from Source/address, supporting later Simulation -> Modbus remapping;
- current Modbus Address Assistant.

Important correction preserved: Internal Memory **exists**. Do not regress to the obsolete Pass 1 conclusion.

## Confirmed product gaps

### Blocking for canonical DEMO Simulation / pre-DEMO product gate

1. `C11-P2-SCR-01` — no active Server Python host/executor/scheduler/timer lifecycle for project-authored Server Scripts.
2. `C11-P2-SIM-01` — no normal engineer-authorable periodic server producer for deterministic EEE process physics.
3. `C11-P2-QUAL-01` — Internal Memory cannot deliberately publish bad/stale/unavailable quality; authored writes force `Good`.
4. `C11-P2-EVT-01` — no first-class general operational Event definition/history surface distinct from alarms/audit.
5. `C11-P2-TREND-01` — historian and a BasicTrend component exist, but no complete canonical operator/authored Trend surface is exposed by the converged Runtime.
6. `C11-P2-CMD-02` — secure backend Command execution exists, but authored visual actions/default Client Visual Python do not expose a canonical Operational Command invocation bridge.
7. `C11-P2-TREND-02` — no configurable first-class Trend visual object insertable into authored Screen/Popup with persisted Engineering configuration.
8. `C11-P2-TREND-03` — audited Trend path is one-Pen-oriented; canonical Trend must support multiple independently configured Pens in the same chart.
9. `C11-P2-BROWSER-01` — global Alarm Center does not replace a configurable Alarm Browser visual object insertable into Screen/Popup with persisted filters/scope/columns/presentation.
10. `C11-P2-BROWSER-02` — no configurable Event Browser visual object insertable into Screen/Popup; this also depends on solving `EVT-01` first-class operational Event infrastructure.

### Other confirmed product gaps

11. `C11-P2-MEM-02` — Internal Memory TAG authoring still exposes meaningless network-style Address semantics.
12. `C11-P2-NAV-01` — Runtime startup Screen is selected by lexical Screen-key ordering rather than a persisted authorable home Screen.
13. `C11-P2-I18N-HIST-01` — mounted Historical Data Browser contains significant hard-coded English visible copy.
14. `C11-P2-POP-02` — Popup open/context is supported, but persisted authorable Popup mount X/Y is absent; requires Development Lead decision on whether shell-defined placement is acceptable.

### Product Owner clarification for Trend / Alarm / Event objects

The Product Owner explicitly requires the following product semantics before the canonical DEMO is allowed to hide the gap in application code:

- Trend, Alarm Browser and Event Browser must be first-class HMI objects usable inside both Screen and Popup;
- their configuration must be canonical/persisted and survive Save -> Publish -> Activate -> Runtime;
- configuration should use normal schema-driven Property Inspector contracts, with a purpose-built collection editor allowed where necessary as long as persisted/runtime contracts remain canonical;
- Trend must be multi-Pen, with independently configurable TAG/reference, label, visibility, appearance, unit, axis and scale per Pen;
- Trend-level configuration must cover practical time range/live-history/refresh/legend/grid/axes/quality behavior;
- Alarm Browser must support practical persisted filters such as state, acknowledgement, priority/severity, Area/Equipment/TAG scope, text, historical time range and configurable columns/order/sort/limits;
- Event Browser must support event type/category/source/scope/user-or-operation where applicable, time range, text and configurable columns/order/sort/limits;
- ordinary operational events must not be fabricated as alarms merely because an Event model/browser is missing;
- a global route, global overlay or standalone Runtime page does **not** satisfy the embeddable-object requirement.

Full normative detail is in `docs/WAVE14-C11-PASS2-HMI-OBJECTS-PRODUCT-OWNER-CLARIFICATION.md`.

## Validation-only items still intentionally unresolved

These are not treated as product failures from static evidence:

- full empty-project Memory Source -> TAG -> Save -> Publish -> Activate -> Runtime browser flow;
- changing Analog Fill presentation in real browser;
- GMB01/GMB02 independent Dynamo-instance binding proof;
- non-color-only final pump fault/unavailable/bad-quality visual treatment;
- Runtime fullscreen/no-scroll/overlay/hit-target acceptance at 1280x720, 1920x1080, 2560x1440 and 3840x2160;
- physical PLC Modbus operation and exact real addresses.

The fixed logical Runtime transform has explicit test coverage for 720p/1080p/1440p/4K and letterboxing, so the remaining viewport question is real-browser acceptance rather than a missing scaling architecture.

The Simulation -> PLC architecture is structurally sound: stable TAG identity, visual `TagReference`, alarm `TagId`, historian TAG identity and Source/address separation permit physical remapping without rebuilding the HMI. Real PLC operation remains `requires PLC validation later`.

## Final C11 Pass 2 recommendation

`KEEP C11 IMPLEMENTATION LOCKED`

This is a recommendation only. C11 has no authority to release implementation.

Required Coordinator/Development Lead route:

1. disposition every confirmed product gap, including the Product Owner clarification for embeddable configurable Trend/Alarm/Event objects;
2. create bounded pre-DEMO correction lanes for approved fixes;
3. do not implement product corrections inside this C11 audit branch;
4. integrate approved fixes into `wave14/corrections-integration`;
5. run exact-head universal and affected specialized CI;
6. freeze a new exact C10 product SHA if product code changes;
7. revalidate affected C11 findings;
8. execute remaining real-browser validation, including authoring and Runtime proof for Trend/Alarm/Event objects inside both Screen and Popup;
9. only after blocking gaps are cleared or explicitly accepted, issue explicit C11 implementation release;
10. then write the full canonical DEMO implementation premise in GitHub and implement DEMO Simulation;
11. later validate the same conceptual application against the physical Modbus PLC;
12. only after Wave 14 acceptance resume Wave 13 release/signing.
