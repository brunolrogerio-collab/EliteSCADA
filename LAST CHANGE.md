# LAST CHANGE — EliteSCADA

**Date:** 2026-09-03 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C07+C04 INTEGRATED AND COMBINED GREEN / C08 DISPATCHED / C09 ACTIVE WITH CHROMIUM REGRESSION WORK / TEST PREVIEW #208/#210 REMAINS VALIDATION HARNESS / WAVE 13 #205/#207 PAUSED**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. GitHub is the development memory. Live refs, PR state and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance an integration/documentation head without superseding the last validated product-code SHA.

## 1. Current Wave 14 integration checkpoint

Live state revalidated before this synchronization:

- `main`: `edbdf446ea657713bdc487be91bf10bfcd03c684` — unchanged;
- Wave 14 integration PR: #212 — OPEN / DRAFT / mergeable;
- last fully validated Wave 14 integration **product-code** SHA before this documentation commit:

`375519e8d87fe76f74121bf9e84f99b98c5ee23a`

That product head contains the accepted coordinator intake of:

- C01 — Identity / secure first-run / password policy;
- C02 — backend-authoritative Driver catalog / Data Source forms;
- C03 — permissive OpenDNP3 production adapter / Step Function commercial dependency removal;
- C04 — TAG Source identity + protocol-aware address assistants + OPC UA discovery/browse;
- C05 — canonical visual properties + schema-driven Property Inspector;
- C06 — Engineering Diagnostics / TAG Monitor boundary;
- C07 — Screen Engineering / Popups / Dynamo authoring maturity.

The integration remains intentionally separate from `main` until Wave 14 C10 owner acceptance is complete.

Accepted Runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

## 2. C04 final intake

Authoritative package PR: #221.

Preserved lineage:

- required C02 base: `7a0515289bdabba157fb1f645b32647746c83371`;
- validated C04 product SHA: `dc1c9e7beba19737713c7f29b61c661cb8e26a7c`;
- documentation-only C04 handoff head: `044ed0b1ec56a3d75ec1f3fac08e0a332c33c499`;
- coordinator integration merge: `375519e8d87fe76f74121bf9e84f99b98c5ee23a`.

C04 is authoritative for:

- stable TAG `DataSourceId` / Source identity and reference behavior;
- rename-safe resolution and explicit orphan handling;
- legacy Source-key migration where real persisted-data compatibility requires it;
- TAG Source selection;
- Driver-aware communication/address authoring;
- Modbus, DNP3 and IEC-104 assistants;
- OPC UA Test / Discover / Browse / Search / bulk TAG creation;
- backend-driven Driver binding schemas/capabilities;
- C04 `pt-BR`, `en`, `es` user-facing resources.

Do not reintroduce obsolete fixture behavior merely to preserve the historical DEMO.

## 3. Combined exact-head validation after C04

The integrated product head `375519e8d87fe76f74121bf9e84f99b98c5ee23a` passed all five PR-associated gates:

- EliteSCADA CI #1223 / `33768584304`: **SUCCESS**;
- L3 Seven-Driver Lab #130 / `33768584361`: **SUCCESS**;
- Wave 11 Active HMI Runtime #153 / `33768584242`: **SUCCESS**;
- Preview Licensing CI #175 / `33768584261`: **SUCCESS**;
- Interop Lab Smoke #52 / `33768584318`: **SUCCESS**.

This is the current trusted combined product-code base for downstream C08 work. A later `[skip ci]` documentation commit on `wave14/corrections-integration` does not supersede this validated product SHA.

## 4. C08 released

Branch created by the coordinator:

`wave14/c08-python-script-assistant`

Exact development base:

`375519e8d87fe76f74121bf9e84f99b98c5ee23a`

This is the required combined C04+C05 base.

C08 scope remains:

- Script Assistant / project object browser for TAGs, Screens, objects, Popups, Dynamos/public interfaces, Client Memory and available script capabilities;
- friendly project navigation resolving to canonical stable references;
- context-sensitive property metadata from the canonical C05 schemas;
- guided generation of normal editable Python;
- event/action assistance, especially Button `Click` workflows;
- safe TAG-write authoring only through an authenticated/authorized product backend/runtime command path;
- preserve generic visual-property `read/write/clear/tween` APIs;
- preserve Pyodide sandbox restrictions;
- no direct shared-TAG, Driver, DOM/React, OS or network bypass.

