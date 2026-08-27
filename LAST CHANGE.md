# LAST CHANGE — EliteSCADA

> Operational handoff. Read with `PROJECT GOAL.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/CHAT-WORK-ASSIGNMENTS.md` and the current task-specific documents before every EliteSCADA action.

**Handoff date:** 2026-08-27  
**Development state:** **ACTIVE — INTERFACE PRODUCT DEVELOPMENT**

Repository truth remains separated into **MERGED**, **IMPLEMENTED IN PR**, **RESEARCH IN PR** and **SPECIFIED / NOT IMPLEMENTED**.

## CURRENT CHECKPOINT

Merged platform foundations now include:

- Internal Memory: **MERGED / COMPLETE** through PR #49;
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55;
- common multi-driver/Data Source diagnostics: **MERGED / COMPLETE** through PRs #56 and #57;
- PR #57 merge SHA: `c8190cc119a2e288834d619084396107103b2f56`;
- PR #57 exact-head CI #350 and post-merge main CI #351: **all green**;
- canonical Engineering: **Schema v9**;
- current production external protocol baseline remains Modbus TCP plus built-in Simulation/Internal Memory;
- additional MQTT/OPC UA/BACnet/S7/Driver Module production work is postponed;
- provisional Windows x64 validation/presentation packaging is postponed until the interface matures further.

The product owner explicitly reprioritized development on 2026-08-27:

> focus on interface development now because it provides more value than additional drivers or the provisional presentation build.

The active sequence is therefore:

`merged foundations -> interface product development -> user validation build/package -> additional external drivers/protocols`

This scheduling change does not weaken the stable architecture in `PROJECT GOAL.md`; it inserts a deliberate product-UX maturation block before the existing preview/driver gate.

## ACTIVE PRODUCT BLOCK — INTERFACE DEVELOPMENT

Authoritative direction: `docs/INTERFACE-DEVELOPMENT.md`.

Primary goals:

1. coherent EliteSCADA application shell across Runtime, Engineering and Audit;
2. replace floating/developer-style navigation with a proper product navigation model;
3. improve Engineering information architecture, search, scalable entity browsing, master/detail behavior and editor consistency;
4. evolve Runtime beyond a hard-coded demo surface by adding a useful operational overview while preserving the demo process screen;
5. expose authenticated session/user context cleanly;
6. align loading/error/empty/status patterns, responsive behavior and `pt-BR` / `en` / `es` experience;
7. keep healthy states visually quiet and abnormal industrial conditions appropriately emphasized;
8. preserve backend-enforced security, Audit, Engineering lifecycle, TAG quality, Gateway and diagnostics semantics.

The future graphical Screen/Popup/Dynamo editor is **not** opened by this block. It remains governed by the locked chain:

`canonical Script integration -> script editor/sandbox -> visual runtime object/property integration -> graphical editor -> advanced visual libraries`

## BRANCH / PARALLEL STATE

### Coordinator

Active branch:

`feature/interface-product-development`

Coordinator owns central product shell, `main.tsx`, `AppNavigation.tsx`, global UX integration, `EngineeringApp.tsx`, central localization, worker integration, browser tests, CI, merges and docs.

### Parked preview branch

`integration/interface-validation-preview`

State at reprioritization:

- **PARKED / NO PR / NOT MERGED**;
- two commits ahead of the earlier `main` base;
- touched only `src/Scada.Api/Program.cs` and `web/scada-web/src/AppNavigation.tsx`;
- preparatory work is preserved but must not be merged merely to avoid losing it;
- Windows packaging/launcher work resumes later from current product state, not from this older UX snapshot.

### DEV 1

New assignment: Engineering workspace/entity-browser ergonomics primitives.

Branch:

`feature/engineering-workspace-ux`

Status: **ASSIGNED**.

### DEV 2

New assignment: Runtime operational overview UI primitives.

Branch:

`feature/runtime-operations-ux`

Status: **ASSIGNED**.

### DEV 3

New assignment: authenticated session/user-menu UX primitive.

Branch:

`feature/session-ux`

Status: **ASSIGNED**.

Research PR #54 remains separate and unchanged.

Full worker scopes and forbidden files are authoritative in `docs/CHAT-WORK-ASSIGNMENTS.md`.

## OPEN RESEARCH INPUTS

- PR #53 graphical visual editor architecture/UX: **RESEARCH IN PR / DELIVERED**, not production editor implementation.
- PR #54 Client Python editor/browser sandbox: **RESEARCH IN PR / DELIVERED**, not production Python/editor implementation.

These remain useful design inputs but do not override the current interface task scopes or prerequisite chains.

## COORDINATOR RESUME POINT

On the next coordinator `siga`:

1. reread mandatory docs from current `main`;
2. verify `main`, `feature/interface-product-development`, worker branches, PRs and CI;
3. continue central application-shell/navigation work;
4. review and integrate worker UI PRs only after they are delivered and green;
5. improve Engineering and Runtime UX in coherent slices with Chromium coverage;
6. do not resume new drivers or provisional Windows packaging unless product-owner priority changes again;
7. merge only current-head green work and update this handoff after material integration.

## Permanent continuity rules

- GitHub branch/PR/head/CI state is operational truth.
- Open functional PRs are **IMPLEMENTED IN PR**, not MERGED.
- Research branches/PRs are architecture inputs, not implemented product functionality.
- Workers never choose their own next task or merge their own PR.
- Shared central integration belongs to the coordinator unless a narrow exception is explicitly assigned.
- Known-failing work is never merged.
- `siga` is the canonical short command; `continue` is equivalent.
