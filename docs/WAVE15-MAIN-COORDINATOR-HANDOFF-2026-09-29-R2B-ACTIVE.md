> **PRODUCT OWNER SCOPE OVERRIDE — 2026-09-30**
>
> GitHub live remains authoritative. This override supersedes older wording below where it conflicts.
>
> - **EliteGO as a separate application/client is DEFERRED INDEFINITELY** and is not a Wave 15 or Wave 16 exit gate.
> - **Redundancy / HA remains mandatory Wave 15 scope.** EliteGO would be only one future consumer; Web Runtime/current EliteSCADA and other future clients consume the same server-authoritative HA contracts.
> - Wave 15 must finish the **EliteSCADA-side foundations needed by a future EliteGO**: public Runtime projection/rendering contracts, backend Authority/effective capabilities, client-neutral Runtime Session Lease/licensing, reconnect-safe identity, HA topology/effective-Active discovery, truthful freshness/reconnect semantics, topology-neutral package boundaries and no direct client Driver/database authority.
> - Already integrated work must not be duplicated: Authority UX #346, Licensing UX/License Generator v2 #345, FND-05 HA authority/manual-transfer #347, FND-07 detach/neutral-bootstrap #348/#352/#353.
> - Remaining original-W15 work no longer waits behind a blanket correction-phase barrier. After a green exact integrated base, disjoint packages may run in parallel with remaining R2 work.
> - The next parallel batch should prioritize: R2-C historical/time-range, HA downstream, Installation UX, and remaining Visual Quality/Library maturity, subject to exact-base and file/contract overlap.
> - Historical Playback follows Query/Time Range; M/copy+i18n follows stable structural/historical surfaces.
> - **EEE Simulation instructional project and its real Modbus variant stay deliberately late**, finalized immediately before the last complete-product Preview so they exercise the final accepted product.
> - Final Wave 15 acceptance still requires exact integrated validation, fresh final Preview and Product Owner audit.
>
> Canonical scope authorities: #297, #298, #299, #300, #305, #306.

# EliteSCADA Wave 15 — MAIN COORDINATOR HANDOFF — R2-B ACTIVE

**Handoff date (BRT):** 2026-09-29  
**Repository:** `brunolrogerio-collab/EliteSCADA`  
**Authority:** GitHub live is the sole authority. Revalidate live before every substantive action.

> This file is the canonical current takeover handoff for the next Main Coordinator.
> It supersedes older current-state wording in previous handoff files whenever there is a conflict.

## 1. Exact live checkpoint at handoff

Product integration:
- branch: `wave15/corrections-integration`;
- exact HEAD: `1d9e3f9123bea8e27c362ee680a4ba70f864becd`;
- exact tree: `512ef505924f3a0a709e8d5a73e2625531586e1c`;
- merge purpose: Portability PR #405 integrated.

Post-Portability verification:
- EliteSCADA CI #1611;
- run id: `36655606469`;
- exact SHA: `1d9e3f9123bea8e27c362ee680a4ba70f864becd`;
- conclusion: **SUCCESS**;
- Web build: SUCCESS;
- Backend build/test/smoke: SUCCESS;
- Chromium end-to-end: SUCCESS.

Portability:
- PR #405 integrated;
- accepted candidate head: `3565f0548409b06a771e42df846e2c45802ab51d`;
- exact T1 #207 / `36655088254`: SUCCESS;
- final merge: `1d9e3f9123bea8e27c362ee680a4ba70f864becd`.
- Main audit caught and corrected missing production DI composition before merge:
  `EngineeringExchangeService -> HistorianCaptureProfileEngineeringExchangeDecorator -> DataQueryEngineeringExchangeDecorator`.

## 2. R2-B current execution state — J / L / N ACTIVE

Product Owner deliberately waited for Portability integration so J/L/N could all start from one common exact base.

After #1611 turned GREEN, Main:
1. revalidated J/L/N had **0 own commits**;
2. force-aligned all three branches to exact `1d9e3f9123bea8e27c362ee680a4ba70f864becd`;
3. revalidated all three as:
   `identical / ahead 0 / behind 0 / total own commits 0`;
4. issued synchronized ACTIVE / NO_MERGE orders.

