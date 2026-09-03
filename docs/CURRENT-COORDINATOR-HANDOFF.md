# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-03 BRT  
**Status:** **WAVE 14 ACTIVE / C01-C08 INTEGRATED GREEN / C09 ACTIVE / C10 PENDING / C11 REQUIREMENTS + PRODUCT-GAP AUDIT ACTIVE WITH IMPLEMENTATION LOCKED / WAVE 13 PAUSED**

> GitHub is the official development memory. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the fastest mutable resume point. Do not resume from conversation history alone.

## 1. Role of the coordinator

The coordinator owns integration, architecture preservation, validation sequencing and acceptance. Package DEVs own their bounded branches; they do not merge directly to `main` or decide the accepted product baseline.

Permanent gates:

- backend is canonical authority;
- authorization is enforced backend-side;
- licensing is host-owned and fail-closed;
- no Preview-only auth/licensing/runtime bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identity remains authoritative;
- accepted lifecycle is `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- product-code changes require universal `EliteSCADA CI` plus impact-specific validation;
- diagnose failures before rerunning; do not weaken security/tests/contracts merely for green.

## 2. Mandatory resume protocol

Before any merge, code change, new branch, C11 release, Preview rebuild or installer decision:

1. read `PROJECT GOAL.md`;
2. read `LAST CHANGE.md`;
3. read this file;
4. read `docs/WAVE14-CORRECTION-PACKAGES.md`;
5. read `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. read `docs/CI-VALIDATION-POLICY.md`;
7. read `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
8. revalidate live `main`, issue #211, PR #212 and exact workflows;
9. revalidate C09 PR #227 and its current branch head;
10. inspect issue #208 / PR #210 only as Preview infrastructure/history;
11. inspect issue #205 / PR #207 only for the paused Wave 13 release boundary.

Copied SHAs are checkpoints. Live GitHub wins.

## 3. Accepted foundation and current integration baseline

Wave 12 remains COMPLETE / ACCEPTED. Accepted historical Wave 12 product baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Wave 14 correction integration is intentionally isolated from `main`:

- branch `wave14/corrections-integration`;
- draft PR #212.

Current trusted integrated Wave 14 **product-code** SHA through C08:

`97b275b9f413c57031e28ac21a08e6190747e7f5`

Exact combined validation:

- EliteSCADA CI #1260 / `33781227083`: SUCCESS;
- Wave 11 Active HMI Runtime #190 / `33781226936`: SUCCESS;
- Preview Licensing CI #212 / `33781227000`: SUCCESS;
- L3 Seven-Driver Lab #167 / `33781227098`: SUCCESS;
- Interop Lab Smoke #89 / `33781227080`: SUCCESS.

Documentation-only `[skip ci]` commits may place the integration branch head after `97b275b9...`; they do not supersede that product baseline.

## 4. Integrated package authorities C01-C08

### C01 — Identity / first-run

- password minimum 8, maximum 1024;
- 7 rejected / 8 accepted;
- empty durable install can create one first Administrator;
- bootstrap closes permanently after first local identity;
- no anonymous self-registration;
- first project is created through canonical Engineering persistence;
- runtime-only users remain valid identities even without Engineering authority.

### C02 — Driver catalog / Data Source forms

- backend/runtime Driver registry is authoritative;
- no normal free-text Driver selector;
- schema/capability-driven Driver-specific configuration;
- stable configured Source identity available for downstream TAG authoring.

### C03 — DNP3 commercial unblock

- restricted Step Function production dependency removed from the integrated product path;
- production adapter uses OpenDNP3 through an isolated native helper;
- independent interop remains based on `dnp3py`;
- Linux/Windows native build, commands, reconnect/fault recovery and distributable dependency closure were proven;
- OpenDNP3 is archived/EOL upstream, creating explicit maintenance debt rather than a commercial-license blocker.

### C04 — TAG Source + address/discovery assistants

C04 is authoritative for:

- stable TAG `DataSourceId` / Source identity;
- rename-safe resolution and explicit orphan behavior;
- Source selector from configured Working Sources;
- protocol-aware binding/address assistants;
- canonical Modbus address builder/runtime bridge;
- DNP3 and IEC-104 assistants;
- OPC UA Test/Discover/Browse/Search/multi-select/bulk TAG creation;
- backend-driven binding schemas/capabilities;
- C04 multilingual copy.

### C05 — Visual properties / Property Inspector

- schema-driven canonical visual Property Inspector;
- friendly typed editors;
- fill/stroke/text/visibility/enabled/shadow/flip/etc. contracts;
- generic Python visual-property read/write/clear/tween parity;
- canonical Runtime precedence remains intact;
- unsafe asset mutation remains fail-closed where project membership cannot be guaranteed.

### C06 — Engineering Diagnostics / TAG Monitor

- TAG Monitor is under Engineering Diagnostics;
- it observes actual Active Runtime data;
- Working vs Active distinction is explicit;
- no silent Runtime TAG write surface was introduced.

### C07 — Screen / Popup / Dynamo maturity

C07 is authoritative for:

- Screen graphical authoring;
- selection/multi-selection/layout/grouping/Z-order/lock/undo-redo and related editor behavior;
- graphical Popup authoring/runtime composition;
- project background/asset behavior;
- exactly eight canonical built-in Dynamos;
- typed/versioned Dynamo public interfaces;
- encapsulation of Dynamo internal children;
- Runtime public TAG binding/state projection and deterministic state precedence;
- command keys remain authoring contracts; commands still use authenticated/authorized backend Runtime paths.

### C08 — Script Assistant / Project Object Browser

- Project Object Browser consumes the actual Working Engineering model;
- TAG identity and Source metadata are canonical;
- Screen/Popup discovery is from real project data;
- visual-property metadata comes from C05 schemas;
- Dynamos expose only public interfaces;
- Client Memory and Script capabilities are discoverable;
- Monaco inserts editable normal Python at cursor with undo boundaries;
- `elite_scada.tag_write(reference, value)` is an official mediated capability using the existing authorized backend write path;
- Engineering Preview process TAG writes remain disabled;
- Pyodide gets no direct Driver/shared-memory/filesystem/process/arbitrary-network authority;
- public product capability vocabulary remains distinct from reserved fail-closed bridge protocol vocabulary.

## 5. Compatibility/precedence rule

The Development Lead explicitly abandoned historical-demo compatibility as an architecture constraint.

When an old fixture/UI expectation conflicts with the new accepted product model:

- C04 wins in TAG Source/communication/address/discovery domains;
- C07 wins in Screen/Popup/Dynamo graphical domains;
- C05 wins for canonical visual-property schemas;
- C06 wins for TAG Monitor placement;
- C08 consumes those contracts;
- C09 must adapt the shell/Runtime to them.

Real persisted-data compatibility still matters where it is part of a supported product contract. Historical Demo behavior does not.

Every failing test must be classified as a real regression or an obsolete expectation. Never blindly delete assertions or preserve old behavior only because a fixture once expected it.

## 6. C09 active state

Branch:

`wave14/c09-application-shell-runtime`

Pinned base:

`b7f4783d7c014c49f8b96874216ed76f0d218ceb`

Validation-only PR: #227.

Live head at this handoff synchronization:

`55c23851cbed0039f8dc82b75ef833a33393f8ed`

The five PR-associated workflows for this head were still in progress when the handoff was written. Recheck them live.

C09 direction:

- effective-capability-driven shell/navigation;
- Runtime-only user gets an operator-focused Runtime and does not see Engineering/admin surfaces, while backend remains security authority;
- first-class semantic Dark/Light shell themes;
- shell theme does not recolor authored HMI content;
- persistent application theme preference;
- multilingual shell/operator UI (`pt-BR`, `en`, `es`);
- fixed logical Runtime composition currently using 1920×1080 as the product canvas contract unless superseded by a canonical accepted Screen-size contract;
- scale formula conceptually `min(viewportWidth/designWidth, viewportHeight/designHeight)`;
- preserve aspect ratio, center, letterbox/pillarbox, never responsive-reflow HMI objects;
- no document scroll;
- Screen/Popup/hit targets use the same logical transform;
- native fullscreen;
- alarm UI as non-reflow operator overlay.

Known unresolved product gap exposed around C09/C07: canonical authorable Popup X/Y placement is not represented by the currently accepted Popup mount contract. Do not invent persisted CSS coordinates in C09 merely to make a DEMO look good.

C09 must not be integrated until final exact-head universal CI is green.

## 7. C10 coordinator gate

C10 is the coordinator's convergence/regression/acceptance phase, not an independent feature DEV.

After C09 final handoff:

1. verify exact branch/head/ancestry/PR body and all CI;
2. review diff against its pinned base;
3. integrate through #212 with expected-head protection;
4. run universal CI and all required specialized workflows on the integration head;
5. inspect browser/Runtime regressions caused by cross-package composition;
6. complete a cross-product multilingual audit for `pt-BR`, `en`, `es`;
7. freeze an exact converged product SHA;
8. release C11 pass 2 against that SHA.

C10 may need to repeat. If C11 pass 2 produces approved product corrections, integrate those corrections and rerun C10 before DEMO implementation is considered safe.

## 8. C11 canonical DEMO / Product Owner lane

C11 was created after the original C01-C10 package plan.

Current status:

**REQUIREMENTS / PRODUCT-GAP AUDIT ONLY. IMPLEMENTATION LOCKED.**

No C11 implementation branch or `.escadapkg` is authoritative yet.

The detailed canonical requirements live in:

`docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`

The Product Owner supplied a real EEE HMI, TAG export and alarms as industrial-process reference. The goal is not to reproduce the old HMI. It is to create a modern canonical EliteSCADA DEMO around a real EEE process.

Core desired behavior:

- two pumps/motors with clear stopped/running/fault/bad-quality states;
- suction well with analog `% Full` and animated liquid level;
- level/flow/pressure/frequency/current and other coherent process values;
- contextual equipment Popups;
- reusable Dynamos/public properties/TAG bindings;
- alarms/events/trends/history exercised naturally;
- support Screens for instrumentation/electrical/operation;
- proper operator vs Engineering/Diagnostics separation;
- PNG/project assets/backgrounds allowed for richer industrial presentation;
- the DEMO must prove normal product authoring and Runtime, not DEV-only custom HTML/CSS hacks.

### Two-pass audit

Pass 1 is already authorized against `97b275b9...` and C11 has found preliminary gaps. They are provisional where C09/C10 may change the result.

Pass 2 is mandatory after C09 integration + C10 convergence/multilingual audit. C11 must re-test all pass-1 findings and deliver a consolidated final gap list to Coordinator/Development Lead.

Gap classifications:

- SUPPORTED;
- PARTIALLY SUPPORTED;
- PRODUCT GAP;
- NEEDS VALIDATION.

Do not reduce a requirement to hide a gap. Every confirmed gap gets an explicit product decision before implementation release.

## 9. DEMO execution model

The future project should support two variants with shared HMI/application structure:

### DEMO Simulation

Use product-supported internal memory/simulation/scripts to make the station alive. Expected examples:

- well level rises/falls;
- pump duty/standby alternation;
- pump start/stop states;
- flow/pressure/current/frequency respond coherently;
- alarm/fault scenarios;
- bad-quality scenarios;
- process transitions visible through Dynamos and animations.

### DEMO PLC

After Simulation validates product behavior, use the Modbus addresses supplied by the Product Owner and connect the same conceptual application to a real PLC.

Avoid designing the HMI around simulation-specific shortcuts that prevent PLC reuse.

## 10. Final project route

Current binding route:

`C09 final green`
`-> coordinator intake into #212`
`-> C10 combined CI/regression`
`-> multilingual audit`
`-> exact converged SHA`
`-> C11 mandatory pass-2 gap audit`
`-> explicit gap disposition`
`-> approved product fixes if needed`
`-> C10 rerun after product changes`
`-> explicit C11 implementation release`
`-> build new EEE canonical DEMO`
`-> Simulation validation`
`-> PLC/Modbus validation when hardware is available`
`-> full exact-head CI`
`-> repository-controlled Preview`
`-> clean Codespace + real browser Product Owner homologation`
`-> accepted Wave 14 baseline`
`-> only then consider Wave 13 release/signing resumption`

