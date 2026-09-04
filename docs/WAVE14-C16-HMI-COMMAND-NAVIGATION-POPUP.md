# Wave 14 — W14-C16 HMI Command / Startup Screen / Popup Positioning

**Package:** `W14-C16`  
**Branch:** `wave14/c16-hmi-command-navigation-popup`  
**Required base:** `2607e03d5445eefe1f434495d0ee81136c6cd220`  
**Audited product that originated the C11 findings:** `97eefd8f4377ff583d1ba20bc89203f4a82b584d`  
**Implementation/evidence code HEAD before this handoff document:** `f6cfb14f7ba1082d4017c1add00da7dfdfafa079`  
**Integration target:** `wave14/corrections-integration` through DRAFT PR #212  
**State:** **IMPLEMENTATION COMPLETE / EXACT-HEAD CI AND WAVE 11 EXECUTION STILL REQUIRED**

GitHub remains the official memory. This document records the bounded C16 implementation and the validation that is versioned in the branch. It does **not** claim that GitHub Actions ran on this package branch.

## 1. C11 findings owned by C16

C16 resolves the package assigned in `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`:

- `C11-P2-CMD-02` — backend Operational Command existed, but authored Screen/Dynamo/Popup had no canonical invocation bridge;
- `C11-P2-NAV-01` — Runtime startup Screen was selected by lexical key order instead of explicit persisted project configuration;
- `C11-P2-POP-02` — Popup had no canonical persisted authorable X/Y placement.

The Product Owner decision that Popup X/Y is mandatory before C11 release is preserved.

## 2. Canonical Operational Command action

### 2.1 Engineering contract

`VisualNavigationActionKind` now includes:

`ExecuteCommand`

`VisualNavigationActionEngineeringDto` carries the stable canonical Command identity in:

`CommandId`

The action does not duplicate Command value, target TAG semantics, authorization rules or execution logic.

Engineering Preview rejects:

- `ExecuteCommand` without a non-empty `CommandId`;
- an unresolved Command identity;
- `TargetKey` on `ExecuteCommand`;
- action Parameters on `ExecuteCommand`, preventing the visual layer from overriding canonical Command semantics;
- `CommandId` on non-Command visual actions.

Command identity validation is performed against the prospective Engineering model so a Command supplied in the same candidate package is valid without requiring a previous hidden mutation.

The validation recursively covers visual elements in:

- Screen;
- Popup;
- Dynamo definition.

### 2.2 Human authoring

`HmiOperationalConfigurationPanel` exposes a normal Engineering path to author `ExecuteCommand` on a stable visual object in a Screen, Dynamo or Popup.

The user selects:

1. visual definition type;
2. definition;
3. stable visual object;
4. event key;
5. enabled canonical Command.

The panel uses the existing public Engineering Preview/Apply boundary and Workspace change version. It does not edit hidden JSON, use DOM persistence or write a TAG directly as a substitute for a Command.

### 2.3 Runtime bridge

The Runtime visual layer recognizes both canonical .NET camel-case wire values and existing in-memory Pascal-case values.

For `ExecuteCommand`, the browser invokes only:

`POST /api/commands/{commandId}/execute`

No request body is produced by the bridge. There is no fallback to `/api/tags/{id}/write`.

Backend remains authoritative for:

- Active Runtime Command resolution;
- enabled/existence checks;
- `SecurityCapability.CommandExecute` authorization;
- Area/Equipment/TAG/Command scope;
- Active revision consistency;
- execution;
- denied/succeeded/failed Audit.

Existing security coverage already verifies anonymous denial, operator CommandExecute success while direct ProcessValueWrite remains denied, and command Audit outcomes. C16 adds the canonical visual route into that same backend authority rather than duplicating those rules in React.

## 3. Explicit Startup / Home Screen

### 3.1 Canonical project contract

`EngineeringPackage` now has the additive optional field:

`StartupScreenId`

It stores stable Screen identity, not a name prefix or browser route convention.

The current Engineering schema version remains `15`; this change is additive and existing packages without the field remain parseable.

`IEngineeringViewRegistry` stores the current Startup Screen identity. A non-empty configured identity must resolve to an existing Screen.

Engineering Preview rejects an unresolved identity with:

`STARTUP_SCREEN_NOT_FOUND`

### 3.2 Authoring and explicit clear

The normal Engineering HMI panel allows a user to select a Home Screen by stable identity or explicitly clear it.

Clearing Home persists `null`. A full Engineering package containing Screens and `StartupScreenId = null` clears registry state, while partial CSV imports that contain no Screens do not accidentally clear Home.

### 3.3 Save / Publish / Activate / Runtime

`StartupScreenId` is exported in canonical Engineering JSON and therefore follows normal project persistence.

`/api/runtime/application` projects `startupScreenId` from the persisted **Active revision**, not from Working state.

Runtime resolves the configured identity and does not sort Screen keys.

Explicit fail-safe diagnostics are:

- `HMI_RUNTIME_STARTUP_SCREEN_REQUIRED` when no Home is configured;
- `HMI_RUNTIME_STARTUP_SCREEN_UNRESOLVED` when the persisted identity is not present in the Active project.

There is no `00_Overview`, `00-...`, `localeCompare` or equivalent lexical fallback contract.

## 4. Persisted Popup X/Y

### 4.1 Canonical contract

`PopupEngineeringDto` now persists:

- `X`;
- `Y`.

Both default to `0` for compatibility with older project payloads.

The coordinates belong to the same fixed logical HMI space used by Screen content. They are not browser viewport pixels.

The Engineering registry rejects non-finite X/Y values.

### 4.2 Authoring

The HMI operational configuration panel exposes normal numeric X/Y editing with Preview/Apply.

The existing Popup visual-authoring adapter was also corrected so opening and saving a Popup through the established C07 editor preserves X/Y instead of silently returning them to zero.

### 4.3 Active Runtime placement and scaling

Popup mount remains inside the C09 fixed logical Runtime stage. X/Y are applied before the stage transform, so the same transform controls:

- Screen visual position;
- Popup visual position;
- browser pointer/hit-target mapping.

Stacking remains deterministic through popup mount order and z-index.

### 4.4 Invalid / off-canvas policy

Canonical Popup does not define width/height fields. C16 deliberately does not invent private frontend dimensions merely to clamp placement.

The Runtime policy is therefore:

- finite normal X/Y are preserved;
- negative X/Y clamp to the logical origin;
- non-finite runtime values fail safe to the origin;
- values beyond the logical stage are bounded so at least `48` logical pixels of the Popup top-left region remain reachable.

Constant:

`POPUP_MIN_VISIBLE_LOGICAL_PX = 48`

This produces deterministic recoverability without browser-dependent off-canvas accidents or DEMO-specific CSS.

## 5. Frontend product surface / i18n

User-visible C16 authoring chrome was added in:

- `pt-BR`;
- `en`;
- `es`.

The panel explicitly explains that:

- Home clear leaves Runtime unavailable until another Home is configured;
- Popup X/Y are logical coordinates;
- visual Command action stores only canonical Command identity while backend remains execution authority.

## 6. Versioned validation added by C16

### Normal Playwright / contract coverage

`web/scada-web/tests-e2e/wave-14-c16-hmi-operational-contract.spec.ts`

Covers source-level contract presence, no lexical Home fallback, canonical Command endpoint delegation, C09 logical transform across 720p/1080p/1440p/4K, Popup stacking/off-canvas policy and alarm-overlay regression boundary.

`web/scada-web/tests-e2e/wave-14-c16-engineering-contract.spec.ts`

Uses the real Engineering API to cover:

- unresolved Home Preview rejection;
- valid stable Command reference on Screen, Popup and Dynamo;
- unresolved Command Preview rejection;
- real Home configure -> Apply -> export -> clear -> Apply -> export -> restore lifecycle in the Working model.

`web/scada-web/tests-e2e/wave-14-c16-startup-screen.spec.ts`

Proves configured stable Home identity beats lexical Screen order and covers missing/unresolved Home diagnostics.

`web/scada-web/tests-e2e/wave-14-c16-runtime-command-api.spec.ts`

Proves the visual bridge performs only the canonical Command POST, sends no body, propagates backend denial/failure and refuses empty identity.

`web/scada-web/tests-e2e/wave-14-c16-popup-position.spec.ts`