At this handoff, a fresh live revalidation still shows:
- J: identical / ahead 0 / behind 0 / no open PR;
- L: identical / ahead 0 / behind 0 / no open PR;
- N: identical / ahead 0 / behind 0 / no open PR.

Therefore **no developer work has started yet** on J/L/N as of this handoff.

### J — DEV-EDITOR-UX-R2

State:
`ACTIVE / R2_B_EDITOR_UX / NO_MERGE`

Branch:
`work/w15-r2-editor-ux`

Order:
`R2B-J-EDITOR-UX-POSTPORT-01`

Canonical Main order:
- issue #367;
- comment `5902887546`.

Mission:
- mature the existing single Screen/Popup WYSIWYG editor;
- compact built-in insertion toolbar;
- Structure + Dynamo/Library/assets side-authoring surface;
- independent side-panel scroll/collapse/reopen;
- selected object `Properties | Dynamics | Events`;
- immutable Id + developer-facing Key rename;
- deep group editing without forced ungroup;
- copy/paste/duplicate/delete/z-order/align/distribute;
- discoverable static appearance editing;
- animation only through canonical/frozen visual semantics;
- canonical VisualAsset identity for image/background;
- visual event association remains `object/screen event -> existing Script -> compatible handler`;
- Screen/Popup parity + mounted evidence.

Forbidden:
- global branding/AppNavigation (L);
- structured non-graphical forms (N);
- new VisualAsset/blob/file authority;
- backend Script runtime redesign;
- second renderer;
- Working -> Active collapse;
- Portability/Fragment implementation;
- shared schema/wire redefinition.

**Chat bootstrap status:** J bootstrap text was already delivered to the Product Owner immediately before this coordinator handoff.

### L — DEV-BRANDING

State:
`ACTIVE / R2_B_BRANDING / NO_MERGE`

Branch:
`work/w15-r2-branding`

Order:
`R2B-L-BRANDING-POSTPORT-01`

Canonical Main order:
- issue #377;
- comment `5902888852`.

Mission:
- branding modes DEFAULT / TEXT / IMAGE / NONE;
- canonical Engineering/application configuration;
- Working preview never mutates Active;
- Publish/Activate makes Active branding authoritative;
- IMAGE references canonical VisualAsset stable ID;
- legacy/default stays EliteSCADA;
- NONE collapses reserved brand space;
- explicit missing/corrupt asset fallback/diagnostic;
- package/export/import/restart/recovery fidelity;
- one resolved branding authority consumed by AppNavigation/global shell;
- dark/light/responsive/accessibility evidence.

Shared asset rule:
- if static sanitized SVG support required by C-VISUAL-ASSET-02 is missing, L owns the bounded canonical VisualAsset SVG validation/sanitization extension;
- no branding-only asset store;
- J consumes the same shared VisualAsset authority.

Forbidden:
- visual-editor redesign;
- generic structured-form overhaul;
- deployment-local logo path;
- browser-local branding authority;
- direct Working -> Active;
- Portability implementation beyond consuming lifecycle.

**Chat bootstrap status:** Product Owner requested the L bootstrap immediately before this handoff, but the prior chat did not deliver it before the coordinator-transfer request. The next Main should generate/send it from the live canonical order if the user still needs it.

### N — DEV-ENG-WORKFLOW-FORMS

State:
`ACTIVE / R2_B_STRUCTURED_FORMS / NO_MERGE`

Branch:
`work/w15-r2-eng-workflow-forms`

Order:
`R2B-N-STRUCTURED-FORMS-POSTPORT-01`

Canonical Main order:
- issue #380;
- comment `5902889313`.

Product model:
`ENTITY_FIRST / TASK_FIRST / ONE_CONTEXT / PROGRESSIVE_DISCLOSURE`.

Initial representative targets:
- TAG;
- Data Source;
- Alarm;
- at least one additional structured surface.

Mission:
- selected entity = edit/action context;
- intentional bulk mode;
- field grouping by user mental model;
- advanced/conditional progressive disclosure;
- human labels/helpers/units/examples/ranges;
- coherent draft/preview/apply/status;
- guided empty states;
- contextual destructive actions;
- preserve CAS/Authority/delete/bulk backend semantics;
- consume integrated K theme tokens.

