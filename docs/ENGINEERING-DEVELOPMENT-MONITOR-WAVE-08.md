# Engineering Development Monitor — Wave 08

Status: **OWNER-LOCKED / REQUIRED BEFORE WAVE 08 CLOSES**  
Date: 2026-08-29  
Parent wave: `GRAPHICAL-EDITOR-WAVE-08`  
Integration branch: `integration/graphical-editor-wave-08`

## Purpose

EliteSCADA must provide an Engineering-side Watch/Monitor Table so a developer/commissioning engineer can observe live variable behavior while creating and validating an application.

The workflow must not require building a temporary HMI screen, adding temporary Python, or opening several unrelated diagnostic pages simply to answer questions such as:

- Is this TAG changing?
- What is its current value and type?
- Is its quality Good, Bad, stale or unavailable?
- Is an internal memory value changing as expected?
- Is a system/runtime diagnostic changing?
- Is a driver/Data Source healthy, reconnecting or failing?

This is development/diagnostic tooling. It is not process-control authority.

## Product placement

Wave 08 must expose a dedicated Engineering Development Monitor surface. The first implementation may be a full Engineering workspace/page or a panel/window within Engineering, provided the workflow is direct and does not depend on Runtime HMI authoring.

Preferred product label in pt-BR: **Monitoramento** or **Monitor de Desenvolvimento**.

The surface must remain useful while the application is being engineered and tested. Later dockable/multi-window behavior may be added without changing the source/sample contract.

## Core user flow

The minimum user path is:

`open Development Monitor -> search OR type exact reference -> add row -> observe live value/type/quality/timestamp -> source changes -> row updates -> remove/clear`

Two equally valid discovery modes are mandatory:

1. **Search/browse** when the engineer does not know the exact reference.
2. **Exact quick-add** when the engineer already knows the TAG/variable name or canonical path and wants to type it directly.

The product must not force browsing when an exact reference is already known.

## Initial source families

The monitor must use an extensible provider/catalog boundary rather than hard-coding one table implementation per domain.

Initial Wave 08 source families must cover, where authoritative product data exists:

### 1. TAGs

- canonical TAG path/name/reference;
- current value;
- canonical data type;
- current TAG quality;
- source/current-value timestamp where available;
- engineering unit when available.

After `08-FOLLOW-A`, first-class bit selectors such as an integer TAG bit reference must become monitorable through the same TAG/provider catalog, not a special monitor-only syntax.

### 2. Internal Memory

- Server Memory values;
- Client Memory values where the browser/session owns the authoritative client-local state;
- exact typed values without unsafe JavaScript number coercion, including Int64-safe transport/presentation;
- source-local lifecycle must remain explicit. A Client Memory value from one browser runtime context must not be silently presented as a global server value.

### 3. System / Runtime variables and diagnostics

The monitor must expose authoritative system/runtime diagnostic variables through a provider contract. It must not manufacture client-side pseudo-values merely to fill the category.

Examples may include runtime state, service health or other first-class system facts already exposed by product services. If a required source is not yet available through a public read-only API, Wave 08 may add the minimum bounded diagnostic API needed for this monitor.

### 4. Data Source / Driver diagnostics

The monitor must allow useful driver/Data Source diagnostic facts to be added as rows, using the existing common diagnostic authority where possible.

Examples include canonical communication state/health, last successful communication/update, error/diagnostic counters or other first-class facts that exist in the diagnostic model.

Do not expose a raw private driver object or arbitrary internal implementation state as the public monitor contract.

### Future provider extension

The provider seam must be able to add later observable domains such as:

- Gateway diagnostics;
- canonical TAG bit selectors;
- expression diagnostics/results where appropriate;
- alarm/dependency diagnostics;
- additional protocol-specific diagnostic facts;
- future Engineering/runtime variable families.

Those future families do not expand the immediate Wave 08 implementation unless separately required.

## Canonical source descriptor

The unified monitor catalog should project every observable item into a stable read-only descriptor conceptually equivalent to:

```text
MonitorSourceDescriptor
- reference              stable canonical monitor reference
- displayName            human-friendly label
- sourceKind             Tag | ClientMemory | ServerMemory | System | DriverDiagnostic | ...
- dataType               canonical value type
- engineeringUnit?       optional
- providerIdentity?      optional Data Source/driver/runtime identity
- searchableText         derived search projection only
```

`reference` is authority. Display labels/search text are convenience projections and must not become ambiguous persistence/runtime identity.

A provider may expose additional metadata, but the central table must not need provider-private objects to function.

## Live sample contract

A monitored row must consume a typed sample conceptually equivalent to:

```text
MonitorSample
- reference
- value
- dataType
- quality                canonical quality when the source defines one
- diagnosticState?       source-specific state when quality is not the native model
- sourceTimestamp?       timestamp attached by the authoritative source
- observedAt             time the monitor received/observed the sample
- detail?                bounded diagnostic detail
```

The implementation does not have to use these exact type names, but it must preserve the semantic separation between catalog identity and live sample state.

## Search and exact quick-add

### Search

Search must support at least:

- canonical name/path/reference;
- display name;
- source family/category;
- useful provider/Data Source identity when relevant.

Search results should show enough metadata to distinguish similarly named variables before adding them.

### Exact quick-add

The engineer must be able to type a known canonical reference/path and add it directly, preferably with Enter.

Rules:

- exact canonical resolution wins;
- case behavior follows the owning domain's canonical identity rules;
- if no exact source exists, show explicit `not found`;
- if the typed text is ambiguous under a legacy/display-name lookup, show choices instead of silently selecting one;
- fuzzy search may suggest candidates but must never silently substitute a different source for an exact quick-add request.

## Table behavior

The table must support heterogeneous rows from multiple providers at the same time.

Minimum columns/facts:

1. **Name / Reference**
2. **Source**
3. **Value**
4. **Data Type**
5. **Quality / State**
6. **Timestamp / Last Update**

Recommended optional presentation:

- engineering unit;
- age/staleness;
- provider/Data Source;
- compact diagnostic detail/open-detail action;
- temporary change highlight for visual diagnosis.

### Quality/state rules

- TAG quality must use the canonical TAG quality semantics.
- Bad/unavailable/uncertain/stale state must remain visible.
- Missing data must never be displayed as `0`, `false`, empty string or `Good` merely to keep the table populated.
- A source that has no process-quality concept must show its authoritative diagnostic state or explicit `N/A`; the UI must not invent a fake `Good` quality.
- Timestamp/age must make frozen or stale observations distinguishable from actively updating values.

## Value formatting

Formatting must be driven by canonical data type, not string guessing.

Requirements:

- Boolean remains Boolean;
- integers remain integers;
- Int64 values remain exact end to end;
- floating values remain finite numeric values and preserve useful precision;
- DateTime uses an unambiguous timestamp format;
- String remains string without numeric coercion;
- Enum/state values preserve canonical identity plus friendly display where available;
- null/unavailable is distinct from numeric zero, Boolean false and empty string.

## Realtime and polling architecture

The monitor is meant to observe behavior, so update architecture matters.

Rules:

- reuse existing Event Bus/realtime/WebSocket/subscription paths where they already expose the authoritative source;
- use bounded/coalesced polling only for providers that do not have an appropriate push path;
- never create one independent API polling loop per table row;
- providers should batch requested references where practical;
- one physical driver/register source must not be re-read independently merely because several logical monitor rows reference it;
- the table may coalesce browser renders while retaining the latest authoritative sample;
- adding/removing a monitor row must not modify TAG scan rate, driver configuration, historian policy or process behavior;
- disconnection/reconnect must produce explicit row state rather than stale values pretending to be current.

### Practical capacity gate

Acceptance must prove at least **100 simultaneous monitored entries** can be held through shared provider batching/subscription infrastructure without creating 100 independent backend polling loops.

This is a correctness/architecture floor, not an invitation to make 100 the permanent product maximum. Any UI/provider limit must be explicit and bounded.

## Read-only boundary

Wave 08 Development Monitor is strictly read-only.

Forbidden from the monitor surface:

- TAG writes;
- forcing values;
- Client/Server Memory writes;
- command execution;
- Alarm ACK/shelving actions;
- driver configuration changes;
- changing polling/scan parameters;
- arbitrary Python/JavaScript evaluation;
- direct database/network/driver-private access from the browser.