If the mediated Client Visual Python TAG-write path is incomplete, implement the safe product path; do not weaken the sandbox.

## 5. C09 active — not accepted yet

Development branch:

`wave14/c09-application-shell-runtime`

Pinned development base:

`b7f4783d7c014c49f8b96874216ed76f0d218ceb`

Current C09 PR: #227 — validation-only draft against `main`.

Latest observed C09 head during this checkpoint:

`2904490066295849e71f8a1394072be957ed199f`

Implemented direction includes:

- backend-effective capability projection;
- capability-driven shell/navigation;
- first-class Dark/Light semantic shell themes;
- operator-focused Runtime with reduced chrome;
- fixed logical Runtime canvas using uniform `min(viewport/design)` scale;
- centered letterbox/pillarbox rather than responsive HMI reflow;
- no document scroll for the HMI composition;
- Screen and Popup composition under the same logical transform;
- fullscreen Runtime while retaining appropriate session/logout controls;
- alarm presentation as overlay rather than permanent HMI reflow region;
- `pt-BR`, `en`, `es` C09 copy.

Validation on `290449...`:

- backend build/test/runtime smoke: SUCCESS;
- Web build: SUCCESS;
- Wave 11 Active HMI Runtime `33766341479`: SUCCESS;
- Preview Licensing `33766341528`: SUCCESS;
- Interop Lab Smoke `33766341608`: SUCCESS;
- L3 Seven-Driver Lab `33766341490`: SUCCESS;
- EliteSCADA CI `33766341541`: **FAILURE in Chromium E2E**.

The coordinator recorded 10 Chromium failures at that head. Several assertions visibly depend on obsolete permanent Runtime regions such as historical operational-overview/alarm chrome and should be updated to the new C09 contract. Failures involving legitimate Report Designer reachability, User Administration reachability or Engineering Preview behavior must be diagnosed individually; do not classify every failure as obsolete by default.

C09 must not be integrated until the final exact head is universally green.

## 6. Binding compatibility direction

The Development Lead explicitly changed the compatibility rule for this Wave:

- C04 prevails in TAG Source / Driver communication / address / discovery-browse domains;
- C07 prevails in Screen Engineering / graphical authoring / Popup / Dynamo domains;
- C09 consumes those contracts and updates old shell assumptions rather than deforming C07;
- C06 remains authority for TAG Monitor under Engineering Diagnostics;
- backend authority/security, lifecycle and real persisted-data compatibility remain mandatory;
- historical fixtures and historical DEMO behavior are not product authority when they contradict accepted Wave 14 contracts.

A failing legacy test must be diagnosed as either:

1. a real product regression; or
2. an obsolete consumer/fixture expectation.

Do not weaken tests merely to obtain green. Update obsolete tests to represent the new contract while preserving meaningful assertions.

## 7. Multilingual interface requirement

Before final Wave 14 acceptance, perform a combined interface audit covering all Wave 14 surfaces for:

- `pt-BR`;
- `en`;
- `es`.

Audit labels, menus, help, tooltips, errors, validation, confirmations, empty states, theme controls, Driver/TAG assistants, Property Inspector, Screen/Dynamo authoring, Script Assistant and Runtime/operator surfaces.

Do not create independent per-component translation catalogs when a shared canonical resource mechanism exists. Persisted protocol/property/reference values remain canonical and are not translated as data.

C04 and C07 already contain scoped multilingual work; C09 contains its own shared shell/operator strings. C10 still owns the final cross-product audit.

## 8. Historical DEMO is no longer the product contract

The previous DEMO application/fixture must not be used to constrain Wave 14 architecture.

Do not add compatibility shims merely to make the historical Demo behave like the pre-Wave-14 product.

Real persisted-data compatibility may still be required where it is part of a current public contract. That is different from preserving an obsolete test fixture.

The historical Demo may remain as historical evidence or a narrowly justified legacy fixture, but it is not the Wave 14 acceptance model.

## 9. New canonical DEMO after package convergence

