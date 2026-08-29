# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-29  
**Merged product state:** **WAVE 07 CLOSED / WAVE 08 NOT MERGED**  
**Active development state:** **WAVE 08 ACTIVE — GRAPHICAL EDITOR GATE GREEN / ENGINEERING DEVELOPMENT MONITOR NOW REQUIRED**  
**CI mode:** **NORMAL — Actions authorized with conservative usage**

## Mandatory resume reading

Before any action read current `main`:
- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `docs/DEVELOPMENT-WAVES.md`
- `docs/CHAT-WORK-ASSIGNMENTS.md`
- `docs/CI-USAGE-POLICY.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/COORDINATOR-HANDOFF.md`
- `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`
- `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`
- current assignment `MustReadSpecific`.

Then verify live GitHub branch/PR/head/CI. GitHub is operational truth.

## Wave 07 — CLOSED

- main merge: `8de706882ba20afedd666532ac41ae11115d06b3`
- post-merge CI #510 / run `33218282760`: **SUCCESS**

## Wave 08 — ACTIVE / NOT MERGED

Integration branch: `integration/graphical-editor-wave-08`  
Draft integration PR: **#90**

### Graphical Editor/Image checkpoint — GREEN

Exact graphical product head: **`a7176a44df3a0af5bc1a271b25101d333da7a161`**  
CI #525 / run `33230239968`: **SUCCESS**

CI #525 proved on that exact product head:

- Web React/Vite/TypeScript build: SUCCESS;
- backend Release build: SUCCESS;
- full backend tests including PostgreSQL/Timescale: SUCCESS;
- Runtime smoke: SUCCESS;
- Chromium E2E: SUCCESS;
- integrated Canvas + Property Inspector + Object Palette + Binding + Image asset workflow: green;
- existing visual/Python/security/runtime regressions remained green.

DEV 1/2/3 original graphical slices were reviewed and integrated by the coordinator. Their worker PRs #91/#92/#93 are closed without direct merge to `main` because their content is incorporated into the central integration train.

The later `[skip ci]` documentation commits that lock the Development Monitor requirement do not invalidate CI #525 evidence for the unchanged graphical product code, but **CI #525 is no longer the final Wave 08 Definition-of-Done gate** because the owner expanded Wave 08 before merge.

## New owner-locked Wave 08 scope — Engineering Development Monitor

Canonical contract:

`docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`

Wave 08 now also requires a read-only Engineering Watch/Monitor Table for development and commissioning analysis.

Required user workflow:

`search OR type exact canonical reference -> add monitored row -> observe live value/type/quality/state/timestamp -> source changes -> row updates -> remove/clear`

Initial source families:

- TAG current values;
- Client Memory and Server Memory;
- authoritative system/runtime variables/diagnostics;
- Data Source / driver diagnostics;
- provider seam extensible to future canonical bit selectors and other development sources.

Required row facts:

- name/reference/path;
- source kind;
- current value;
- canonical data type;
- quality or authoritative diagnostic state;
- source timestamp / last update when defined.

Locked behavior:

- engineer may search/browse or type a known exact TAG/reference directly;
- ambiguous/not-found references fail explicitly rather than silently matching another source;
- live updates reuse shared realtime/subscription paths where available and bounded/coalesced polling otherwise;
- no independent polling loop per monitored row;
- acceptance must prove at least 100 simultaneous monitored entries through shared batching/subscription infrastructure;
- bad/unavailable/stale/disconnected state remains explicit and is never coerced to `0`, `false`, empty string or fake `Good`;
- Int64/exact typed values remain exact;
- monitor is strictly read-only: no TAG/memory writes, forcing, commands, ACK, driver changes or scan-rate changes;
- current values/qualities/timestamps are Runtime/diagnostic state and never become canonical Engineering merely because they are monitored.

## Worker state

- DEV 1: **STOPPED / WAIT_FOR_COORDINATOR** — original Canvas delivery integrated.
- DEV 2: **STOPPED / WAIT_FOR_COORDINATOR** — original Property Inspector delivery integrated.
- DEV 3: **STOPPED / WAIT_FOR_COORDINATOR** — original Palette/Binding delivery integrated.

The Development Monitor requirement does **not** silently reopen any old worker mission. A new assignment must be explicitly recorded on `docs/CHAT-WORK-ASSIGNMENTS.md` before a worker starts it.

## PR #90 merge gate

PR #90 stays **DRAFT / DO NOT MERGE**.

Wave 08 now closes only after both gates are green:

1. **Graphical Editor/Image gate** — currently green at `a7176a4...`, CI #525.
2. **Engineering Development Monitor gate** — specified, not implemented yet.

After Development Monitor implementation, run final exact-head integrated CI, merge PR #90 only if green, then confirm post-merge `main` health.

## Ordered work after Wave 08

- **08-FOLLOW-A:** TAG Bit Access + Driver Bit-Level Boolean Binding.
- **08-FOLLOW-B:** Typed Visual Expressions + Boolean Conditions + Analog Fill.
- **Wave 09:** remains NOT ACTIVE until required preceding work is green.

## Next coordinator execution

1. verify current `main`, integration head, PR #90 and CI evidence;
2. keep graphical checkpoint `a7176a4...` as validated evidence while its code is unchanged;
3. inspect existing TAG realtime, Client/Server Memory and Data Source diagnostic seams;
4. freeze a Development Monitor provider/catalog architecture that reuses authoritative sources rather than creating a second variable model;
5. create explicit parallel-safe worker assignment(s) if useful;
6. integrate and validate the Development Monitor;
7. run one final full Wave 08 matrix on the exact combined head;
8. merge only after both Wave 08 gates are green and verify post-merge main.