Covers normal, negative, invalid and far-off-canvas placement plus logical coordinate invariance at 720p, 1080p, 1440p and 4K.

`web/scada-web/tests-e2e/wave-14-popup-visual-authoring-model.spec.ts`

Now proves Popup X/Y survive the existing Popup visual-authoring adapter round-trip.

### Existing backend security/audit coverage consumed by C16

`web/scada-web/tests-e2e/security.spec.ts` already verifies the canonical endpoint contract C16 delegates to, including:

- anonymous Command execution denied;
- invalid authentication denied;
- operator with `CommandExecute` can execute Command;
- the same operator lacks direct `ProcessValueWrite` authority;
- developer Command execution succeeds;
- successful and denied `command.execute` Audit entries are recorded.

C16 does not fork or weaken these tests.

## 7. Wave 11 Active HMI Runtime acceptance

`playwright.wave11.config.ts` now executes the C16 scenarios in the real Wave 11 lifecycle chain:

1. `chromium-wave11-c16-startup-bootstrap`;
2. existing `chromium-wave11-lifecycle`;
3. `chromium-wave11-c16-operational-runtime`;
4. existing owner-package export.

`tests-wave11/c16-startup-bootstrap.spec.ts` ensures the initial Working package has an explicit persisted `demo.overview` Home before the existing lifecycle first activates an Engineering Runtime.

`tests-wave11/c16-operational-runtime.spec.ts` then uses the real lifecycle:

`Working -> Preview -> Apply -> Save -> Publish -> Activate -> /api/runtime/application -> browser Runtime`

The scenario authors and proves:

- Screen button -> `ExecuteCommand` Start;
- Dynamo child button -> `ExecuteCommand` Start;
- Screen button -> `OpenPopup`;
- Popup button -> `ExecuteCommand` Stop;
- Popup authored at logical `X=360`, `Y=220`;
- Active projection retains Home and Popup coordinates;
- browser Runtime at 1280x720 uses the C09 `2/3` logical scale;
- real browser clicks reach Screen, Dynamo and Popup command hit targets;
- the commanded process TAG changes through backend Command execution;
- two Popup mounts preserve stack indexes `0` and `1` and the same logical position.

The existing owner-test package remains downstream of the C16 scenario so its exported Active revision contains the validated C16 product contracts.

## 8. CI / execution status

At the time this handoff was prepared, GitHub Actions reported **zero workflow runs** for branch:

`wave14/c16-hmi-command-navigation-popup`

This is expected from the repository workflow triggers:

- `EliteSCADA CI` runs automatically for `push` to `main` and PRs targeting `main`, with manual `workflow_dispatch` available;
- `Wave 11 Active HMI Runtime` runs automatically for relevant `push` to `main` and PRs targeting `main`, with manual `workflow_dispatch` available.

A package-branch push does not automatically start either workflow.

The connected GitHub action surface available during this DEV session supports reading/re-running existing workflow runs but does not expose creation of a new `workflow_dispatch` run. Therefore **no CI green claim is made here**.

Per `docs/CI-VALIDATION-POLICY.md`, C16 must receive exact-head validation before acceptance/integration:

1. `EliteSCADA CI`;
2. `Wave 11 Active HMI Runtime`.

Do not broaden workflow path filters or add temporary push triggers merely to manufacture a branch run. The Coordinator/integrator should manually dispatch the existing workflows on the accepted C16 head or validate through the authorized integration surface before merge acceptance.

## 9. Integration notes

C16 intentionally touches shared contracts used by other Wave 14 packages:

- `EngineeringPackage` additive Startup Screen field;
- `PopupEngineeringDto` additive X/Y;
- `VisualNavigationActionEngineeringDto` additive Command action/identity;
- Runtime Active HMI package projection;
- `playwright.wave11.config.ts` lifecycle ordering.

During integration, preserve the semantic changes rather than choosing either branch version wholesale when another package has changed the same shared files.

Particular care is required around `playwright.wave11.config.ts`: parallel C17 or other Wave 14 packages may also add dependent Wave 11 projects. The Coordinator should compose all required projects/dependencies rather than dropping one package's acceptance lane.

No C16 commit merges directly to `main`. PR #212 remains DRAFT until Coordinator acceptance and all required correction packages/gates are complete.
