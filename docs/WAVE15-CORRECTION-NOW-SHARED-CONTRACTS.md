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


### Integration eligibility vs phase acceptance

Do not conflate GitHub mechanical `mergeable` with Main product acceptance.

For CORRECTION-NOW integration into `wave15/corrections-integration`:

- docs/contract-only change: E0/E1 as applicable;
- backend/library correction: E2 minimum;
- ordinary user-facing correction: **E2 + E3** is the normal Main integration gate;
- E4 is **not** a universal merge prerequisite. It is required before accepting/closing a claim whose correctness specifically depends on the real remote/forwarded environment, such as #307/#359 remote timing/root closure;
- E5 is **not** a normal PR merge prerequisite. It is a later Human Preview / correction-phase acceptance gate.

Therefore a user-facing correction may become:

`MAIN_ACCEPTED_FOR_INTEGRATION`

after E2+E3 even though broader E4/E5 validation is still scheduled, provided:
- the branch does not claim an unproven remote-specific fix;
- no known P0/P1 remains in the corrected local/product path;
- Main records any outstanding E4/E5 as downstream validation obligations.

For remote-specific correction branches, Main may require E4 before integration when the change itself cannot be safely accepted without proving the real forwarded path.


### C-VISUAL-IDENTITY-01 compatibility refinement from Main review of #371

Current evidence adds one compatibility rule before freeze:

- canonical stable identity for new authoring/persistence is `visualDefinitionId + visualObjectId`;
- property references add canonical `propertyKey`;
- mutable `Key/name` remains developer-facing and renameable;
- current Visual Python runtime accepts `objectKey` as a compatibility alias for targetReference;
- this Key alias is not the canonical rename-safe identity and must not be emitted by new #369 authoring;
- existing/manual Key-based references require bounded compatibility handling before correction-round acceptance.

Until #371 passes E3 and is integrated:
`C-VISUAL-IDENTITY-01 = DRAFT_OWNER / E2_ACCEPTED / WAIT_E3`.

After #371 integration Main may freeze the contract with the compatibility rule above and release #367/#368/#369 consumers.


### C-AUTHORITY-01 — Main review after #354 candidate

Candidate under review:
`work/w15-p0-demo-runtime-authority-correction@c84dee88268ecf786737cf3ec9364d0113d5f028`

State:
`DRAFT_OWNER / E2_ACCEPTED / WAIT_E3`.

Provisional semantics accepted by Main:
- Working, Published and Active are distinct lifecycle authorities;
- Runtime project/revision/application truth derives only from persisted/recovered Active authority;
- no Active revision means `neutral`, with no implicit Demo/application fallback;
- neutral Runtime exposes no unowned operational reads and permits no process effects;
- recovery may rehydrate only the persisted Active project/revision and must not mint or silently select another application;
- Demo licensing/session authority does not select project/application content;
- any intentionally supported Demo application must use the same explicit publish/activate authority as another project.

Freeze remains blocked until exact-candidate E3 mounted authority/lifecycle proof and integration.


### C-AUTHORITY-01 — FROZEN_FOR_CONSUMERS

Frozen at integrated correction:
`wave15/corrections-integration@33e514eb3f5cf8f984779c0091069387741e7296`

Integrated tree:
`d7219cbbc27d39bdebb54193d6a71b9d3f65db2f`

Evidence:
- exact candidate E2 T1 `36370397556` SUCCESS;
- exact candidate E3 mounted PostgreSQL/API/Web/Chromium PASS;
- merge commit tree is byte-identical to the validated candidate tree.

Frozen semantics:
1. Working, Published and Active are distinct lifecycle authorities.
2. Runtime project/revision/application truth comes only from persisted/recovered Active authority.
3. No Active revision means neutral Runtime; no implicit Demo/application fallback.
4. Neutral Runtime exposes no unowned operational reads and permits no process effects.
5. Recovery may rehydrate only persisted Active authority; it must not mint/select another project/application.
6. Demo licensing/session authority does not select project/application content.
7. Any explicitly supported Demo application must enter through the same normal publish/activate authority as another project.

