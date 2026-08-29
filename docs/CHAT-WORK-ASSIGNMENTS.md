# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-29 — Wave 08 remains ACTIVE and unmerged. The graphical editor/image path is green at exact product head `a7176a44df3a0af5bc1a271b25101d333da7a161` with CI #525 / run `33230239968` SUCCESS. The owner added a mandatory Engineering Development Monitor before Wave 08 may close. DEV 1/2/3 original graphical deliveries are integrated and their worker PRs are closed; all workers are STOPPED until a new explicit assignment is issued.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/COORDINATOR-HANDOFF.md`, and current MustReadSpecific documents. Then verify real branch/head/PR/CI and execute only the current authorized assignment.

## Current product gate

`GRAPHICAL-EDITOR-WAVE-08` is **ACTIVE / NOT MERGED**.

Wave 08 now has two mandatory product gates:

1. **Graphical Editor + Image** — **GREEN** on exact product head `a7176a44df3a0af5bc1a271b25101d333da7a161`, CI #525 / `33230239968` SUCCESS.
2. **Engineering Development Monitor** — **OWNER-LOCKED / SPECIFIED / NOT IMPLEMENTED** under `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`.

Draft integration PR: **#90**.  
Integration branch: `integration/graphical-editor-wave-08`.  
PR #90 remains Draft / DO NOT MERGE until both gates are green on the final exact integrated head.

### Graphical delivery state

- DEV 1 Canvas / Selection delivery head `d6542643014e955b013756fd8ee53a5629b8e82a` — integrated; worker PR #93 closed unmerged.
- DEV 2 Property Inspector delivery head `2c974cadafbcc773a4645864440c190f128ea808` — integrated; worker PR #91 closed unmerged.
- DEV 3 Object Palette / Binding delivery head `d614a00dda0903a0f7c78641dc0b803dfd8085df` — integrated; worker PR #92 closed unmerged.

The coordinator composition also normalized visual binding kinds at the canonical boundary and added early typed TAG/property compatibility validation before CI #525.

### Development Monitor locked behavior

Canonical contract: `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`.

Required initial source families:

- TAGs;
- Client Memory and Server Memory;
- authoritative system/runtime variables/diagnostics;
- Data Source / driver diagnostics.

Required user paths:

- search/browse a source and add it;
- type a known exact canonical reference/path and add it directly;
- monitor heterogeneous rows together;
- see value, data type, quality/state and timestamp/last-update;
- remove rows / clear table.

Authority/performance boundary:

- monitor is read-only;
- current samples are Runtime/diagnostic state, never authored Engineering;
- reuse realtime/subscription paths where available;
- bounded/coalesced provider polling only where necessary;
- no one-poll-loop-per-row architecture;
- acceptance proves at least 100 simultaneous rows through shared batching/subscription infrastructure.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `ACTIVE — GRAPHICAL GATE GREEN / DEVELOPMENT MONITOR REQUIRED`  
**IntegrationBranch:** `integration/graphical-editor-wave-08`  
**DraftPR:** `#90`  
**ValidatedGraphicalProductHead:** `a7176a44df3a0af5bc1a271b25101d333da7a161`  
**ValidatedGraphicalCI:** `#525 / 33230239968 — SUCCESS`

**CurrentTask:** preserve the green graphical editor/image checkpoint; inspect existing read-only realtime/memory/diagnostic authorities; freeze and implement the unified Engineering Development Monitor provider/catalog + table workflow; integrate it into Wave 08; run final combined CI; merge only after both Wave 08 gates are green.

**MustReadSpecific:**
- `docs/COORDINATOR-HANDOFF.md`
- `docs/GRAPHICAL-EDITOR-WAVE-08-IMPLEMENTATION-DECISION.md`
- `docs/ENGINEERING-DEVELOPMENT-MONITOR-WAVE-08.md`
- `docs/VISUAL-ASSET-STORAGE-WAVE-08.md`
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`
- current TAG realtime/current-cache APIs and contracts;
- current Client/Server Memory APIs/contracts;
- current Data Source/driver diagnostics APIs/contracts;
- Engineering shell/navigation/localization/security seams.

**AllowedScope:** coordinator integration branch; Development Monitor architecture/provider boundary; central Engineering route/workspace/API/types/localization; read-only backend composition needed to expose canonical monitor sources; tests; explicit worker delegation; final PR/CI/merge.

**ForbiddenScope:** monitor writes/forcing/commands; Wave 09 Screen navigation/Popup/Dynamo product semantics; Wave 10 Python event/tween work; new protocols; Server Python.

**CompletionCriteria:** graphical gate remains green; Development Monitor acceptance from its canonical spec is implemented; final combined Wave 08 exact-head Web/backend/full tests/smoke/Chromium green; PR #90 merged; post-merge main healthy; docs synchronized.

**NextActions:**
1. inspect live authoritative TAG, memory, system/runtime and driver diagnostic sources;
2. define a single monitor source descriptor/sample/provider seam without creating a second variable model;
3. decide parallel-safe worker slices and record new assignments before activating any worker;
4. implement search + exact quick-add + heterogeneous read-only table;
5. prove quality/state/timestamp and exact typed values;
6. prove shared subscriptions/batching for at least 100 monitored rows;
7. run final combined Wave 08 CI and merge only if green.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED / WAIT_FOR_COORDINATOR`  
**PreviousTask:** Canvas / Selection — DELIVERED AND INTEGRATED  
**PreviousDeliveryHead:** `d6542643014e955b013756fd8ee53a5629b8e82a`  
**PreviousPR:** `#93` closed without direct merge.

**CurrentTask:** none.

**Authorization state:** NOT AUTHORIZED for Development Monitor work until this board is explicitly changed.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED / WAIT_FOR_COORDINATOR`  
**PreviousTask:** Property Inspector — DELIVERED AND INTEGRATED  
**PreviousDeliveryHead:** `2c974cadafbcc773a4645864440c190f128ea808`  
**PreviousPR:** `#91` closed without direct merge.

**CurrentTask:** none.

**Authorization state:** NOT AUTHORIZED for Development Monitor work until this board is explicitly changed.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `GRAPHICAL-EDITOR-WAVE-08`  
**Status:** `STOPPED / WAIT_FOR_COORDINATOR`  
**PreviousTask:** Object Palette / Binding — DELIVERED AND INTEGRATED  
**PreviousDeliveryHead:** `d614a00dda0903a0f7c78641dc0b803dfd8085df`  
**PreviousPR:** `#92` closed without direct merge.

**CurrentTask:** none.

**Authorization state:** NOT AUTHORIZED for Development Monitor work until this board is explicitly changed.

## Follow-up ordering

After the complete Wave 08 gate, the separately locked sequence remains:

1. `08-FOLLOW-A` — TAG Bit Access + Driver Bit-Level Boolean Binding;
2. `08-FOLLOW-B` — Typed Visual Expressions + Boolean Conditions + Analog Fill;
3. Wave 09 only after required preceding work is green.
