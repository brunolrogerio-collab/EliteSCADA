# Wave 14 C17 - Internal Memory Authoring UX + Full Lifecycle E2E

**Package:** `W14-C17`  
**Branch:** `wave14/c17-memory-authoring-e2e`  
**Required base:** `2607e03d5445eefe1f434495d0ee81136c6cd220`  
**Implementation/test HEAD before this handoff update:** `82d8c9d7dfd2646302c0ddab67159d6a4b8210cf`  
**Integration target:** `wave14/corrections-integration`  
**State:** **IMPLEMENTATION COMPLETE / EXACT-HEAD CI AND WAVE 11 EXECUTION STILL REQUIRED**

GitHub is the official memory. This handoff records the bounded C17 implementation and versioned validation. It does not claim that GitHub Actions executed successfully on this package branch.

## Scope

C17 resolves the Wave 14 C11 Internal Memory authoring/lifecycle findings `MEM-02` and `MEM-04` without changing the semantic split between Server Memory and Client Memory.

The package does not merge into `wave14/corrections-integration` or `main` directly. PR #212 remains under Coordinator control and must stay DRAFT until the Wave 14 correction gates are satisfied.

## MEM-02 - source capability-aware Address authoring

`TagAddressEditor` no longer assumes that every Data Source is a network communication driver.

The editor resolves the selected Data Source type through the normal Engineering Data Source catalog. The Address visibility decision is isolated in:

`web/scada-web/src/engineering/tagAddressPolicy.ts`

The policy preserves the existing behavior exactly:

- while a selected Data Source type is still being resolved, Address remains hidden;
- when the catalog classifies the type as `sourceProvider`, Address and protocol assistants remain hidden;
- normal driver-backed Data Sources keep the existing Address surface;
- if catalog resolution completes without a known kind, the editor fails open and preserves manual Address authoring;
- when no Data Source is selected, manual Address authoring remains available.

This is intentionally capability-aware rather than a hard-coded check for `builtin.memory.server` or `builtin.memory.client`. Internal Memory receives the correct UX because both built-in Memory types are source providers, while communication drivers retain their existing authoring behavior.

Focused regression:

`web/scada-web/tests-e2e/wave-14-c17-tag-address-policy.spec.ts`

It covers pending type resolution, source-provider hiding, case-insensitive source kind handling, normal driver visibility, unresolved-kind fail-open behavior and no-Data-Source manual authoring.

## MEM-04 - normal product lifecycle proof

`web/scada-web/tests-wave11/c17-memory-lifecycle.spec.ts` extends the real Wave 11 Active Runtime browser workflow. It starts from the normal Engineering workspace and uses the public UI and APIs used by the product.

The acceptance path is:

1. create a Server Memory Data Source through the Data Source catalog UI;
2. create a Client Memory Data Source through the Data Source catalog UI;
3. create a Server Memory TAG through the normal TAG editor;
4. create a Client Memory TAG through the normal TAG editor;
5. prove that the generic network Address field is absent for both Memory source providers;
6. configure typed initial/default values through `MemoryTagSettingsPanel`;
7. enable Historian for the Server Memory TAG;
8. Save the Engineering revision;
9. Publish the saved revision;
10. Activate the published revision;
11. read the active Server Memory initial value;
12. execute an authorized Server Memory write and read back the new value;
13. prove Historian capture for the configured Server Memory TAG;
14. re-activate and prove Server Memory retention restores the compatible retained value instead of the Engineering default;
15. query the public Client Memory definitions and prove Server Memory is not projected into the client/session store;
16. initialize two independent browser sessions, write Client Memory in one session, and prove the other session remains at its own default value;
17. restore the original Engineering package and Save/Publish/Activate it as test cleanup.

The test is chained into `web/scada-web/playwright.wave11.config.ts` after the base Active Runtime lifecycle and before the final owner-package artifact test, so it exercises the real API/frontend runtime and leaves the subsequent consumer on restored state.