State:
`C-AUTHORITY-01 = FROZEN_FOR_CONSUMERS @ 33e514eb3f5cf8f984779c0091069387741e7296`.

Round-2 Preview must recheck these semantics independently before correction-phase acceptance.


### C-REUSE-01 — Main-reviewed draft after DEV-REUSE audit

Evidence:
#365 handoff `5862150703`.

State:
`MAIN_REVIEWED_DRAFT / CORE_SEMANTICS_ACCEPTED / WAIT_C-VISUAL-IDENTITY`.

Accepted core semantics:
- Template, Equipment and Dynamo definitions use their existing stable Guid IDs as canonical entity identity;
- mutable Key/Path aliases remain authoring/display/legacy compatibility, not sole authority for newly-authored cross-domain links;
- inserted Dynamo instance identity is the owning stable visual object identity;
- Dynamo instance -> Dynamo definition is mandatory;
- Dynamo instance -> Equipment is optional;
- Template association is optional affinity/context, not mandatory 1:1 ownership;
- Dynamo definitions remain live-linked to instances rather than copied as snapshots;
- .escadalib incorporation preserves stable resource identity and is not duplicate/copy;
- stable-ID/alias conflicts must fail closed before mutation;
- canonical Preview must use the actual canonical renderer/composition and cannot be satisfied by category glyphs or screenshots;
- nested Dynamo remains unsupported in v1 unless a later explicit contract changes it.

Still dependent on C-VISUAL-IDENTITY-01:
- exact persisted Screen/Popup visual-instance reference adapter/wire shape for DynamoDefinitionId and optional EquipmentId;
- compatibility migration from current DynamoKey/EquipmentPath references at the Editor boundary.

No DEV-REUSE product implementation is released while this dependency remains open.


### C-VISUAL-IDENTITY-01 — FROZEN_FOR_CONSUMERS

Frozen at integrated correction composition:
`wave15/corrections-integration@50b2750c73623b7ffef77f0ca93755c3e8278676`

Integrated tree:
`bf0b43ff9ea1211443d614487f4a05bab2ee2c97`

Evidence:
- #371 recomposed exact E2 T1 `36374037238` SUCCESS;
- combined #371 + #370 E3 handoff `5863212189` = `COMBINED_E3_PASS`;
- CODEX validated combined tree `bf0b43ff9ea1211443d614487f4a05bab2ee2c97`;
- Main merged #371 then #370;
- final integration tree is byte-identical to the CODEX-validated combined tree.

Frozen semantics:
1. every persisted visual object has an immutable stable object Id across rename/property edits/reorder/save-reopen;
2. canonical stable object reference is `visualDefinitionId + visualObjectId`;
3. property-level stable reference adds canonical `propertyKey`;
4. mutable `Key/name` is developer-facing authoring identity, not canonical Runtime/Script identity;
5. sibling Key uniqueness is case-insensitive; rename must not regenerate Id;
6. groups and children each retain their own stable Id; hierarchy does not replace identity;
7. Canvas/Outliner/Properties/Animations/Script authoring must resolve the same canonical object identity;
8. the visual property registry remains authority for canonical property key/type/editability/runtime-read-write/animatable-bindable semantics;
9. new Script object/property authoring must emit stable Id-based references;
10. current Visual Python `objectKey` targetReference remains a bounded legacy/runtime compatibility alias only; #369 owns detection/migration/warning behavior for legacy/manual Key references before correction-round acceptance.

State:
`C-VISUAL-IDENTITY-01 = FROZEN_FOR_CONSUMERS @ 50b2750c73623b7ffef77f0ca93755c3e8278676`.

This freeze releases #367/#368/#369 consumers and clears the visual-identity blocker for the next bounded #365 implementation slice.


### C-REUSE-01 — visual-identity blocker cleared

C-VISUAL-IDENTITY-01 is now frozen at integration `50b2750c73623b7ffef77f0ca93755c3e8278676`.

