# Wave 14 C25.6 — Reusable Libraries / Engineering Lock — Binding Product Decision

**Status:** BINDING PRODUCT OWNER REFINEMENT  
**Checkpoint:** C25.6 — Reusable Resource Libraries  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Branch:** `wave14/c25-post-demo`

> GitHub live state remains the sole project authority. This decision refines the C25.6 reusable-libraries architecture and must be applied together with `docs/WAVE14-C25-REUSABLE-LIBRARIES-ARCHITECTURE.md`.

## Decision

Reusable libraries are an **Engineering-only** product capability.

When the current application is protected by Engineering Lock and is in the **locked** state, the reusable-libraries feature must not be exposed as an available Engineering operation.

### Locked-state UI contract

While Engineering Lock is locked:

- the `Bibliotecas` surface/navigation entry must not be visible;
- `Associar biblioteca` must not be visible;
- `Criar/Exportar biblioteca` must not be visible;
- library catalog browsing/search/preview must not be reachable;
- `Usar` / `Importar recurso` from a library must not be reachable;
- disassociation/management actions must not be reachable from normal locked application UI;
- direct client-side navigation to a library route must not reveal or render protected library/Engineering content.

This is **not** merely a presentation rule. Hiding buttons or routes in the frontend is insufficient.

### Backend authority contract

The backend must enforce the same lock boundary for reusable-library operations.

When Engineering Lock is locked, reusable-library endpoints/commands that create, export, associate, inspect/browse, import/use, mutate or manage reusable-library Engineering state must be denied according to the existing Engineering Lock authority model.

Frontend state is never authorization authority.

### Important distinction from existing Import/Export exemptions

Reusable-library operations do **not** inherit the existing Engineering Lock exemption for canonical application Import/Export merely because their implementation may reuse import/export/package primitives internally.

Semantically:

- canonical application Import/Export and approved recovery flows retain their already-established C25 Engineering Lock exemptions;
- reusable libraries remain an Engineering authoring/reuse capability and therefore remain unavailable while the application is locked.

Implementation must not bypass this distinction by routing library actions through an exempt generic Import/Export endpoint.

### Unlock behavior

After the application is legitimately unlocked through the existing Engineering Lock contract, the Libraries capability may become visible/usable again only if the authenticated user also has the required backend Engineering capabilities.

Engineering Lock never grants capability by itself; backend Authority remains the first capability authority.

## Required C25.6 validation

C25.6 cannot close without proving at least:

1. locked application does not expose the Libraries navigation/surface;
2. direct route/deep-link cannot reveal the library UI while locked;
3. backend library operations reject requests while locked even if invoked directly;
4. the normal application Import/Export and approved recovery exemptions remain unchanged;
5. unlocking restores library visibility only for an identity that has the required backend Engineering capability;
6. existing Engineering Lock browser/backend contracts remain green on the exact candidate SHA.

This decision is intentionally stricter than treating libraries as generic Import/Export. Libraries are part of Engineering authoring and must follow the Engineering Lock visibility and authority boundary.