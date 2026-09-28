# Wave 15 — CORRECTION-NOW Shared Contracts

**State:** ACTIVE CONTROL / CONTRACT-FIRST PARALLEL EXECUTION  
**Control branch:** `coord/w15-correction-now-parallel-control`  
**Product integration authority:** `wave15/corrections-integration`  
**Control base at creation:** `00d17e716b877e4cc00e25ea093f53f0da485c24`  
**Product Owner rule:** `BACKEND_CAPABILITY != PRODUCT_DELIVERY`

GitHub live remains the sole authority. Revalidate integration HEAD and every contract owner before execution.

## 1. Purpose

Wave 15 correction work is now large enough that parallel chats are useful, but only if they consume the same canonical contracts.

This document defines the **shared contracts that must be frozen once and then consumed by multiple DEV/AUD lanes**.

A lane may:
- implement behind a frozen contract;
- improve mounted UX over an existing frozen contract;
- add tests/evidence for that contract.

A lane may **not** independently redefine a shared identity, lifecycle, timing, write, visual-property or reusable-object contract because another lane needs a convenient local shortcut.

Contract state vocabulary:

- `DRAFT_OWNER` — one owner is allowed to inspect/propose;
- `FROZEN_FOR_CONSUMERS` — Main accepted the public semantics; consumers may implement;
- `IMPLEMENTED_PENDING_INTEGRATION` — owner implementation exists but is not yet integrated;
- `INTEGRATED` — implementation is on live integration;
- `REOPEN_REQUIRED` — evidence proves the frozen contract itself is wrong/incomplete.

## 2. Global acceptance contract — C-SURFACE-01

**State:** `FROZEN_FOR_CONSUMERS`  
**Owner:** Main / #305

For normal user-facing product behavior:

`schema/API/test presence != delivered feature`.

A feature is accepted only when mounted UI proves, where applicable:

`DISCOVER -> OPEN/SELECT -> CONFIGURE -> VALIDATE/PREVIEW -> SAVE/REOPEN -> USE -> ERROR/STATE FEEDBACK`.

Add for Runtime-affecting behavior:

`PUBLISH/ACTIVATE -> RUNTIME EFFECT`.

Add for writes:

`AUTHORIZATION -> AUDIT -> AUTHORITATIVE READBACK/FAILURE`.

Add for visual authoring:

`SCREEN + POPUP PARITY where semantically applicable`.

No DEV may return DONE using backend/unit evidence alone for a user-facing capability.

---

## 3. Project / Runtime authority — C-AUTHORITY-01

**State:** `DRAFT_OWNER` until #354 correction is accepted.  
**Owner:** #354 / CODEX high-risk correction.  
**Consumers:** #303, #355, #356/#365, #368, #369, #364, Stage 2.

### Public semantics to preserve

- Working, Published and Active remain distinct authorities.
- Runtime renders the intentionally selected/persisted Active project/revision.
- Fresh/Neutral state never adopts hidden Demo/EEE application content as an implicit Runtime fallback.
- Explicit Demo licensing/session semantics may exist, but they do not manufacture a Demo project as project authority.
- Restart/recovery does not mint project authority.
- Cross-project recovery/activation fails closed when identity cannot be reconciled.
- UI labels/fallback text do not become data authority.

### Freeze output required from owner

Owner must return a compact contract block containing:
- canonical source of Working identity;
- canonical source of Published revision identity;
- canonical source of Active Runtime identity;
- legal no-project/Neutral behavior;
- legal explicit Demo behavior;
- restart/recovery rule;
- exact stable identifiers used to compare project/revision authority.

Consumers must wait for `FROZEN_FOR_CONSUMERS` before changing authority-sensitive behavior.

---

## 4. Remote request / timing / mutation semantics — C-TRANSPORT-01

**State:** `DRAFT_OWNER / DIAGNOSTIC`.  
**Owner:** #307 + concrete scenario #359.  
**Consumers:** #355, Security/Authority UI, #364, #369, future remote DB/admin UI.

### Semantics

Requests are classified rather than governed by one global timeout:

`TRANSPORT_CONNECT | REQUEST_READ | LONG_OPERATION | REALTIME_HEARTBEAT | RECONNECT | STALE_DATA | COMMAND_WRITE | INTERNAL_EXECUTION | SECURITY_SESSION | HA_AUTHORITY`.

Rules:
- no arbitrary universal timeout multiplier;
- retry-safe reads may use bounded retry/backoff;
- mutations/writes are never blindly repeated after ambiguous transport outcome;
- late responses cannot overwrite newer state;
- UI distinguishes loading / stale / reconnecting / unavailable / server error;
- transport failure does not mutate project/Active authority;
- auth/session/HA/internal execution bounds stay fail-closed.

### Freeze output