State transition:
`C-REUSE-01 = MAIN_REVIEWED_DRAFT / R1_IMPLEMENTATION_ACTIVE / OWNER #365`.

The previously blocked Editor identity dependency is resolved semantically. R1 remains deliberately backend/contracts/tests-first:
- canonical TemplateId / EquipmentId / DynamoDefinitionId references;
- alias compatibility for legacy Key/Path payloads;
- fail-closed stable-ID/alias collision handling;
- package/library roundtrip preservation.

Exact persisted wire field names/reference adapter details are owned by #365 implementation and must remain consistent with frozen C-VISUAL identity. New Editor UI/Preview remains outside R1.


### C-TRANSPORT-01 — local discriminator update

Evidence:
- shared CODEX #305 handoff `5863670500`;
- duplicate summary `5863679165` contains one SHA typo; authoritative integration remains `50b2750c73623b7ffef77f0ca93755c3e8278676`.

State:
`PROPOSAL / E3_A_PASS / E3_B_PASS / WAIT_E4_C_REAL_CODESPACE`.

Accepted evidence:
- A local normal Security users/roles/create/edit/save/reload = PASS;
- B deterministic delayed responses at 2.5s, 28s and 32s = PASS for the tested eventual-response profiles;
- no automatic mutation retry observed;
- a 32s delayed mutation response eventually completed and authoritative readback matched;
- Data Source catalog secondary witness returned 200;
- local health remained good.

Interpretation boundary:
- local normal does not reproduce the Human Codespace failure;
- deterministic local delay beyond the ordinary-read budget also did not reproduce it;
- therefore no generic local timeout increase or Security-specific retry correction is justified from A/B;
- the historical HTTP 402 source remains unassigned;
- real Codespace/forwarded-path E4-C is still required before remote root closure or C-TRANSPORT freeze.

The current lack of a distinct unknown-mutation-outcome state for a truly lost response remains a resilience-design gap, but A/B tested eventual delayed delivery rather than response loss. Do not infer a concrete mutation bug without the required discriminator.


### C-TRANSPORT-01 — E4 harness retry state

rev0104 did not execute the remote journey because the pinned no-delta Codespace lacked the exact .NET SDK and a durable DB/container capability.

State remains:
`PROPOSAL / E3_A_PASS / E3_B_PASS / E4_ENVIRONMENT_RETRY_ACTIVE`.

No transport/product inference is added from that environment failure.

rev0105 uses a dedicated Codespaces harness around the same exact product bytes:
`preview/w15-e4-codespace-harness-50b275@604806d012a3caaa804c7ce505a12b38cde3a939`.

Only homologation infrastructure differs from product baseline. C-TRANSPORT-01 still requires actual forwarded-browser E4 evidence before freeze/root closure.


### C-TRANSPORT-01 — E4 retry2

State remains:
`PROPOSAL / E3_A_PASS / E3_B_PASS / E4_ENVIRONMENT_RETRY2_ACTIVE`.

rev0105 did not reach product execution; the only failure was an over-strict harness SDK equality guard inconsistent with repository `latestFeature` policy.

No transport/product conclusion is added.

rev0106 uses the same real Codespace with harness HEAD `62c5b8068638681eddb6e707df2b2d393e8dbd71` and the same canonical product bytes.


---

## Round-2 shared contracts — prepared after Product Owner visible Preview

These contracts are **PREPARED / NOT RELEASED** while shared CODEX rev0113 is active.

No R2 DEV may implement a shared semantic before Main records the relevant contract as `FROZEN_FOR_CONSUMERS`.

### C-ENG-DENSITY-01 — Engineering information hierarchy / graphical wide mode

**State:** `PREPARED_DRAFT / WAIT_POST_#375_BASE`  
**Owner:** Main + shared CODEX contract review  
**Implementation consumer:** Chat I / DEV-ENG-DENSITY  
**Downstream consumers:** Chat J Editor UX, Chat L Branding settings integration, all Engineering modules.

