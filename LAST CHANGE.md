# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 BRT  
**Operational state:** **WAVE 14 #211 ACTIVE / C11 IMPLEMENTATION RELEASED / CANONICAL EEE DEMO BUILD ACTIVE / PRODUCT OWNER CODESPACE HOMOLOGATION AFTER DEMO / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only commits do not redefine product-code authority.

## C11 release

Coordinator completed C11 revalidation after C12–C19 convergence and records:

`RELEASE C11 IMPLEMENTATION`

Binding release record:

`docs/WAVE14-C11-IMPLEMENTATION-RELEASE.md`

Exact C11 implementation product base:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

Tree:

`da6b406ac111cb40b99e5b13031601eb71606ddd`

Implementation branch created directly from that exact product SHA:

`wave14/c11-canonical-eee-demo`

The combined pre-DEMO baseline has SUCCESS evidence from EliteSCADA CI #1370, Wave11 #298, Preview Licensing #320, L3 #276, Interop #197, dedicated C03 #113 and automated Test Preview `33935493882`.

## Why C11 can now build the DEMO

The Pass-2 `fix before C11` product gaps are closed through generic product mechanisms:

- C12: Active Server Script runtime + deterministic shared-state automation;
- C13: canonical server-authoritative Bad/Stale/Unavailable quality publication;
- C14: first-class Operational Event runtime/history contract;
- C15: embeddable Multi-Pen Trend for Screen/Popup;
- C16: canonical HMI Command action, explicit Startup/Home and persisted Popup X/Y;
- C17: normal Internal Memory Source/TAG authoring and full lifecycle proof;
- C18: embeddable Alarm Browser + Event Browser and affected historical/browser i18n;
- C19: normal Operational Event Engineering authoring + generic Server Script emission bridge through C14 Active Runtime.

No EEE-specific product code is required to start building the application.

## Product Owner sequencing decision

The Product Owner will perform the real fresh-Codespace visual homologation **after the new canonical EEE DEMO has been created**.

This does not waive that homologation. It moves visual/use acceptance to the point where the actual completed application exists.

Remaining final-browser evidence includes live Analog Fill, final Dynamo operational/fault/bad-quality semantics, two independent Dynamo instances, Runtime scaling/resolution behavior, no-scroll/reflow behavior, contextual Popups and the complete living chain from automation through TAG/quality/alarm/event/history/HMI/command.

## C11 implementation contract

Build the living deterministic EEE Simulation only through ordinary generic EliteSCADA mechanisms:

Drivers/Data Sources, TAGs, Server Memory, Server Scripts, Operational Events, Alarms, Historian, Trend, Alarm Browser, Event Browser, Commands, Screens, Popups, Dynamos, project assets and Startup/Home.

Forbidden: EEE-specific service/Driver/private runtime/hidden DEMO package or route, direct history insertion, Alarm/Event/Audit conflation, auth/licensing/lifecycle bypass, frontend-only fake runtime behavior.

If implementation exposes a missing generic product capability, stop the workaround, classify a new product gap, fix it generically on a separate branch and revalidate exact bytes.

## Immediate route

1. implement the canonical deterministic EEE DEMO on `wave14/c11-canonical-eee-demo` from exact base `3fda880...`;
2. prove repository-side lifecycle and deterministic behavior;
3. update the Preview harness to consume the new canonical DEMO without product bypasses;
4. Product Owner performs fresh Codespace visual homologation on product + canonical DEMO;
5. classify/fix/revalidate any discovered product defect;
6. final Wave14 acceptance;
7. only then resume Wave13 #205/#207 packaging/signing.

## Hard boundaries

- PR #212 remains OPEN/DRAFT and must NEVER merge to `main` without later explicit Product Owner authorization;
- Wave13 #205/#207 remains PAUSED;
- backend Active revision remains canonical authority;
- authorization/backend security and host-owned fail-closed licensing remain unchanged;
- Alarm / Operational Event / Audit remain distinct;
- diagnose every red before rerun.