Owner must publish:
- central timing categories/default budgets or owning policy abstraction;
- request correlation/cancellation rule;
- safe-read retry rule;
- mutation reconciliation/idempotency rule;
- stale/reconnect UX states;
- evidence from local-normal -> injected-latency -> remote/Codespace.

#355 may fix an independent selector bug, but if failure is reproduced only through timing, it consumes this contract rather than inventing its own timeout.

---

## 5. Visual object identity + property registry — C-VISUAL-IDENTITY-01

**State:** `DRAFT_OWNER`.  
**Owner:** #303 first-user Editor correction + #367 identity slice.  
**Consumers:** #367 dynamics, #368 HMI IO, #369 Script object UX, #308/#365 Dynamo/reusable objects.

Existing foundation:
- `VisualElementEngineeringDto.Id` = stable identity;
- `VisualElementEngineeringDto.Key` = developer-facing sibling-local name;
- canonical object/property schemas live in shared Engineering visual contracts/registry.

### Required frozen semantics

- Rename changes developer-facing Key/name, never stable Id.
- Script/runtime references use stable identity/reference semantics, not mutable display labels.
- Group is a first-class visual element with stable Id + renameable Key/name.
- Object properties are addressed only through canonical registered property keys/types.
- Canvas, Outliner, Properties, Animations and Script browser resolve the same object/property identity.
- No DOM/CSS/private renderer property becomes project API.

### Freeze output

Owner must publish:
- stable object reference format used across Runtime and scripts;
- sibling name uniqueness rule;
- rename/reference migration rule;
- group identity rule;
- canonical property metadata shape required by Properties/Animations/Scripts;
- selection synchronization contract.

#369 and advanced #367 work remain `WAIT_CONTRACT` until this freezes.

---

## 6. Visual dynamics / animation mapping — C-VISUAL-DYNAMIC-01

**State:** `WAIT_C-VISUAL-IDENTITY-01`, then `DRAFT_OWNER`.  
**Owner:** #367.  
**Consumers:** Runtime visual renderer, #368 display/input visibility, #369 Script authoring/help, #308 visual previews.

Existing canonical primitives to reuse:
- `PropertyExpressions`;
- `BooleanConditions`;
- `AnalogFill`;
- Visual Property Registry validation.

Required contract:
- visibility/boolean condition;
- numeric Analog Fill;
- property expression;
- canonical typed value/range -> property mapping for color/style where current schema is insufficient;
- deterministic ordered-range overlap/fallback behavior;
- typed destination validation;
- design-preview vs Runtime distinction;
- group vs child composition/precedence.

No browser-only range->color map is permitted.

---

## 7. Operator TAG write contract — C-TAG-WRITE-01

**State:** `FROZEN_BASE / EXTENSION_OWNER #368`.  
**Existing reference:** Slider write semantics from historical #191.  
**Owner for extension:** #368.  
**Consumers:** `core.numericInput`, future operator value-entry controls.

Required semantics:
- stable writable TAG binding;
- backend Runtime authorization is authoritative;
- bad/unavailable/read-only/unauthorized target cannot be written;
- no frontend -> Driver bypass;
- input edit is buffered locally;
- default commit is explicit Enter/Apply, not write-on-keystroke;
- failed write does not fabricate process value;
- authoritative readback wins after commit;
- audit follows existing protected write policy;
- Design mode never writes process values.

#368 extends this contract to numeric input without creating a second write service.

---

## 8. Reusable object relationship — C-REUSE-01

**State:** `DRAFT_OWNER`.  
**Owner:** #365, with specialized owners #356 and #308.  
**Consumers:** #303 Screen/Popup Editor, import/export/package, Runtime visual composition.

Canonical mental model:

`Template -> Equipment instance -> Dynamo visual definition -> Screen/Popup instance/use`.

Required contract:
- Template = reusable data/process definition;
- Equipment = project instance/path/context/bindings;
- Dynamo = reusable graphical definition/public visual interface;
- stable references between these domains;
- one canonical renderer/model;
- package/import-export preserves definitions/references;
- no hidden auto-created TAG/Equipment merely to satisfy preview;
- preview/inspection does not mutate Working.

Freeze must specify which relationships are mandatory vs optional so UX does not invent fake coupling.

---

## 9. TAG Gateway route contract — C-GATEWAY-01

**State:** `FROZEN_BASE / UX OWNER #364`.  
**Canonical source:** `docs/TAG-GATEWAY.md`.  
**Owner:** #364 for mounted product workflow.

Semantics:
- endpoint identity is TAG -> TAG, not protocol-pair/DataSource-pair authority;
- multiple independent routes are allowed;
- fan-out is allowed: one source TAG -> multiple destination TAGs via separate routes;
- multiple active writers to the same destination TAG remain rejected under current deterministic single-writer rule;
- route diagnostics remain per-route;
- no frontend-to-driver shortcut;
- Preview/Apply/Active authority remains canonical.

#364 should primarily build a first-class route-management surface over this existing contract.

