# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-03 BRT  
**Status:** **WAVE 14 ACTIVE / C01-C10 CONVERGED GREEN / C11 PASS 2 AUTHORIZED WITH IMPLEMENTATION LOCKED / WAVE 13 PAUSED**

> GitHub is the official development memory. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the fastest mutable resume point. Do not resume from conversation history alone.

## 1. Coordinator role and permanent gates

The coordinator owns integration, architecture preservation, validation sequencing and acceptance. Package DEVs do not merge directly to `main` and do not decide the accepted product baseline.

Permanent gates:

- backend is canonical authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- no Preview-only auth/licensing/runtime bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identity remains authoritative;
- accepted lifecycle is `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- product-code changes require universal EliteSCADA CI plus impact-specific validation;
- diagnose failures before rerunning;
- do not weaken tests/security/contracts merely to obtain green.

## 2. Current exact product freeze

Wave 14 remains isolated from `main` on:

- branch `wave14/corrections-integration`;
- draft PR #212.

Frozen C10 converged **product-code** SHA:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Exact post-merge evidence:

- EliteSCADA CI #1273 / run `33795337274` — SUCCESS;
- Wave 11 Active HMI Runtime #203 / run `33795337288` — SUCCESS;
- Preview Licensing CI #225 / run `33795337280` — SUCCESS;
- L3 Seven-Driver Lab #180 / run `33795337252` — SUCCESS;
- Interop Lab Smoke #102 / run `33795337270` — SUCCESS.

Documentation-only `[skip ci]` commits may advance the branch after this SHA. They do not supersede `97eefd8...` as the current product freeze.

Full C10 freeze and C11 Pass 2 release evidence:

`docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`

## 3. Accepted package authorities

C01-C10 are now included in the converged candidate.

- C01 — Identity / secure first-run / password policy.
- C02 — backend-authoritative Driver catalog / Data Source forms.
- C03 — OpenDNP3 production adapter / Step Function commercial dependency removal.
- C04 — TAG Source identity, address assistants and OPC UA discovery/browse.
- C05 — canonical visual properties / schema-driven Property Inspector.
- C06 — Engineering Diagnostics / TAG Monitor product boundary.
- C07 — Screen Engineering / Popup / Dynamo authoring and Runtime contracts.
- C08 — Python Script Assistant / Project Object Browser / mediated TAG-write capability.
- C09 — capability-driven application shell and operator Runtime presentation.
- C10 — combined regression, multilingual convergence and exact-SHA freeze.

Cross-package precedence remains: C04 owns TAG Source/communication/address/discovery; C05 owns canonical visual-property schemas; C06 owns TAG Monitor placement; C07 owns Screen/Popup/Dynamo graphical contracts; C08 consumes those contracts; C09 shell/Runtime consumes the resulting product model.

The historical DEMO is not a compatibility authority.

## 4. C09 accepted state

Accepted C09 DEV head:

`55c23851cbed0039f8dc82b75ef833a33393f8ed`

Coordinator intake PR #230 merged C09 into integration. Post-C09 product SHA:

`ea0d05bd29404c25e97b4d857b20912ed08059f6`

It passed the five combined workflows before C10.

Accepted C09 direction:

- effective-capability-driven shell/navigation;
- operator-focused Runtime for Runtime-only users;
- semantic Dark/Light shell themes;
- authored HMI colors are not recolored by shell theme;
- `pt-BR`, `en`, `es` shell/operator UI;
- native fullscreen;
- fixed logical HMI composition with uniform scaling and letterbox/pillarbox;
- no responsive object reflow or document-scroll-based HMI composition;
- Screen/Popup/hit targets share the logical transform;
- alarm UI is an operator overlay that does not reflow the authored HMI.

Known unresolved product gap remains: canonical persisted authorable Popup X/Y placement is not represented in the accepted Popup mount contract. Do not invent ad-hoc persisted CSS coordinates merely for DEMO cosmetics.

## 5. C10 convergence history

C10 branch:

`wave14/c10-multilingual-convergence`

Base:

`ea0d05bd29404c25e97b4d857b20912ed08059f6`

Actual integration PR: #231.  
Validation-only draft PR: #232, **DO NOT MERGE TO MAIN**.

Final validated C10 branch head:

`be32dd4aac39afd6158a82a66b37d670b18c78ab`

C10 corrected narrow user-facing multilingual defects in TAG Monitor, Script Engineering and Script Assistant while preserving canonical persisted identifiers and existing product/security contracts.

The first C10 universal CI failure was isolated to an obsolete E2E expectation: with `locale: 'pt-BR'`, the test still required English `Engineering / Diagnostics` after the UI was correctly localized to `Engenharia / Diagnósticos`. The fixture alone was corrected, the product was not rolled back, and no assertion was weakened.

`be32dd4...` passed all five gates, PR #231 merged with expected-head protection, and integrated SHA `97eefd8...` passed all five again.

## 6. C11 current status

**PASS 2 PRODUCT-GAP AUDIT AUTHORIZED. IMPLEMENTATION REMAINS LOCKED.**

Canonical requirements:

`docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`

Pass 2 exact target:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

C11 must re-test every Pass 1 finding against the converged C01-C10 product and return one consolidated gap list using exactly:

- `SUPPORTED`;
- `PARTIALLY SUPPORTED`;
- `PRODUCT GAP`;
- `NEEDS VALIDATION`.

Every non-SUPPORTED finding needs product evidence, impact, Simulation/PLC applicability, recommended owner/action, pre-DEMO-fix recommendation and any mitigation clearly separated from a real fix.

Do not narrow requirements to hide gaps. Resolved Pass 1 findings must be explicitly marked superseded/resolved with Pass 2 evidence.

## 7. Canonical EEE requirements that remain binding

The future canonical application remains a modern realistic **Estação Elevatória de Esgoto** based on real process reference rather than historical-demo compatibility.

Required experience includes:

- two pumps/motors with stopped/running/fault/bad-quality states;
- suction well level with analog `% Full` and animated liquid fill;
- coherent process level/flow/pressure/current/frequency values;
- contextual equipment Popups;
- reusable Dynamos/public properties/TAG bindings;
- alarms/events/trends/history;
- instrumentation/electrical/operation support Screens;
- correct operator versus Engineering/Diagnostics separation;
- project PNG/assets/background support through normal project contracts;
- scripts/animations through canonical APIs without direct DOM/Driver/security bypass;
- `pt-BR`, `en`, `es` user-facing surfaces;
- living Simulation process behavior;
- later real PLC/Modbus validation with the same conceptual HMI/TAG architecture and Product Owner supplied addresses.

## 8. C11 implementation lock

Pass 2 authorization is not DEMO implementation authorization.

Until explicit later Coordinator/Development Lead release, C11 must not:

- create the authoritative implementation branch;
- implement product or DEMO code;
- create the canonical `.escadapkg`;
- alter Preview/Codespaces for the future DEMO;
- introduce one-off HTML/CSS/JavaScript bypasses;
- silently workaround a product gap so the DEMO stops being canonical.

After Pass 2, every confirmed gap must be dispositioned. Approved pre-DEMO fixes go to bounded correction work, are integrated through the coordinator, and force a new exact C10 validation/freeze before affected C11 conclusions are accepted.

When C11 implementation is eventually explicitly released, document the full premise, functional architecture, approved requirements, Simulation vs PLC variants, process model, validation goals, known gaps/limitations and Product Owner decisions in the repository before or alongside implementation.

## 9. Binding route from here

`97eefd8... C10 converged freeze`
`-> C11 Pass 2`
`-> consolidated final gap matrix`
`-> explicit gap disposition`
`-> bounded corrections if required`
`-> C10 rerun/new freeze if product changes`
`-> explicit C11 implementation release`
`-> repository premise/architecture record`
`-> canonical EEE DEMO Simulation`
`-> PLC/Modbus-compatible mapping and real-PLC validation`
`-> full CI`
`-> Preview harness update`
`-> clean Codespace`
`-> real-browser Product Owner homologation`
`-> accepted Wave14 baseline`

No later step is automatically authorized.

## 10. Preview and release boundaries

Issue #208 / PR #210 remains historical Preview infrastructure, not product authority. Use `docs/CODESPACES-PREVIEW-RUNBOOK.md` for the proven temporary browser environment. The historical Wave11 DEMO launcher is not the final Wave14 acceptance fixture.

Wave13 issue #205 / PR #207 remains paused. Do not sign stale pre-Wave14 product bytes. When release work resumes, product bytes must come from the accepted Wave14 baseline and packaging/signing from the proven Wave13 machinery, with exact product and packaging SHAs recorded.

## 11. Mandatory resume protocol

Read and revalidate in this order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/WAVE14-C10-CONVERGENCE-C11-PASS2-RELEASE.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. `docs/WAVE14-CORRECTION-PACKAGES.md`;
7. `docs/CI-VALIDATION-POLICY.md`;
8. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview infrastructure history;
11. issue #205 / PR #207 only for paused Wave13 context.

Live GitHub wins over copied SHAs or conversation history.

## 12. Immediate next actions

1. C11 executes Pass 2 against `97eefd8...` only.
2. C11 returns the consolidated gap matrix with evidence and recommended disposition.
3. Coordinator/Development Lead decides every confirmed gap.
4. If product fixes are approved, dispatch bounded correction lanes, integrate and rerun C10.
5. If the product is acceptable for the canonical DEMO, explicitly release C11 implementation.
6. Record the complete C11 premise/architecture before or alongside implementation.
7. Build Simulation first while preserving PLC/Modbus reuse by design.
8. Update Preview only after the new canonical DEMO exists.
9. Finish with full exact-head CI plus clean Codespace and real-browser Product Owner acceptance.
