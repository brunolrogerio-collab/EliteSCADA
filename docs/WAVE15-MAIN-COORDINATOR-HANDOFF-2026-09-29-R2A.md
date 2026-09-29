# EliteSCADA Wave 15 — Main Coordinator Handoff — 2026-09-29 — R2-A parallel execution

> **CANONICAL TAKEOVER DELTA FOR THE NEXT MAIN COORDINATOR**
>
> GitHub live is the sole authority. Revalidate every branch, PR, issue comment and CI run before acting. If this handoff differs from GitHub live, GitHub live wins.

## 1. Permanent Main rules to preserve

- Repository: `brunolrogerio-collab/EliteSCADA`.
- Product integration branch: `wave15/corrections-integration`.
- Main coordination branch: `coord/w15-correction-now-parallel-control`.
- Shared CODEX control: `coord/w15-fnd04-dev-aud-control:docs/WAVE15-FND04-DEV-AUD-CONTROL.md`.
- Product Owner is **not** a messenger between agents. Main writes durable orders/comments/controls directly.
- Do not merge because `mergeable=true`; require exact-head evidence and scope/ownership audit.
- Missing frozen shared semantic => `BLOCKED_CONTRACT / <contract-id> / <missing semantic>`; DEV must not invent local architecture.
- Never write product changes directly to `main` or `wave15/corrections-integration`.
- Shared CODEX is scarce. Keep it PARKED unless a genuinely environment-dependent proof requires it.
- Customer installers/compiled distributions must not contain internal development docs/materials.
- After R2 corrections integrate, the mandatory exit is still:
  `SECOND CODEX black-box Preview + Product Owner human Preview + independent technical Audit -> residual correction/recheck -> correction accepted`.
- Every Main response to the Product Owner must end with a table:
  `| Chat/lane | Status atual | Sua ação |`.
- When several parallel chats exist, list each separately and explicitly tell the Product Owner whether to send `SIGA`, wait, or do nothing.
- When the Product Owner asks for parallel bootstraps, send **one bootstrap per message** so the workflow remains understandable.

## 2. Exact product integration checkpoint at handoff

Live integration after TAG commissioning merge:

- branch: `wave15/corrections-integration`;
- HEAD: `37fcfb6ab9f25b2c2478d0a06b38379a9f3bcf5a`;
- tree: `8b42220c31877c40affb34c4fe96426dfdcb4244`;
- commit: `W15 R2 TAG-C: transient TAG commissioning reads (#401)`.

TAG-C / #390 evidence:
- PR #401 exact head: `92fe1fa171d203820fd89caa6a065553a95b4ade`;
- Wave 15 T1 #160 / `36633255440`: SUCCESS;
- Main audited read-only authority, bounded monitor, effective physical transform normalization and no process/Historian mutation;
- merged as `37fcfb6ab9f25b2c2478d0a06b38379a9f3bcf5a`.

Post-merge EliteSCADA CI:
- #1602 / `36634093326`;
- Backend build/test/Runtime smoke: GREEN at handoff;
- Web build: GREEN at handoff;
- Chromium E2E: **still in progress at handoff**.

**First action of the next Main:** revalidate #1602. Do not infer its final state from this document.

If #1602 is globally GREEN:
- record `TAG_COMMISSIONING_INTEGRATED_VERIFIED` in #390/#305/control;
- continue the R2-A integration sequence below.

If #1602 is RED:
- inspect the exact failing job;
- do not weaken TAG-C authority or the previously stabilized Runtime smoke without causal evidence.

## 3. Product version authority — PR #403

The Product Owner explicitly defined the version model and Main has frozen it in:
`docs/WAVE15-CORRECTION-NOW-SHARED-CONTRACTS.md`.

Current human-facing identity:
`EliteSCADA Alpha 0.15.2.1`.

Approximate numeric convention while pre-1.0:
`0.WAVE.REVISION.DELIVERY`

Current meaning:
- `0`: pre-1.0;
- `15`: Wave 15;
- `2`: second revision/convergence cycle;
- `1`: first delivery/correction package in that revision.

Preserve the axes approximately for future versions. Do **not** replace them with arbitrary component/package increments. Alpha/Beta/RC/1.0 are product maturity/channel semantics. Engineering schema/project revision/build SHA remain separate technical metadata.

Main-created foundation:
- PR #403 — `W15 R2: canonical product version authority`;
- branch `work/w15-r2-product-version-authority`;
- exact head `2eae0339036c79155126a15ac2cbc0be8e3ed66e`;
- base `37fcfb6ab9f25b2c2478d0a06b38379a9f3bcf5a`;
- ready / mergeable;
- Wave 15 T1 #162 / `36634435918`: SUCCESS.