## 11. Codespaces / Preview operating model

Issue #208 / PR #210 records the historical temporary Preview harness. It must not become a feature bucket.

The proven operational instructions have been copied into the active integration branch at:

`docs/CODESPACES-PREVIEW-RUNBOOK.md`

Important setup facts from successful real use:

- SDK must match repository `global.json` exactly; historical required SDK is .NET 10.0.400;
- Node 24;
- TimescaleDB/PostgreSQL through `.devcontainer/docker-compose.yml`;
- disposable machine identity generated for the Codespace and mounted read-only at `/etc/machine-id`;
- protected Codespaces secret `ELITESCADA_PREVIEW_ADMIN_PASSWORD`;
- launcher `scripts/preview/launch-test-preview.sh` started by `postAttachCommand`;
- Web Vite port 5173 forwarded and kept Private;
- API 5080 and DB 5432 remain internal;
- no manual startup step should be required in the accepted path;
- 5173 HTTP 502 means port forwarding exists but Web is not listening;
- inspect exact SHA, Compose state, `.preview/api.log`, `.preview/web.log`, localhost health before changing code;
- use recovery levels A reload, B launcher restart, C rebuild container, D fresh Codespace;
- final Wave 14 proof must use a clean Codespace.

The historical launcher reconstructs an old Wave11 DEMO package. That is not the final Wave14 acceptance fixture. After C11 implementation, update the harness to use the new canonical EEE DEMO through normal Import/Save/Publish/Activate, without bypassing first-run/auth/licensing/runtime contracts.