Forbidden:
- Screen/Popup editor internals (J);
- global branding/AppNavigation (L);
- backend mutation/CAS/Authority contract changes;
- Portability/Fragment/Library implementation;
- shared Engineering schema rewrite;
- broad copy/i18n sweep.

**Chat bootstrap status:** Product Owner requested the N bootstrap immediately before this handoff, but the prior chat did not deliver it before the coordinator-transfer request. The next Main should generate/send it from the live canonical order if the user still needs it.

### Synchronized release ledger

- #378 comment `5902889694`;
- #305 comment `5902890118`.

Latest prepared coordination docs:
- `docs/WAVE15-R2B-J-L-N-BOOTSTRAPS-2026-09-30.md`;
- `docs/WAVE15-POST-E3-DEVELOPMENT-ROUTE.md`;
- `docs/WAVE15-CORRECTION-NOW-CHAT-BOOTSTRAPS.md`.

Latest release-state doc commits:
- J/L/N bootstrap release: `0e700d8f02be36f5e8c1d44e70cafb48a068a2eb`;
- post-E3 route: `b70014335d1bc9545f999288dfbb609cf2c26376`;
- correction-now chat bootstraps: `0d2ec622e01b6e35d78d2805c2b7ad208813c49a`.

## 3. Foundation already integrated before R2-B

The common R2-B base already includes and has passed combined CI with:
- Engineering Density I;
- stale density E2E closeout;
- O Historian Capture;
- K Theme/Contrast;
- Data Query / AlarmView core;
- Portability / Fragment / Library core;
- TAG-C / PointReadTest foundation and behavior;
- product identity `EliteSCADA Alpha 0.15.2.1`;
- F0-D2 historical target wire;
- prior C0/F0/Gate0/Gate1 foundations.

Do not reopen accepted foundations merely because J/L/N consume them.

## 4. Holds after J/L/N release

Do **not** auto-release these without a new Main order:
- M — UX Copy / i18n;
- P — historical absolute/relative time-range consumers;
- TAG-D — copy/paste/duplicate/sequential TAG workflow;
- Historical Playback;
- Script Authoring R2.

Important:
- Script Authoring R2 remains intentionally held to reduce overlap with J visual/event authoring.
- TAG-D remains a separate held lane even though Portability is now integrated; do not silently fold it into N unless Main explicitly changes scope.
- P remains held until the relevant authoring dependency is satisfied.
- M runs after structural consumers so copy/i18n targets the final UI structure.

## 5. Shared CODEX

Shared CODEX is parked and is **not** on the current critical path.

Control plane:
- branch `coord/w15-fnd04-dev-aud-control`;
- file `docs/WAVE15-FND04-DEV-AUD-CONTROL.md`;
- live control reports `MAIN_ORDER_REV: 0124`;
- shared CODEX pointer: `SHARED_CODEX_ORDER_REV: 0121`;
- order:
  `SHARED-CODEX-PARKED-AFTER-GATE1-C0-FREEZE-102`;
- state:
  `PARKED / GATE1_GREEN / C0_FROZEN / PRESERVE_REMAINING_CAPACITY / NO_PRODUCT_MUTATION / NO_MERGE`.

Do not send SIGA to shared CODEX unless Main explicitly needs an environment-only proof later.

## 6. Mandatory Wave 15 correction exit remains

After R2 development/integration:
1. integrated corrected candidate;
2. second independent CODEX black-box Preview in a fresh environment if tooling/capacity is available;
3. Product Owner human Preview;
4. independent technical Audit;
5. residual correction/recheck;
6. only then:
   `CORRECTION PHASE ACCEPTED / THIRD-PARTY TEST CANDIDATE`;
7. then return to deferred original Wave 15 route.

CI/T1 alone never substitutes for the second Preview + human Preview + technical Audit.

## 7. Wave 16 is prepared but NOT current execution

The Product Owner and Main used the Portability/CI wait to consolidate the Wave 16 distribution direction. This is future architecture, not a reason to distract current Wave 15 execution.

Canonical Wave 16 direction:
`docs/WAVE16-WINDOWS-NATIVE-INSTALLER-DIRECTION.md`.

