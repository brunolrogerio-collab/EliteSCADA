# PROJECT GOAL — EliteSCADA

> Persistent product north and continuity contract.
>
> This file preserves stable product goals and locked architecture across ChatGPT conversations, developers and tooling. It defines intent, not merely current implementation state.

**Last reviewed:** 2026-09-01

## Mandatory continuity protocol

1. At the beginning of every EliteSCADA task, read `PROJECT GOAL.md` and `LAST CHANGE.md` before planning or changing code.
2. The repository, not chat history, is the persistent coordination memory. A fresh ChatGPT conversation must be able to resume safely from repository state alone.
3. Every material coordination cycle, decision, blocker, fix, validation run or change of next action must review and synchronize **both** `PROJECT GOAL.md` and `LAST CHANGE.md` in the same task when their recorded state is affected.
4. Stable product goals, architecture and permanent coordination rules belong in `PROJECT GOAL.md`; exact mutable branch/SHA/run/issue/blocker/next-action state belongs in `LAST CHANGE.md`.
5. No critical decision, blocker, diagnosis, acceptance evidence or next action may exist only in chat history.
6. If the user adds, removes or clarifies a stable product goal or architectural rule, update this file in the same task.
7. Before the final response of an EliteSCADA task, verify `LAST CHANGE.md` reflects the actual repository/CI state and update it if anything material changed.
8. `LAST CHANGE.md` must distinguish **MERGED**, **IMPLEMENTED IN PR/BRANCH** and **SPECIFIED / NOT IMPLEMENTED OR NOT ACCEPTED**.
9. `docs/ROADMAP.md` must remain consistent with this product north.
10. Permanent architectural decisions must not exist only in a feature branch or chat history; when a temporary coordination branch is active, the handoff must explicitly preserve what still needs propagation to `main`.
11. If conversation memory, documentation and repository disagree, inspect live `main`, the active branch, issues and exact-SHA CI. Repository/CI state wins for what is implemented; this file wins for explicitly locked future product intent.
12. When the Development Lead says `siga`, treat it as an instruction to continue executing the active coordination sequence until completion or a real external/blocking condition, rather than stopping after each intermediate diagnosis. Persist material checkpoints while executing.

### Current release-sequencing gate

The seven communication Drivers completed the integrated L3 interoperability gate under issue #180, and the subsequent pre-Wave-11 owner-usability gate #191 covering the graphical License Generator, canonical industrial Slider, explicit application-file saving/opening and minimum ready-to-insert Dynamo library has been accepted and integrated. **Wave 11 is authorized as the next development stage.** Exact current issue/branch/SHA/run state remains in `LAST CHANGE.md`.

## Product mission

EliteSCADA is intended to become a serious industrial SCADA/supervisory platform, not merely a monitoring dashboard.

The product must support the complete application lifecycle:

- Engineering configuration;
- reusable equipment/classes, Dynamos and visual libraries;
- screens and popups;
- communication drivers and internal value sources;
- live runtime;
- commands, alarms and events;
- historian, trends and industrial reporting;
- Python scripting;
- security, identity and audit;
- project revisioning, publication and activation;
- backup, restore, import/export and cross-project reuse;
- public contracts/SDKs and installable driver modules.

The long-term responsibility is comparable to established industrial supervisory platforms while retaining EliteSCADA's own architecture, data model and implementation.

## Demo, Preview distribution and hardware-bound licensing

EliteSCADA must support an externally distributable **Demo/Preview mode** plus a machine-bound licensed mode. Licensing is a product/host capability and must never be implemented independently inside individual Drivers.

### Demo mode

When no valid license is installed, EliteSCADA operates in Demo mode.

Locked Demo behavior:

- Engineering may create, import, save and edit projects containing more than 200 TAGs;
- **Run/activation is permitted only when the project contains at most 200 TAGs**;
- if the project exceeds 200 TAGs, Run is blocked fail-closed without deleting/truncating Engineering data or replacing the previous active runtime;
- Demo industrial runtime may execute for at most **300 continuous minutes per explicit Run session**;
- the 300-minute limit is per continuous execution, not a cumulative lifetime quota;
- expiry stops industrial runtime gracefully through the normal lifecycle while Engineering/application UI remains available;
- the user is clearly informed that the 300-minute evaluation period expired;
- a later explicit Run starts a fresh 300-minute Demo session;
- elapsed enforcement must use monotonic time so wall-clock changes cannot extend the session.

The accepted licensing implementation enforces the Demo TAG entitlement at Run/activation while preserving unrestricted Engineering authoring above 200 TAGs.

### Licensed/evaluation mode

A valid EliteSCADA license is bound to the target hardware and removes the 300-minute Demo continuous-runtime limit.

