# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C08 INTEGRATED AND COMBINED GREEN / C09 ACTIVE AND NOT YET ACCEPTED / C10 COORDINATOR CONVERGENCE PENDING / C11 REQUIREMENTS + TWO-PASS PRODUCT-GAP AUDIT ACTIVE, IMPLEMENTATION LOCKED / WAVE 13 #205/#207 PAUSED**

> Mutable coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only `[skip ci]` commits may advance this branch without superseding the last validated product-code SHA.

## 1. Current trusted Wave 14 product baseline

`main` remains intentionally untouched by Wave 14 correction packages until owner acceptance.

Coordinator integration surface:

- branch: `wave14/corrections-integration`;
- draft PR: #212;
- current trusted combined **product-code** SHA containing C01 through C08:

`97b275b9f413c57031e28ac21a08e6190747e7f5`

C08 was integrated through coordinator intake PR #229. Exact combined validation on `97b275b9...`:

- EliteSCADA CI #1260 / `33781227083`: **SUCCESS**;
- Wave 11 Active HMI Runtime #190 / `33781226936`: **SUCCESS**;
- Preview Licensing CI #212 / `33781227000`: **SUCCESS**;
- L3 Seven-Driver Lab #167 / `33781227098`: **SUCCESS**;
- Interop Lab Smoke #89 / `33781227080`: **SUCCESS**.

The integrated package authorities are:

- C01 — Identity / secure first-run / password policy;
- C02 — backend-authoritative Driver catalog / Data Source forms;
- C03 — OpenDNP3 production adapter / Step Function commercial dependency removal;
- C04 — TAG Source identity + address assistants + OPC UA discovery/browse;
- C05 — canonical visual properties + schema-driven Property Inspector;
- C06 — Engineering Diagnostics / TAG Monitor boundary;
- C07 — Screen Engineering / Popups / Dynamo authoring/runtime contracts;
- C08 — Python Script Assistant / Project Object Browser / mediated TAG-write capability.

Accepted Runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

## 2. Cross-package precedence and compatibility rule

When old UI/fixtures conflict with accepted Wave 14 contracts:

- C04 prevails in TAG Source / Driver communication / address / discovery-browse domains;
- C07 prevails in Screen Engineering / Popup / Dynamo / graphical authoring domains;
- C05 remains authority for canonical visual-property schemas;
- C06 remains authority for TAG Monitor under Engineering Diagnostics;
- C08 consumes those contracts and may not bypass backend security or Dynamo encapsulation;
- C09 must consume the new model instead of deforming it to preserve the old shell.

Backend authority, authorization, licensing, lifecycle and genuine persisted-data compatibility remain mandatory.

The historical DEMO is **not** product authority. A legacy fixture may be updated when its expectation is obsolete. Do not weaken meaningful tests merely to obtain green.

## 3. C09 — active, not accepted

Branch:

`wave14/c09-application-shell-runtime`

Pinned development base:

`b7f4783d7c014c49f8b96874216ed76f0d218ceb`

Validation-only draft PR: #227.

Live head at this synchronization:

`55c23851cbed0039f8dc82b75ef833a33393f8ed`

C09 owns:

- backend-effective capability-driven application navigation;
- operator-focused Runtime shell;
- Dark/Light semantic shell themes without recoloring authored HMI content;
- `pt-BR`, `en`, `es` shell/operator copy;
- Runtime fullscreen;
- fixed logical HMI canvas using uniform `min(viewport/design)` scaling;
- centered letterbox/pillarbox;
- no document scroll for HMI composition;
- Screen and Popup composition under one logical transform;
- alarm presentation without responsive HMI reflow.

The current head was still running its five PR-associated workflows when this documentation was synchronized. Do not integrate C09 until its final exact head is universally green. Prior Chromium failures were being diagnosed and corrected rather than hidden by weakening tests.

Known product-contract point: the accepted Popup runtime mount currently lacks a canonical persisted X/Y placement contract. C09 must not invent ad-hoc persisted `left/top` semantics. Treat authorable Popup placement as a product gap unless a canonical contract is added.

## 4. C10 — coordinator-owned iterative convergence gate

C10 is not a feature branch. It is the coordinator acceptance/convergence lane.

After C09 becomes a final green candidate:

1. review C09 ancestry/scope/CI;
2. integrate C09 into #212 with C07/C06/C04/C05 authorities preserved;
3. run universal and impact-specific workflows on the new integration head;
4. audit cross-package regressions, especially shell, Runtime, Screen/Popup/Dynamo, Script Assistant and capabilities;
5. perform the combined multilingual audit for `pt-BR`, `en`, `es`;
6. establish an exact converged product SHA for C11 pass 2.

C10 is iterative. If C11 pass 2 identifies gaps that are approved for correction before DEMO implementation, integrate those fixes and rerun C10 on the new exact SHA.

## 5. W14-C11 — canonical EEE DEMO lane

C11 exists as a Product Owner requirements/product-gap lane. **Implementation remains locked.**

No authoritative C11 implementation branch, product code, fixture or `.escadapkg` should exist until explicit coordinator release.

Canonical requirements are now to be recorded in:

`docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`

The historical DEMO must not be resurrected as the compatibility target. The new DEMO is built from the converged product and normal Engineering workflows.

Primary process: **Estação Elevatória de Esgoto (EEE)**, based on a real industrial application as process reference, not as a screen to copy.

Required experience includes two pumps/motors with clear stopped/running/fault/bad-quality states, animated suction-well level in `% Full`, realistic process values, contextual Popups, reusable Dynamos/public properties/TAG bindings, alarms/events/trends/history, support screens, correct operator-vs-Engineering separation and safe use of project PNG/assets/backgrounds.