Frozen semantics must define:
- `TASK_FIRST / COMPACT_CONTEXT / SECONDARY_INFORMATION_ON_DEMAND`;
- one compact persistent Engineering context row;
- dirty / Engineering Lock / CAS/conflict / current-task safety state remains visible when relevant;
- schema/base revision/snapshot timestamps move to Info/Diagnostics in healthy normal flow;
- graphical Screen/Popup surfaces can opt into a wide/full-width section mode;
- ordinary forms/lists retain readable widths;
- no change to Working/Published/Active authority.
- persistent security/lock controls follow `FEATURE_NAME_ON_DEMAND / CURRENT_STATE_PERSISTENT`: the compact header shows only a clear state + icon, while the full feature name/details remain in tooltip/aria-label/expanded management UI.
- only Workspace dirty/saved/conflict state remains permanently visible from the old Schema/Base revision/Workspace/Snapshot strip; it moves into the compact top Engineering context row so the dedicated metadata strip can disappear entirely.
- schema, base revision and snapshot timestamp move to an on-demand Engineering Information surface.
- the Information surface distinguishes human-facing product version from Engineering schema/project revision metadata.

Consumes:
- `C-AUTHORITY-01`;
- `C-SURFACE-01`;
- `C-TEST-EVIDENCE-01`.

Forbidden:
- redefining project lifecycle;
- hiding conflict/dirty/Lock state;
- adding browser-local layout state as project authority.

Chat J must consume the integrated wide-section/layout hook rather than invent a second one.

### C-PRODUCT-VERSION-01 — human-facing EliteSCADA product version

**State:** `PREPARED_DRAFT / FREEZE_BEFORE_CHAT_I`  
**Owner:** Main + shared CODEX contract review  
**Consumers:** Chat I / Engineering Information, global shell/help/diagnostics, packaging/release tooling.

Problem:
- current Web package declares generic `0.1.0`;
- Engineering schema versions such as `scada.engineering v19` are internal compatibility metadata;
- project base revision/snapshot are lifecycle metadata;
- none of those should be presented as the human-facing EliteSCADA product version.

Product Owner proposed current visible development identity:
`Alpha 0.15.2.1`.

Intended coordination meaning for this development cycle:
- `0` = pre-1.0 product;
- `15` = Wave 15 generation;
- `2` = post-Preview 2 convergence/revision cycle;
- `1` = correction package 1.

Freeze must define:
1. one canonical product-version source consumed by Web/API/distribution metadata;
2. human display string, including stage/channel such as `Alpha`;
3. build/commit provenance available under technical details without becoming the marketing version;
4. clear separation from Engineering schema version;
5. clear separation from project Working/base revision;
6. increment rule for future Wave/Preview/correction packages;
7. compatibility with future Beta/RC/1.0 release naming;
8. no component-local hard-coded version strings.

Normal Engineering Information presentation:
- primary: product name + version, e.g. `EliteSCADA Alpha 0.15.2.1`;
- secondary technical details: schema, base revision, snapshot timestamp, exact build/commit.

Until frozen, `Alpha 0.15.2.1` is the Product Owner-approved proposed display identity for the current cycle, not yet an implementation authority.

### C-USER-COPY-I18N-01 — user-facing terminology and multilingual identity

**State:** `PREPARED_DRAFT / DEFERRED_UNTIL_R2-A+R2-B_INTEGRATED`  
**Owner:** Main + #379 terminology/glossary review  
**Implementation consumer:** future Chat M / DEV-UX-COPY-I18N  
**Supported locales:** pt-BR / en / es.

Purpose:
- keep coordination/architecture vocabulary out of ordinary product UI;
- establish one intentional product glossary;
- remove accidental language mixing introduced by recent parallel development.

Internal-only vocabulary normally forbidden in ordinary user workflows:
- Wave / Wave 15;
- canonical/canônico when used as architecture-authority jargon;
- fail closed / falhar fechado;
- E0/E1/E2/E3/E4/E5;
- exact SHA/tree/branch/PR;
- control plane;
- C-* contract IDs;
- validation harness / coordinator wording.

