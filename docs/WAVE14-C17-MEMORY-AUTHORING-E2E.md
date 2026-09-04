# Wave 14 C17 - Internal Memory Authoring UX + Full Lifecycle E2E

**Package:** `W14-C17`  
**Branch:** `wave14/c17-memory-authoring-e2e`  
**Required base:** `2607e03d5445eefe1f434495d0ee81136c6cd220`  
**Latest E2E correction commit:** `a184a754ee614321df96c533ba28ae7c4059da7b`  
**Integration target:** `wave14/corrections-integration`  
**State:** **CORRECTED CANDIDATE / EXACT-HEAD CI AND WAVE 11 REVALIDATION REQUIRED**

GitHub is the official memory. This handoff records the bounded C17 implementation and versioned validation. It does not claim a green exact-head validation until GitHub records successful gates for the final branch HEAD.

## Validation history

### First validation failure: ambiguous checkbox locator

Coordinator exact-head validation of candidate `15dda2845370d71a0b2cff3fce0303be57259b07` failed in **Wave 11 Active HMI Runtime #225 / run `33827655527`**.

The official diagnosis in validation PR #244, comment `5534613449`, classified the failure as deterministic test ambiguity rather than evidence of an Internal Memory product defect. Playwright artifact `9920620171` confirms that `page.getByLabel('Somente leitura')` matched both the intended checkbox and an unrelated select under strict mode, causing the C17 scenario to stop before Save/Publish/Activate/Runtime assertions.

C17 corrected only that locator in `web/scada-web/tests-wave11/c17-memory-lifecycle.spec.ts`:

- previous: `page.getByLabel('Somente leitura')`;
- corrected: `page.getByRole('checkbox', { name: 'Somente leitura' })`.

### Second validation failure: test navigated before asynchronous Apply completed

The corrected locator candidate was published as branch HEAD `6f162e579d44721a4e7b0397ceb943413958fa05`. Exact-head validation started automatically from PR #244.

- `Preview Licensing CI` #251 / run `33829863374`: **SUCCESS**;
- `EliteSCADA CI` #1300 / run `33829863402`: Web build and Backend build/test/runtime-smoke passed before the second C17 correction made that head historical;
- `Wave 11 Active HMI Runtime` #229 / run `33829863365`: **FAILURE** in the C17 lifecycle;
- Playwright report artifact: `9921329270`.

The locator fix worked: Wave11 passed the three base lifecycle tests and entered C17 without the previous strict-mode collision. The new failure occurred later because the consolidated Working export still showed `Server Memory initialValue = null`.

Trace inspection demonstrates a deterministic synchronization defect in the test helper, not a product persistence failure:

1. the first `MemoryTagSettingsPanel` candidate payload correctly contained `C17.Server.Value.initialValue = { dataType: "double", value: 12.5 }`;
2. its `POST /api/engineering/import/json/apply` was aborted with trace status `-1` when the test immediately navigated to configure Client Memory;
3. the helper had used `page.waitForLoadState('domcontentloaded')` after clicking Apply, but that wait could resolve against the already-loaded current document before the component's asynchronous Apply completed and triggered its reload;
4. the subsequent Client Memory candidate therefore started from the prior Working model, with Server Memory initial value still null, and successfully applied that stale Server field alongside the Client default.

C17 now waits for canonical Working state instead of relying on the current document load state. After clicking the normal `memory-initial-apply` UI button, the helper polls `/api/engineering/export/json` until the target TAG exposes the exact configured initial value. Only then can the next UI navigation begin.

This correction strengthens the acceptance test: it proves that the normal UI Apply actually reached canonical Working state. It does not change product code, UI text, authoring semantics or C17 acceptance scope.

The final candidate is the branch HEAD containing this handoff update. Exact-head gate IDs are reported in PR #244 / DEV delivery after GitHub Actions completes, without adding a post-validation commit that would invalidate the tested SHA.

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

These backend tests remain part of the normal Core test suite and are validation dependencies for C17; this document does not claim they passed on the corrected final C17 HEAD until the exact-head gates complete.

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

Before Coordinator acceptance/integration, the corrected final C17 HEAD requires actual execution of:

1. normal frontend/Playwright regressions including the focused C17 policy test;
2. backend Core regressions, including the existing Internal Memory suites above;
3. `EliteSCADA CI`;
4. `Wave 11 Active HMI Runtime`, including `chromium-wave11-c17-memory-lifecycle`.

PR #244 is the validation-only pull request used for exact-head gating against `main`. It must remain DRAFT/validation-only and must not be merged. Integration remains Coordinator-owned through `wave14/corrections-integration` / draft PR #212.

No green CI claim is made until GitHub records successful runs for the corrected exact head.

## Integration notes

C17 intentionally keeps its production change narrow: `TagAddressEditor` delegates Address visibility to a pure policy helper; Server/Client Memory backend semantics are unchanged.

During Wave 14 integration, compose `playwright.wave11.config.ts` with the projects added by other correction packages rather than replacing the file wholesale. Parallel packages may add their own Wave11 dependency chain.

No C17 commit merges directly to `main`.
