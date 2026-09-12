# Wave 15 — Current Coordinator Handoff — 2026-09-11

**STATUS:** ACTIVE / MAIN COORDINATOR HANDOFF

GitHub live is the official memory and sole authority over project state. This document is an operational snapshot only. Before any decision, code/document change, PR action, workflow rerun, integration or merge, revalidate the live GitHub state. If this handoff conflicts with newer GitHub evidence, the newer live evidence prevails.

Repository: `brunolrogerio-collab/EliteSCADA`

Primary Wave 15 ledger: issue #297

Execution/dependency ledger: issue #305

Wave 15 integration branch: `wave15/corrections-integration`

Validated Wave 15 starting baseline and integration head at handoff creation: `efbb81699a7fd4cb342fa75aeca97966008a100a`

Wave 13 #205 / PR #207 remains PAUSED pending a separate Product Owner maturity decision.

## 1. Wave 15 product goal

Wave 15 is a **complete-product delivery wave**, not only a defect-correction wave. The end state expected before Product Owner Preview includes:

1. Wave 14 generic product corrections materially closed;
2. developer-functional Screen/Popup Engineering;
3. developer-functional Script Engineering;
4. configurable Authority with roles/capabilities/hierarchy scopes;
5. Runtime viewer/session licensing shared by Web Runtime and EliteGO, including Demo quotas and redundancy entitlement;
6. EliteGO implemented as a Runtime-only client of EliteSCADA;
7. Redundancy/HA implemented with explicit authority/fencing/session continuity contracts;
8. one-installation developer workflow for detach/switch Application + Authority and separately keep/remove/replace License;
9. generic remote/WAN latency resilience and truthful reconnect/stale/error behavior;
10. optimized CI proportional to change risk rather than full Driver matrix for every DEV PR;
11. industrial-quality symbols/Dynamos and real visual previews in Library/Dynamos;
12. updated Manual/Help, complete pt-BR/en/es localization, EEE Simulation v15 and EEE Real Modbus v15;
13. coherent complete-product integration and a **fresh Codespaces Preview** from the final exact candidate;
14. real browser/Product Owner audit, correlated diagnostics and targeted residual corrections.

Preview is downstream of Productization; #300 must not be treated as the next immediate step after feature coding.

## 2. Core Wave 15 issues

- #297 — principal Wave 15 coordination ledger.
- #298 — EliteGO Runtime-only client, server licensing and seamless host failover.
- #299 — Redundancy / HA authority, topology service and Runtime Session continuity.
- #300 — complete-product package and fresh Codespaces Preview.
- #301 — shared Web/EliteGO Runtime viewer quotas, redundancy entitlement and License Generator v2.
- #302 — configurable Authority roles, granular capabilities and hierarchy-scoped access.
- #303 — single-canvas WYSIWYG Screen/Popup authoring, icon-first tools and compact Property Inspector.
- #304 — detach/switch Application + Authority and independently manage License without reinstall.
- #305 — dependency graph, F0 gates, GPT DEV orchestration and CI policy.
- #306 — Productization: Manual/Help, localization, EEE Simulation/real-Modbus demos and Preview readiness.
- #307 — high-priority remote/WAN timing, timeout and reconnect resilience.
- #308 — visual quality, industrial symbols/Dynamos and Library/Dynamo visual previews.

Read the latest comments on these issues because their bodies/comments evolved during planning.

## 3. EliteGO binding product direction

EliteGO must be developed in Wave 15. It is **Runtime-only** and does not have its own license, Drivers, Historian, Engineering authority, project authority or hidden SCADA runtime.

The normal EliteSCADA Web Runtime already supports browser access. EliteGO exists as a dedicated Runtime client/product, particularly to provide a resilient user experience around distributed/HA operation while consuming public/authenticated EliteSCADA contracts and the canonical Runtime renderer.

On first use, EliteGO asks for one EliteSCADA destination. On successful server bootstrap it receives and stores the server topology package containing four access paths for two logical nodes:

- Host Local A;
- Host Remote A;
- Host Local B;
- Host Remote B.

The four endpoints are connectivity paths, not four independent server authorities. If the connected path fails or Active changes, EliteGO automatically selects another valid host, validates cluster/node/Active identity, resumes/revalidates the logical Runtime Session Lease, reacquires the Active snapshot and resubscribes realtime. EliteGO never elects the industrial Active node itself.

EliteGO should render the canonical Runtime experience: Screens, Popups, Dynamos/assets, TAGs, alarms, Operational Events, Trends/Historian and authorized commands according to the final Runtime contracts and Authority.

Binding source to reread: `docs/ELITESCADA-DISTRIBUTED-RUNTIME-HA-ROADMAP.md` plus `docs/WAVE14-C25-DISTRIBUTED-RUNTIME-FOUNDATION.md`.

## 4. Licensing product decisions

Licensing counts remote Runtime clients regardless of whether the client is Web Runtime or EliteGO. The pools are shared, not client-specific.

Demo policy decided by Product Owner:

- the Runtime local to the EliteSCADA installation remains available and does not consume the additional remote-viewer quota;
- +2 concurrent **Interactive** remote Runtime Session Leases;
- +2 concurrent **View Only** remote Runtime Session Leases.

Commercial licensing must separately encode/configure Interactive and View Only capacity.

Both Web Runtime and EliteGO must present a pre-login `Somente visualização / View Only` choice. If a user requests Interactive and that pool is full but View Only has capacity, the server grants View Only and the client clearly informs the user of the downgrade. If both pools are full, admission is rejected. If View Only was explicitly requested and its pool is full, do not consume an Interactive seat against the user request.

A user whose Authority is permanently read-only should receive/consume View Only even if the checkbox was not selected.

View Only is enforced server-side. Hiding controls in the client is not sufficient; writes, Operational Commands and other process mutations must be rejected at the backend for a View Only session.

The unit is a logical Runtime Session Lease, not HTTP/WebSocket count. Reconnect and A→B failover for the same logical client must not consume a second seat.

Redundancy licensing is separate and machine-bound: each HA machine requires its **own valid license**, and that license must carry a redundancy entitlement/flag. A license bound to machine A never authorizes B. The License Generator GUI/CLI must be upgraded accordingly. A failover must never silently increase effective licensed capacity.

## 5. Authority product decisions

Do not solve Authority by adding more hard-coded user types. The existing architecture already points toward accounts + roles + capabilities + scopes.

Profiles such as Administrator, Engineer, Plant Manager, Supervisor, Operator and Viewer are editable templates/default roles, not a rigid privilege hierarchy. Custom roles must be possible.

Effective authorization is conceptually:

`user grants/capabilities + hierarchy/scope + restrictive current session class`.

Hierarchy/scopes must be able to constrain plant/area/equipment/TAG/screen/command using stable identifiers and descendant semantics.

Important distinction already present in the security model: `CommandExecute` is not `ProcessValueWrite`. An Operator may, for example, be permitted to execute an engineered manual pump command while being forbidden from arbitrary setpoint writes. A Plant Manager may have broad Runtime control for one plant with no Engineering modification access.

Web Runtime and EliteGO consume the same backend Authority. EliteGO must not create a parallel permission system.

## 6. Screen/Popup Editor product decisions

The current split model — authoring canvas plus a separate preview below — is considered inadequate for professional development.

Wave 15 must move to a **single-canvas WYSIWYG** model: the Working Screen/Popup being edited is also the visual representation the developer sees and manipulates. Selection, move, resize, properties, bindings and visual changes should update the same canvas immediately.

Whenever technically safe, reuse the canonical Runtime renderer and add Engineering-only overlays such as selection bounds, resize handles, guides, grid and snap. This does **not** change lifecycle authority: Working remains Working; Runtime still uses Published/Activated Active.

A dynamic Preview mode may exist as `Design <-> Preview` in the same viewport, but the normal workflow must not require a second complete preview underneath the editor.