C11 must not reduce a legitimate DEMO requirement merely because the product lacks support. Missing capability is a product-gap finding.

### Two-pass gap audit

**Pass 1:** already authorized against integrated C01-C08 baseline `97b275b9...`. C11 has already found preliminary gaps. Those findings remain provisional where C09/C10 may affect them.

**Pass 2:** mandatory after C09 is integrated and C10 convergence/multilingual validation establishes a new exact SHA. C11 must re-audit every previous finding, add newly exposed gaps, and return a consolidated list to Coordinator/Development Lead before implementation is released.

Every gap must be explicitly dispositioned: fix before C11, mitigate knowingly, defer to later Wave, or narrow scope only by Development Lead decision.

## 6. DEMO variants

The canonical DEMO should be designed once and support two execution variants with maximum shared visual/project logic:

1. **DEMO Simulation** — internal memory/simulation/scripts give the station life: level changes, pump starts/stops, alternation, failures, alarms and coherent analog values.
2. **DEMO PLC** — later validation against a real PLC using the Product Owner supplied Modbus addresses.

Do not create a simulation-only visual architecture that must be rebuilt for PLC validation. TAG contracts and authored HMI should be reusable wherever technically possible.

## 7. Final project route

The binding route from the current checkpoint is:

`C09 final green -> C09 intake into #212 -> C10 combined regression + multilingual audit -> exact converged SHA -> C11 pass-2 gap audit -> gap disposition/corrections -> C10 rerun if product changes -> explicit C11 implementation release -> build new canonical EEE DEMO -> validate Simulation -> validate PLC variant when hardware is available -> exact-head full CI -> repository-controlled Preview/Codespace -> clean real-browser Product Owner homologation -> accepted Wave 14 baseline`

Do not reverse this by building the new DEMO on an unaccepted Runtime shell.

## 8. Codespaces / Preview continuity

Issue #208 / PR #210 remains the historical Preview harness. It is infrastructure, not product scope.

A complete operational copy of the proven Codespaces procedure is maintained on the integration branch as:

`docs/CODESPACES-PREVIEW-RUNBOOK.md`

Critical facts:

- exact SDK contract from repository (`global.json`; historically .NET 10.0.400);
- Node 24;
- TimescaleDB/PostgreSQL through devcontainer Compose;
- disposable per-Codespace machine identity mounted at `/etc/machine-id`, preserving normal fail-closed licensing;
- protected Codespaces secret `ELITESCADA_PREVIEW_ADMIN_PASSWORD`;
- automatic launcher `bash scripts/preview/launch-test-preview.sh` through `postAttachCommand`;
- Web forwarded on 5173 and kept Private;
- API 5080 and DB 5432 remain internal;
- HTTP 502 on 5173 means forwarding exists but the Web process is not listening;
- startup failures must be diagnosed from exact SHA, `.preview/api.log`, `.preview/web.log`, Compose state and local health probes;
- use recovery levels A reload, B launcher restart, C rebuild container, D fresh Codespace;
- final acceptance must use a **clean Codespace** and real browser, not CI alone.

The old launcher logic that reconstructs the historical Wave 11 DEMO is not the final Wave 14 acceptance path. After C11 builds the new canonical DEMO, Preview automation must be updated to use that DEMO through normal Import/Save/Publish/Activate flow.

## 9. Multilingual requirement

Before final acceptance audit all Wave 14 user-facing surfaces in:

- `pt-BR`;
- `en`;
- `es`.

Do not translate canonical persisted protocol/property/reference values. Do not create scattered local translation catalogs when a shared resource mechanism exists.

## 10. DNP3 commercial boundary

C03 is integrated. The product line no longer depends on the restricted Step Function adapter for the accepted integrated Wave 14 path. The replacement uses OpenDNP3 with independent `dnp3py` interop evidence. OpenDNP3 upstream is archived/EOL, so EliteSCADA assumes an explicit maintenance responsibility.

This removes the old Step Function commercial dependency blocker; it does not itself constitute Wave 14 release acceptance.

## 11. Wave 13 remains paused

Issue #205 / PR #207 remains paused at its preserved green repository-side checkpoint.

Do not resume final signing using stale pre-Wave-14 product bytes.

Any future Windows installer must use:

`product = latest corrected/accepted Wave 14 baseline`

`packaging/signing mechanism = proven Wave 13 machinery`

Record exact product SHA and packaging SHA.

## 12. Mandatory new-coordinator reading

Read in order:

1. `PROJECT GOAL.md`;
2. this `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
7. `docs/CI-VALIDATION-POLICY.md`;
8. live issue #211 and PR #212;
9. live PR #227 / C09 branch and exact workflows;
10. issue #208 / PR #210 for Preview infrastructure history;
11. issue #205 / PR #207 only for paused Wave 13 context.

Then revalidate live GitHub before changing anything.

## 13. Exact next actions

1. monitor C09 head `55c23851...` and its workflows; if the branch advances, use the new live head, not this snapshot;
2. do not integrate C09 until its final exact head is green;
3. integrate the validated C09 candidate through coordinator-owned #212;
4. run C10 combined universal/specialized CI and cross-package regression;
5. finish multilingual audit;
6. freeze and record the exact converged SHA;
7. instruct C11 to perform mandatory pass 2 against that SHA and return its consolidated product-gap list;
8. decide each gap explicitly and apply any approved pre-DEMO product fixes;
9. rerun C10 after any product correction;
10. release C11 implementation only after gap disposition is acceptable;
11. build the new canonical EEE DEMO, Simulation first and PLC/Modbus-compatible by design;
12. update the Preview launcher/harness to the new DEMO;
13. execute full CI plus clean Codespace/browser Product Owner acceptance;
14. only then establish the accepted Wave 14 baseline and consider Wave 13 signing/release resumption.
