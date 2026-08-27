# LAST CHANGE — EliteSCADA

> Operational handoff. A new coordinator must be able to resume from GitHub without depending on chat history.

**Handoff date:** 2026-08-27  
**Development state:** **INTERFACE-WAVE-04 ACTIVE**

Repository truth remains separated into `MERGED`, `IMPLEMENTED IN PR`, `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED` and `SPECIFIED / NOT IMPLEMENTED`.

## Mandatory resume reading

Before any EliteSCADA action read current `main`:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/PARALLEL-WORK.md`;
5. `docs/DEVELOPMENT-WAVES.md`;
6. `docs/CHAT-WORK-ASSIGNMENTS.md`;
7. `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
8. `docs/VISUAL-ASSETS-AND-IMAGES.md`;
9. current assignment `MustReadSpecific`.

GitHub branch/PR/head/CI state is operational truth.

## Wave 00 / persistence hardening — MERGED

Second interface-wave coordinator PR #69 is merged with exact-head CI #403 green. PostgreSQL schema-initialization concurrency hardening PR #70 is merged with exact-head CI #405 green; merge `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`.

## INTERFACE-WAVE-03 — MERGED

Wave 03 objective was the coherent non-graphical cycle:

`Engineering -> Save Revision -> Publish -> Activate -> Runtime operation`

Worker deliveries:

- PR #71 — Runtime TAG Inspector + Recent History; final head `c4f1b8cf48c4231119fdaf17b805852c2347c00f`; CI #411 SUCCESS;
- PR #72 — Interface Validation Readiness Harness; final head `9a21d1402edd2a3ee9188576263d280dca4faa67`; CI #412 SUCCESS;
- PR #73 — Engineering Lifecycle Workspace; final head `360b156c60c2d75d63dd05de1c2894a75d07ad2e`; CI #410 SUCCESS.

Coordinator PR #74 `Integrate Interface Wave 03` is **MERGED**:

- frozen WaveBaseSHA: `0aae8317aff5b0640eb713c1ce404224ccbcbbc2`;
- final integration head: `41d44d513d337e8ef6d3cc0e04ef0cf07a697b41`;
- exact integration CI #418 / run `33124948655`: Web SUCCESS, backend/full tests SUCCESS, Runtime smoke SUCCESS, Chromium SUCCESS;
- merge commit: **`37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`**.

### Important integration findings now MERGED

The Lifecycle UI no longer treats `ActiveRevision = null` plus live Runtime revision `null` as meaningful Active consistency. With no durable Active revision it explicitly reports that none exists.

The integrated Chromium gate initially exposed and preserved the backend protection `RUNTIME_NO_ACTIVE_SOURCES`: a persisted Engineering revision containing only the legacy `builtin.simulation` fallback cannot masquerade as a valid published Runtime activation. The test was corrected rather than the guard weakened.

Final acceptance builds a canonical Server Memory candidate through Engineering Preview/Apply, saves a persisted revision, publishes it through the Lifecycle Workspace, activates it through the protected backend path, proves durable Active/live Runtime revision consistency and verifies the active Server Memory TAG in the mounted Runtime TAG Inspector.

This is the first integrated browser proof in the current wave model of a supported persisted source crossing the full lifecycle into Runtime.

## INTERFACE-WAVE-04 — ACTIVE

**WaveBaseSHA:** `37e64b8ff2bbc431ab1368eab2b3125ec5a5b636`  
**IntegrationBranch:** `integration/interface-wave-04`

All Wave 04 product branches were created from the exact merge commit above:

- coordinator: `integration/interface-wave-04`;
- DEV 1: `feature/interface-wave-04-project-management`;
- DEV 2: `feature/interface-wave-04-trend-viewer`;
- DEV 3: `feature/interface-wave-04-administration-workspace`.

Documentation/coordination commits after the WaveBaseSHA do not invalidate the logical product base. Keep unrelated product-code changes out of `main` until the wave closes.

### DEV 1 — ACTIVE

`Project Management / Portability Workspace`

Goal: make canonical project portability understandable and usable: JSON Export, JSON Import with Preview before Apply/CAS, and every backup/restore/`.escadapkg` operation that current public contracts genuinely support. Do not invent a fake package format or bypass canonical Engineering authority.

### DEV 2 — ACTIVE

`Basic Trend Viewer`

Goal: bounded historical visualization from existing TAG/historian contracts with TAG selection, interval/window, timestamp/value/quality, refresh and live-vs-historical context. Multiple pens only if current contracts support them without a premature new architecture.

### DEV 3 — ACTIVE

`Administration Workspace ergonomics`

Goal: coherent user/role/session administration using existing backend-authoritative local identity contracts: supported create/update, enable/disable, role assignment, password operation, confirmations, authorization/session consequences and `pt-BR`/`en`/`es` states. No frontend-trusted authority or credential leakage.

Exact scope, ReservedFiles, validation and completion criteria are authoritative in `docs/CHAT-WORK-ASSIGNMENTS.md`.

## Next architectural gate — Wave 05

Wave 05 remains **QUEUED**, not executable by workers yet.

It is an architecture-first wave. The coordinator must first stabilize the canonical Script Engineering contract in the integration branch before worker slices begin: Scripts collection/entity identity, source/scope/enabled state, references/events/dependencies, import/export/migration, Preview/Apply, revision/PostgreSQL and `.escadapkg` compatibility.

## First owner validation product gate — LOCKED

The first true product-owner validation build remains:

# EliteSCADA v0.1 — Full Product Validation Preview

Authoritative scope/order: `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`.

It requires the complete vertical path:

`Engineering -> TAG/Alarm/History -> Screen/Popup/Dynamo -> assets/bindings -> Client Visual Python -> Save/Revision -> Publish -> Activate -> graphical Runtime -> restart/Active recovery`.

Modbus TCP is sufficient as the real industrial protocol for v0.1. Production MQTT, OPC UA, BACnet, S7, Allen-Bradley and final Driver Module expansion remain post-v0.1 owner validation unless the product owner deliberately changes the gate.

## Permanent continuity rules

- No permanent product/coordination decision may exist only in chat history.
- Workers never choose their own next task, modify `main`, merge their own PR or broaden their mission.
- Final wave quality is proven on integrated composition, not inferred from isolated worker PRs.
- Documentation-only coordination commits do not invalidate the logical WaveBaseSHA.
- Research merge is not production implementation.
- Python/visual dependency order remains canonical Script -> Python editor/sandbox -> visual Runtime property/asset model -> graphical editor.
- The editor/renderer never becomes private project truth; canonical Engineering remains authoritative.
- Known failing work is never merged and tests/security/runtime guards are never weakened merely to make CI green.