---

## 10. Deployment DB topology — C-DB-TOPOLOGY-01

**State:** `HOLD / ARCHITECTURE_ONLY`.  
**Owner:** #366 / #363 consumer.  
**Not a current correction dependency.**

Preserved direction:
- managed local PostgreSQL/TimescaleDB profile for normal installation;
- supported remote DB profile configurable at deployment/system level;
- migration/cutover required before changing active DB with existing state;
- DB topology/secrets do not enter Engineering or `.escadapkg`;
- constrained Edge may use remote DB and not run local DB service after validated cutover.

Do not activate implementation while current correction gates remain open.

---

## 11. Contract freeze protocol

A shared contract becomes `FROZEN_FOR_CONSUMERS` only after Main records:

1. owner issue/branch;
2. exact product/base SHA;
3. existing authoritative contract/source files;
4. public semantics;
5. identifiers/types/API surface;
6. compatibility/migration rule;
7. negative/forbidden behavior;
8. representative regression;
9. explicit downstream consumers.

A consumer finding a missing field/semantic must return:

`BLOCKED_CONTRACT / <contract-id> / <missing semantic>`

instead of silently extending the contract in its own branch.

## 12. Integration rule

Consumers never merge a shared-contract redefinition indirectly.

Order:
1. contract owner candidate;
2. Main/AUD review;
3. integrate owner on `wave15/corrections-integration`;
4. mark contract `INTEGRATED`;
5. consumer rebase/recreate from the new exact integration HEAD;
6. consumer implementation/proof;
7. sequential integration.

No branch-to-branch feature dependency becomes permanent authority.


---

## 13. Test/evidence capability contract — C-TEST-EVIDENCE-01

**State:** `FROZEN_FOR_CONSUMERS`  
**Owner:** Main / #305  
**Purpose:** parallel DEV/AUD chats must not be blocked or allowed to overclaim merely because their execution environment differs.

### Core rule

A lane is responsible for the **highest evidence tier its environment can truthfully execute**.

A lane must never:
- invent a PASS for a test it could not run;
- weaken tests because Docker/browser/Codespace is unavailable;
- mutate product merely to avoid environment limitations;
- claim mounted UI acceptance from source inspection.

If a required higher tier is unavailable, the lane returns an explicit capability gap and Main routes the exact candidate to a capable test executor.

### Evidence tiers

#### E0 — source/contract review

Available to all GitHub-capable chats.

Evidence:
- exact base->candidate diff;
- scope;
- contract compliance;
- static reasoning;
- regression tests added/updated;
- no execution claim.

#### E1 — local lightweight execution

When runtime permits:
- parser/typecheck;
- focused unit tests;
- pure-library tests;
- lint/static validation;
- deterministic tests that do not need Docker/browser/services.

#### E2 — exact-SHA CI

Preferred shared automation for:
- build;
- .NET/Web test suites;
- analyzers;
- component/e2e jobs already supported by repository Actions;
- exact candidate status.

A DEV may prepare tests without being able to execute them locally; Main/AUD must obtain E2 evidence before integration whenever the affected gate requires it.

#### E3 — mounted local product

Requires a capable Docker/browser/harness executor.

Evidence:
- real DB/API/Web;
- normal mounted product UI;
- persistence/restart;
- lifecycle;
- local product behavior.

This tier is **not assumed available in ordinary parallel chats**.

#### E4 — remote/forwarded environment

Requires a real Codespace or accepted equivalent remote topology.

Evidence:
- forwarding/proxy/network path;
- remote timing;
- browser waterfall;
- environment-specific lifecycle.

A local simulator may provide controlled latency evidence, but it cannot certify actual Codespaces forwarding behavior.

#### E5 — independent Human Preview

Product Owner performs normal user journey without internal implementation guidance.

This remains a distinct acceptance source and cannot be replaced by CODEX/source tests.

### Standard lane handoff fields

Every DEV/AUD handoff must include:

`EVIDENCE_CAPABILITY`
- `AVAILABLE: E0,E1,...`
- `EXECUTED: <tiers actually run>`
- `NOT_AVAILABLE: <tiers unavailable in this environment>`
- `REQUIRED_NEXT: <exact missing validation>`

If a required tier is unavailable, use:

`ENV_CAPABILITY_GAP / <tier> / <reason> / <recommended executor>`

This is **not** a product failure.

### Integration minimum

Main determines the minimum tier by change type:

- contract/docs-only: E0/E1 as applicable;
- backend/library logic: E2 minimum;
- user-facing mounted UI: E2 + E3 before acceptance;
- remote-timing claim: E2 + E3 + E4 before root closure;
- fresh-install/lifecycle authority: E2 + E3, plus E5 at the scheduled Human gate;
- final correction-round acceptance: E2 + E3 + applicable E4 + E5.

No lane may lower the required tier because its own environment is limited.