Product/industrial terms requiring an explicit per-locale glossary decision:
- Engineering;
- Runtime;
- TAG;
- Driver;
- Data Source;
- Historian;
- Script;
- Popup;
- Workspace;
- Preview.

Rules:
1. backend/API/internal enum names do not need renaming merely for display copy;
2. user-facing actions/statuses must be natural and consistent in the selected locale;
3. shared concepts should use shared translation keys/glossary, not divergent component-local wording;
4. no accidental English fallback in pt-BR/es for ordinary copy;
5. accessibility labels/tooltips follow locale too;
6. backend technical codes may remain available under diagnostic details, while primary message is localized/actionable;
7. no blind string replacement: inventory + glossary + migration + regression first.

Schedule:
- after R2-A and R2-B structural changes integrate;
- before the next integrated E3 + SECOND Preview/Audit.

This sequencing avoids merge conflicts while ensuring the next Product Owner Preview evaluates a coherent multilingual UI.

### C-EDITOR-UX-R2-01 — shared Screen/Popup interaction model

**State:** `PREPARED_DRAFT / WAIT_C-REUSE-01_INTEGRATED / WAIT_C-VISUAL-ASSET-02`  
**Owner:** Main + shared CODEX contract review  
**Implementation consumer:** Chat J / DEV-EDITOR-UX-R2

Frozen semantics must define:
- frequent built-in object insertion through compact toolbar actions;
- Structure + Dynamo/library/assets in one shared side-authoring surface;
- no Outliner overlay over canvas;
- independent scrolling of Structure and Library regions;
- collapsed state releases nearly all layout width;
- reopen affordance remains visible, focusable and high-contrast;
- object/group contextual action model;
- click-vs-drag threshold/lifecycle;
- group deep-edit without identity loss;
- object label visibility mode = all / selected / hidden;
- Screen/Popup parity.

Consumes without redefining:
- `C-VISUAL-IDENTITY-01`;
- `C-VISUAL-DYNAMIC-01`;
- `C-TAG-WRITE-01`;
- `C-REUSE-01` after #375 integration/freeze;
- `C-VISUAL-ASSET-02`;
- `C-AUTHORITY-01`;
- `C-SURFACE-01`;
- `C-TEST-EVIDENCE-01`.

Forbidden:
- new visual identity;
- second renderer;
- private property registry;
- alternate TAG write path;
- alternate Dynamo/reuse relationship;
- alternate asset store/import authority.

### C-VISUAL-ASSET-02 — common asset/import authority for Editor + Branding

**State:** `PREPARED_DRAFT / SHARED_R2_FOUNDATION_REQUIRED`  
**Owner:** Main + shared CODEX contract review  
**Consumers:** Chat J Editor UX, Chat L Branding, Screen/Popup/Dynamo/Runtime/package paths.

Existing canonical authority:
- `docs/VISUAL-ASSETS-AND-IMAGES.md`;
- stable `VisualAssetEngineeringDto` identity;
- existing project asset import/storage/package boundary;
- current raster support PNG/JPEG/BMP.

This contract must freeze **before Chat J and Chat L run in parallel**.

Required semantics:
1. one canonical project asset identity/store/API;
2. local filesystem paths never become persisted object/branding references;
3. Screen/Popup/image/background/branding all reference stable asset ID;
4. import validation remains server-authoritative;
5. project package/export/import preserves asset identity/hash/content;
6. missing/corrupt assets fail explicitly;
7. existing PNG/JPEG/BMP support remains compatible;
8. if SVG is implemented, it extends this same authority rather than creating a branding-only file path;
9. safe SVG means static sanitized vector only:
   - bounded XML parse;
   - reject scripts;
   - reject event handlers;
   - reject `foreignObject`;
   - reject external/network references;
   - reject active/executable content;
   - deterministic sanitized/canonical representation;
10. Runtime and Engineering consume only the accepted served asset representation.