Initial TAG entitlement tiers are:

- 500;
- 1,000;
- 1,500;
- 3,000;
- 5,000;
- Unlimited.

A license issued for authorized development/customer evaluation above 200 TAGs uses the same entitlement mechanism and, under the current contract, also has no 300-minute continuous-runtime restriction.

If a licensed project exceeds its signed TAG entitlement, Run is blocked without deleting Engineering content.

### Machine request and offline issuance

EliteSCADA must expose a copyable, versioned **machine request code** derived from a deterministic canonical hash of stable hardware identity. Raw hardware serials should not be exposed when a one-way fingerprint is sufficient.

The machine request code can be sent to the EliteSCADA licensing authority through normal business channels such as email.

A controlled offline **EliteSCADA License Generator** accepts the request code and selected entitlement tier and returns a versioned signed license code/file.

The Windows License Generator must be directly usable by the licensing authority: double-clicking the distributed executable opens a graphical interface. Command-line issuance may remain available for controlled automation, but a transient console that closes because required arguments were omitted is not an acceptable primary interface.

The normal EliteSCADA product verifies licenses using asymmetric cryptography:

- the private signing key exists only in the controlled License Generator environment;
- the private signing key must never be committed to GitHub, embedded in customer binaries or published in CI artifacts;
- distributed EliteSCADA contains only public verification material/key identifiers;
- license payload and signature validation are versioned and fail closed;
- license identity must match the machine fingerprint before licensed runtime is allowed.

If no license is installed, the product enters Demo mode. If a license is explicitly installed but has an invalid signature, unsupported schema, tampered payload, unknown signing key or hardware mismatch, **Run is blocked** and the user receives a clear invalid-license diagnostic rather than silently falling back to Demo.

Full locked semantics: `docs/LICENSING-AND-DEMO-MODE.md`.

## Authoritative Engineering model

The **public, versioned Engineering model is authoritative**.

Runtime, editor, persistence, import/export, reusable libraries, scripting references, administrative tools and future extensions consume the same model. No graphical editor, database schema, browser state, script runtime or driver-specific representation may silently become the only project truth.

The Core remains independent of a specific PLC/protocol, database implementation and frontend/rendering technology.

Shared/server runtime flow:

`Device/Server Source -> Driver / Source Provider -> TAG Engine / Current Cache -> Event Bus -> Historian / Alarm Engine / Realtime / Gateway / Server Scripts`

Client-local presentation flow may include Client Memory, visual-object runtime instances and Client Visual Scripts, but client-local state is never global process truth.

The frontend never accesses industrial drivers directly. Concrete drivers never call one another directly. Protocol-independent transfers happen through TAG/runtime boundaries.

## Technology baseline

Current direction:

- Backend/Core: .NET 10 LTS.
- Frontend: React + TypeScript.
- Engineering/persistence: PostgreSQL.
- Historian: PostgreSQL + TimescaleDB.
- Realtime client transport: WebSocket.
- Public integration: REST API.
- Scripting language: Python.
- Client visual scripting: sandboxed Python runtime, with exact browser/WASM implementation selected by technical spike.
- Server scripting: separately sandboxed Python host/runtime in a later server-scripting slice.
- External protocol expansion: MQTT, OPC UA, BACnet and DNP3, plus installable modules including Siemens S7; later Allen-Bradley research.
- Extension direction: public SDK plus installable/versioned driver modules.

Implementation technologies may evolve deliberately, but public product contracts should remain decoupled from incidental frameworks.

## Mandatory Engineering Import/Export principle

Engineering Import/Export is a cross-cutting core capability, not a utility added after the GUI is complete.

Every relevant Engineering entity must be:

- serializable;
- versioned;
- importable/exportable through a public interface;
- usable without depending on the graphical editor;
- validated before application;
- compatible with the common preview/apply workflow.

Canonical technical representation is versioned JSON (`scada.engineering`). CSV is supported for appropriate bulk entities and XLSX is a future Engineering surface. Tabular formats do not replace the canonical model.

Mandatory import flow:

`parse -> validate -> preview -> choose merge mode -> apply`

Merge semantics include create-only, update-existing and create-and-update.

### Required Engineering domains

The public model covers or must evolve to cover:

1. **TAGs**: stable ID, path/name, type, unit, description, source/Data Source, address/binding where applicable, scaling, deadband, historian policy, access policy, typed memory initial value and metadata.
2. **Alarms**: TAG reference, type, limits/setpoint, priority, class, area, message, delay, ACK, shelving and metadata.
3. **Data Sources / Drivers / Internal Sources**: technical source configuration and protected secret references.
4. **Equipment Templates and Equipment instances**.
5. **Dynamos / reusable visual definitions** with typed public properties, bindings, script/event references and dependencies.
6. **Screens and Popups** with routes, visual-object trees, properties, bindings, assets and script/event references.
7. **Visual assets/resources** such as project images through stable IDs/references, not arbitrary filesystem paths.
8. **Python Scripts**: stable ID/path, scope, language/version, source, event/entry-point references, dependencies and metadata.
9. **Reports**: stable identity, page/layout configuration, typed provider/query definitions, runtime parameters/defaults, ordered sections/groups, report controls, aggregation/formatting rules and dependency metadata.
10. **Security Roles / Policies**.
11. **Gateway / TAG Bridge routes**.
12. **Operational Commands**.
13. Future trends, shell regions, libraries, Engineering Fragments and plugin-owned configuration.

Plugin-owned driver/Data Source configuration must expose a public versioned schema so it can participate in validation, import/export, backup/restore and migration without becoming opaque private state.

### Secrets rule

Passwords, tokens, private keys and equivalent secrets never appear in plaintext Engineering packages. Credentials/password hashes are not Engineering configuration. Technical configuration uses protected secret references.

## TAG bit access and bit-level driver binding

Integer TAGs must expose deterministic Boolean bit selectors as a reusable public reference capability. The preferred Engineering notation is `<TAG>.NN`, for example `Word_comando.00` for bit 0 and `Word_comando.07` for bit 7. Canonical persistence must resolve this to stable TAG identity plus bit index rather than depending only on the display text.

Initial fixed-width semantics are:

- `Int16`: bits `00..15`;
- `Int32`: bits `00..31`;
- `Int64`: bits `00..63`;
- bit `00` is the LSB;
- signed integer selectors use the fixed-width two's-complement representation.

A bit selector is a Boolean projection of the authoritative integer TAG. It inherits source quality/timestamp and must not convert an unavailable/bad source into `false`. It may be consumed by visual expressions/bindings, alarms and scripting/reference surfaces wherever a Boolean TAG reference is valid.

Physical bit binding is a permanent driver capability contract, not a Modbus-only convenience. Every future production driver that exposes bit-addressable byte/word/register/integer storage must expose structured bit reads through its public versioned binding/capability schema. Where the underlying protocol/address is writable, the driver must also support a safe Boolean bit write that preserves unrelated bits. Intrinsically read-only protocol areas remain explicitly read-only rather than fabricating impossible write support.

For Modbus, a Boolean TAG may bind to one bit `0..15` of a Holding Register or Input Register while retaining the normal register address/area semantics. Input Registers remain read-only; writable Holding Register bit writes must preserve all unrelated bits through a native mask-write operation where supported or through a concurrency-safe coordinated read-modify-write path.

The same principles apply to later drivers: declared width/range, LSB/MSB policy, quality propagation, read/write capability, native atomic bit-write support where available, coordinated fallback when needed and common conformance tests. Concurrent EliteSCADA writes to different bits of the same physical word must not lose each other through an unsafe local race.

The product must distinguish human register notation such as `4xxxxx` from the zero-based Modbus wire offset and make that conversion policy explicit in Engineering. Multiple bit TAGs sharing one register should reuse/coalesce the same physical read where practical.

A logical bit selector is not automatically a separate historian series. If the engineer needs independent retention/alarm identity for a physical bit, they create a first-class Boolean TAG bound to that bit.