PR #403 establishes:
- one canonical identity in `Directory.Build.props`;
- assembly/file/informational distribution metadata derived from the same source;
- public safe `GET /api/product/info`;
- Web mirror `getProductIdentity()`;
- build/commit provenance as technical metadata.

**Integration order:** do not merge #403 while TAG-C post-merge #1602 is still unresolved. If #1602 is GREEN, revalidate #403 exact head/base and merge it, then require a fresh post-merge EliteSCADA CI before accepting downstream version consumers.

## 4. Chat I — Engineering Density — PR #400

State:
- branch `work/w15-r2-eng-density`;
- PR #400 open/draft/mergeable;
- exact head `38235fce3a55c97b0f036d047b2b307237bbff6a`;
- original exact base `3140ad20b759924a15e3e29d74726b6912bf3da6`;
- Wave 15 T1 #159 / `36633158710`: SUCCESS.

Implemented density/layout foundation:
- compact Engineering context/header;
- Workspace state persistence;
- compact Lock state;
- Information technical details;
- graphical-editor wide-section hook;
- focused shell/density evidence.

DEV correctly reported a contract gap instead of hard-coding a version:
`C-PRODUCT-VERSION-01` had no canonical API/build source at its creation base.

Main resolved that gap with PR #403.

**Next safe action for I after #403 integrates GREEN:**
1. issue a direct Main correction order to Chat I;
2. rebase/recompose #400 onto the then-current integration;
3. consume `GET /api/product/info` / Web mirror;
4. display the canonical `EliteSCADA Alpha 0.15.2.1` identity under Engineering Information;
5. do not hard-code the version in `EngineeringApp.tsx`;
6. rerun exact-head T1;
7. Main re-audits before merge.

## 5. Chat K — Theme / Contrast — PR #402

State at handoff:
- branch `work/w15-r2-theme-contrast`;
- PR #402 open/draft/mergeable;
- exact head `128dbbdbed73119b66bd95b88e6e8366038c1dc0`;
- current PR base has advanced to TAG-C integration `37fcfb6a...`;
- scope: semantic theme tokens, Report chrome/paper boundary, Script/Python/Monaco theme, structured form/mutation contrast;
- Wave 15 T1 #169 / `36635571606`: **in progress at handoff**.

Changed product surface is Web/theme only; Main must still audit final exact diff and dark/light evidence.

Do not merge until:
- T1 #169 is final GREEN;
- current integration ancestry is revalidated;
- no overlap/conflict with I is silently resolved by GitHub;
- Main confirms K stayed out of Engineering layout architecture.

## 6. Chat O — Historian Capture — PR #404 — OWNERSHIP CORRECTION STILL OPEN

State at handoff:
- branch `work/w15-r2-historian-capture`;
- PR #404 open/draft/mergeable;
- exact head `01174d30197019291adaa8fddeb92dc75565abd0`;
- Wave 15 T1 #168 / `36635438819`: **FAILURE**;
- focused .NET failed 2 of 33 TimescaleDB tests;
- both failures are timestamp equality at sub-microsecond precision after PostgreSQL/Timescale round-trip:
  - `PeriodicCapture_OneHundredMillisecondObservationsPersistOnlyAcceptedRows`;
  - `MemoryAndTimescaleCapture_DecisionsAndPersistedSamplesAgree`;
- example: expected `...8402336Z`, actual persisted `...8402330Z`;
- this may be a test/precision normalization defect, but do not correct it until the ownership-invalid ImportExport paths are first removed from O's net diff.

Main has already rejected O's current ownership composition.

**Forbidden paths still present in the net diff at this exact head:**
- `src/Scada.Engineering/ImportExport/EngineeringDtoMapper.cs`;
- `src/Scada.Engineering/ImportExport/EngineeringExchangeService.cs`;
- `src/Scada.Engineering/ImportExport/Handlers/HistorianCaptureProfileEngineeringHandler.cs`;
- `src/Scada.Engineering/ImportExport/Handlers/TagEngineeringHandler.cs`.

Those paths are owned by Portability / #385.

Therefore:
`R2_A_O_CORRECTION_STILL_OPEN / FORBIDDEN_PATHS_REMAIN / NO_MERGE`.

Do not accept a follow-up commit that merely comments around this. The four forbidden paths must disappear from O's **net diff from its exact creation base**.

O may retain:
- Historian profile registry;
- Active/runtime resolver;
- capture policy;
- in-memory/Timescale capture decisions;
- writer diagnostics;
- focused Historian tests.

