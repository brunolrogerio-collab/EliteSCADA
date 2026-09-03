# Wave 14 C17 - Internal Memory Authoring UX + Full Lifecycle E2E

## Scope

This package resolves the Wave 14 C11 findings `MEM-02` and `MEM-04` without changing the semantic split between Server Memory and Client Memory.

Required base: `2607e03d5445eefe1f434495d0ee81136c6cd220`.

The package does not merge into `wave14/corrections-integration` or `main`.

## MEM-02 - source capability-aware Address authoring

`TagAddressEditor` no longer assumes that every Data Source is a network communication driver.

The editor resolves the selected Data Source type through the normal Engineering Data Source catalog. When the catalog classifies the type as `sourceProvider`, the generic network Address field and protocol address assistants are not rendered.

This is intentionally capability-aware rather than a check for `builtin.memory.server` or `builtin.memory.client`. Internal Memory therefore receives the correct UX because both built-in Memory types are source providers, while communication drivers keep their existing Address authoring surface.

If the catalog cannot resolve a type, the editor preserves the previous Address behavior instead of silently removing an authoring surface for an unknown driver.

## MEM-04 - normal product lifecycle proof

`web/scada-web/tests-wave11/c17-memory-lifecycle.spec.ts` extends the real Wave 11 Active Runtime browser workflow. It starts from the normal Engineering workspace and uses the same public UI and APIs used by the product.

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

The test is chained into `playwright.wave11.config.ts` after the base Active Runtime lifecycle and before the final owner-package artifact test so it runs against the real API/frontend runtime and leaves the subsequent consumer on restored state.

## Server Memory contract covered

- server/shared ownership;
- typed initial value;
- active runtime read;
- authorized write/read;
- Historian capture when enabled;
- persisted retention across runtime re-activation when the retained type is compatible.

## Client Memory contract covered

- client/session ownership;
- typed initial/default value;
- public runtime definition projection;
- isolation from Server Memory definitions;
- separate browser-session values;
- a write in one client does not mutate another client or shared process state.

## Explicitly not used as acceptance shortcuts

The C17 lifecycle test does not:

- inject `builtin.memory.server` or `builtin.memory.client` into a hidden package JSON as its creation path;
- edit `.escadapkg` files manually;
- depend on a fixture where the C17 Memory Sources and TAGs are already present;
- use a private provider ID channel unavailable to normal Engineering;
- emulate Server Memory in React or treat Client Memory as server-owned process state.

The provider type keys are selected through the public Data Source catalog UI, which is the normal human authoring contract.

## Validation targets

The package is intended to run through:

- Engineering component/regression tests;
- backend catalog regressions;
- normal browser E2E;
- Wave 11 Active HMI Runtime workflow, including the C17 lifecycle case;
- EliteSCADA CI.

GitHub Actions run IDs and final HEAD are reported in the package delivery after the branch reaches its final commit.