Frequent editor actions should be icon-first, compact and grouped. Hover/focus reveals the function name and shortcut. Include at least selection/pan/zoom/grid/snap, undo/redo/copy/paste/duplicate/delete, grouping/lock/z-order, object align/distribute and separate text align/justify actions. Icons must reflect enabled/disabled/toggled state.

The Property Inspector must also be redesigned for engineering density: compact fields, grouped/collapsible sections, type-appropriate editors, efficient X/Y/W/H editing where relevant, keyboard flow, local validation errors, immediate WYSIWYG updates and a resizable/collapsible panel.

Preserve strict unknown visual type validation. Known persisted legacy types must have generic compatibility/migration/safe-degradation so selecting them does not blank the SPA.

## 7. Developer installation switching

A developer should be able to use one EliteSCADA installation for multiple customer/projects over time without reinstalling.

Provide an authorized **detach Application + Authority** workflow that safely returns the installation to a neutral/bootstrap state where a new project can be created or another package/Authority can be imported/restored.

Before detach, recommend separate exports of Application (`.escadapkg`) and Authority and warn about unsaved Working. The exports are recommended, not mandatory after explicit confirmation.

Detach must fence the old project safely: stop/prevent old Runtime process effects according to product contracts, block new commands, handle Drivers/Server Scripts safely and invalidate sessions/tokens belonging to the old Authority. Project A must not remain a hidden industrial authority after the UI has switched to Project B.

Application, Authority, Historian/DB and License remain separate authorities. Do not silently erase Historian as part of detach.

License may be retained across project changes. Provide separate authorized operations to remove license (returning to Demo) or replace license. Validate a replacement license completely before replacing the valid current license.

## 8. Remote/WAN latency resilience — FND-08 / #307

This is high priority and must influence downstream Wave 15 development. Wave 14 showed that local API/Vite could be fast while remote/forwarded browser paths experienced large delays and transient failures. The product requirement is generic remote/WAN resilience, not a Codespaces special case.

Do not solve this by multiplying every timeout. Establish timing/error/reconnect policy by operation class. Safe idempotent reads may use bounded retry/backoff; process commands/writes and lifecycle mutations must never be blindly replayed because a delayed/lost response can mean an **unknown outcome**.

Preserve last known-safe Runtime projection when the same last-verified authority identity is still applicable, while truthfully indicating slow/retrying/stale/offline. Late/out-of-order responses must not overwrite newer state.

Security/JWT lifetimes, Server Script execution sandbox limits, Driver protocol timing and HA fencing/authority timing remain separate strict domains and must not become adaptive simply because WAN RTT is high.

Work performed a detailed read-only audit in #307 comment `5638692168`; summary is in #297 comment `5638695060`. Proposed slices include central timing/error taxonomy, shared request executor, Engineering long-operation migration, Runtime projection/freshness, realtime supervisor and Session Lease/EliteGO continuity. Revalidate those comments before implementation.

## 9. Productization before Preview

#306 is a mandatory PREVIEW-READY gate before #300.

Manual/Help already has an existing foundation from Wave 14, including contextual help/topic structure and pt-BR/en/es coverage, but the content is still too simple and must be rewritten for the final Wave 15 product and all new surfaces.

All new user-facing Wave 15 UI must respect the multilingual requirement `pt-BR`, `en`, `es`. Translation must never modify TAG paths, IDs, addresses, enums, schema values or Runtime semantics.

Create/update two EEE product examples before Preview:

- **EEE Simulation v15** — deterministic, polished and suitable for Preview;
- **EEE Real Modbus v15** — based on `docs/WAVE14-C11-EEE-REAL-REFERENCE-MAPPING.md`.

The real Modbus mapping is authoritative where evidence exists. Do not invent scaling; preserve `TO VALIDATE WITH REAL PLC` where the physical conversion is not proven. Do not infer write/command semantics merely from a TAG/PLC variable name.

