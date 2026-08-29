# COORDINATOR HANDOFF — EliteSCADA

> Persistent coordinator resume checkpoint. Read this with the mandatory current `main` documents, then verify live GitHub branch/PR/head/CI before acting.

**Handoff date:** 2026-08-29  
**Current wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Merged product:** Wave 07 closed; Wave 08 unmerged  
**Wave status:** **ACTIVE — GRAPHICAL EDITOR/IMAGE GATE GREEN / ENGINEERING DEVELOPMENT MONITOR REQUIRED**  
**CI policy:** `NORMAL`; Actions authorized with conservative usage

## Exact checkpoint

- integration branch: `integration/graphical-editor-wave-08`;
- Draft integration PR: **#90**;
- graphical product head: **`a7176a44df3a0af5bc1a271b25101d333da7a161`**;
- full CI #525 / run `33230239968`: **SUCCESS**;
- documentation-only successors use `[skip ci]` and do not alter graphical product behavior;
- PR #90 remains Draft because the owner expanded Wave 08 with a mandatory Development Monitor before merge.

## Graphical gate — validated

CI #525 proved the fully composed graphical editor/image path:

- Web React/Vite/TypeScript build: SUCCESS;
- backend Release build: SUCCESS;
- full PostgreSQL/Timescale tests: SUCCESS;
- Runtime smoke: SUCCESS;
- Chromium E2E: SUCCESS;
- Canvas + selection/move/resize/rotate;
- Property Inspector;
- Object Palette;
- canonical Binding authoring with early type compatibility validation;
- project Image import + stable `assetRef`;
- Preview/Apply/CAS -> export -> reopen;
- transient Canvas state not persisted;
- prior visual/Python/security/runtime regressions green.

Worker deliveries incorporated into the integration train:

- DEV 1 head `d6542643014e955b013756fd8ee53a5629b8e82a`; PR #93 closed unmerged after coordinator integration.
- DEV 2 head `2c974cadafbcc773a4645864440c190f128ea808`; PR #91 closed unmerged after coordinator integration.
- DEV 3 head `d614a00dda0903a0f7c78641dc0b803dfd8085df`; PR #92 closed unmerged after coordinator integration.

All three worker chats are now STOPPED / WAIT_FOR_COORDINATOR.

## New mandatory Wave 08 requirement

Canonical contract:

`docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`

Owner intent: provide a development/commissioning Watch Table inside Engineering so an engineer can inspect variable behavior without creating a temporary HMI or temporary code.

Minimum flow:

`open monitor -> search OR type exact canonical reference -> add -> see live value/type/quality/state/timestamp -> observe changes -> remove/clear`

Initial provider families:

- TAGs;
- Client Memory;
- Server Memory;
- authoritative System/Runtime variables/diagnostics;
- Data Source / driver diagnostics.

The architecture must use a unified provider/catalog seam. Do not build separate unrelated tables or a second variable namespace for each family.

### Locked monitor semantics

- search by canonical name/path/reference and useful metadata;
- direct exact-reference quick-add when the engineer already knows the TAG/variable;
- explicit not-found/ambiguous behavior, no silent fuzzy substitution;
- heterogeneous rows in one table;
- minimum facts: reference/name, source kind, current value, canonical data type, quality or diagnostic state, timestamp/last update;
- preserve exact typed values, especially Int64;
- missing/bad/unavailable/stale/disconnected is explicit and never coerced into normal values;
- reuse existing realtime/Event Bus/WebSocket/subscription paths when authoritative;
- bounded/coalesced polling only where needed;
- never one independent backend poll loop per row;
- acceptance proves at least 100 simultaneous rows using shared provider batching/subscription infrastructure;
- monitor is strictly read-only;
- adding/removing monitor rows must not alter scan rate, driver configuration, TAG policy or process outputs;
- current monitored samples are Runtime/diagnostic state and never authored Engineering/project package state.

## Important scope decision

The new owner requirement does **not** reopen the completed DEV 1/2/3 graphical missions automatically.

Before any worker starts Development Monitor code, the coordinator must inspect the existing authoritative source seams, define parallel-safe ownership and update `docs/CHAT-WORK-ASSIGNMENTS.md` with a new explicit assignment.

## Existing authorities to inspect before implementation

On next execution, inspect rather than reinvent:

1. TAG registry/current-value cache and protected Runtime TAG read/realtime WebSocket/Event Bus paths;
2. Client Memory contracts and browser-local runtime identity/lifecycle;
3. Server Memory contracts and protected read endpoints;
4. current Data Source/common driver diagnostics model/API;
5. Runtime/service/system diagnostic facts already public;
6. Engineering shell/navigation/localization/security patterns;
7. batching/subscription possibilities so the monitor does not poll per row.

If a category has no public read-only seam, add only the minimum bounded API/provider needed. Do not expose private driver objects or secrets.

## Development Monitor persistence boundary

The live samples are not Engineering.

Never save/export/package current:

- values;
- qualities;
- timestamps;
- transient communication state;
- observed errors.

The watchlist itself may initially be session/user-workspace state. If persisted for convenience, only canonical references/order/display preferences may be stored and must remain separate from process logic. Named project-portable Watch Tables require a later explicit decision if desired.

## Current Wave 08 Definition of Done

Wave 08 now closes only after both are green:

### Gate A — Graphical Editor/Image

**GREEN** at `a7176a4...`, CI #525.

### Gate B — Engineering Development Monitor

**SPECIFIED / NOT IMPLEMENTED.** Acceptance is defined in `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`.

After Gate B implementation:

- run final full Web/backend/tests/smoke/Chromium on one exact combined head;
- PR #90 may leave Draft only after exact-head evidence is green;
- merge to `main`;
- verify post-merge main health;
- synchronize docs.

## Follow-up order after Wave 08

The owner-locked order remains:

1. `08-FOLLOW-A` — TAG Bit Access + Driver Bit-Level Boolean Binding;
2. `08-FOLLOW-B` — Typed Visual Expressions + Boolean Conditions + Analog Fill;
3. Wave 09 only after required preceding work is green.

Bit selectors introduced in 08-FOLLOW-A must later participate through the same monitor provider/catalog seam, not through monitor-private `.NN` parsing.

## Resume procedure

On coordinator `siga`:

1. reread current-main mandatory docs and the Development Monitor spec;
2. verify live main/integration/PR #90/CI and that DEV 1/2/3 remain stopped unless explicitly reassigned;
3. preserve CI #525 as graphical checkpoint while graphical code is unchanged;
4. inspect TAG/memory/diagnostic source seams and authorization;
5. freeze monitor source descriptor/sample/provider contract;
6. split implementation into parallel-safe worker scopes if useful and explicitly authorize them;
7. integrate Development Monitor behind the read-only authority boundary;
8. validate focused behavior and capacity architecture;
9. run one final combined full matrix;
10. merge only green.