Ownership split after freeze:
- **Chat J** may change Editor image/background selection/import UX, but may not change asset persistence/validation semantics.
- **Chat L** may implement the agreed SVG validator/asset extension and branding consumer, but may not create another asset store or Editor-specific behavior.
- if either consumer discovers a missing asset semantic, return:
  `BLOCKED_CONTRACT / C-VISUAL-ASSET-02 / <missing semantic>`.

### C-ENG-WORKFLOW-01 — task-oriented Engineering forms and entity workflows

**State:** `PREPARED_DRAFT / FREEZE_AFTER_R2-A / BEFORE_CHAT_N`  
**Owner:** Main + #380 workflow review  
**Implementation consumer:** Chat N / DEV-ENG-WORKFLOW-FORMS  
**Downstream consumer:** Chat M / UX-COPY-I18N.

Core principle:
`ENTITY_FIRST / TASK_FIRST / ONE_CONTEXT / PROGRESSIVE_DISCLOSURE`.

Required semantics:
1. one selected entity context drives normal single-entity operations;
2. delete/contextual actions reuse the current selected entity instead of requiring duplicate selection;
3. bulk edit is an explicit mode, not a permanently dominant secondary editor;
4. bulk mode should reuse the primary entity list/multi-selection where practical rather than render a duplicate entity list;
5. fields are grouped by user mental model instead of DTO/property order;
6. required/common fields are primary; advanced/rare fields use progressive disclosure;
7. conditionally irrelevant fields are hidden or disabled with concise explanation;
8. non-obvious fields provide unit/example/range/helper information;
9. empty states lead to the first useful creation action;
10. destructive actions are secondary/contextual, with dependency/CAS safety preserved;
11. Preview/Apply/Working/Published/Active semantics remain truthful and consume existing authority contracts;
12. shared small form primitives are preferred over another large generic form framework;
13. Screen/Popup graphical Editor internals remain owned by Chat J;
14. Branding settings remain owned by Chat L but may consume the shared form grammar;
15. no backend/API/DTO semantic change is authorized merely to make the UI look simpler.

Initial structured surfaces:
- Data Sources;
- TAGs;
- TAG Gateway;
- Alarms;
- Events;
- Templates;
- Equipment;
- non-graphical Dynamo/library forms;
- Historian;
- Security/Engineering Lock settings;
- Reports non-canvas settings;
- Script metadata/entry points outside Monaco;
- authoring/configuration diagnostics where applicable.

Consumes:
- integrated `C-ENG-DENSITY-01`;
- integrated/frozen `C-ENG-THEME-01`;
- `C-AUTHORITY-01`;
- `C-SURFACE-01`;
- `C-TEST-EVIDENCE-01`.

Must preserve:
- CAS/version checks;
- dependency validation;
- secured delete behavior;
- bulk preview-before-apply;
- Working/Published/Active authority;
- existing entity stable IDs and API contracts.

Any missing shared semantic returns:
`BLOCKED_CONTRACT / C-ENG-WORKFLOW-01 / <missing semantic>`.

### C-BRANDING-01 — canonical client branding

**State:** `PREPARED_DRAFT / WAIT_C-VISUAL-ASSET-02 / WAIT_C-ENG-DENSITY-01_INTEGRATED`  
**Owner:** Main + #377 contract review  
**Implementation consumer:** Chat L / DEV-BRANDING  
**Product consumers:** global application shell, Engineering, Runtime and other shell surfaces.

Required semantics:
- mode = `DEFAULT | TEXT | IMAGE | NONE`;
- absence/legacy = default EliteSCADA branding;
- text is plain display text, never HTML;
- image references canonical `C-VISUAL-ASSET-02` asset ID;
- no branding-only blob/file authority;
- Working/Preview/Apply/Published/Active semantics consume `C-AUTHORITY-01`;
- unsaved/Working branding does not silently mutate Active Runtime branding;
- Runtime resolves Active branding;
- branding settings may preview Working candidate locally without changing Active authority;
- `NONE` removes reserved brand width cleanly;
- package/export/import/restart/recovery preserve config and asset reference;
- missing/corrupt image produces explicit fallback/diagnostic.