The current built-in drawings/Dynamos are too simplistic. Before Productization closes, #308 requires professional, original, consistent minimalist industrial symbols for common assets such as valves, motors, pumps, compressors, tanks and instrumentation. Library/Dynamos must show **real visual previews/thumbnails**, not only object names.

## 10. CI/Actions optimization

Wave 15 should not run the full industrial Driver matrix after every small DEV delivery. Maintain evidence proportional to risk:

- T0 — local/focused tests;
- T1 — DEV PR minimal build/sanity + its validation profile;
- T2 — coherent package integration broader tests;
- T3 — major checkpoints such as FC0-A/FC0-B/PREVIEW-READY;
- T4 — exact final candidate full matrix, including Driver labs and all applicable product gates.

If a change touches Driver/transport/TAG routing/shared industrial communication semantics, the relevant heavy Driver evidence becomes immediately required.

Existing workflows were largely designed around `main` and Wave 14 branches. `INFRA-CI-01` should establish a Wave 15 T1 path for PRs targeting `wave15/corrections-integration` instead of attaching every heavy workflow to every DEV PR. Preserve heavy workflows for meaningful checkpoints/manual dispatch as appropriate. Diagnose red before any rerun.

## 11. F0 dependency model and parallel GPT DEVs

Foundations move through:

`NOT_STARTED -> ACTIVE -> PR_READY -> INTEGRATED -> VERIFIED -> FROZEN`

A downstream DEV may consume a foundation only after the Main Coordinator explicitly marks it `VERIFIED / FROZEN FOR DOWNSTREAM CONSUMPTION` on an exact integration SHA.

Current foundation families:

- FND-01 — Working/Lifecycle bootstrap;
- FND-02 — Authority Core;
- FND-03 — Runtime Session Lease + Licensing v2;
- FND-04 — Server Script recovery/ownership;
- FND-05 — HA authority/fencing/topology;
- FND-06 — renderer + visual stability;
- FND-07 — Installation detach primitives;
- FND-08 — remote/WAN timing resilience;
- INFRA-CI-01 — optimized Wave 15 T1 CI route.

Do **not** release arbitrary parallel DEVs before their dependencies are frozen. FC0-A is intended to release a first batch such as Editor, Script Engineering, Authority UX and Licensing UX after the required common foundations/CI route are frozen. FC0-B releases EliteGO, Installation UX and delegable HA downstream work only after their stronger dependencies are frozen. Revalidate the latest #305 comments for the exact gate definition because FND-08 was added after the first DAG version.

Every parallel DEV must receive a closed work contract: DEV-ID, exact base SHA, dependencies, allowed/prohibited files/components, frozen contracts, tests, branch/PR target and handoff format. A DEV that needs to change a frozen/shared foundation must stop with `BLOCKED-CONTRACT`, not improvise a parallel architecture. DEVs do not merge their own PRs.

## 12. Current active technical item — PR #309 / FND-01

PR #309: `[Wave 15][FND-01] Restore deterministic persisted Working bootstrap`

Base: `wave15/corrections-integration`

At the last live revalidation before this handoff, PR #309 remained OPEN / non-draft / mergeable and still reported head:

`386602b4c229e267e6db8ae14dec7f3f60c40d29`

Do not trust that SHA without revalidating again.