Package/Fragment preservation of capture-profile references is a downstream Portability responsibility.

## 7. Data Query Core / #384 — BLOCKED_CONTRACT, branch intentionally clean

Branch:
`work/w15-r2-data-query-core`

State:
- exact creation base `3140ad20b759924a15e3e29d74726b6912bf3da6`;
- ahead 0 / behind 0;
- no product mutation;
- no PR.

DEV correctly returned:

`BLOCKED_CONTRACT / C-DATA-QUERY-VIEW-01 / point-target and deterministic interpolation-sampling-aggregation semantics`

Main must freeze or add a bounded shared-wire delta for:

1. single-point target timestamp authority for AtOrBefore/AtOrAfter/Exact/Interpolated;
2. interpolation admissible quality set + maximum-gap default/null behavior;
3. SampledFixedStep grid origin/alignment/endpoints/fallback/quality;
4. Aggregate bucket origin/alignment/partial edges/returned timestamp/mixed-quality provenance.

Important wire gap:
- current F0 `HistorianRetrievalEngineeringDto` has no explicit target timestamp;
- do not tell the DEV to guess `FromUtc` or `ToUtc`.

**Next Main action:** resolve this as shared contract/F0 bounded delta before telling Data Query to continue.

## 8. Engineering Portability Core / #385 — active implementation, no PR yet

Branch:
`work/w15-r2-engineering-portability-core`

At handoff:
- 8 commits ahead / 0 behind its exact creation base;
- no PR yet.

Current changed implementation paths include:
- `EngineeringExchangeFormatAuthority.cs`;
- `EngineeringFragmentDependencyResolver.cs`;
- `EngineeringFragmentPackageBuilder.cs`;
- `EngineeringFragmentPlanBuilder.cs`;
- `EngineeringFragmentRemapper.cs`;
- `EngineeringFragmentService.cs`;
- `ReusableLibraryProvenance.cs`.

Portability owns:
- `src/Scada.Engineering/ImportExport/**`;
- `src/Scada.Engineering/Libraries/**`;
- Fragment dependency/preview/remap/apply;
- Library provenance/Compare/Upgrade/Keep/Fork.

Portability must eventually preserve canonical Historian Capture Profile references by consuming the integrated O semantics; O must not implement a second import/export path.

Let the Portability chat continue. Main reviews when it produces a formal handoff/PR.

## 9. Shared CODEX

Shared CODEX remains:
`PARKED / PRESERVE_REMAINING_CAPACITY / NO_PRODUCT_MUTATION / NO_MERGE`.

Do **not** send SIGA merely because R2 lanes are active.

Use shared CODEX only if Main explicitly issues a new environment-dependent proof/audit order after normal DEV/CI evidence is insufficient.

## 10. Still not released

Keep these HOLD unless live route/control later explicitly releases them:
- J — Editor UX R2;
- L — Branding;
- N — Structured Engineering workflow forms;
- M — UX Copy / i18n;
- P — historical UI consumers;
- TAG-D / #391 duplication;
- Historical Playback.

Do not auto-generate bootstraps simply because package names exist.

## 11. Immediate Main priority order

At takeover:

1. re-read this handoff + live #305 + control planes;
2. revalidate integration HEAD;
3. close post-merge CI #1602 for TAG-C;
4. if GREEN, record TAG-C integrated/verified;
5. integrate #403 only after exact revalidation; require fresh post-merge CI;
6. then direct Chat I/#400 to consume canonical product version, recompose onto live integration and rerun T1;
7. independently monitor #402 T1 #169; audit before integration;
8. keep #404 blocked until forbidden ImportExport paths vanish from its net diff;
9. resolve #384 BLOCKED_CONTRACT centrally before Data Query resumes;
10. allow Portability #385 to continue until formal handoff/PR;
11. preserve CODEX capacity;
12. after R2-A foundations integrate, reassess downstream R2-B release from live route rather than old memory.

## 12. Product Owner status table expected on every Main response

Always end with something equivalent to:

| Chat/lane | Status atual | Sua ação |
|---|---|---|
| MAIN / CI | exact current integration/gate | usually none |
| TAG commissioning | integrated/pending CI/etc. | explicit |
| I | active/blocked/delivered | explicit |
| K | active/T1/delivered | explicit |
| O | correction/active | explicit |
| Data Query | BLOCKED_CONTRACT until Main fix | do not send SIGA unless released |
| Portability | active | let work |
| Shared CODEX | PARKED | do not touch |

This interaction contract matters to the Product Owner and must survive coordinator transitions.
