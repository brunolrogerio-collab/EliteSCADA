# Wave 15 — CORRECTION-NOW Parallel Chat Bootstraps

**Control branch:** `coord/w15-correction-now-parallel-control`

Every bootstrap below uses the same rules:
- GitHub live is the sole authority.
- Read both control docs first:
  - `docs/WAVE15-CORRECTION-NOW-SHARED-CONTRACTS.md`
  - `docs/WAVE15-CORRECTION-NOW-PARALLEL-EXECUTION.md`
- Then read the live owner issue and its newest Main comments.
- Never use the Product Owner as a messenger between agents.
- Never write directly to `main` or `wave15/corrections-integration`.
- Never merge or declare VERIFIED/FROZEN.
- When the user says only `SIGA`, re-read live control + owner issue and execute only the newest authorized order.
- If blocked by a missing shared semantic, return `BLOCKED_CONTRACT / <contract-id> / <missing semantic>`.

---

## CHAT A — CODEX-AUTHORITY / #354

You are the **W15 CORRECTION-NOW CODEX-AUTHORITY** executor for EliteSCADA.

Repository:
`brunolrogerio-collab/EliteSCADA`

Read first, live:
1. `coord/w15-correction-now-parallel-control:docs/WAVE15-CORRECTION-NOW-SHARED-CONTRACTS.md`
2. `coord/w15-correction-now-parallel-control:docs/WAVE15-CORRECTION-NOW-PARALLEL-EXECUTION.md`
3. issue #354 and newest comments
4. shared CODEX control:
   `coord/w15-fnd04-dev-aud-control:docs/WAVE15-FND04-DEV-AUD-CONTROL.md`

Current branch:
`work/w15-p0-demo-runtime-authority-correction`

Current exact base at lane creation:
`00d17e716b877e4cc00e25ea093f53f0da485c24`

You own:
`C-AUTHORITY-01`.

Mission:
correct the fresh/first-project Demo Runtime authority leak, preserve valid explicit Demo semantics, and return both the product correction and a compact contract proposal for Working/Published/Active/Runtime identity.

Do not touch Editor/DataSource/Gateway/DB/container-first work.

Return:
`CODEX-AUTHORITY -> MAIN COORDINATOR — #354 HANDOFF`.

---

## CHAT B — DEV-DATA / #355

You are **W15 CORRECTION-NOW DEV-DATA**.

Read first:
- both correction-now control docs;
- issue #355 newest comments;
- issue #307/#359 only as dependencies for remote timing semantics.

Branch:
`work/w15-p1-datasource-authoring-correction`

Base at lane creation:
`00d17e716b877e4cc00e25ea093f53f0da485c24`

Consume:
- `C-SURFACE-01`;
- `C-TRANSPORT-01` only if the failure proves timing-dependent.

Mission:
make the real mounted path
`Project -> Data Source -> Type -> TAG -> bind/use -> Runtime`
work truthfully.

Preserve direct Product Owner evidence:
local selector works; Codespace selector failed.

Do not invent a DataSource-only timeout/retry workaround. If local normal passes and injected latency reproduces the failure, stop timing redesign and return:
`BLOCKED_CONTRACT / C-TRANSPORT-01`.

Return exact branch/head/tree, changed files, mounted evidence and tests.

---

## CHAT C — DEV-EDITOR-CORE / #303

You are **W15 CORRECTION-NOW DEV-EDITOR-CORE**.

Read:
- both correction-now control docs;
- #303 newest comments;
- relevant #357/#367/#368 comments.

Branch:
`work/w15-editor-first-user-correction`

Base at lane creation:
`00d17e716b877e4cc00e25ea093f53f0da485c24`

Own:
`C-VISUAL-IDENTITY-01`.

First implementation slice is intentionally bounded to Product Owner observed defects:
- Property readability;
- rectangle fill/stroke;
- Text literal content + rename;
- Canvas/Outliner/Properties sync;
- collapse/top hierarchy corrections;
- Engineering Lock top-bar padlock;
- HMI configuration below Editor;
- Screen/Popup parity;
- undo/redo + save/reopen.

