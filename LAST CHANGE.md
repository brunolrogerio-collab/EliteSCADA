# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 CONVERGED AND COMBINED GREEN / C11 PASS 2 PRODUCT-GAP AUDIT AUTHORIZED, IMPLEMENTATION LOCKED / WAVE 13 #205/#207 PAUSED**

> Mutable coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only `[skip ci]` commits may advance the integration branch without superseding the frozen product-code SHA.

## 1. Current Wave 14 integration state

`main` remains intentionally untouched by Wave 14 correction packages.

Coordinator integration surface:

- branch: `wave14/corrections-integration`;
- draft PR: #212;
- frozen C10 converged **product-code** SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

A later documentation-only commit may be the branch HEAD. Do not mistake docs-only movement for a new product baseline.

Exact post-merge validation on `97eefd8...`:

- EliteSCADA CI #1273 / run `33795337274`: **SUCCESS**;
- Wave 11 Active HMI Runtime #203 / run `33795337288`: **SUCCESS**;
- Preview Licensing CI #225 / run `33795337280`: **SUCCESS**;
- L3 Seven-Driver Lab #180 / run `33795337252`: **SUCCESS**;
- Interop Lab Smoke #102 / run `33795337270`: **SUCCESS**.

Detailed convergence evidence and the formal C11 Pass 2 release are recorded in:

`docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`

## 2. Accepted package authorities C01-C10

The integrated product now includes:

- C01 — Identity / secure first-run / password policy;
- C02 — backend-authoritative Driver catalog / Data Source forms;
- C03 — unrestricted OpenDNP3 production adapter / commercial dependency removal;
- C04 — TAG Source identity / address assistants / OPC UA discovery-browse;
- C05 — canonical visual properties / schema-driven Property Inspector;
- C06 — Engineering Diagnostics / TAG Monitor boundary;
- C07 — Screen Engineering / Popup / Dynamo authoring and Runtime contracts;
- C08 — Python Script Assistant / Project Object Browser / mediated TAG-write capability;
- C09 — capability-driven application shell + operator Runtime presentation;
- C10 — coordinator convergence, combined regression and multilingual corrections.

Accepted lifecycle remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Historical DEMO behavior is not product authority. Real persisted-data compatibility remains mandatory where it is part of a supported product contract.

## 3. C09 accepted and integrated

Accepted C09 DEV head:

`55c23851cbed0039f8dc82b75ef833a33393f8ed`

Integrated through PR #230. Post-C09 integration SHA:

`ea0d05bd29404c25e97b4d857b20912ed08059f6`

That exact SHA passed all five combined workflows before C10 began.

C09 accepted authority includes:

- backend-effective capability-driven shell/navigation;
- operator-focused Runtime shell;
- semantic Dark/Light shell themes without recoloring authored HMI content;
- `pt-BR`, `en`, `es` shell/operator copy;
- native fullscreen;
- fixed logical HMI viewport using uniform scaling;
- centered letterbox/pillarbox and no responsive HMI reflow;
- Screen/Popup composition under the same logical transform;
- alarm overlay behavior.

Known product gap remains: canonical authorable persisted Popup X/Y placement is not represented by the accepted Popup mount contract. Do not invent ad-hoc CSS persistence merely to satisfy the future DEMO.

## 4. C10 accepted convergence

Coordinator C10 branch:

`wave14/c10-multilingual-convergence`

Actual integration PR: #231.  
Validation-only draft PR: #232, **DO NOT MERGE TO MAIN**.

Final validated C10 branch head:

`be32dd4aac39afd6158a82a66b37d670b18c78ab`

The first C10 exact-head Chromium failure was diagnosed rather than blindly rerun. The product correctly rendered the new pt-BR TAG Monitor eyebrow `Engenharia / Diagnósticos`; the E2E fixture still asserted obsolete English `Engineering / Diagnostics` despite `locale: 'pt-BR'`. Only that obsolete test contract was updated. Product behavior and assertions were not weakened.

Exact-head validation on `be32dd4...` then passed:

- EliteSCADA CI #1272 / `33794196904`: SUCCESS;
- Wave 11 Active HMI Runtime #202 / `33794196890`: SUCCESS;
- Preview Licensing CI #224 / `33794196856`: SUCCESS;
- L3 Seven-Driver Lab #179 / `33794196844`: SUCCESS;
- Interop Lab Smoke #101 / `33794196855`: SUCCESS.

