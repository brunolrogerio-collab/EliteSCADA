# Wave 14 — Post-DEMO Contextual Product User Manual

**Recorded:** 2026-09-05 BRT  
**Authority:** Product Owner decision during Wave 14 canonical EEE DEMO closure  
**State:** DESIGN LOCK / NOT IMPLEMENTED  
**Scope:** full EliteSCADA product manual + contextual help integration

## 1. Objective

EliteSCADA requires a **detailed product user/developer manual**, written for the people who install, configure, engineer, operate and maintain the product.

The current repository contains substantial architecture, implementation, Driver, scripting, TAG, reporting and validation documentation. Those documents are valuable source material, but they are not by themselves the final user manual. Internal ADRs, handoffs and CI notes must not be exposed to the customer as if they were operating instructions.

The manual must explain both **how to use a feature** and **what its parameters mean**, including concrete examples, constraints, addressing syntax, runtime behavior and troubleshooting.

Special depth is required for:

- Scripts;
- Data Sources;
- TAGs;
- protocol/Driver addressing;
- Driver-specific parameters and peculiarities;
- security/roles;
- reports;
- HMI bindings, Screens, Popups and Dynamos;
- lifecycle and project portability.

## 2. Manual is a product surface

The manual must be versioned together with EliteSCADA.

The installed product must be able to open documentation compatible with the installed version. A v0.x installation must not silently send a user to an incompatible future manual whose fields or APIs no longer match the screen being used.

Default requirement:

- documentation is available as web pages;
- a compatible copy is bundled/served locally with the EliteSCADA installation or otherwise available offline;
- an online mirror may exist, but normal contextual help must not depend on Internet connectivity;
- help may open in a new browser tab/window so Engineering state is not lost.

Industrial operation and commissioning frequently happen on isolated networks. Context help that disappears because the Internet is unavailable is not acceptable product behavior.

## 3. Stable contextual Help IDs

Every manual topic that can be opened from the product receives a stable, language-neutral **Help ID**.

Conceptual examples:

- `runtime.session`
- `eng.projects.lifecycle`
- `eng.sources.overview`
- `eng.sources.connection`
- `eng.tags.overview`
- `eng.tags.addressing`
- `eng.tags.quality`
- `eng.drivers.modbus.source`
- `eng.drivers.modbus.addressing`
- `eng.drivers.s7.addressing`
- `eng.drivers.opcua.nodes`
- `eng.scripts.server.overview`
- `eng.scripts.server.initialize`
- `eng.scripts.tag-api`
- `eng.reports.designer`
- `eng.hmi.dynamos.parameters`
- `admin.security.users`
- `admin.recovery.overview`

These examples establish the naming concept, not the final exhaustive registry.

Rules:

1. Help IDs are stable product identifiers, not translated display strings;
2. one ID resolves to one canonical topic/anchor for the installed documentation version;
3. changing a heading must not unnecessarily break the Help ID;
4. IDs must be unique;
5. a removed/renamed topic must preserve compatibility or provide a deliberate redirect where practical;
6. product UI must not hard-code arbitrary public Internet URLs per control;
7. a central help resolver/registry maps `helpId -> installed manual route/topic`;
8. missing IDs must fail gracefully to the nearest valid section, while automated validation reports the broken mapping.

A conceptual route may look like `/help/<helpId>` or an equivalent resolver. The exact URL implementation is intentionally left to the implementation package.

## 4. Contextual help UX

Major Engineering sections must expose a small, unobtrusive help icon such as `?` or `i`.

At minimum provide section-level help for:

- Projects / lifecycle;
- Data Sources;
- TAGs;
- Drivers;
- Internal Memory;
- TAG Gateway;
- Scripts;
- Alarms;
- Operational Events;
- Historian;
- Trends;
- Reports;
- Screens / graphical editor;
- Popups;
- Dynamos;
- Commands;
- Security / users / roles;
- Licensing;
- Backup / System Recovery when that feature is implemented;
- Diagnostics.

Complex fields should also support field-level help when the meaning is protocol-specific or non-obvious.

Examples:

- a Data Source `Unit ID` field can link directly to the current Driver's Unit/Station addressing explanation;
- a TAG address field can link to the exact syntax used by that Driver;
- a Script trigger selector can link to its lifecycle semantics;
- a report data-source field can link to its query/source contract.

The active Driver must be part of the context. A help icon beside a Modbus TAG should not open generic OPC UA documentation because both happen to be called “Address”.

## 5. Required manual structure

The final manual should contain, at minimum, these product chapters.

### 5.1 Getting Started

- supported installation environments;
- first startup;
- authentication;
- initial administrator;
- creation/opening of projects;
- high-level Runtime vs Engineering model;
- normal workflow for creating the first functioning application.