## 12. Multilingual policy

Final audit must cover all changed user-facing surfaces in `pt-BR`, `en`, `es`, including shell, menus, errors, dialogs, assistants, Property Inspector, Screen/Dynamo authoring, Script Assistant and Runtime controls.

Canonical persisted values such as protocol keys, NodeIds, property keys and references are data and are not translated.

## 13. Wave 13 / Windows release boundary

Wave 13 issue #205 / PR #207 remains paused.

Preserved validated historical implementation:

`9f26a2bc02ae77017e266c52ff128dc39eece4b4`

Preserved branch documentation head:

`fda87ba4445127c174f6ea533a6bcabaabc7bb20`

Do not final-sign stale product bytes while Wave14 is changing product content.

When Windows packaging resumes:

- product bytes = latest accepted Wave14 product baseline;
- packaging/signing mechanism = proven Wave13 machinery;
- record exact product SHA and packaging SHA;
- private signing material remains outside repository/normal CI.

## 14. New coordinator immediate checklist

1. Revalidate live #212 and distinguish product-code SHA from later docs-only head.
2. Revalidate PR #227 head and all current C09 workflows.
3. If C09 is green and its handoff is complete, review/integrate it; otherwise leave it with its DEV.
4. After C09 intake, run C10 combined CI and multilingual/cross-package regression.
5. Freeze the exact converged SHA in GitHub.
6. Tell C11 to execute pass 2 against that SHA and return the full gap list.
7. Do not release C11 implementation until the gaps are dispositioned.
8. If product fixes are approved, create bounded correction work, integrate, rerun C10 and re-check affected C11 gaps.
9. When C11 is explicitly released, document its full premise/architecture/Simulation-vs-PLC plan in the repository before or alongside implementation.
10. After the new DEMO exists, update Preview automation, create a clean Codespace and perform real browser owner validation.
11. Record final accepted Wave14 exact SHA before touching final Wave13 signing/release.
