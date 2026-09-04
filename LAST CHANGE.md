# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 (BRT)  
**Operational state:** **WAVE 14 #211 ACTIVE / C01-C10 LAST FULL FREEZE GREEN / C11 PASS 2 CONSOLIDATED + IMPLEMENTATION LOCKED / C12+C13+C14 ACCEPTED+INTEGRATED AND COMBINED GREEN / C15 CORRECTED 5/5 GREEN — COORDINATOR REVIEW PENDING / C17 CORRECTED 5/5 GREEN — READY FOR INTAKE / C16 4/5 GREEN — ELITESCADA CHROMIUM FAILURE + INTEGRATION COMPOSITION PENDING / C18 BLOCKED UNTIL C15 / WAVE13 PAUSED**

> GitHub is the development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only `[skip ci]` commits may move coordinator branches without redefining a product-code freeze or the last validated product-code checkpoint.

## 1. Integration authority

Coordinator integration branch:

`wave14/corrections-integration`

Draft integration PR:

`#212`

PR #212 remains **DRAFT** and is not authorized to merge to `main`.

Last fully converged/frozen product-code SHA before the pre-DEMO correction stage:

`97eefd8f4377ff583d1ba20bc89203f4a82b584d`

Its five post-C10 gates were green:

- EliteSCADA CI #1273 / `33795337274`;
- Wave 11 Active HMI Runtime #203 / `33795337288`;
- Preview Licensing CI #225 / `33795337280`;
- L3 Seven-Driver Lab #180 / `33795337252`;
- Interop Lab Smoke #102 / `33795337270`.

Current accepted **pre-DEMO integration product-code checkpoint** after C12+C13+C14 composition:

`59a338d67dc956af0aa7907a54d59aaf8e80d764`

This checkpoint is not yet the new Wave14 product freeze. A new exact freeze is declared only after C12-C18 convergence, combined validation and C10 convergence cycle 2.

## 2. C11 gate remains locked

C11 Pass 2 branch:

`wave14/c11-pass2-product-gap-audit`

Canonical result:

**PASS 2 CONSOLIDATED / KEEP C11 IMPLEMENTATION LOCKED**

Binding Product Owner decisions:

1. persisted authorable Popup X/Y is mandatory before C11 release;
2. the living deterministic EEE Simulation must be buildable from normal generic EliteSCADA tools;
3. no EEE-specific simulator service, historical DEMO runtime, hidden `.escadapkg` mutation, direct DOM/React bypass, private Driver/host hook, authorization bypass or licensing bypass is acceptable.

Canonical correction plan:

`docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`

Common original DEV base:

`2607e03d5445eefe1f434495d0ee81136c6cd220`

## 3. C13 — ACCEPTED + INTEGRATED

Package:

**C13 — Canonical Simulation Quality Contract**

Accepted exact candidate:

`b9ce08b7466ffe4cb4b01a64d4fe16921f2c9cf8`

Coordinator review confirmed server-authoritative qualified samples, Source/TAG ownership enforcement, ordinary writes remaining `Good`, no Client Visual physical-Driver quality spoofing, and canonical propagation of `Bad` / `Stale` / `Unavailable`.

Five exact-head gates were green.

Integration PR #242 merged with expected-head protection.

C13 integration merge SHA:

`84b12f7795b6ab5c63a4433ee7818ed418c442a1`

Validation-only PR #239 is DO NOT MERGE TO MAIN.

## 4. C14 — ACCEPTED + INTEGRATED

Package:

**C14 — First-Class Operational Events**

Accepted exact candidate:

`70e311d3a359e7b00e8f0ed035478d51bb6ee001`

Coordinator review confirmed:

- Event is first-class and distinct from Alarm/Audit;
- canonical Engineering persistence/import-export;
- Active-revision authority;
- immutable runtime occurrence;
- dedicated durable PostgreSQL history;
- protected Historical Query dataset `operational.events`;
- application/protocol-neutral contract suitable for C18;
- Engineering schema authority advanced to v16.

Five exact-head gates were green.

Coordinator intake PR #235 merged with expected-head protection.

C14 integration merge SHA:

`3d6389069015996a5a68fbb055f620560da9e839`

Validation-only PR #240 is DO NOT MERGE TO MAIN.

C14 backend Event prerequisite for C18 is satisfied.

## 5. C12 — ACCEPTED + INTEGRATED / COMBINED GREEN

Package:

**C12 — Server Runtime Automation / Generic Simulation Authoring**

Final corrected DEV candidate:

`aa0fb1700cb805cfdbb6072ce7ce6bccda687067`

The original C12 delivery was returned because Server Scripts could only write values and therefore could not build mandatory simulated `Bad` / `Stale` / `Unavailable` scenarios through normal tools.

The corrected C12 consumes the accepted C13 contract instead of creating a parallel quality model.

