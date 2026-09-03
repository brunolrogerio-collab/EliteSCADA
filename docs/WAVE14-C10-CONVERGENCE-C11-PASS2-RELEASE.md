# Wave 14 — C10 Convergence Freeze and C11 Pass 2 Release

**Date:** 2026-09-03 BRT  
**Coordinator state:** C09 accepted and integrated; C10 convergence accepted; C11 Pass 2 audit authorized; C11 implementation remains locked.

> GitHub is the official development memory. This checkpoint freezes the exact converged product candidate that C11 Pass 2 must audit. It does **not** authorize C11 implementation, a DEMO branch, a canonical `.escadapkg`, Preview rewiring, or product changes from the C11 lane.

## 1. Exact converged product SHA

Coordinator integration branch:

`wave14/corrections-integration`

Draft integration PR:

`#212 — Wave 14: coordinated correction integration`

Frozen C10 converged **product-code** SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

This SHA contains accepted C01-C09 plus the coordinator-owned C10 multilingual convergence corrections.

`main` remains intentionally untouched by Wave 14 correction integration.

## 2. C09 acceptance now included in the freeze

C09 source branch:

`wave14/c09-application-shell-runtime`

Accepted C09 DEV head:

`55c23851cbed0039f8dc82b75ef833a33393f8ed`

C09 was integrated through coordinator intake PR #230. The first post-C09 integration product SHA was:

`ea0d05bd29404c25e97b4d857b20912ed08059f6`

Exact post-C09 validation was green:

- EliteSCADA CI #1270 / run `33790414005` — SUCCESS;
- Wave 11 Active HMI Runtime #200 / run `33790413982` — SUCCESS;
- Preview Licensing CI #222 / run `33790413985` — SUCCESS;
- L3 Seven-Driver Lab #177 / run `33790414074` — SUCCESS;
- Interop Lab Smoke #99 / run `33790413978` — SUCCESS.

Accepted C09 product authorities include capability-driven application navigation, operator-focused Runtime presentation, semantic Light/Dark shell themes, `pt-BR`/`en`/`es` shell/operator copy, native fullscreen, fixed logical HMI viewport with uniform scaling/letterbox rather than responsive HMI reflow, Runtime Screen navigation, Popup presentation and alarm-overlay behavior.

The known canonical authorable Popup X/Y persistence gap was **not** papered over with ad-hoc CSS persistence.

## 3. C10 multilingual convergence

Coordinator-owned C10 branch:

`wave14/c10-multilingual-convergence`

Base:

`ea0d05bd29404c25e97b4d857b20912ed08059f6`

Final validated C10 branch head:

`be32dd4aac39afd6158a82a66b37d670b18c78ab`

Actual integration PR:

`#231 — Wave 14 C10: multilingual convergence corrections`

Validation-only draft PR:

`#232 — W14-C10 validation only: multilingual convergence`

PR #232 exists only to trigger the main-targeted validation workflows and must **not** be merged to `main`.

C10 corrected narrow visible-copy defects without changing persisted identifiers, Driver/TAG contracts, Runtime authorization, scripting sandbox boundaries, Screen/Popup/Dynamo contracts or licensing behavior. Corrections cover:

- Engineering Diagnostics TAG Monitor eyebrow in `pt-BR`/`en`/`es`;
- Script Engineering scope/event/dependency presentation labels;
- Disabled and Preview create/update/skip/error summary labels;
- Script Assistant visible Equipment/Dynamo/TAG, Runtime read/write, Binding, Animation, Current and Enum labels.

Canonical technical identifiers and persisted enum/key values remain data and are not translated.

## 4. C10 E2E diagnosis and correction

The first exact-head validation on C10 head `07b1a79c2d6b12f76b07805a18d6853b70e2e40b` produced four green specialized gates but EliteSCADA CI #1271 failed only in Chromium E2E.

The failure was diagnosed from the published Playwright report, not blindly rerun. `engineering-tag-monitor.spec.ts` used `locale: 'pt-BR'` while still asserting the obsolete English text `Engineering / Diagnostics`. The product correctly rendered `Engenharia / Diagnósticos` after C10 localization.

The test contract alone was corrected in commit:

`be32dd4aac39afd6158a82a66b37d670b18c78ab`

No product behavior was reverted and no assertion was weakened.

Exact-head validation on `be32dd4...` then passed all five gates:

- EliteSCADA CI #1272 / run `33794196904` — SUCCESS;
- Wave 11 Active HMI Runtime #202 / run `33794196890` — SUCCESS;
- Preview Licensing CI #224 / run `33794196856` — SUCCESS;
- L3 Seven-Driver Lab #179 / run `33794196844` — SUCCESS;
- Interop Lab Smoke #101 / run `33794196855` — SUCCESS.

## 5. Post-merge C10 exact-head validation

PR #231 was merged into `wave14/corrections-integration` with expected-head protection, producing:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

The exact integrated product SHA passed the complete combined gate again:

- EliteSCADA CI #1273 / run `33795337274` — SUCCESS;
- Wave 11 Active HMI Runtime #203 / run `33795337288` — SUCCESS;
- Preview Licensing CI #225 / run `33795337280` — SUCCESS;
- L3 Seven-Driver Lab #180 / run `33795337252` — SUCCESS;
- Interop Lab Smoke #102 / run `33795337270` — SUCCESS.

This establishes `97eefd8...` as the current frozen C10 converged product-code SHA for C11 Pass 2.

## 6. C11 Pass 2 is now authorized

C11 must perform **Pass 2 product-gap audit only** against exact product SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

C11 must re-test every Pass 1 finding against the converged C01-C10 product rather than carrying old conclusions forward by assumption.

For every canonical EEE DEMO requirement, classify the result as exactly one of:

- `SUPPORTED`;
- `PARTIALLY SUPPORTED`;
- `PRODUCT GAP`;
- `NEEDS VALIDATION`.

Every non-SUPPORTED item must include:

1. requirement being tested;
2. exact product evidence/code/runtime behavior;
3. current impact on the canonical EEE DEMO;
4. whether the gap blocks Simulation, PLC/Modbus, both, or neither;
5. recommended owner/action;
6. whether a bounded product correction should occur before DEMO implementation;
7. any safe mitigation, clearly distinguished from a real product fix.

C11 must consolidate Pass 1 and Pass 2 into one final gap list. Superseded Pass 1 findings must be marked as resolved/superseded with evidence rather than silently disappearing.

## 7. Requirements that must not be narrowed during Pass 2

The canonical application remains a realistic **Estação Elevatória de Esgoto (EEE)** built through normal EliteSCADA Engineering/Runtime contracts. Pass 2 must test at minimum:

- two pumps/motors with clear stopped/running/fault/bad-quality states;
- suction-well analog level and animated liquid fill with `% Full` presentation;
- coherent level, flow, pressure, current, frequency and related process values;
- contextual equipment Popups;
- reusable Dynamos with public properties and TAG bindings;
- alarms, events, trends and history;
- support Screens for instrumentation, electrical system and operation;
- correct operator vs Engineering/Diagnostics boundaries;
- project PNG/assets/background support without bypassing project asset contracts;
- scripts/animations using canonical product APIs, without direct DOM/Driver/security bypass;
- `pt-BR`, `en` and `es` user-facing behavior;
- a living Simulation process model;
- later PLC/Modbus execution using the same conceptual HMI/TAG architecture and Product Owner supplied addresses.

A missing capability is a product-gap result, not permission to shrink the DEMO requirement.

## 8. C11 implementation remains locked

Pass 2 authorization is **not** implementation authorization.

Until a later explicit Coordinator/Development Lead release, C11 must not:

- create the authoritative C11 implementation branch;
- create or commit the canonical EEE `.escadapkg`;
- implement DEMO-specific product code;
- alter Preview/Codespaces for the future DEMO;
- introduce DEV-only HTML/CSS/JavaScript shortcuts;
- silently work around a confirmed product gap in a way that would make the DEMO non-canonical.

After Pass 2, the Coordinator/Development Lead must disposition every confirmed gap as one of: fix before C11, knowingly mitigate, defer, or narrow only by explicit product decision.

If any pre-DEMO product correction is approved, it must be implemented in a bounded correction lane, integrated into `wave14/corrections-integration`, revalidated, and C10 must freeze a new exact product SHA before affected C11 findings are rechecked.

## 9. Release gate after Pass 2

The next binding route is:

`97eefd8... C10 converged freeze`
`-> C11 Pass 2 audit`
`-> consolidated gap list`
`-> Coordinator/Development Lead disposition`
`-> bounded pre-DEMO corrections if required`
`-> exact-head C10 revalidation if product changes`
`-> explicit C11 implementation release`
`-> repository documentation of full C11 premise/architecture`
`-> canonical EEE DEMO Simulation`
`-> PLC-compatible mapping/validation`
`-> full CI`
`-> Preview harness update`
`-> clean Codespace`
`-> real-browser Product Owner homologation`

No later step is implicitly authorized by this document.