Consumes:
- `C-AUTHORITY-01`;
- `C-VISUAL-ASSET-02`;
- `C-ENG-DENSITY-01`;
- `C-SURFACE-01`;
- `C-TEST-EVIDENCE-01`.

Forbidden:
- deployment-local logo file as project truth;
- direct Working -> Runtime shell mutation;
- independent shell branding state per page.

### C-ENG-THEME-01 — specialized Engineering surface theme/contrast

**State:** `PREPARED_DRAFT / FREEZE_BEFORE_CHAT_K`  
**Owner:** Main + shared CODEX contract review  
**Implementation consumer:** Chat K / DEV-THEME-CONTRAST  
**Consumers:** Report Designer, Script/Python editor, shared structured Engineering forms/mutation panels, future specialized Engineering authoring surfaces.

Purpose:
- specialized editors must consume the same Engineering semantic theme authority;
- no accidental fallback to unrelated generic tokens or hard-coded light surfaces inside dark Engineering;
- third-party/editor surfaces such as Monaco must track the active EliteSCADA theme deliberately.

Required semantics:
1. Engineering semantic tokens (`--eng-*` or a deliberately shared semantic layer) remain the source of truth for surrounding authoring chrome;
2. generic fallbacks such as `--surface: #fff` / `--border: #d6dae2` must not silently override dark Engineering;
3. shared structured forms/mutation panels must not hard-code light-only surfaces such as `#fff`, `#fafbfd`, `#eef2f7` when rendered inside dark Engineering; they must consume semantic theme tokens;
4. document/paper-like content may intentionally remain light only when that light surface is semantically part of the authored artifact, e.g. report paper;
5. embedded code editors must deliberately select a light/dark editor theme consistent with the active app theme;
6. foreground/background contrast remains readable for normal, muted, disabled, hover, selected, focus-visible, warning, error and success states;
7. theme correction must not alter report/script/entity data models, lifecycle, mutation semantics, Script capability rules or Runtime semantics;
8. dark and light themes are both acceptance targets.

### R2 contract dependency matrix

| Consumer | Required shared contracts before implementation |
|---|---|
| Chat I — ENG-DENSITY | C-AUTHORITY-01, C-SURFACE-01, C-TEST-EVIDENCE-01, frozen C-ENG-DENSITY-01, frozen C-PRODUCT-VERSION-01 |
| Chat K — THEME-CONTRAST | C-SURFACE-01, C-TEST-EVIDENCE-01, frozen C-ENG-THEME-01; consume existing Engineering semantic theme tokens |
| Chat J — EDITOR-UX-R2 | C-ENG-DENSITY-01 integrated, C-VISUAL-IDENTITY-01, C-VISUAL-DYNAMIC-01, C-TAG-WRITE-01, C-REUSE-01 integrated/frozen, C-VISUAL-ASSET-02 frozen, C-SURFACE-01, C-TEST-EVIDENCE-01 |
| Chat L — BRANDING | C-ENG-DENSITY-01 integrated, C-AUTHORITY-01, C-VISUAL-ASSET-02 frozen, C-BRANDING-01 frozen, C-SURFACE-01, C-TEST-EVIDENCE-01 |
| Chat N — ENG-WORKFLOW-FORMS | C-ENG-DENSITY-01 integrated, C-ENG-THEME-01 integrated/frozen, frozen C-ENG-WORKFLOW-01, C-AUTHORITY-01, C-SURFACE-01, C-TEST-EVIDENCE-01 |

### Parallel release consequence

The planned R2 parallelism remains valid only after the dependency gates above:

1. post-#375 exact integration base;
2. freeze C-ENG-DENSITY-01;
3. R2-A: I + K may run in parallel;
4. integrate I/K;
5. freeze C-VISUAL-ASSET-02 and C-BRANDING-01 against the new base;
6. ensure C-REUSE-01 is integrated/frozen;
7. R2-B: J + L + N may run in parallel only with explicit file/authority ownership and after C-ENG-WORKFLOW-01 is frozen.

If these conditions are not met, do not release the corresponding chat.