Coordinator review confirmed:

- `publish_server_memory_sample(stable_tag_id, value, quality)` is available only for declared `ServerMemoryTag` script dependencies;
- the .NET host revalidates active revision, capability, Source/TAG ownership and read-only policy;
- quality is validated against canonical `TagQuality`;
- qualified values become C13 `QualifiedSourceSample` and flow through `ServerAuthoritativeSamplePublisher`;
- ordinary value-only Server Memory writes remain `Good`;
- Client Visual receives no quality-authoring authority;
- Server Script cannot spoof physical Driver TAG quality;
- `Bad`, `Stale` and `Unavailable` propagate through cache/realtime, Communication alarm semantics and historian/query;
- Server Script lifecycle covers Initialize/Timer/TagChanged/ServerRuntimeEvent/Dispose under Active revision ownership;
- no EEE-specific simulator/service was introduced.

C12 isolated exact-head validation was green:

- EliteSCADA CI #1309 / `33830373534`;
- Wave 11 Active HMI Runtime #238 / `33830373668`;
- Preview Licensing CI #260 / `33830373527`;
- L3 Seven-Driver Lab #215 / `33830373596`;
- Interop Lab Smoke #137 / `33830373587`;
- dedicated exact-head validation `33830613919`: SUCCESS.

C12 and already integrated C14 both touched `src/Scada.Api/Runtime/ScadaRuntimeFacade.cs`. The Coordinator explicitly composed the two contracts rather than overwriting either side.

Composed C12+C13+C14 product-code checkpoint:

`59a338d67dc956af0aa7907a54d59aaf8e80d764`

Parents:

- integration/C13+C14 authority `c7d35186c169bfd212b4270dd5bd31899a53598a`;
- C12 candidate `aa0fb1700cb805cfdbb6072ce7ce6bccda687067`.

The composed facade preserves both C14 Operational Event projection/emission and C12 Server Script diagnostics/runtime snapshot.

Five **combined exact-head** gates on `59a338d...` are green:

- EliteSCADA CI #1325 / `33832217232`: SUCCESS;
- Wave 11 Active HMI Runtime #253 / `33832217225`: SUCCESS;
- Preview Licensing CI #275 / `33832217213`: SUCCESS;
- L3 Seven-Driver Lab #230 / `33832217277`: SUCCESS;
- Interop Lab Smoke #152 / `33832217221`: SUCCESS.

PR #248 records the C12 intake/composition into `wave14/corrections-integration`. Validation-only PR #241 was closed without merge to `main`.

C12 findings `SCR-01` and the generic producer side of `SIM-01`, including use of C13 quality publication from Server Runtime, are accepted as corrected product capabilities. Full C11 revalidation remains intentionally deferred until the complete pre-DEMO correction set converges.

## 6. C17 — CORRECTED EXACT HEAD 5/5 GREEN / READY FOR COORDINATOR INTAKE

Package branch:

`wave14/c17-memory-authoring-e2e`

Current corrected head:

`6db4fb33f06159f108ca17ceca23a35ee158b228`

The earlier Wave11 failure on `15dda284...` was a deterministic ambiguous Playwright selector, not a Memory product failure.

Correction commit:

`ecc3bc5e081bdaa1d6d1faffa4bb6c00f4159fa0`

The locator was changed from ambiguous label matching to the intended accessible checkbox role/name without changing product UI semantics or weakening the scenario.

Additional follow-up strengthened the lifecycle truth against backend Active revision and Client Memory session isolation.

Five exact-head gates on `6db4fb33...` are green:

- EliteSCADA CI #1317 / `33831604880`;
- Wave 11 Active HMI Runtime #246 / `33831604903`;
- Preview Licensing CI #268 / `33831604857`;
- L3 Seven-Driver Lab #223 / `33831604845`;
- Interop Lab Smoke #145 / `33831604867`.

Coordinator architectural review already found the production change appropriately capability/catalog-based (`sourceProvider`) rather than hard-coded to private Memory provider IDs.

Next action: open C17 intake against the current C12+C13+C14 integration checkpoint, verify composition, integrate with expected-head protection if clean, then run combined exact-head gates.

## 7. C15 — CORRECTED EXACT HEAD 5/5 GREEN / COORDINATOR ARCHITECTURE REVIEW PENDING

Package branch:

`wave14/c15-hmi-trend-multipen`

Current corrected head:

`2abee30a25577368999f16d52a0802e6eea473ca`

The prior head `01d98d88...` failed deterministic TypeScript compilation in Trend authoring/runtime models. DEV corrected the typing and initializer defects without disabling strictness.

Five exact-head gates on `2abee30a...` are green:

- EliteSCADA CI #1316 / `33831346148`;
- Wave 11 Active HMI Runtime #245 / `33831346137`;
- Preview Licensing CI #267 / `33831346120`;
- L3 Seven-Driver Lab #222 / `33831346128`;
- Interop Lab Smoke #144 / `33831346156`.