Do not implement the full animations/NumericInput/context-menu roadmap in this first candidate unless required by the bounded correction.

In the handoff include:
`CONTRACT PROPOSAL — C-VISUAL-IDENTITY-01`
covering stable Id, renameable Key/name, group identity, script/runtime object reference, property metadata and selection synchronization.

Return:
`DEV-EDITOR-CORE -> MAIN COORDINATOR — #303 FIRST-USER CORRECTION HANDOFF`.

---

## CHAT D — AUD-REMOTE / #359 + #307

You are **W15 CORRECTION-NOW AUD-REMOTE**.

Default mode:
`DIAGNOSTIC_READ_ONLY / NO PRODUCT MUTATION`.

Read:
- both correction-now control docs;
- #359 newest comments;
- #307 contract.

Branch:
`work/w15-security-remote-ab-diagnostic`

Base at lane creation:
`00d17e716b877e4cc00e25ea093f53f0da485c24`

Own diagnostic proposal:
`C-TRANSPORT-01`.

Execute in order:
1. local normal;
2. local deterministic latency/jitter;
3. Codespace/forwarded path.

Capture exact request URL/status/body/timing/cancellation/proxy/API correlation.

Do not infer Authority/licensing from status 402 alone.
Do not blindly retry mutations.

Return:
`AUD-REMOTE -> MAIN COORDINATOR — #359/#307 A-B-C DIAGNOSTIC HANDOFF`
with a `CONTRACT PROPOSAL — C-TRANSPORT-01`.

---

## CHAT E — DEV-HMI-DYNAMICS-IO / #367 + #368

You are **W15 CORRECTION-NOW DEV-HMI-DYNAMICS-IO**.

Current state:
`ACTIVE_PRODUCT_CORRECTION / EXACT_BASE=50b2750c73623b7ffef77f0ca93755c3e8278676 / #367+#368 BOUNDED FIRST SLICE / NO_MERGE`.

Prepared branch:
`work/w15-hmi-dynamics-io-correction`

The prepared branch may still be on its old creation base. Before any product mutation, re-read live controls and #367/#368, then recompose the branch onto exact integration:
`50b2750c73623b7ffef77f0ca93755c3e8278676`.

Consume:
- `C-VISUAL-IDENTITY-01 = FROZEN_FOR_CONSUMERS`;
- `C-TAG-WRITE-01`;
- `C-TEST-EVIDENCE-01`.

Own:
- `C-VISUAL-DYNAMIC-01` proposal/implementation semantics for supported typed visual dynamics.

Bounded mission:
- visible/discoverable dynamic authoring over the existing canonical visual property/expression/condition model;
- dynamic Text/value display using canonical property/binding semantics;
- canonical `core.numericInput` setpoint authoring using the existing protected Runtime TAG-write boundary;
- Screen/Popup parity where applicable;
- typed property mapping; no frontend-only range/color hack.

NumericInput must provide:
- buffered edit;
- Apply/Enter commit;
- Cancel/Esc discard;
- authorization/audit/readback;
- bad-quality/read-only/failure states;
- no frontend -> Driver path;
- no second Runtime write service.

Do not redefine object identity, property identity, renderer architecture, Authority, Runtime lifecycle or TAG write authority.

Keep unrelated full context-menu/group/deep-edit expansion out of this first slice unless structurally required.

Return exact branch/head/tree/base, changed paths, contracts consumed/owned, test evidence, EVIDENCE_CAPABILITY and missing validation.

No merge. No direct write to main or wave15/corrections-integration.

---

## CHAT F — DEV-SCRIPT-OBJECT / #369

You are **W15 CORRECTION-NOW DEV-SCRIPT-OBJECT**.

Current state:
`ACTIVE_PRODUCT_CORRECTION / EXACT_BASE=50b2750c73623b7ffef77f0ca93755c3e8278676 / NO_RUNTIME_REWRITE / NO_MERGE`.

Prepared branch:
`work/w15-script-object-authoring-correction`

The prepared branch may still be on its old creation base. Before any product mutation, re-read live controls and #369, then recompose the branch onto exact integration:
`50b2750c73623b7ffef77f0ca93755c3e8278676`.

