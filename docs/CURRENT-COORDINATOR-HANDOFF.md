# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION RELEASED / CANONICAL EEE DEMO BUILD ACTIVE / PRODUCT OWNER CODESPACE HOMOLOGATION AFTER DEMO / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs and exact-SHA workflows before acting. Documentation-only commits do not redefine product authority.

## 1. Permanent gates

- backend is canonical authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- lifecycle remains `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- diagnose a red before rerun;
- never weaken tests/security/contracts/identity/lifecycle for green;
- PR #212 stays OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization;
- Wave13 #205/#207 stays paused until final accepted Wave14 bytes.

## 2. C11 release authority

Coordinator revalidated the C11 Pass-2 matrix against the converged C12–C19 product and records:

`RELEASE C11 IMPLEMENTATION`

Binding release record:

`docs/WAVE14-C11-IMPLEMENTATION-RELEASE.md`

Exact implementation product base:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

Product tree:

`da6b406ac111cb40b99e5b13031601eb71606ddd`

Canonical implementation branch:

`wave14/c11-canonical-eee-demo`

The branch was created directly from the exact product base, not from later documentation HEADs.

## 3. Why the product gap gate is closed

C12–C19 now provide the generic product mechanisms previously missing from the Pass-2 audit:

- C12 — Active Server Script host and deterministic Server Memory automation;
- C13 — deliberate canonical Bad/Stale/Unavailable quality from server-owned simulated/internal Sources;
- C14 — first-class Operational Event definition/runtime/history/query model;
- C15 — first-class Screen/Popup Multi-Pen Trend;
- C16 — HMI ExecuteCommand, explicit StartupScreenId and persisted Popup X/Y;
- C17 — human Internal Memory authoring plus full Save/Publish/Activate/Runtime lifecycle;
- C18 — first-class configurable Alarm Browser and Event Browser, including affected pt-BR/en/es chrome;
- C19 — human Operational Event authoring and generic Server Script emission through canonical C14 Active Runtime.

Combined exact product evidence on `3fda880...` is green across EliteSCADA CI #1370, Wave11 #298, Preview Licensing #320, L3 #276, Interop #197, C03 #113 and automated Test Preview `33935493882`.

## 4. Product Owner sequencing decision

The Product Owner explicitly decided that the **real fresh-Codespace visual homologation will be performed after the new canonical EEE DEMO has been created**.

Therefore that visual pass no longer blocks C11 implementation release. It remains mandatory before final Wave14 Product Owner acceptance and must validate the completed application.

Do not claim final browser/Product Owner acceptance before that session occurs.

## 5. Current immediate task

Implement the canonical living deterministic EEE DEMO on:

`wave14/c11-canonical-eee-demo`

Use only ordinary generic product mechanisms available to a normal EliteSCADA project:

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
- project assets;
- Startup/Home.

Shared simulated physical process state must be server-authoritative. Client Memory must not become process truth.

## 6. Canonical Simulation behavior

The first executable target is DEMO Simulation. It must be deterministic and visibly alive:

- wet-well inflow raises level;
- running pump(s) lower level;
- automatic start/stop thresholds;
- duty/standby alternation;
- second pump under high demand;
- pump fault/trip injection;
- unavailable/bad-quality scenario;
- coherent current, frequency, flow and pressure;
- alarms arise from process conditions;
- meaningful Operational Events are emitted through C14/C19;
- historian/trend data evolves;
- operator actions use canonical Commands;
- HMI shows two reusable independently bound pump Dynamos, live Analog Fill and contextual Popups.

## 7. Forbidden shortcuts

Do not use or introduce as canonical behavior:

- EEE-specific simulator service or Driver;
- historical SimulationDriver / DemoRuntimeServices as the new simulation engine;
- DEMO-only API/route/runtime;
- hidden/private package mutation as the normal authoring path;
- Driver/DI/backend internals from project scripts;
- direct historical inserts;
- frontend-fabricated process events;
- Alarm/Event/Audit conflation;
- security/licensing/lifecycle bypass;
- direct DOM/React tricks to fake process behavior.

If implementation reveals a missing generic capability, stop and open a narrow generic product correction package instead of working around it inside the DEMO.

## 8. Deferred final Product Owner homologation

After the canonical DEMO exists, perform the fresh Codespace / real browser Product Owner pass using `docs/CODESPACES-PREVIEW-RUNBOOK.md`.

That pass owns final visual/use evidence including Analog Fill, Dynamo state semantics, two independent instances, canonical scaling/resolutions, no-scroll/reflow behavior, contextual Popups and the integrated living chain.

## 9. PR hygiene

- #212: OPEN/DRAFT, NEVER merge to main without later explicit Product Owner authorization;
- #257: C19 merged only into integration;
- #259/#260/#262: validation-only PRs closed without merge;
- #208/#210: Preview harness/history, not product authority.

## 10. Mandatory resume order

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/WAVE14-C11-IMPLEMENTATION-RELEASE.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C12–C19 package docs as needed;
7. `docs/CI-VALIDATION-POLICY.md`;
8. live issue #211 and PR #212;
9. revalidate exact implementation branch/base;
10. continue canonical DEMO implementation.

Do not ask the Product Owner to repeat decisions already committed to GitHub.