Before integration acceptance, Coordinator must complete architecture review of:

- first-class `core.trend` visual object;
- Screen and Popup insertion;
- canonical persisted Multi-Pen configuration;
- protected historian/live data access;
- quality/no-data presentation;
- independent multiple instances;
- pt-BR/en/es chrome;
- Save/Publish/Activate/Active Runtime lifecycle;
- absence of DEMO-only React/private runtime wiring.

C15 is the remaining upstream visual prerequisite for releasing C18 implementation.

## 8. C16 — 4/5 GREEN / ELITESCADA CHROMIUM FAILURE + INTEGRATION COMPOSITION PENDING

Package branch:

`wave14/c16-hmi-command-navigation-popup`

Current candidate:

`2a4377cfa93c6020bc89882f6d2b3088cefd030b`

The earlier Wave11 Dynamo interaction failure was corrected on this head.

Current exact-head results:

- EliteSCADA CI #1294 / `33827619323`: FAILURE in Chromium end-to-end;
- Wave 11 Active HMI Runtime #223 / `33827619319`: SUCCESS;
- Preview Licensing #245 / `33827619337`: SUCCESS;
- L3 Seven-Driver Lab #200 / `33827619326`: SUCCESS;
- Interop Lab Smoke #122 / `33827619361`: SUCCESS.

Backend build/test/smoke and Web build are green inside EliteSCADA CI. The remaining failure must be diagnosed from the Chromium job before any rerun; no unchanged rerun is authorized.

Coordinator intake PR #245 against `wave14/corrections-integration` is non-mergeable against the parallel correction integration. This is a composition task, not permission to discard upstream work.

When C16 is composed, preserve:

- C14 Engineering schema v16 and Operational Event contracts;
- accepted C12 Server Runtime + C13 quality contracts;
- C16 `ExecuteCommand`, Startup/Home Screen and persisted Popup X/Y;
- all accepted Wave11 lanes;
- backend Operational Command authorization/audit authority;
- Popup X/Y in canonical logical HMI coordinates.

Strategy: integrate C17 and C15 first, then compose C16 once against the resulting integration authority.

## 9. C18 — BLOCKED UNTIL C15 ACCEPTANCE/INTEGRATION

Package:

**C18 — Embeddable Alarm + Event Browser HMI Objects + related history i18n**

C14 Event backend prerequisite is satisfied.

C18 implementation remains blocked until C15/common embeddable visual-object pattern is accepted/integrated and Coordinator publishes an exact C18 implementation base.

C18 must consume:

- C14 `operational.events` backend/query contract;
- accepted canonical visual-object authoring/persistence/runtime lifecycle;
- normal Screen/Popup placement and Property Inspector contracts;
- pt-BR/en/es chrome;
- backend authority for ACK/shelve/interactive operations.

No frontend-only parallel Event schema is permitted.

## 10. Current coordination order

1. C12+C13+C14 combined checkpoint is accepted and green;
2. integrate C17 after current-head composition check and rerun combined gates;
3. complete C15 architecture review, integrate if accepted and rerun combined gates;
4. after C15 acceptance, publish exact C18 base and release C18 DEV;
5. diagnose/fix C16 EliteSCADA Chromium failure;
6. compose C16 once against the then-current integration authority and revalidate the composed head;
7. implement/accept/integrate C18;
8. run complete combined exact-head integration regression;
9. execute C10 convergence cycle 2;
10. freeze a new exact product-code SHA;
11. revalidate affected C11 findings and remaining real-browser acceptance;
12. only then issue explicit `RELEASE C11 IMPLEMENTATION`;
13. finalize `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-PREMISE.md`;
14. build the canonical living EEE Simulation solely through corrected normal Engineering tools;
15. later validate the same conceptual application against the physical Modbus PLC;
16. only after Wave14 acceptance resume Wave13 packaging/signing.

## 11. Official tracker references

- issue #211 is the Wave14 owner/coordinator tracker;
- C12 corrected/integration checkpoint comment: `5535117282`;
- draft integration PR #212 remains the sole Wave14 integration surface toward `main`;
- Preview issue #208 / PR #210 remains infrastructure/history, not product authority;
- Wave13 issue #205 / PR #207 remains paused.

## 12. Mandatory resume reading

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
5. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
6. C11 consolidated audit + HMI clarification on `wave14/c11-pass2-product-gap-audit`;
7. package handoff docs for the package being reviewed;
8. `docs/CI-VALIDATION-POLICY.md`;
9. live issue #211 and draft PR #212;
10. issue #208 / PR #210 only for Preview history;
11. issue #205 / PR #207 only for paused Wave13 context.

Then revalidate live GitHub before acting.