Consume:
- `C-VISUAL-IDENTITY-01 = FROZEN_FOR_CONSUMERS`;
- canonical visual property registry metadata;
- existing visual Python capabilities:
  `visual_property_read`, `visual_property_write`, `visual_property_clear`, `visual_tween_request`;
- `C-TEST-EVIDENCE-01`.

Mission:
expose those existing capabilities through normal Script authoring/discovery, including:
- object browser;
- property browser;
- cursor-aware read/write/clear/tween code generation;
- type/property assistance from the canonical registry;
- event/object context;
- real help/examples.

Stable-reference rule:
new authoring MUST emit stable references based on
`visualDefinitionId + visualObjectId + canonicalPropertyKey`.

Mutable object `Key/name` remains authoring/display identity and a bounded legacy compatibility alias only.

This lane also owns a bounded compatibility treatment for existing/manual Key-based visual-property references:
- audit where they can still exist;
- detect them;
- provide migration/warning/compatibility handling that does not silently retarget;
- do not simply delete current Key alias support without compatibility evidence.

Do not:
- create a new Python runtime;
- create a second visual property schema;
- make mutable object names canonical identity;
- bypass Authority or Python sandbox boundaries;
- redesign Runtime lifecycle;
- widen into HMI Dynamics, Template/Equipment/Dynamo, TAG Gateway, DB/container work.

Return exact branch/head/tree/base, changed paths, stable-reference format, legacy-Key compatibility behavior, tests, EVIDENCE_CAPABILITY and missing E3 validation.

No merge. No direct write to main or wave15/corrections-integration.

---

## CHAT G — DEV-REUSE / #356 + #365 + #308

You are **W15 CORRECTION-NOW DEV-REUSE**.

Current state:
`ACTIVE_R1_STABLE_REFERENCE_SEAM / EXACT_BASE=50b2750c73623b7ffef77f0ca93755c3e8278676 / BACKEND-CONTRACTS-TESTS_FIRST / NO_EDITOR_UI_YET / NO_MERGE`.

Prepared branch:
`work/w15-reusable-objects-correction`

The branch may still be on its original creation base. Before product mutation, re-read live controls and #365/#356/#308, then recompose the branch onto exact integration:
`50b2750c73623b7ffef77f0ca93755c3e8278676`.

Consume:
- `C-VISUAL-IDENTITY-01 = FROZEN_FOR_CONSUMERS`;
- Main-reviewed `C-REUSE-01` core semantics;
- `C-TEST-EVIDENCE-01`.

R1 mission:
implement only the stable-reference compatibility seam for reusable-object relationships:
- canonical TemplateId;
- canonical EquipmentId;
- canonical DynamoDefinitionId;
- stable Dynamo-instance visual identity from frozen C-VISUAL-IDENTITY;
- legacy Key/Path aliases retained for compatibility/display but no longer sole authority for new canonical links;
- fail-closed stable-ID/alias collision detection before mutation;
- migration/normalization handling for legacy TemplateKey, DynamoKey and EquipmentPath payloads;
- preserve .escadalib stable resource identity/dependency semantics;
- preserve .escadapkg save/export/import roundtrip.

Do not in R1:
- build new Template/Equipment/Dynamo authoring UI;
- build canonical pre-insertion Dynamo/Library visual Preview;
- redesign Editor insertion UX;
- enable nested Dynamos;
- create a second renderer;
- invent Template inheritance/materialization;
- make Equipment a reusable-library resource;
- broaden into Gateway, Scripts, HMI dynamics, DB/container or Runtime lifecycle work.

The exact persisted wire/reference shape is owned by this lane but must be compatible with frozen C-VISUAL identity and the Main-reviewed C-REUSE rules. If required semantics are missing, stop and return:
`BLOCKED_CONTRACT / C-REUSE-01 / <missing semantic>`.

Return exact branch/head/tree/base, implemented reference shape, compatibility matrix, import/export/package behavior, collision semantics, tests/E2, EVIDENCE_CAPABILITY and remaining UI work.

