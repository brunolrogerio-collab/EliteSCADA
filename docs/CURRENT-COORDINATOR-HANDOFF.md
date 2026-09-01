# EliteSCADA — Current Coordinator Handoff

Last operational audit: **2026-09-01 BRT**  
Operational status: **WAVE 11 ACTIVE — issue #194 / `coordination/wave11-hmi-runtime`**

> **THIS FILE IS THE SINGLE OPERATIONAL HANDOFF FOR COORDINATOR CONTINUITY.**
>
> Live GitHub refs and exact-head Actions evidence override SHAs copied into prose. Stable product intent is governed by `PROJECT GOAL.md`. Mutable exact state belongs in `LAST CHANGE.md`.

## 1. Mandatory resume protocol

A replacement Coordinator must read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/ROADMAP.md`;
5. live `main`, issue #194, `coordination/wave11-hmi-runtime` and latest Actions;
6. `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` for completed Driver evidence;
7. issues #174, #180, #183 and #191 as historical acceptance authority.

Repository state, not old chat messages, is the continuity source.

## 2. Last accepted mainline code checkpoint

Pre-Wave-11 issue #191 is **COMPLETE / ACCEPTED / INTEGRATED**.

- PR #193 — merged;
- validated main code SHA: `64ba134f88df61233c492f6c5e2b1ea8f244bf19`;
- implementation head: `aeb9b3b5641adee344c4ead166b97cc0adba3dbf`;
- Preview Licensing CI #92 / `33527294658`: **SUCCESS**;
- EliteSCADA CI #1035 / `33527294657`: **SUCCESS after unchanged rerun of a transient IEC-104 timing assertion**;
- final jobs: backend build/tests/runtime smoke **SUCCESS**, Web build **SUCCESS**, Chromium E2E **SUCCESS**.

Pre-merge exact-head evidence:

- EliteSCADA CI #1033 / `33525910566`: **SUCCESS**;
- Preview Licensing CI #90 / `33525910582`: **SUCCESS**;
- L3 Seven-Driver Lab #39 / `33525910552`: **SUCCESS**.

Documentation-only synchronization after the validated code merge uses `[skip ci]` and does not supersede that code validation checkpoint.

## 3. Pre-Wave-11 accepted scope

- graphical Windows License Generator on double-click, with controlled CLI retained;
- external-only private RSA signing PEM;
- canonical industrial `core.slider` with passive and protected/audited interactive behavior;
- explicit developer-selected `.escadapkg` Save Application As / Open Application workflow;
- canonical built-in eight-Dynamo starter library;
- Wave 13 Authenticode + trusted timestamp requirement retained.

Post-main License Generator artifact:

- artifact id `9808306320`;
- `EliteSCADA.LicenseGenerator.exe`, 116,257,103 bytes;
- PE32+ Windows GUI x86-64;
- SHA-256 `841dea832d67f44e07aa10b2de96ccfffd5d518beeadafb48ed34e16d0317523`.

Historical #184 is closed as superseded/resolved. #191 is closed completed.

## 4. Current active stage — Wave 11

- issue: **#194 — Wave 11 — Active Engineering HMI Runtime vertical slice**;
- state: **OPEN / ACTIVE**;
- branch: `coordination/wave11-hmi-runtime`;
- branch was initially created from documentation-synchronized `main` SHA `4be2cb68225cc4222f768ef34a6ed3c808391400`;
- follow-up Wave 11 activation docs were then committed to `main`; before code work, fast-forward the still-code-empty branch to the latest `main`.

Wave 12 remains blocked until #194 passes and closes.

## 5. Wave 11 architectural target

Replace the current hand-authored process surface in `web/scada-web/src/main.tsx` as Runtime application truth with the **active persisted canonical Engineering revision**.

Authoritative lifecycle:

`Working -> saved Revision -> Published -> Active -> HMI Runtime projection`

Working/unsaved state must never silently appear in Runtime.

### Existing foundations that MUST be reused

- canonical Screen/Popup/Dynamo Engineering model;
- `RuntimeVisualDefinitionRenderer` / `CanonicalVisualRenderer`;
- runtime visual catalog and Screen/Popup navigation action model;
- Dynamo composition and `{equipmentPath}` substitution;
- Client Visual Python/event runtime;
- realtime TAG values;
- protected/audited Runtime TAG write boundary;
- alarm center, TAG Inspector, Trends and Historical Data Browser;
- PostgreSQL lifecycle with Active revision;
- persistence service internal `LoadActiveAsync(projectKey)` capability.

### Required Wave 11 slices

**A. Protected active-revision projection**

Create a protected backend boundary that returns the active canonical Engineering package for the configured/runtime project. It must fail closed for missing persistence, no active revision, project/revision inconsistency or malformed canonical content, and must not fall back to Working state.

**B. Canonical HMI Runtime mount**

When Engineering runtime is active, the default Runtime must resolve active project/revision, load the matching package, create the canonical visual catalog and render active Screen/Popups/Dynamos through the existing renderer/navigation stack. Protected Slider writes and Client Visual behavior stay on their established paths.

A separate simulation fallback may remain only when no Engineering runtime is active.

**C. Lifecycle isolation evidence**

Tests must prove Active A renders; Working changes do not affect Runtime; activation of B changes Runtime; missing/inconsistent active projection fails closed; navigation/Popup/Dynamo remain canonical; protected writes remain protected; no frontend-to-Driver bypass is introduced.

## 6. Wave 11 acceptance gate

Before #194 closes:

1. dedicated branch from live synchronized `main`;
2. focused backend/frontend/lifecycle tests;
3. exact implementation SHA normal EliteSCADA CI green;
4. normal PR integration to `main`;
5. exact post-main CI green;
6. `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, this handoff and #194 synchronized;
7. no test/security/lifecycle weakening.

## 7. Exact next action

1. fast-forward `coordination/wave11-hmi-runtime` to current `main` while it still has no Wave 11 code;
2. inspect current active persistence and visual-navigation tests/contracts;
3. implement Slice A first;
4. implement Slice B on top of existing canonical renderer/navigation;
5. add Slice C evidence;
6. run exact-head CI and continue until integration or a real blocker.

## 8. Non-negotiable rules

- Repository/CI state overrides stale chat/prose for implementation truth.
- Stable product rules belong in `PROJECT GOAL.md`; mutable exact state belongs in `LAST CHANGE.md`.
- No red CI into `main`.
- Do not weaken tests to manufacture green evidence.
- No Driver-to-Driver calls or canonical TAG/cache/event bypass.
- No plaintext protected material.
- `CommunicationBinding` remains canonical in schema v15.
- Licensing remains host-owned; Drivers never inspect license files/hardware IDs directly.
- Private license-signing material never enters GitHub, CI or distributed product binaries.
- L2 does not imply L3; L3 does not imply physical L4.
- Every material coordination transition must be persisted before reporting completion.