### 5.2 Runtime Operator Guide

- login/session;
- current operator identity;
- logout and switch-user behavior;
- Runtime navigation;
- fullscreen;
- commands and operator interaction;
- alarms;
- operational events;
- trends/history;
- quality/unavailable-state meaning;
- what an operator cannot access when capability-restricted.

### 5.3 Engineering Project Lifecycle

Explain clearly:

`Working -> Save -> Revision -> Publish -> Activate`

Also cover:

- startup/home Screen;
- Active Runtime authority;
- revisions;
- project export/import;
- `.escadapkg` Inspect / Preview / Apply behavior;
- safe project portability;
- difference between application package and system backup/recovery.

### 5.4 Data Sources

Explain:

- purpose of a Data Source;
- Driver selection;
- connection identity;
- endpoint/host/IP/port;
- device/unit/station identifiers as applicable;
- scan/poll behavior;
- timeouts;
- reconnect behavior;
- Driver options;
- security parameters where supported;
- diagnostics and connection state;
- normal examples.

### 5.5 TAGs

This chapter must be particularly practical.

Cover:

- name, stable identity and path;
- datatype;
- association with Data Source;
- address syntax;
- Driver-specific addressing parameters;
- readable/writable semantics;
- selector/bit access where supported;
- quality;
- timestamps;
- engineering unit;
- scaling when implemented;
- decimal display formatting as a separate presentation concept;
- Historian participation;
- Alarm use;
- Script use;
- HMI bindings;
- Internal Memory TAGs;
- TAG Gateway;
- examples for booleans, integers, analogs and structured/protocol-specific cases.

The manual must explicitly distinguish **transport/raw addressing**, **canonical TAG value**, **engineering scaling** and **visual formatting**.

## 6. Mandatory Driver documentation template

Every production-supported Driver must have its own chapter built from the same minimum template.

For each Driver document:

1. **Purpose / when to use it**;
2. supported protocol/version/profile;
3. Data Source fields;
4. default ports and transport rules where applicable;
5. endpoint/device/station configuration;
6. exact TAG address syntax;
7. datatype mappings;
8. boolean/bit access;
9. byte order / word order / endianness where relevant;
10. read support;
11. write support and restrictions;
12. quality mapping;
13. timestamp semantics where applicable;
14. polling/subscription behavior;
15. reconnect and timeout behavior;
16. security/authentication/certificates where relevant;
17. Driver-specific parameters;
18. valid configuration examples;
19. common invalid examples;
20. troubleshooting and diagnostics;
21. known interoperability details/limitations;
22. interaction with TAG scaling where relevant.

The manual inventory must be derived from the **actual production Driver registry/contracts of the product**, not from a stale handwritten list.

Examples of current documentation sources that can seed the manual include S7 ISO connection, OPC UA, Driver diagnostics, TAG bit access, TAG Gateway and Driver SDK/interop documents. They must be rewritten into user-facing instructions and verified against the implementation before publication.

## 7. Scripts require a first-class manual

Scripting is a high-risk/high-value Engineering capability and must not be documented as a few code snippets without lifecycle semantics.

The Script manual must document only APIs and behaviors actually available in the product version being shipped.

At minimum cover:

### 7.1 Script types and execution location

Explain every supported script type and where it executes, for example server/runtime/visual/client distinctions **only where those distinctions actually exist in the current product**.

Do not document historical or planned APIs as if they were available.

### 7.2 Lifecycle / triggers

For every supported trigger explain:

- when it executes;
- whether it executes once or repeatedly;
- ordering guarantees, if any;
- initialization semantics;
- timer/cyclic semantics where supported;
- TAG/event-triggered semantics where supported;
- behavior across Save/Publish/Activate and runtime restart.

### 7.3 TAG APIs

Document with signatures and examples:

- reading a TAG;
- writing a TAG where permitted;
- quality access/handling;
- timestamp access where exposed;
- TAG lookup/reference rules;
- behavior for missing/unavailable TAGs;
- write failures and authorization.

### 7.4 Commands and operational events

Document supported ways to:

- invoke or cooperate with Commands;
- emit canonical Operational Events;
- distinguish an Operational Event from Alarm and Audit;
- handle one-shot request/acknowledgment patterns where relevant.

### 7.5 Context and data

Explain:

- available context objects/variables;
- input/output conventions;
- supported data types;
- parameter passing;
- reusable Script patterns;
- Dynamo/visual parameters only when actually exposed to the Script contract.

### 7.6 Runtime safety

Explain:

- execution timeouts;
- cancellation;
- fault behavior;
- diagnostics counters/state;
- concurrency/reentrancy semantics;
- deterministic behavior requirements;
- restart behavior;
- prohibited/unsupported operations;
- security and authorization boundaries.

