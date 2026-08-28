# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-28  
**Development state:** **WAVE 05 MERGED — WAVE 06 DEFINITION OF READY**  
**CI budget mode:** **CONSTRAINED until the GitHub Actions allowance resets on 2026-09-01**

Repository truth remains separated into `MERGED`, `IMPLEMENTED IN PR`, `MERGED_TO_INTEGRATION`, `RESEARCH MERGED / PRODUCTION NOT IMPLEMENTED` and `SPECIFIED / NOT IMPLEMENTED`.

## Mandatory resume reading

Read current `main` before action:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/ROADMAP.md`;
4. `docs/PARALLEL-WORK.md`;
5. `docs/DEVELOPMENT-WAVES.md`;
6. `docs/CHAT-WORK-ASSIGNMENTS.md`;
7. `docs/CI-USAGE-POLICY.md`;
8. `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`;
9. current assignment `MustReadSpecific`.

GitHub branch/PR/head/CI state is operational truth.

## CI BUDGET CONSTRAINT — ACTIVE

GitHub Actions usage is near the included monthly allowance. Until reset on 2026-09-01, `docs/CI-USAGE-POLICY.md` is mandatory for Coordinator and DEV 1/2/3.

Use focused validation during iteration, reuse exact-head evidence, never rerun unchanged heads for reassurance, and reserve full matrices for meaningful integration/final gates. Final quality requirements remain unchanged. If a mandatory final matrix cannot run, use `BLOCKED_BY_CI_BUDGET`; never merge with weaker evidence.

## Wave 04 — MERGED

PR #78 merge: `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`. Final CI #446 fully green.

## SCRIPT-WAVE-05 — MERGED

**Logical WaveBaseSHA:** `e9e596f482c83bf5864b34a7f54d9fd3b0b67baa`  
**Frozen central ContractSHA:** `b08b45201bf25a6d4d403b07c511cc34444177db`  
**Final integration head:** `13d3f8283275dc957d9d6168fc7fb165df992d7e`  
**Final CI:** #466 / run `33139334379` — Web SUCCESS, backend Release/full tests including PostgreSQL SUCCESS, Runtime smoke SUCCESS, Chromium SUCCESS  
**Coordinator PR:** #79 — MERGED  
**Main merge:** `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`

### Merged Wave 05 product

- canonical `scada.engineering` schema v10;
- first-class `Scripts` and `ScriptVisualEventReferences`;
- stable Script ID/path ownership in the Engineering Workspace;
- normal dirty/changeVersion semantics;
- canonical JSON Import/Export and Preview/Apply;
- v9 backward compatibility;
- `.escadapkg` fidelity;
- PostgreSQL immutable revision fidelity for Script source/metadata/state;
- protected Script read endpoints;
- dependency-safe Script delete through normal CAS/security/Audit;
- deterministic stable reference resolution for TAG, Client Memory, Server Memory and Screen/Popup/Dynamo definitions;
- Client Memory resolves only as `ClientMemoryTag`; Server Memory resolves as generic `Tag` plus `ServerMemoryTag`;
- no fabricated VisualObject IDs before the future visual identity contract;
- practical Script Engineering Workspace mounted in Engineering, with list/select/create/edit/delete, plain source editing, entry points and dependencies;
- create/update uses canonical Preview before Apply with exact Workspace CAS;
- browser acceptance covers Script workspace presence and explicitly confirms that Python execution is not yet part of Wave 05.

### Worker deliveries

- DEV 3 PR #80, head `69042461228108c74ef3fb921150e56910166141`: compatibility/acceptance validation; CI #459 fully green;
- DEV 2 PR #81, head `c31f26178660ab6831b90026cee60cbe619e0adc`: stable reference resolver. Worker full CI encountered unrelated known-flaky Modbus diagnostics timing; build/focused resolver tests passed and final integrated CI #466 fully passed, so no unchanged-head rerun was wasted;
- DEV 1 PR #82, final head `4ee01c75bcda650eca44310ce5b36cb852db271f`: Script Engineering Workspace foundation, accepted after exact diff review and backend-real focused E2E.

No production Python interpreter/editor sandbox, graphical editor, new protocol or arbitrary driver/database/filesystem/shell/network Script authority was introduced. Those remain later-wave work.

## PYTHON-WAVE-06 — PREPARATION ONLY

Wave 06 is **not yet ACTIVE for workers**. Coordinator alone is executable.

Base candidate after Wave 05 merge: `7f629bf660bb16fd46fbf6abeb72b9ca1676e087`.

Coordinator must pin the Definition of Ready before creating/promoting worker branches:

- browser Client Visual Python sandbox implementation/isolation mechanism;
- narrow/versioned EliteSCADA API exposed to Python;
- cancellation/time budgets/bounded queues and deterministic cleanup;
- source validation and line/column diagnostics;
- editor versus execution authority boundary;
- TAG read/write permission model and Client Memory behavior;
- event/timer lifecycle;
- final acceptance gate and parallel dependency map;
- CI plan under constrained budget.

Worker queues remain planning only:

- DEV 1: Python Editor UX;
- DEV 2: Client Visual Python sandbox/runtime adapter;
- DEV 3: sandbox execution safety/acceptance.

Do **not** send worker implementation forward until `docs/CHAT-WORK-ASSIGNMENTS.md` explicitly promotes these tasks ACTIVE.

## First owner validation gate — LOCKED

The first true owner-facing build remains **EliteSCADA v0.1 — Full Product Validation Preview** after functional Client Visual Python + graphical Engineering/Runtime. Modbus TCP is sufficient as the real industrial protocol. MQTT/OPC UA/BACnet/S7/Allen-Bradley production work remains post-v0.1 unless deliberately changed by the product owner.

## Permanent continuity rules

- workers never choose their own next task, modify `main`, merge their own PR or broaden scope;
- final wave quality is proven on integrated composition, not worker CI alone;
- documentation-only `main` movement does not invalidate logical WaveBaseSHA;
- known failing work is never merged;
- tests/security/CAS/runtime guards are never weakened for green CI or budget savings;
- research does not equal production implementation;
- canonical Engineering remains authority;
- when CI mode is `CONSTRAINED`, optimize frequency, never the required final evidence.