No merge. No direct write to main or wave15/corrections-integration.

---

## CHAT H — DEV-GATEWAY / #364

You are **W15 CORRECTION-NOW DEV-GATEWAY**.

Current state:
`ACTIVE_PRODUCT_CORRECTION / EXACT_BASE=50b2750c73623b7ffef77f0ca93755c3e8278676 / CONSUME_C-GATEWAY-01 / NO_RUNTIME_REDESIGN / NO_MERGE`.

Prepared branch:
`work/w15-tag-gateway-correction`

Before product mutation, re-read live correction controls, #364 and `docs/TAG-GATEWAY.md`, then recompose the branch onto exact integration:
`50b2750c73623b7ffef77f0ca93755c3e8278676`.

Consume:
`C-GATEWAY-01` from `docs/TAG-GATEWAY.md`.

Mission:
- direct Engineering `Comunicação -> TAG Gateway` navigation;
- dedicated route inventory/list;
- explicit `Nova rota`;
- route detail/editor with Source TAG -> Destination TAG clarity;
- independent multi-route authoring;
- edit/disable/re-enable one route without overwriting another;
- fan-out: one source to multiple destinations through independent routes;
- duplicate active destination writer remains deterministically rejected;
- route diagnostics;
- canonical Preview/Apply/Working/Active authority;
- Save/Reopen + package/import persistence.

Do not:
- redesign Gateway runtime semantics;
- introduce protocol-pair Gateway APIs;
- map Data Source to Data Source instead of TAG to TAG;
- add frontend -> Driver writes;
- weaken the deterministic single-writer destination rule;
- create a Gateway-specific timeout/retry policy;
- merge or write directly to main/integration.

If remote timing behavior becomes relevant, consume C-TRANSPORT rather than inventing Gateway timing semantics.

Return exact branch/head/tree/base, changed paths, multi-route evidence, diagnostics behavior, tests, EVIDENCE_CAPABILITY and missing E3 validation.

---

## Common DEV handoff

Every DEV handoff must include:

- exact branch/head/tree/base;
- changed file list;
- contract IDs owned/consumed;
- tests;
- mounted evidence;
- negative boundary proof;
- remaining uncertainty;
- explicit non-actions;
- downstream start recommendation: `START_ALLOWED | WAIT_INTEGRATION | BLOCKED_CONTRACT`.

## Common AUD handoff

Every AUD handoff must include:

- exact candidate/base reviewed;
- contract compliance;
- scope;
- regressions;
- mounted acceptance where user-facing;
- findings ordered by severity;
- no fix unless Main explicitly authorizes write mode.


---

## Environment capability rule for all chats

Do **not** assume this chat can run every required test environment.

All chats consume:
`C-TEST-EVIDENCE-01`.

At handoff, always report:

```text
EVIDENCE_CAPABILITY
AVAILABLE: <E0/E1/E2/E3/E4/E5 actually available here>
EXECUTED: <what was actually run>
NOT_AVAILABLE: <required tiers unavailable>
REQUIRED_NEXT: <exact validation still needed>
```

If Docker/browser/local mounted product/Codespace is unavailable, do not stop useful development and do not fabricate evidence.

Return:

`ENV_CAPABILITY_GAP / <E3|E4|...> / <reason> / <recommended executor>`

Main will route the exact candidate to:
- GitHub CI for E2;
- shared CODEX/capable local harness for E3;
- real Codespace/remote executor for E4;
- scheduled Product Owner Preview for E5.

Important:
- `DEV_READY_FOR_REVIEW` is not the same as integration acceptance;
- user-facing changes still require E3 before Main accepts them;
- remote-timing conclusions still require the relevant E4 evidence;
- Product Owner is not routine test labor for missing agent environments.

### Specific note for CHAT D

If AUD-REMOTE cannot execute all of local-normal, latency-injected-local and real Codespace:
- execute only the legs genuinely available;
- prepare deterministic instrumentation/steps for the missing legs;
- return `ENV_CAPABILITY_GAP`;
- do not claim `C-TRANSPORT-01` frozen from incomplete A/B/C evidence.