After C08 and C09 are complete and integrated, create a new Demo application from the **current** product model and normal product workflows.

Required end-to-end construction path:

`fresh install -> first Administrator -> create project -> configure Sources/Drivers -> create TAGs -> Screens/Popups/Dynamos -> Scripts/Bindings -> Save -> Publish -> Activate -> HMI Runtime`

The new Demo should intentionally exercise, where applicable:

- current Driver/Data Source catalog and forms;
- TAG Source selection and protocol address assistants;
- discovery/browse, including OPC UA;
- canonical visual properties;
- multiple Screens;
- Popups;
- all canonical built-in Dynamos or a representative catalog exercise with inventory validation;
- bindings and state behavior;
- Python / Script Assistant;
- alarms/history where appropriate;
- user/capability boundaries including Runtime-only identity;
- Dark and Light shell themes;
- `pt-BR`, `en`, `es`;
- Runtime at representative 1280x720, 1920x1080, 2560x1440 and 3840x2160 viewports without document scroll.

This new Demo becomes the reproducible owner-validation fixture for the clean Codespace run.

## 10. C10 / Codespace final sequence

The binding Wave 14 acceptance order is:

`corrections converged -> multilingual audit -> new canonical DEMO -> full exact-head CI -> clean Codespace -> real browser Product Owner validation`

Preview issue #208 / PR #210 remains a **validation harness**, not the product feature branch.

Do not update the product architecture merely to preserve the old Demo launcher assumptions. Adapt the Preview fixture/automation to the new canonical Demo after convergence.

## 11. DNP3 commercial boundary

C03 is integrated and the current product dependency graph uses the permissive OpenDNP3-based adapter rather than the restricted Step Function production package.

The C03 candidate proved native Linux/Windows helper build, independent dnp3py interop, supported command behavior, reconnect/fault recovery and Windows commercial package/dependency closure. Combined universal CI has subsequently passed on integrated Wave 14 heads.

This clears the old Step Function dependency blocker for the integrated product line. It does **not** mean Wave 14 as a whole is released or accepted; C10 remains outstanding.

OpenDNP3 upstream is archived/EOL, so the integration remains an explicit maintenance responsibility for EliteSCADA.

## 12. Wave 13 remains paused

Wave 13 issue #205 / PR #207 remains paused at its preserved green repository-side checkpoint.

Do not resume final signing against the stale pre-owner-validation product snapshot.

Any requested Windows installer during/after this Wave must use:

`product = latest corrected/accepted Wave 14 product baseline`

`packaging/signing mechanism = proven Wave 13 machinery`

Record exact product SHA and packaging SHA.

## 13. Mandatory coordinator resume reading

A coordinator resuming from here must read, in order:

1. `PROJECT GOAL.md`;
2. this `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-CORRECTION-PACKAGES.md`;
5. `docs/CI-VALIDATION-POLICY.md`;
6. issue #211;
7. draft PR #212 and its latest comments/workflows;
8. active C08 branch and its future PR/handoff;
9. PR #227 / current C09 head and failing/passing Actions;
10. issue #208 / PR #210 only for Preview-harness state;
11. issue #205 / PR #207 only for the paused Wave 13 boundary.

GitHub, not previous chats, is the official development memory.

## 14. Exact next actions

1. dispatch W14-C08 on `wave14/c08-python-script-assistant` from exact base `375519e8d87fe76f74121bf9e84f99b98c5ee23a`;
2. keep C09 DEV on #227 until the Chromium failures are individually diagnosed and the final exact head is universally green;
3. do not integrate C09 while its universal CI remains red;
4. when C08 is final, review scope/ancestry/security boundaries, validate exact head and integrate through #212;
5. when C09 is final, review scope/ancestry/capability/theme/Runtime-scale behavior and integrate through #212;
6. run combined universal and impact-specific workflows after every accepted intake;
7. perform the final multilingual audit on the converged product;
8. create the new canonical Demo from current contracts;
9. update/rebuild the Preview harness for that Demo;
10. run full C10 CI and a clean Codespace/browser owner-validation pass;
11. only then establish the accepted corrected Wave 14 baseline and consider Wave 13 release/signing resumption.
