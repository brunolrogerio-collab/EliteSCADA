# EliteSCADA — Next Coordinator Chat Handoff

**Prepared:** 2026-09-04 BRT  
**Purpose:** authoritative resume point after C11 implementation release.

> GitHub is the official development memory. Revalidate live refs before acting. Documentation-only commits do not redefine product authority.

## 1. High-level state

Repository: `brunolrogerio-collab/EliteSCADA`  
Wave14 issue: #211 ACTIVE  
Integration branch: `wave14/corrections-integration`  
Integration PR: #212 OPEN/DRAFT  
C11: **IMPLEMENTATION RELEASED**  
Canonical DEMO branch: `wave14/c11-canonical-eee-demo`  
Wave13 #205/#207: **PAUSED**

Critical permanent rule: #212 must NEVER merge to `main` without later explicit Product Owner authorization.

## 2. C11 release

Coordinator records:

`RELEASE C11 IMPLEMENTATION`

Binding release record:

`docs/WAVE14-C11-IMPLEMENTATION-RELEASE.md`

Exact C11 implementation product base:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

Tree:

`da6b406ac111cb40b99e5b13031601eb71606ddd`

Implementation branch `wave14/c11-canonical-eee-demo` was created directly from that exact SHA.

## 3. Product Owner sequencing

The Product Owner will perform the real fresh-Codespace visual homologation **after the new canonical EEE DEMO has been created**.

Do not block DEMO construction on that session and do not claim final visual acceptance before it occurs.

## 4. Why C11 is released

Pass-2 pre-DEMO product gaps are closed by generic integrated product work:

- C12 Server Runtime automation/deterministic shared-state simulation foundation;
- C13 canonical simulated quality;
- C14 first-class Operational Events;
- C15 embeddable Multi-Pen Trend;
- C16 canonical Command action + Startup/Home + Popup X/Y;
- C17 Internal Memory authoring/lifecycle;
- C18 embeddable Alarm/Event Browsers + browser/history i18n;
- C19 Operational Event human authoring + generic Server Script emission bridge.

Combined product `3fda880...` passed Elite #1370, Wave11 #298, Preview #320, L3 #276, Interop #197, C03 #113 and automated Test Preview `33935493882`.

## 5. FIRST TASK

Continue implementation of the new canonical deterministic **EEE — Estação Elevatória de Esgoto** DEMO on `wave14/c11-canonical-eee-demo`.

Before writing the canonical package/fixture, audit current project authoring/import/export/lifecycle surfaces and choose a reproducible construction method that uses ordinary generic product contracts rather than hidden package mutation.

The implementation must be a real EliteSCADA project, not a second application architecture.

## 6. Required generic mechanisms

Use normal product mechanisms only:

- Data Sources / Drivers;
- TAGs;
- Server Memory;
- Server Scripts;
- Operational Events;
- Alarms;
- Historian;
- Trend;
- Alarm Browser;
- Event Browser;
- Commands;
- Screens;
- Popups;
- Dynamos;
- normal project assets;
- Startup/Home.

## 7. Required Simulation behavior

- two pumps/motors;
- inflow raises wet-well level;
- running pumps reduce level;
- automatic start/stop thresholds;
- duty/standby alternation;
- second pump under high demand;
- pump fault/trip injection;
- unavailable/bad-quality scenario;
- coherent current/frequency/flow/pressure;
- alarms from real simulated process conditions;
- meaningful first-class Operational Events;
- evolving historian/trend data;
- canonical operator Commands;
- two independent reusable pump Dynamos;
- live Analog Fill;
- contextual Popups;
- explicit Startup/Home and intended operator Runtime.

The Simulation must be deterministic enough for regression and repeated Product Owner homologation.

## 8. Forbidden shortcuts

No EEE-specific service/Driver, hidden private runtime/package, DEMO-only API, direct history insertion, direct Driver/DI access from scripts, frontend-fabricated events, Alarm/Event/Audit conflation, security/licensing/lifecycle bypass or historical DemoRuntime/SimulationDriver reuse as the canonical engine.

If a required behavior cannot be built through normal product mechanisms, classify a new generic PRODUCT GAP and correct the product separately. Do not hide the gap inside the DEMO.

## 9. After DEMO creation

1. repository-side deterministic lifecycle validation;
2. Preview harness updated to consume the new canonical DEMO;
3. Product Owner fresh Codespace / real-browser homologation on completed product + DEMO;
4. correction/revalidation loop for any real defect;
5. final Wave14 acceptance;
6. only then resume Wave13 #205/#207.

## 10. Mandatory resume order

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C11-IMPLEMENTATION-RELEASE.md`;
5. this file;
6. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
7. relevant C12–C19 package docs;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211 and PR #212;
10. revalidate `wave14/c11-canonical-eee-demo` is still based on accepted product authority;
11. continue DEMO construction.
