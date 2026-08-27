# EliteSCADA Interface Product Development

Status: **ACTIVE PRODUCT BLOCK**.

This block was explicitly prioritized by the product owner on 2026-08-27. The immediate objective is to turn the already substantial backend/runtime/Engineering foundation into a coherent industrial product experience before spending more effort on additional drivers or on the provisional Windows presentation package.

## Priority decision

Current order of work is:

`merged platform foundations -> interface product development -> user validation build/package -> additional external drivers/protocols`

The following work is therefore deliberately postponed, not cancelled:

- new production protocol families and Driver Module expansion;
- completion/delivery of the provisional Windows x64 presentation/validation package.

The parked `integration/interface-validation-preview` branch may retain unmerged preparatory work, but it is not the active development branch and must not be merged merely to preserve progress.

## Product objective

The UI must stop feeling like a collection of technical proof surfaces and become a consistent SCADA application suitable for daily Engineering and Runtime use.

The active interface effort focuses on four layers.

### 1. Product shell and navigation

Create one coherent EliteSCADA application shell across Runtime, Engineering and Audit:

- persistent and predictable primary navigation;
- clear current-area identity;
- project/workspace/runtime context where relevant;
- authenticated user/session affordance;
- consistent language, status, loading, error and empty-state behavior;
- responsive desktop-first layout without floating developer-style navigation controls;
- restrained high-performance-HMI visual language where healthy/normal state remains quiet and abnormal state carries emphasis.

### 2. Engineering workspace ergonomics

The Engineering UI must evolve from long technical sections into a productive workspace:

- clearer information architecture and grouping;
- fast search/filtering and direct navigation to entities;
- useful counts/status/context without visual clutter;
- reusable list/master-detail patterns for TAGs, Data Sources, alarms and other entities;
- compact tables that scale beyond demo-size projects;
- explicit dirty/working/revision/published/active context;
- clear Preview/Apply/save/publish/activate semantics when those operations are exposed;
- actionable validation/error presentation;
- consistent editors, labels, units, descriptions and technical details;
- keyboard/focus/accessibility behavior suitable for desktop Engineering work;
- complete `pt-BR` / `en` / `es` behavior for product surfaces that are touched.

This block does **not** authorize the future graphical Screen/Popup/Dynamo editor to bypass the locked Script/visual prerequisite chain.

### 3. Runtime operations experience

Runtime must grow beyond the current hard-coded demonstration screen and expose useful platform-level operational context while preserving the demo process screen:

- runtime/connection health summary;
- active Data Source communication state;
- active alarm visibility and acknowledgement workflow;
- TAG/current-value visibility suitable for diagnosis;
- Gateway status where operationally useful;
- historian/trend entry points using current backend capabilities;
- clear online/offline/degraded states;
- separation between process visualization and Engineering/diagnostic tools.

A generic operational overview is allowed now. A full graphical HMI editor/runtime generated from Screen/Dynamo Engineering remains governed by `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

### 4. Session, administration and cross-product consistency

Improve the surrounding product experience:

- visible authenticated identity/roles where appropriate;
- clear logout/session behavior;
- consistent administration entry points;
- Audit presentation aligned with the application shell;
- common spacing, typography, control states, colors and feedback patterns;
- no security decision may be implemented only in the frontend.

## Architecture guardrails

Interface development must preserve all current product boundaries:

- canonical public/versioned Engineering remains authoritative;
- frontend never accesses drivers directly;
- security and Audit remain backend enforced;
- Working / Revision / Published / Active semantics remain distinct;
- TAG quality remains authoritative per point;
- Data Source identity remains distinct from Driver type and runtime instance;
- Internal Memory and Gateway semantics remain unchanged;
- no fake diagnostics or demo-only private Engineering truth;
- no weakening of Preview/Apply/CAS safety for UI convenience.

## Parallel work split

The coordinator owns central shell/routing/integration and final UX composition.

Worker slices should be isolated so they can be integrated without competing edits to central files:

- **DEV 1:** Engineering workspace/entity-browser ergonomics primitives;
- **DEV 2:** Runtime operational overview primitives using existing protected APIs;
- **DEV 3:** authenticated session/user-menu UX using the existing Auth context;
- **COORDENADOR:** product shell, central navigation, EngineeringApp/main integration, global visual system, worker integration, browser tests, CI and documentation.

## First acceptance checkpoint

Before calling the first interface-development slice complete:

1. the global Runtime/Engineering/Audit navigation no longer looks or behaves like a floating development helper;
2. Engineering has a clearer workspace hierarchy and scalable entity navigation pattern;
3. Runtime has a useful operational overview in addition to the demo process surface;
4. logged-in identity/session controls are visible and understandable;
5. existing authorization, Audit, diagnostics, Gateway, Internal Memory and Engineering flows still pass automated tests;
6. Web build and Chromium E2E are green on the integrated candidate head.

## Deferred validation package

`docs/INTERFACE-VALIDATION-MILESTONE.md` remains valid, but execution of its packaging/launcher deliverable is deferred until the interface has matured enough that user testing produces higher-value feedback.

When resumed, the Windows x64 validation build must package the interface resulting from this active block, not freeze the older demo-oriented UI merely to satisfy a milestone checkbox.