### 7.7 Examples and anti-patterns

Provide complete, copyable examples for common tasks, but also show what **not** to do.

Examples should include normal process automation, initialization, TAG calculation, command mediation, operational event generation, quality handling and failure handling as supported by the actual product.

The manual must never invent helper functions because they “would be convenient”. Every public Script example must be validated against the shipped Script contract.

## 8. HMI / graphical engineering

Document:

- Screens;
- logical design coordinate system;
- objects;
- bindings;
- boolean conditions;
- analog fill;
- TAG references;
- navigation;
- Commands;
- Popups and positioning;
- Dynamos;
- Dynamo parameters and per-instance binding;
- Trends and pens;
- Alarm/Event Browsers;
- visual quality/unavailable behavior;
- Runtime scaling/fullscreen behavior.

Where an inspector/property is complex, it should have a field-level Help ID.

## 9. Reports, Historian, Alarm/Event and Security

The manual must contain user-facing material for:

- Historian configuration and retention/query concepts supported by the product;
- Trend live vs historical behavior;
- Report designer, data sources, parameters and generation;
- Alarm definition/lifecycle/acknowledgment;
- Operational Event definition/use;
- Audit purpose and security attribution;
- users;
- roles;
- capabilities/permissions;
- local/external authentication where supported;
- session behavior.

Alarm, Operational Event and Audit must remain clearly separated in terminology and examples.

## 10. Administration / recovery

Once System Recovery is implemented, the manual must document the authoritative recovery workflow defined by:

`docs/WAVE14-POST-DEMO-SYSTEM-RECOVERY-BACKUP-RESTORE.md`

This includes application `.escadapkg`, Security Authority protected backup/import and native Database/Historian recovery responsibilities.

The manual must state prerequisites, ordering, validation and failure/retry procedure rather than merely listing backup buttons.

## 11. Troubleshooting model

Each major chapter must include a troubleshooting section containing:

- symptom;
- likely causes;
- what to inspect;
- relevant diagnostics screen/log/state;
- safe corrective actions;
- when a configuration is invalid rather than merely disconnected.

Driver troubleshooting should point to the Driver's actual diagnostics fields and quality/reconnect semantics.

## 12. Localization and versioning

Help IDs are language-neutral.

The documentation system must be localization-ready. Portuguese (`pt-BR`) is the canonical Product Owner content requirement for the initial manual unless a later release decision expands mandatory language coverage.

If multiple languages are provided, changing language must resolve the same Help ID to the equivalent localized topic.

Every rendered manual should make the compatible EliteSCADA product/manual version visible.

## 13. Content governance

The source of truth for user-facing behavior is the **actual shipped product contract**.

Existing ADRs, technical documents, tests and implementation handoffs are references for constructing the manual, but they must be audited before being promoted to user-facing documentation.

The manual must avoid:

- internal coordinator instructions;
- obsolete Wave state;
- test-only URLs/credentials;
- private implementation shortcuts;
- APIs that are planned but not shipped;
- Driver assumptions not supported by the current implementation.

A product change that modifies a user-visible field, Script API, addressing syntax or Driver contract must identify the corresponding manual Help IDs that require review.

## 14. Automated documentation gates

When implemented, CI should validate at least:

1. Help ID uniqueness;
2. every product `helpId` resolves to a manual topic;
3. no broken internal topic/anchor links;
4. every current production Driver has the mandatory Driver documentation sections;
5. field-level Driver Help IDs resolve to the correct Driver context;
6. Script examples/contracts remain compatible with the current public Script API, using automated validation where practical;
7. the locally served/offline manual can be opened by the product build;
8. documentation version metadata matches the product release contract;
9. removed Help IDs are detected rather than silently becoming dead links.

## 15. Suggested repository/product structure

A later implementation may use a structure such as:

- `docs/manual/` for user-facing source content;
- a manifest/registry of Help IDs;
- a generated static web manual bundled with the product;
- a small shared `HelpLink`/`ContextHelp` component in Engineering;
- a resolver that opens the correct installed/manual version and locale.

This is a suggested implementation shape, not a requirement to adopt a particular documentation framework.

## 16. Implementation classification

As of this design lock:

- technical source documentation in repository: **EXISTS / NOT A COMPLETE USER MANUAL**;
- detailed product user/developer manual: **NOT IMPLEMENTED**;
- stable Help ID registry: **NOT IMPLEMENTED**;
- section/field contextual help icons: **NOT IMPLEMENTED**;
- locally available version-compatible web manual: **NOT IMPLEMENTED**;
- this document: product requirement/design authority only.