Future write/force tooling requires a separate security/audit/product decision.

## Persistence boundary

Current samples are Runtime/diagnostic state and are never canonical Engineering.

Never persist/export/package:

- current monitored values;
- quality results;
- timestamps;
- transient connection state;
- temporary diagnostic messages merely observed by the table.

The selected watchlist may initially be session-local or user-workspace state.

If Wave 08 persists the watchlist definition for usability, it may persist only non-process tooling configuration such as:

- canonical source references;
- row order;
- column/display preferences.

It must remain clearly separate from process logic. Named project-portable Watch Tables can become a first-class Engineering tooling entity later if required; do not invent that project schema merely to deliver this live-monitor requirement.

## Relationship to Historian and Trends

The Development Monitor shows current/live diagnostic behavior. It is not a historian.

- no guarantee that every transient change is recorded;
- no independent time-series is created merely by monitoring a row;
- historical analysis continues through Historian/Trend tooling;
- monitor render coalescing is acceptable provided the latest source state is correct and no false semantics are introduced.

A later sparkline/change-history convenience can consume existing Historian data without turning the Watch Table into a second historian.

## Relationship to 08-FOLLOW-A and 08-FOLLOW-B

### 08-FOLLOW-A — TAG Bit Access

When bit selectors become first-class canonical Boolean references, they must automatically participate in the monitor provider/catalog contract. The monitor must not parse `.NN` display text independently of the canonical bit-reference model.

### 08-FOLLOW-B — Visual Expressions

Typed expression authoring/evaluation remains separate. If expression diagnostics/results are later exposed in the monitor, they must come through a bounded canonical diagnostic provider. The monitor must never evaluate arbitrary expression text itself.

## Security and authorization

- monitor APIs use the existing authenticated/authorized product boundary;
- the surface must not leak TAGs/diagnostics the current identity is not allowed to read;
- provider catalog search must enforce authorization, not merely hide rows after returning a global catalog;
- read access should be auditable at an appropriate structural level if the existing security model requires it, without generating an Audit event for every live sample repaint;
- no secret/password/key values from Data Source configuration may be exposed.

## Localization

Major UI states must follow existing Engineering pt-BR/en/es localization behavior.

At minimum localize:

- monitor title;
- search/add/remove/clear actions;
- source category labels;
- not-found/ambiguous/unavailable/stale states;
- column labels;
- empty/loading/error states.

Canonical references and type identities are never translated.

## Acceptance gate

Wave 08 Development Monitor is complete only when one exact integrated head proves:

1. Engineering exposes the Development Monitor surface.
2. Search can find a canonical TAG and add it.
3. A known canonical TAG/reference can be typed directly and added without browsing.
4. A Server Memory or Client Memory source can be added and monitored with correct scope semantics.
5. A system/runtime diagnostic source can be added.
6. A Data Source/driver diagnostic source can be added.
7. Rows show reference/name, source, current value, data type, quality/state and timestamp/last-update.
8. At least one source change is reflected live.
9. Bad/unavailable/disconnected state is explicit and not coerced to a normal value.
10. Int64/exact typed-value behavior remains correct.
11. A row can be removed and the table can be cleared without affecting source configuration.
12. Network/API evidence proves the monitor is read-only.
13. At least 100 monitored entries share batched/subscription infrastructure rather than one poll per row.
14. Current values/quality/timestamps do not appear in canonical Engineering JSON, revisions or `.escadapkg` merely because they were monitored.
15. Existing Runtime TAG Inspector, diagnostics, Client/Server Memory, security and visual/Python regressions remain green.

## Definition of Done impact

The graphical editor candidate being green is no longer sufficient to close Wave 08.

Wave 08 may merge only after:

- Graphical Editor/Image acceptance is green;
- Engineering Development Monitor acceptance is green;
- final exact integrated CI is green;
- post-merge `main` health is confirmed.

Wave 09 remains blocked until Wave 08 plus the separately ordered mandatory 08-FOLLOW-A and 08-FOLLOW-B requirements are satisfied according to the roadmap.