The Codex implementation introduced deterministic persisted Working bootstrap and removed unconditional persisted-mode Demo seeding. The first Work review (#297 comment `5638644406`) classified it **CHANGES REQUIRED**, not because the implementation direction was broadly wrong, but because FND-01 is a shared lifecycle authority and acceptance evidence was incomplete.

Work findings:

- **F1** — new tests used a `RecordingCheckout` that directly set workspace checkout and therefore did not prove real/composed startup + persistence + Working checkout + Active recovery, nor durable non-mutation of Published/Active;
- **F2** — no direct regression proving `Active=A`, explicit `Working=B`, activation of B rejected and activation service not invoked;
- **F3** — fallback project-key ordering used only `OrdinalIgnoreCase`, so case-colliding keys such as `Plant` / `plant` could depend on incoming catalog order; require a final ordinal tie-break and reversed-input regression;
- also require explicit invalid project/revision fail-closed coverage where not already proven.

The Main Coordinator authorized Work in #297 comment `5639765762` to add **one bounded correction commit on the existing PR #309 branch**, addressing only F1/F2/F3 and directly necessary test seams. PR #309 comment `5639771895` records that no merge/freeze is allowed until the new exact head is reviewed.

At handoff creation, GitHub still showed the old #309 head and no `WORK -> MAIN COORDINATOR — PR #309 FND-01 CORRECTION HANDOFF` comment yet. The Product Owner reported that Work had made a delivery, so the **first action of the next coordinator must be live revalidation** of #297 and #309 for a newly-pushed commit/handoff that may have landed after this snapshot.

Work proved its environment can install and use the repository-pinned `.NET SDK 10.0.400` without Codespaces. It used the official user-local/ephemeral install, `dotnet --info` showed SDK 10.0.400 / MSBuild 18.9.6, serial focused restore succeeded, and focused build/tests on the original #309 head passed. Therefore Work is suitable as a temporary Foundation DEV while Codex quota is unavailable.

## 13. Agent/resource policy

**Main Coordinator** — principal coordination, classification, dependency freeze, integration order and repository memory. Must always revalidate GitHub live.

**GPT Codex** — technical co-coordinator/Foundation DEV and the preferred agent when direct Codespace/browser/process/runtime/ports correlation is truly needed. Product Owner reports Codex weekly availability is exhausted until **2026-09-16**. Do not waste Codex/Codespace quota on GitHub reading or ordinary repository editing that another environment can perform.

**ChatGPT Work** — temporary Foundation DEV / technical reviewer. Use GitHub first and its own cloud/local environment second. It must **not use Codespaces**. It has now demonstrated that it can install the pinned .NET SDK and run focused builds/tests itself.

Codespaces should be reserved for browser-real/runtime/realtime/HA/process validation that genuinely requires it, and later for the fresh #300 Preview. GitHub and ordinary build/test work should not be made dependent on an open browser Codespace.

## 14. Permanent guardrails

Never mutate `main` directly. `siga` authorizes autonomous safe progression but never substitutes explicit Product Owner authorization for a protected final merge to main.

Do not force-push, destructively rebase, delete evidence branches, weaken tests or blind-rerun failed workflows.

Never weaken Security, Identity, authentication/authorization, Engineering Lock, Licensing, lifecycle, package contracts, Active Runtime authority, Historian semantics or Drivers to make a test pass.

Runtime/Active remains independent of `.escadalib`.

Alarm, Operational Event and Audit remain distinct authorities.

Do not use EEE-specific workarounds to hide generic product defects.

Wave 13 #205/#207 remains paused.

Historical validation/preview PRs marked MUST NEVER MERGE remain evidence only; do not resurrect them into the Wave 15 route.

## 15. Immediate next actions for the next Main Coordinator

1. Revalidate #297 latest comments, PR #309 exact head/diff/comments/checks and `wave15/corrections-integration` exact head.
2. Determine whether Work's bounded F1/F2/F3 correction has actually landed. The Product Owner says Work made a delivery, but it was not yet visible in the last GitHub read when this handoff was written.
3. If a new #309 head exists, review exactly that diff, Work test evidence and lifecycle invariants. Do not accept the old head by mistake.
4. If F1/F2/F3 are closed, integrate #309 into `wave15/corrections-integration` through the authorized PR route, validate the exact integrated SHA with the appropriate proportional evidence, and only then declare FND-01 `VERIFIED/FROZEN FOR DOWNSTREAM CONSUMPTION` in #305/#297.
5. After FND-01 disposition, move to high-priority FND-08/#307 in bounded slices, starting from the read-only timing audit. Keep it isolated from FND-01.
6. Continue F0 according to #305. Do not release parallel GPT DEVs until their exact dependency gate is met.
7. Keep GitHub as operational memory after every substantial stage.