Durable north:
`canonical EliteSCADA -> architecture-specific immutable release payload -> host executor/adapter -> external durable state`.

Initial profiles:
- win-x64;
- linux-x64;
- linux-arm64;
- OCI is a supported representation/profile, not the identity of the product.

Key future decisions already recorded:
- .NET self-contained native payloads;
- Npgsql is bundled app-local, not separately installed;
- Host prerequisites vs Database Profile prerequisites are explicit;
- Windows installer is offline-first;
- VC++ Redistributable is detected/reused or installed from approved local media if required;
- OpenSSL belongs to the database/host dependency graph when required;
- PostgreSQL/TimescaleDB Local Managed remain separate service/lifecycle components;
- PostgreSQL redistribution is permissive;
- current TimescaleDB 2.29.2-pg18 image is Community/TSL-capable;
- raw current Historian is Apache-capable by source inspection;
- retention/downsampling continuous-aggregate features depend on TSL and are currently not mounted in normal production DI;
- Wave 16 should validate `2.29.2-pg18-oss` before commercial Local Managed packaging;
- Release Factory owns SBOM/provenance/signatures/hashes/prerequisite profiles.

Important recent Wave 16 direction commits:
- immutable payload + host executor north: `afe40a29a1d5e4865779a5f5ae7336920fc5b404`;
- Timescale feature/license audit: `664a13741d7a7a745a7bcc71c814cb4d95b8fb32`;
- host prerequisites/offline bootstrap model: `442faf5df5e3f5fab2ede0397d813ac3540109cb`.

Do not start Wave 16 implementation before Wave 15 acceptance.

## 8. Product Owner coordination agreements — MUST PRESERVE

### A. GitHub live is sole authority
Always revalidate live before merge, release, rerun, order, freeze, or status claim.

### B. Product Owner is not a courier
Developer/auditor/CODEX handoffs belong in GitHub. Main recovers them directly.

For separate user-controlled DEV chats, Product Owner should normally need only:
`SIGA`
once Main has issued the durable GitHub order.

### C. Meaning of SIGA in Main chat
When user says only `SIGA`:
- revalidate live;
- execute the next safe authorized action;
- do not merely narrate a plan.

### D. Mandatory final status/action table
Every Main Coordinator response about project coordination must end with one table:

`| Chat/lane | Status atual | Sua ação |`

Cover all active/parallel chats plus CODEX and give an explicit Product Owner action.

### E. Bootstrap discipline
Do not invent new lanes or stale bootstraps.
Generate a bootstrap from the current live branch/order when the Product Owner requests it.

## 9. Immediate replacement-Main startup

The replacement Main should:

1. read this file completely;
2. read live `docs/WAVE15-POST-E3-DEVELOPMENT-ROUTE.md`;
3. read live `docs/WAVE15-R2B-J-L-N-BOOTSTRAPS-2026-09-30.md`;
4. read newest #305 and #378 comments;
5. revalidate `wave15/corrections-integration`;
6. revalidate J/L/N branches and any newly opened PRs;
7. inspect live CODEX control only if considering CODEX use;
8. correct stale docs before acting if GitHub live differs.

### Immediate likely user interaction

At this exact handoff, J/L/N have not started.

The Product Owner already received the J bootstrap.

The Product Owner asked for L and then N bootstraps immediately before requesting this coordinator transfer. Therefore, if the next user message continues that intent, first revalidate live and provide the requested L/N bootstrap(s) from the canonical orders above.

If instead the user says `SIGA`, revalidate live and process whatever J/L/N progress/handoff now exists. Do not assume they remain untouched.

## 10. Product identity

Current product identity:
`EliteSCADA Alpha 0.15.2.1`

Approximate scheme chosen by Product Owner:
`0.WAVE.REVISION.DELIVERY`.

Do not create independent hard-coded version strings.

## 11. Current status snapshot for user workflow

At handoff creation:
- J ACTIVE, branch identical to common base, no PR yet;
- L ACTIVE, branch identical to common base, no PR yet;
- N ACTIVE, branch identical to common base, no PR yet;
- Shared CODEX PARKED;
- M/P/TAG-D/Playback/Script Authoring R2 HOLD.

GitHub live must be rechecked before presenting this snapshot as current.