## Server Memory contract covered

- server/shared ownership;
- typed initial value;
- active runtime read;
- authorized write/read;
- Historian capture when enabled;
- retained value restoration across runtime re-activation when the retained type is compatible.

## Client Memory contract covered

- client/session ownership;
- typed initial/default value;
- public runtime definition projection;
- isolation from Server Memory definitions;
- separate browser-session values;
- a write in one client does not mutate another client or shared process state.

## Existing backend regression evidence consumed by C17

C17 does not duplicate backend tests that already exercise the canonical Internal Memory contracts.

`tests/Scada.Core.Tests/InternalMemoryEngineeringTests.cs` covers, among other cases:

- Server Memory typed initial value Preview/Apply/export round-trip;
- Client Memory round-trip as a runtime-client-local source;
- typed initial values through TAG CSV;
- compatibility with older Engineering payloads;
- rejection of Historian and global Alarm use on Client Memory;
- validation of unsafe transitions from Server/shared semantics to Client Memory.

`tests/Scada.Core.Tests/InternalMemorySourceProviderTests.cs` covers:

- Server Memory typed initial value and no-network source-provider descriptor;
- retention across provider restart;
- retention keyed by stable TAG identity across path rename;
- fail-closed retained type mismatch behavior;
- exact runtime write typing;
- Client Memory isolation per runtime client and reset to its initial value for a new client.

`tests/Scada.Core.Tests/EngineeringClientMemoryCommandValidationTests.cs` verifies that server-side Commands cannot target Client Memory TAGs and that a Data Source cannot be transitioned to Client Memory while an existing server Command still targets its TAG.

These backend tests remain part of the normal Core test suite and are validation dependencies for C17; this document does not claim they were executed on the final C17 HEAD during this DEV session.

## Explicitly not used as acceptance shortcuts

The C17 lifecycle test does not:

- inject `builtin.memory.server` or `builtin.memory.client` into hidden package JSON as its creation path;
- edit `.escadapkg` files manually;
- depend on a fixture where the C17 Memory Sources and TAGs are already present;
- use a private provider ID channel unavailable to normal Engineering;
- emulate Server Memory in React or treat Client Memory as server-owned process state.

The provider type keys are selected through the public Data Source catalog UI, which is the normal human authoring contract.

## Versioned validation added by C17

- `web/scada-web/tests-e2e/wave-14-c17-tag-address-policy.spec.ts` - focused MEM-02 authoring policy regression;
- `web/scada-web/tests-wave11/c17-memory-lifecycle.spec.ts` - real Engineering -> Save -> Publish -> Activate -> Runtime lifecycle acceptance;
- `web/scada-web/playwright.wave11.config.ts` - C17 Wave11 project/dependency ordering.

## Required acceptance execution

Before Coordinator acceptance/integration, the final C17 HEAD still requires actual execution of:

1. normal frontend/Playwright regressions including the focused C17 policy test;
2. backend Core regressions, including the existing Internal Memory suites above;
3. `EliteSCADA CI`;
4. `Wave 11 Active HMI Runtime`, including `chromium-wave11-c17-memory-lifecycle`.

Do not broaden workflow triggers, merge to `main`, or create an artificial PR to `main` merely to manufacture a branch run. If the package branch does not trigger Actions automatically, the Coordinator should use the repository's authorized existing validation path/manual workflow dispatch or validate the accepted exact head through the integration surface.

No green CI claim is made until GitHub records successful runs for the accepted exact head.

## Integration notes

C17 intentionally keeps its production change narrow: `TagAddressEditor` delegates Address visibility to a pure policy helper; Server/Client Memory backend semantics are unchanged.

During Wave 14 integration, compose `playwright.wave11.config.ts` with the projects added by other correction packages rather than replacing the file wholesale. Parallel packages may add their own Wave11 dependency chain.

No C17 commit merges directly to `main`.