PR #231 then merged with expected-head protection and the integrated result `97eefd8...` passed the complete five-workflow gate again.

## 5. C11 Pass 2 is authorized now

C11 status:

**PASS 2 PRODUCT-GAP AUDIT AUTHORIZED / IMPLEMENTATION LOCKED.**

C11 must audit exact converged product SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

It must re-test every Pass 1 finding against the converged C01-C10 product and deliver one consolidated final gap list. Required classifications:

- `SUPPORTED`;
- `PARTIALLY SUPPORTED`;
- `PRODUCT GAP`;
- `NEEDS VALIDATION`.

Every non-SUPPORTED item must include product evidence, impact on the EEE DEMO, Simulation/PLC relevance, recommended owner/action and whether a bounded product fix should occur before DEMO implementation.

Do not shrink a legitimate DEMO requirement because the product lacks support. Missing capability is a gap finding.

## 6. C11 implementation remains explicitly locked

No authoritative C11 implementation branch, product code, canonical `.escadapkg` or Preview rewiring is authorized yet.

The canonical requirements remain in:

`docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`

The future canonical application remains a realistic **Estação Elevatória de Esgoto (EEE)** with:

- two pumps/motors and stopped/running/fault/bad-quality states;
- animated suction-well liquid level and `% Full`;
- coherent analog process values;
- contextual Popups;
- reusable Dynamos/public properties/TAG bindings;
- alarms/events/trends/history;
- instrumentation/electrical/operation support Screens;
- proper operator versus Engineering/Diagnostics separation;
- project PNG/assets/background support through normal contracts;
- product-supported scripts/animations without direct DOM/Driver/security bypass;
- `pt-BR`, `en`, `es`;
- living Simulation process model;
- later PLC/Modbus variant using the same conceptual HMI/TAG architecture and Product Owner supplied addresses.

After Pass 2, every confirmed gap must be explicitly dispositioned by Coordinator/Development Lead before implementation release.

## 7. Next binding route

`97eefd8... C10 converged freeze`
`-> C11 Pass 2 audit`
`-> consolidated gap list`
`-> Coordinator/Development Lead disposition`
`-> bounded pre-DEMO fixes if required`
`-> C10 rerun and new exact freeze if product changes`
`-> explicit C11 implementation release`
`-> document full C11 premise/architecture in repository`
`-> canonical EEE DEMO Simulation`
`-> PLC/Modbus-compatible mapping and validation`
`-> full exact-head CI`
`-> Preview harness update`
`-> clean Codespace`
`-> real-browser Product Owner homologation`
`-> accepted Wave 14 baseline`

Do not jump directly from C10 convergence to DEMO implementation.

## 8. Preview and Wave 13 boundaries

Issue #208 / PR #210 remains Preview infrastructure/history, not product authority. The operational procedure is in `docs/CODESPACES-PREVIEW-RUNBOOK.md`. The historical launcher reconstructing the old Wave11 DEMO is not the final Wave14 acceptance fixture.

Wave 13 issue #205 / PR #207 remains paused. Do not sign stale product bytes. Future Windows packaging must use the accepted Wave14 product with the proven Wave13 packaging/signing machinery and record both exact SHAs.

## 9. Mandatory resume reading

Read in order:

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. `docs/WAVE14-CORRECTION-PACKAGES.md`;
7. `docs/CI-VALIDATION-POLICY.md`;
8. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview history;
11. issue #205 / PR #207 only for paused Wave13 context.

Then revalidate live GitHub before acting.

## 10. Exact next actions

1. C11 performs Pass 2 only against `97eefd8...`;
2. C11 returns the consolidated gap matrix with evidence and disposition recommendation;
3. Coordinator/Development Lead decides every confirmed gap;
4. if product fixes are approved, create bounded correction work, integrate and rerun C10;
5. if no blocking corrections remain, explicitly release C11 implementation;
6. before or alongside C11 implementation, record the full DEMO premise, functional architecture, Simulation-vs-PLC model, validation goals, known limitations and Product Owner decisions in the repository;
7. build and validate Simulation first without creating a simulation-only HMI architecture;
8. preserve PLC/Modbus reuse by design;
9. update Preview only after the new canonical DEMO exists;
10. finish with full CI plus a clean Codespace and real-browser Product Owner homologation.