Full locked semantics: `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.

## Current merged platform baseline

The following important slices are already official `main` state:

- real Modbus TCP runtime and common driver boundary;
- PostgreSQL Engineering persistence and revision lifecycle;
- TimescaleDB historian baseline;
- Engineering Schema v7 first-class operational commands through PR #35, merge `2fd568976fc6277d0b069adeeb560f6ea3d8205f`;
- protected sensitive read/realtime/WebSocket surfaces through PR #36, merge `10b0320149c1ef2109e9517539717a8800b200c2`;
- Engineering UI foundation/localization through PR #37, merge `4553aa7ab5ba7e05a209a7c8462286d1a34a1ad6`;
- trusted local identity/browser login foundation through PR #38, merge `2a581d279a428cb605429d5939c333ff7ad8d1b4`;
- pre-Wave-11 owner-usability integration through PR #193, main code merge `64ba134f88df61233c492f6c5e2b1ea8f244bf19`: graphical Windows License Generator, canonical industrial Slider, explicit `.escadapkg` Save/Open workflow and eight-definition built-in Dynamo starter library; exact pre/post-main validation evidence is retained in `LAST CHANGE.md` and issue #191.

The current Engineering UI includes `/engineering`, Runtime↔Engineering navigation, `pt-BR`/`en`/`es`, and structured TAG/Data Source/Alarm editors whose current mutation behavior remains intentionally preview-oriented until secured Apply/Delete/bulk workflows are added.

Local identities remain separate from Engineering roles/policies. Local users reference role keys; the active Engineering revision remains authoritative for capabilities/scopes. Browser authentication uses the same trusted JWT boundary and HttpOnly cookie support without replacing normal Bearer-token integration.

## Project lifecycle and persistence

Engineering work is distinct from operational runtime.

The lifecycle explicitly distinguishes:

- editable **Working** state;
- immutable saved **Revisions**;
- **Published** revision;
- **Active** revision driving runtime.

Required behavior:

- Engineering Workspace is isolated from Active Runtime.
- Checkout restores a persisted revision into an isolated editable workspace.
- Saves preserve revision lineage through `BasedOnRevision`.
- Publication does not automatically imply activation.
- Activation is transactional: candidate runtime is staged/validated before replacing the active runtime.
- Failed activation leaves the previous active runtime intact.
- Restart recovery uses the persisted Active Revision and fails closed if industrial runtime recovery cannot be performed safely.
- Persistence follows public Engineering contracts, not the reverse.

### Developer-selected application file

The developer must be able to choose where a portable EliteSCADA application is saved and later open that file explicitly.

The preferred initial representation is one versioned `.escadapkg` application file containing the canonical Engineering model, manifest/integrity metadata and project-owned visual assets. It deliberately favors one file over a loose directory tree. Internally it may remain a structured ZIP container; users must not have to manage its internal files.

This portable application file is distinct from server persistence:

- **Working/Revisions/Published/Active** remain durable server-side lifecycle state, initially in PostgreSQL;
- **Save Application As / Open Application** create or consume the portable `.escadapkg` through validation/Preview/Apply;
- secrets, historian samples, transient Runtime values and deployment-specific credentials remain outside the application file;
- project image references remain stable project assets rather than absolute developer-machine filesystem paths;
- future multi-project/domain or external-library composition may add a thin root descriptor with relative references, but must not fragment ordinary applications without a real need.

The Elipse E3 Domain/Project separation is a functional reference for explicit application composition and relative paths, not a requirement to reproduce `.dom`, `.prj` and `.lib` file proliferation. Full contract: `docs/APPLICATION-PROJECT-STORAGE.md`.

Project engineering backup/restore uses versioned `.escadapkg` data containing canonical Engineering content plus integrity metadata. It is not a historian/database image.

## Internal memory TAG sources

Before new external protocol families, EliteSCADA must implement two explicit built-in memory sources:

- `builtin.memory.client`: one value set per opened Runtime Client/session, non-retentive server-side initially;
- `builtin.memory.server`: one server-owned shared value per TAG, retentive by design.

### Client Memory

- initialized from a typed Engineering initial/default value;
- different clients may hold different values for the same engineered definition;
- intended for popup/navigation state, selected equipment/context, local UI flags, temporary filters, demo controls and Client Visual Scripts;
- may be read/written by the owning client's scripts;
- is never an authentication source, safety interlock, global permissive, server sequence truth or audit identity;
- does not drive global historian/alarm semantics because it has no single global value.

### Server Memory

- shared consistently across authorized clients;
- suitable for simulation/internal variables, retained parameters, intermediate/server sequence state and future Server Scripts;
- participates in shared cache/events/realtime/security and historian/alarm behavior when configured;
- retentive runtime values are persisted separately from immutable Engineering revisions;
- stable TAG ID is the primary retention identity so path renames preserve values;
- incompatible type changes require explicit validation/reset/migration, never silent coercion;
- engineered typed initial/default value is used when no compatible retained value exists.

Internal memory sources do not fabricate network timeout/reconnect/latency metrics.

Full locked semantics: `docs/INTERNAL-MEMORY-TAGS.md`.

## Protocol-independent TAG Gateway

Before additional external protocol families, EliteSCADA must implement a server-side first-class Gateway/TAG Bridge:

`Source TAG -> Gateway route -> Destination TAG`

Locked rules:

- routes are versioned Engineering entities with stable IDs;
- concrete drivers never call each other directly;
- `builtin.memory.server` is valid as source/destination; `builtin.memory.client` is not a server Gateway endpoint;
- destination must be active, writable and type-compatible;
- first version is unidirectional;
- direct/indirect cycles are rejected;
- multiple active Gateway writers to one destination are rejected unless a future arbitration policy explicitly allows them;
- fan-out is allowed through separate routes;
- OnChange and Periodic modes, bounded intervals, deadband/minimum interval/coalescing;
- default transfer requires source quality `Good`;
- unsafe implicit coercion is forbidden;
- simple transform may use `destination = source × gain + offset`;
- route diagnostics are independent from driver transport diagnostics;
- route failures do not corrupt source TAG quality.

Full locked semantics: `docs/TAG-GATEWAY.md`.

## Multi-Data-Source communication and diagnostics

EliteSCADA supports the architecture of multiple simultaneous Data Sources, including multiple instances of the same driver type and different protocol families.

The model distinguishes:

- **Driver type** = protocol/implementation;
- **Data Source** = concrete configured runtime source/connection/device context;
- **TAG** = point owned by one Data Source/source provider per revision plus protocol binding/address where applicable.

Failure of one Data Source must not contaminate another independent Data Source.

Common protected diagnostics must expose where meaningful:

- Data Source identity/type and sanitized endpoint identity;
- healthy/degraded/reconnecting/faulted state;
- state-change time;
- last success and last failure;
- request/cycle, success, failure and timeout counters;
- consecutive failures;
- reconnect/disconnect count;
- failure rate;
- response/round-trip latency;
- configured scan and observed data age;
- associated TAG count and quality aggregation such as Good/BadCommunication;
- sanitized last error.

TAG quality remains authoritative per point. Driver health is a summary, not a replacement for TAG quality.

Full locked semantics: `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`.

## Locked prerequisite order before new external protocols

The architectural sequence is:

**internal memory -> TAG-to-TAG Gateway -> common multi-driver diagnostics -> USER INTERFACE VALIDATION PREVIEW -> new external drivers/protocols**

The interface preview is an explicit product gate. It must provide a practical Windows x64 test path with local login, demo project, required services/startup automation or reliable instructions, visible version identification and a short validation checklist. Feedback is reviewed before investing heavily in the next protocol wave.

Research/specification spikes for future protocols may run earlier only when they do not register a production Data Source, alter active runtime composition or bypass the locked gate. Their purpose is to reduce uncertainty, not to smuggle protocol implementation ahead of the product sequence.

Full milestone: `docs/INTERFACE-VALIDATION-MILESTONE.md`.

## Python scripting and visual runtime foundation

Before the full graphical screen/popup/Dynamo editor is created, EliteSCADA must first establish the scripting and visual-property contracts described in `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`.

### Two script scopes

**Client Visual Scripts** execute in the Runtime Client and may:

- read permitted shared TAGs;
- read/write that client's Client Memory;
- react to screen/object/TAG/Client-Memory change, timers and an optional bounded frame/tick callback;
- read/change explicitly runtime-writable visual properties of the current screen/popup/Dynamo instance;
- request normal authorized backend operations through explicit APIs.

They cannot directly access drivers, database, filesystem, OS/shell, arbitrary network/DOM internals, secrets or a stronger principal than the logged-in user.

**Server Scripts** are a separate server-owned capability for shared calculations/automation using shared TAGs and Server Memory. They never manipulate one browser's visual-object instances or choose one client's Client Memory as global truth.

### Visual-object property contract

Every visual object type exposes one typed public property schema consumed by both the graphical property inspector and the script API.

Properties declare stable key, type/default/constraints, Engineering editability, runtime readability/writability, binding support and animatability.

Common properties include, as applicable:

- x/y;
- width/height;
- rotation and scale;
- z-order;
- visible;
- opacity;
- background/fill color;
- line/stroke color;
- line/stroke width/thickness and style;
- corner radius/effects;
- text, text color, font size/style/alignment;
- image/resource reference and image presentation options.

Type-specific objects may add explicit schema properties.

#### Declarative visual expressions, conditions and Analog Fill

Every renderable Screen/Popup/Dynamo object must expose the public boolean `visible` property, defaulting to `true` unless a deliberate schema rule says otherwise.

Visual properties that declare Binding/Expression support must accept a typed, side-effect-free expression over canonical runtime data sources, initially TAGs and Client Memory. Expressions belong to the existing Binding/Expression precedence layer and never become a second scripting system.

Minimum expression direction includes:

- boolean `and`, `or`, `not`, comparisons and parentheses;
- numeric `+`, `-`, `*`, `/`, `%`, unary sign and normal mathematical precedence;
- explicit type conversion between boolean/numeric domains where needed, never implicit coercion;
- a bounded whitelist of deterministic pure helpers such as `abs`, `min`, `max`, `clamp`, `round`, `floor`, `ceil`, `bool` and `number`;
- canonical dependency resolution so expressions survive validation, rename/import/export and do not bind silently to ambiguous display labels;
- reactive reevaluation when referenced sources change;
- integer TAG bit selectors such as `Word_status.03` as typed Boolean dependencies once the TAG-bit contract is implemented;
- bounded parsing/evaluation with no arbitrary JavaScript/Python code, loops, assignments, driver/database/network/DOM access or arbitrary function invocation.

Examples include boolean expressions such as `falha_inversor1 or falha_bomba1`, bit-aware expressions such as `Word_status.03 or falha_bomba1`, and numeric formulas such as `(nivel1 + nivel2) * 3`. A numeric expression driving a boolean property must convert explicitly, for example `(falha_inversor1 + falha_bomba1) > 0` or `bool(falha_inversor1 + falha_bomba1)` when those fault TAGs are numeric.

More generally, every public visual property whose schema type is boolean must support declarative boolean evaluation in the Binding/Expression layer without requiring Python. Simple direct-boolean and numeric-interval authoring remain required as convenient structured presets over the same expression semantics.

Bad/unavailable/wrong-type dependencies must not silently coerce to `false` or `0`; that Binding/Expression evaluation becomes unavailable and normal property precedence falls back with diagnostics.

Closed visual objects that opt into fill capability must support Analog Fill: a compatible numeric Binding/Expression result is scaled through configured engineering minimum/maximum to a clamped `0..100%` filled region, with explicit direction (`bottom->top`, `top->bottom`, `left->right`, `right->left`) and a filled-region color distinct from the unfilled/base appearance.

These are canonical, versioned Engineering behaviors and participate in import/export, Preview/Apply, revisions and project packages. Runtime-evaluated expression results, boolean results and fill percentages remain presentation state and never become saved base values automatically.

Visual expressions/conditions are presentation behavior only. They must never become safety/interlock/permissive authority.

Full locked semantics and deferred implementation boundary: `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`.

### Engineering value versus runtime visual state

This separation is mandatory:

- Engineering stores design-time/base property values.
- TAG bindings, animations and scripts may create runtime presentation overrides for one visual-object instance.
- script changes to width, position, colors, thickness, opacity, rotation, visibility etc. do **not** silently mutate saved Engineering revisions.
- different clients may have different runtime presentation state.
- closing/disposal of a visual instance also disposes its subscriptions/timers and re-establishes deterministic state on the next instance.

### Script editor and animation

Before the graphical editor, Engineering must provide a practical Python code editor with syntax highlighting, line numbers, indentation, API autocomplete where practical, line/column diagnostics, validation, script scope/event association and a sandboxed test/preview workflow.

Scripts are first-class versioned Engineering entities and participate in import/export, revisions, `.escadapkg`, dependency validation and Engineering Fragments.

Client scripting is primarily event driven. Required event direction includes load/unload, object interaction, TAG/Client-Memory change, timers and an optional bounded frame/tick callback.

Normal smooth animation should use renderer-native animation/tween primitives invoked from Python, with duration/easing/repeat/cancel behavior, instead of requiring high-frequency Python busy loops. Binding/script/animation precedence must be deterministic and diagnosable.

A faulty script must be isolated with time budgets, cancellation, bounded event queues, diagnostics and no ability to freeze the backend or unrelated clients indefinitely.
### Required sequence before graphical visual engineering

**Python scripting contract + visual property schema -> script editor/sandbox -> visual runtime object instances/property API -> graphical screen/popup/Dynamo editor -> advanced reusable visual libraries**

The interface-validation preview after driver diagnostics may occur before this visual-editor sequence. The final screen/Dynamo editor may not bypass this scripting/property foundation.

## Reusable visual/engineering libraries

Equipment Templates/Equipment and Dynamos evolve into version-aware reusable class/instance systems with:

- reusable definitions and typed properties/bindings;
- application-specific instance context;
- nested reusable components;
- script/event behavior through stable references;
- deterministic dependency validation;
- independent import/export;
- controlled version migration;
- preservation of safe instance overrides.

Cross-project copy/paste uses canonical **Engineering Fragments**, with dependency-aware preview, conflict handling, rebinding and selected-only/selected-with-dependencies modes. Browser clipboard state is not authoritative Engineering data.

EliteSCADA must ship a minimum original built-in Dynamo library ready for insertion from Engineering. The initial equipment catalog contains at least two practical variants in each requested family:

- motors: standard and VFD;
- pumps: centrifugal and submersible;
- valves: on/off and modulating/control;
- tanks: vertical and horizontal.

These definitions use canonical Dynamo/Visual Engineering, are serialized with the project and may use an instance equipment path to resolve portable child bindings. They are not loose vendor images or renderer-private artwork.

## Windows distribution trust and code signing

Windows x64 product distribution must have an explicit trust boundary. Production Preview/installable executables and installers must be Authenticode-signed with an authorized organizational code-signing identity, include a trusted timestamp, preserve publisher identity and be verifiable in the release pipeline before publication.

Unsigned internal/early Preview builds may still display Windows 11 unknown-publisher/SmartScreen warnings and may be used only when clearly identified as such. Compilation alone does not make a binary trusted, and the project must never claim certification for an unsigned artifact.

Secure signing credentials must not be committed to GitHub or embedded in normal build artifacts. The production signing workflow belongs to the Windows packaging/release stage (Wave 13) and should prefer a protected signing service or hardware-backed key. SmartScreen reputation is separate from signature validity and may require reputation-building even after correct signing.

## Historian, trends and reporting

TimescaleDB remains the historian direction. Required evolution includes retention, aggregation/downsampling, multiple-Pen trends, historical/live sources, engineered and ad-hoc/saved trends and expressions where appropriate.

Historian storage implementation details must not leak into public Engineering concepts.

Wave 09 must also provide a first-class canonical **Reporting / Report Designer** capability. A Report is versioned Engineering, not a generated PDF file or browser-only layout.

Required Reporting direction includes:

- section-based layouts with Report Header/Footer, Page Header/Footer, repeatable Detail and nested Group Header/Footer;
- a visual designer with typed fields, text, Boolean state fields, project images/resources, barcode, charts, shapes and deliberate page breaks;
- layout ergonomics such as grid/snap, alignment, z-order, borders, fonts, pan/zoom and page configuration;
- protected typed query providers shared with Historical Data Browser/Trends, including historian and alarm/event datasets;
- graphical parameter/filter authoring and runtime requery without requiring arbitrary SQL;
- declarative count/sum/average/min/max and deterministic grouped/time-bucket summaries;
- runtime parameters such as relative/absolute time, TAG selection, area, alarm type/severity and context identifiers;
- print preview, page numbering, printing and bounded authorized export to PDF, XLSX, HTML, RTF, text and CSV;
- canonical JSON/Preview/Apply/Working/revision/PostgreSQL/`.escadapkg` fidelity;
- server-side authorization, parameterized database access, cancellation/timeouts and output/result bounds;
- no unrestricted report scripting or arbitrary SQL in the first Wave 09 slice.

Historical Data Browser, Reporting and Trends share data/query authority but have distinct jobs: interactive tabular exploration, engineered paginated presentation/export and chart-oriented time-series visualization.

Full locked Wave 09 data/reporting semantics:

- `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`;
- `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`.

## Security and audit

Security is backend-enforced, never merely hidden UI.

Capabilities distinguish, as the product grows:

- view/read;
- TAG read;
- operational command execution;
- process/setpoint write;
- alarm ACK;
- alarm shelving;
- trend use/save;
- Engineering modification;
- user/role administration;
- system/module administration.

Scopes may restrict by area, equipment, screen, TAG or command. Protected-action identity comes from trusted authentication, not caller-supplied actor fields.

Sensitive mutations and administrative operations are auditable. Audit is durable/append-only and requires retention/outage-buffering policy hardening.

Python scripts never bypass these capabilities. Editing/applying/publishing scripts is itself a protected Engineering operation.

## Alarm philosophy

Alarms are runtime objects backed by Engineering definitions. State, priority/class/area/message, ACK and shelving are backend runtime/security concerns. ACK/shelving remain permission-controlled and auditable. Future shell UI may expose persistent alarm regions without becoming alarm truth.

## Configurable application shell

Applications must support configurable persistent regions such as header, footer, navigation, alarm summary and optional side regions, with controlled global/application/screen overrides.

Common widgets include application identity, logged-in user, navigation, alarm summary and date/time.

Date/time presentation may use the EliteSCADA server clock or TAG-provided PLC/RTU time. Displaying a device clock never silently synchronizes it; active clock synchronization is a separate future protected industrial command.

## Engineering UI localization

The complete Engineering/development interface supports:

- Português (`pt-BR`);
- English (`en`);
- Español (`es`).

Localization includes Data Sources, TAGs, historian/diagnostics, alarms, equipment/Dynamos, scripts, screen/popup editing, trends, reports/report designer, lifecycle, users/security, modules, Gateway, property editors, dialogs and validation/help text.

Language is a presentation/user preference. It never changes stable Engineering IDs, paths, enum values, public schema keys, script API identifiers or runtime semantics.

Runtime-HMI multilingual application content is a separate capability.

## External protocols and installable driver modules

Modbus TCP remains the current real protocol baseline.

After the prerequisite foundation and interface-preview gate:

1. MQTT;
2. OPC UA;
3. BACnet;
4. DNP3;
5. installable/versioned Driver Module framework;
6. Siemens S7 ISO Connection as the first intended installable module target;
7. later Allen-Bradley research based on public documentation/libraries, licensing, testability and representative hardware/simulator access.

Driver modules declare stable identity/version, EliteSCADA compatibility, provided driver/Data Source types and public versioned Engineering configuration schema. Missing/disabled/incompatible modules preserve project configuration and expose explicit diagnostics. Module installation/upgrade/removal is security-sensitive and auditable. Package integrity/trust must be evaluated before executable code is enabled.

Every future driver with bit-addressable word/byte/register storage is subject to the common bit conformance contract: structured read selection, safe bit write where the protocol/address is writable, quality propagation, range validation, unrelated-bit preservation and concurrency-focused tests. Native read-only areas remain read-only, and native Boolean points do not need artificial word-bit selectors.

### OPC UA Engineering/discovery experience

When OPC UA reaches production implementation, EliteSCADA must provide more than manual endpoint/NodeId entry.

Locked OPC UA product direction:

- manual endpoint configuration plus standard server/endpoint discovery;
- an opt-in, bounded and cancellable **Scan network for OPC UA devices/servers** tool using standard OPC UA discovery mechanisms where available and controlled host/port probing only as fallback;
- endpoint inspection covering transport, security mode/policy, supported authentication/user-token types and server-certificate identity;
- explicit certificate trust with fail-closed handling for unexpected server identity changes rather than silently trusting arbitrary servers;
- connection test before importing TAGs;
- lazy, searchable/filterable address-space tree browser;
- multiple selection and optional subtree candidate collection;
- import preview mapping OPC UA variables into canonical EliteSCADA TAG Engineering before Apply;
- subscription/update profiles so imported TAGs use native OPC UA monitored-item/subscription semantics;
- imported bindings preserve NodeId plus namespace-aware portable BrowsePath/namespace URI information so nodes can be safely re-resolved after server/namespace changes;
- a Refresh/Re-resolve Node IDs workflow with preview and deterministic mismatch/type-change handling;
- Rescan/diff workflow for new, missing and changed server nodes without silently deleting EliteSCADA Engineering or historian data;
- unsupported/lossy data types are reported explicitly, never silently coerced;
- production runtime continues to use normal TAG/security/Audit/Gateway/diagnostic boundaries and never creates a private protocol bypass.

The official OPC Foundation UA .NET Standard client stack is the primary implementation candidate and must be evaluated during the technical spike/implementation slice rather than reimplementing OPC UA privately without cause.

Full locked semantics and the permitted early non-production spike: `docs/OPC-UA.md`.

## Development quality rules

- Prefer small coherent slices with automated validation.
- Do not merge known-failing changes into `main`.
- Use CI as the external .NET validation environment when local .NET execution is unavailable.
- Validate backend build/tests/runtime smoke, frontend build and Chromium E2E when affected.
- Fix root causes instead of weakening tests, security or concurrency for a green pipeline.
- Preserve Engineering schema compatibility or explicit migration behavior/tests.
- Keep industrial runtime safety ahead of UI convenience.
- Do not create placeholder security endpoints without a real domain model.
- Do not let scripts or visual editors bypass public Engineering/security/runtime boundaries.
- Documentation updates must not erase locked future requirements.

## Relationship to other repository documents

- `PROJECT GOAL.md`: stable product intent and locked architecture.
- `LAST CHANGE.md`: exact operational resume point and MERGED / IMPLEMENTED IN PR / SPECIFIED state.
- `docs/ROADMAP.md`: ordered implementation status.
- `docs/ARCHITECTURE.md`: current architecture/data flow.
- `docs/ADR-*.md`: accepted focused decisions.
- `docs/SECURITY-AUTHORIZATION-AUDIT.md`: security implementation boundary.
- `docs/VISUAL-COMPONENT-LIBRARY.md`: reusable visual-component direction.
- `docs/INTERNAL-MEMORY-TAGS.md`: Client/Server Memory semantics.
- `docs/TAG-GATEWAY.md`: TAG Gateway semantics.
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`: integer TAG bit selectors and protocol bit-level Boolean binding semantics, including mandatory future-driver conformance.
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`: multi-driver diagnostic contract.
- `docs/INTERFACE-VALIDATION-MILESTONE.md`: mandatory product-owner preview gate.
- `docs/OPC-UA.md`: OPC UA discovery, browse, import, security and future driver Engineering experience.
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`: Python scripting, script editor, visual property schema and runtime visual-state contract.
- `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`: typed visual expressions, universal boolean visual conditions and analog proportional fill direction.
- `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`: Wave 09 protected historical browsing/query contract.
- `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`: Wave 09 canonical reporting, report designer, preview/print/export contract.
- `docs/LICENSING-AND-DEMO-MODE.md`: Demo 200-TAG Run gate, 300-minute session limit, hardware request code, signed-license validation and offline License Generator contract.

These documents must remain consistent. `PROJECT GOAL.md` wins for locked product intent; current repository code/`main` wins for implementation truth; `LAST CHANGE.md` records the exact handoff.
