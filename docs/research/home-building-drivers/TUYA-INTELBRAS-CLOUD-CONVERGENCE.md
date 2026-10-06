# Tuya / Intelbras Cloud Convergence

**Issue:** #547 — HOME-RESEARCH-06 — Tuya Cloud + Intelbras official API readiness dossier  
**Contract:** C-HOME-CLOUD-TUYA-INTELBRAS-RESEARCH-01  
**Order:** HOME-CLOUD-TUYA-INTELBRAS-RESEARCH-01  
**Lane:** research/home-tuya-intelbras-cloud  
**Research date:** 2026-10-06  
**State:** RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

## 1. Scope

This document covers **Checkpoint 3 — Tuya / Intelbras cloud convergence**.

Inputs:
- `TUYA-CLOUD-READINESS-RESEARCH.md`
- `INTELBRAS-INTEGRATION-READINESS-RESEARCH.md`
- live issue #500 diagnostics authority
- live issue #546 transient-event / rich-command research
- current official Tuya and Intelbras material revalidated on 2026-10-06

This checkpoint does not implement any cloud connector, shared framework, schema, runtime change, event model, rich-command model, dependency, credential flow, CI change, or merge.

## 2. Revalidated vendor decisions

**TUYA = GO_WITH_GATES**

**INTELBRAS = GO_WITH_GATES**

Neither decision is upgraded to unconditional GO.

Tuya has the more complete public developer surface:
- current Cloud Development / Cloud Service APIs;
- IoT Core;
- explicit project/user authorization models;
- documented signed requests;
- documented 2-hour token lifecycle;
- explicit data-center mapping;
- current Message Service with test/production channels and Pulsar integration;
- public trial/resource-plan information.

Intelbras GDI is a real official commercial API and is suitable for bounded Mibo integration, but several runtime/commercial facts remain login-gated or non-public:
- token lifetime/refresh/scopes;
- event/push model;
- exact current request quota/QPS;
- data region;
- complete proprietary-software commercial terms;
- public sandbox.

## 3. Revalidated GitHub product foundations

### 3.1 Diagnostics — #500

#500 is closed/completed and its accepted direction is authoritative:
- one canonical `CommunicationDriverDiagnostic` health authority;
- DriverHost/Data Source health;
- truthful communication states;
- counters;
- safe protocol details;
- no second driver-health model.

Cloud connectors must extend this existing diagnostic surface. They must not create:
- CloudDiagnostics;
- CloudHealth;
- TuyaHealth;
- GdiHealth

as separate canonical systems.

Vendor-specific safe details may live in bounded `protocolDetails`/equivalent existing extension points.

### 3.2 Protected Material

The existing host-owned Protected Material direction is the shared authority for all cloud secrets.

Cloud-specific project/package formats must store only references.

### 3.3 Canonical Runtime

Both vendors must converge to the existing product flow:

`Driver / Integration -> Data Source -> Equipment -> TAG / Command -> CurrentTagCache / Event -> Historian / Alarm / Gateway / Scripts / Runtime`

No vendor cloud object becomes a second canonical TAG or Equipment model.

### 3.4 #546 — transient events / rich commands

#546 currently remains research-only.

Its preliminary direction is relevant but must not be implemented by this lane.

Cloud connector rule:
- state belongs in canonical TAGs;
- ordinary one-value state change uses `Runtime.WriteAsync(TAG)`;
- genuine ephemeral device events may depend on future `DRIVER-TRANSIENT-EVENT-01`;
- parameterized/stateless operations may depend on future `RICH-COMMAND-BINDING-01`;
- no raw vendor action endpoint is exposed as a product bypass.

## 4. Common cloud patterns actually proven

The following patterns are common enough to standardize as **product policy**, but not enough to justify a new framework today.

### 4.1 Protected credential boundary

Both need:
- host-side secret resolution;
- secret redaction;
- no plaintext secrets in project/package/log/diagnostic URLs.

Difference:
- Tuya: client secret + access/refresh token + possible Message Service secret/signing material.
- GDI: currently one linked-account Bearer token is publicly documented.

### 4.2 Account/project authorization identity

Both have a cloud authorization identity distinct from devices.

Tuya:
- cloud project;
- authorization mode;
- data center/region.

GDI:
- linked Mibo account/token under a GDI company account.

This belongs at Data Source scope, not TAG scope.

### 4.3 Inventory and selected import

Both support the same Engineering posture:

`authenticate/authorize -> inventory -> select -> candidate -> preview -> apply`

This should reuse the existing Driver Engineering discovery/candidate path.

**No whole-account automatic import.**

### 4.4 Stable device identity

Both expose vendor instance identifiers plus product/model identifiers.

Rule:
- vendor device identity stays inside driver binding/discovery metadata;
- canonical Equipment ID remains EliteSCADA authority;
- friendly name never becomes identity;
- Location remains protocol-neutral.

### 4.5 Reads/status and writes/commands

Both can:
- enumerate devices;
- query state/status;
- submit state-changing operations.

Both therefore fit the ordinary communication-driver model for bounded scalar process state.

### 4.6 Process truth

Both require the same rule:

`cloud command acceptance != confirmed process state`

Future stateful write flow:

`Runtime.WriteAsync(TAG)`
-> vendor dispatch
-> dispatch/acceptance classification
-> authoritative vendor/device report or readback
-> CurrentTagCache update

Never publish the requested value as Good solely from HTTP/API success.

### 4.7 Quota-aware operation

Both are commercial cloud services with request/resource limits.

Common product behavior:
- bounded retry;
- exponential/backoff behavior;
- jitter;
- no reconnect storm;
- quota/rate classification;
- user-visible safe diagnostics;
- no per-TAG tight polling loop;
- no automatic paid overage purchase.

Exact quota algorithms remain vendor-specific.

### 4.8 Cloud outage behavior

Common semantics:
- DNS/TLS/internet/vendor outage never leaves stale cloud TAGs Good indefinitely;
- preserve last value when useful but degrade freshness/quality;
- distinguish authorization failure from network failure;
- distinguish vendor/cloud failure from one device offline;
- terminal auth/commercial failures stop blind retries.

### 4.9 Diagnostics

Both should report through #500 authority:
- Data Source/driver identity;
- sanitized endpoint/region/account label;
- operational state;
- last successful/failed request;
- latency;
- reconnects/retries;
- read/write/update counters;
- data freshness;
- vendor-safe auth/quota/device-online details.

## 5. Important differences that block a broad generic framework

### 5.1 Authentication

**Tuya**
- client ID/client secret;
- HMAC-SHA256 request signing;
- timestamp;
- optional nonce;
- access token;
- refresh token;
- known token lifetime;
- project/user authorization variants.

**GDI**
- linked-account Bearer token;
- no public refresh/scopes contract established;
- browser-mediated account linking occurs in Intelbras platform.

A common "CloudAuth" abstraction would either:
- be too weak to represent Tuya safely, or
- expose irrelevant complexity to GDI.

### 5.2 Region

**Tuya**
- region/data center is first-class and operationally significant;
- Brazil currently maps to Eastern America;
- historical-account migration can preserve previous mapping.

**GDI**
- Brazil service availability is clear;
- public hosting/data-center region is not established;
- API base is currently fixed/documented.

Do not force every cloud driver to pretend it has a Tuya-style region selector.

### 5.3 Event transport

**Tuya**
- official Message Service;
- test/production messaging rules;
- subscription channels;
- Pulsar SDK integration.

**GDI**
- public docs currently prove HTTP request/response only;
- no public event/push stream contract established.

A shared message-broker abstraction would be speculative for GDI.

### 5.4 Token lifecycle

**Tuya**
- refresh and expiry are documented.

**GDI**
- lifecycle remains vendor qualification work.

A generic token refresher cannot truthfully be required by both.

### 5.5 Commercial model

**Tuya**
- IoT Core subscription/resource packs;
- Trial is non-commercial;
- monthly API/message resources.

**GDI**
- company/CNPJ only;
- API Plan and Video Plan;
- monthly quotas;
- optional paid extra packages and automatic purchase option.

Commercial-state diagnostics can share categories, not implementation.

## 6. CloudConnectorFoundation decision

### Question

Should EliteSCADA create a future generic:

`CloudConnectorFoundation`

for Tuya and Intelbras?

### Decision

**NO NEW GENERAL CLOUDCONNECTORFOUNDATION YET**

Use:

**existing common product contracts + vendor-specific driver implementations**

This is the smallest sufficient architecture.

### Why

The proven reusable parts already belong to existing foundations:
- Protected Material Authority;
- Driver lifecycle/runtime;
- CommunicationDriverDiagnostic;
- Data Source;
- discovery/candidate/preview/apply;
- Equipment/TAG/Command;
- quality/freshness;
- audit/security;
- Runtime write/readback.

The remaining mechanics are not sufficiently uniform:
- HMAC signing;
- token refresh;
- region binding;
- Pulsar;
- opaque Bearer token;
- Mibo account linkage;
- polling economics;
- service-plan semantics.

A new framework now would mostly wrap .NET HTTP/client primitives while introducing an extra contract layer with unclear stable semantics.

### Revisit threshold

Reconsider a small shared cloud library only after:
1. Tuya implementation exists;
2. GDI implementation exists;
3. a third cloud connector or concrete duplicated code proves a stable repeated pattern.

Even then, extract only proven mechanics such as:
- sanitized HTTP error classification;
- Retry-After/backoff helper;
- bounded quota counters;
- safe endpoint diagnostics.

Do not introduce an inheritance-heavy cloud driver hierarchy.

## 7. Data Source model

### 7.1 Tuya

Recommended Data Source:

**one cloud project + data center + authorization scope**

A project may expose many devices.

Do not create one Data Source per device.

Configuration:
- selected/validated Tuya data center;
- canonical endpoint derived from that data center;
- non-secret project/client ID;
- authorization mode;
- protected-material references;
- polling/message policy;
- timeout/backoff limits.

### 7.2 Intelbras GDI

Recommended Data Source:

**one linked Mibo account/token authorization**

Reason:
- each linked Mibo account receives its own token;
- inventory is authorization-specific.

Configuration:
- canonical GDI endpoint;
- linked-account display label;
- protected token reference;
- polling/readback policy;
- quota budget/expected polling interval;
- timeout/backoff limits.

### 7.3 Shared Data Source UX

Common user-facing concepts can be consistent without one common backend schema:
- Account / Project;
- Region when required;
- Authorization status;
- Subscription/plan status;
- Inventory;
- Last sync;
- Diagnostics;
- Reauthorize / replace credential.

Vendor-specific advanced fields remain inside the driver editor.

## 8. Selected import convergence

Required shared workflow:

`Connect`
-> `Retrieve Inventory`
-> `Select Devices`
-> `Discovery Candidates`
-> `Assign Name + Location`
-> `Preview Equipment/TAGs/Commands`
-> `Apply`
-> normal Save/Publish/Activate

Rules:
- no automatic account-wide mutation;
- re-running discovery does not overwrite canonical project state silently;
- shared/linked/foreign ownership remains visible;
- unknown vendor features remain protocol details until mapped;
- unsupported devices remain visible as unsupported candidates rather than being fabricated into generic capabilities.

## 9. Capability mapping convergence

Both vendors must map through canonical HOME capabilities only when semantics are explicit.

Examples:
- OnOff;
- Dimmer;
- Light;
- ColorLight;
- Temperature;
- Humidity;
- Occupancy/Motion;
- Contact;
- Battery;
- Power/Energy;
- Voltage/Current;
- Cover;
- Lock;
- Climate;
- Fan.

Rules:
- no string-only DP/property mapping;
- no product-name-only mapping;
- vendor binding retains raw identity/property references;
- canonical capability remains protocol-neutral;
- unsupported semantics stay advanced/protocol metadata.

## 10. Cloud process-truth matrix

| Stage | Tuya | Intelbras GDI | EliteSCADA meaning |
|---|---|---|---|
| command request built | signed API request | Bearer HTTP request | intent only |
| request dispatched | transport success/failure | transport success/failure | dispatch evidence only |
| cloud API accepts | API result | HTTP/API result | not process truth |
| device online status | official status/online metadata | online query | reachability/availability signal, not target value |
| device state report | Message Service/status APIs | no public push proven | candidate authoritative state |
| poll/readback | official latest status | status/read APIs/Swagger | authoritative fallback |
| TAG update | after validated report/readback | after validated readback | canonical current truth |
| timeout/no confirmation | pending/unknown/degraded | pending/unknown/degraded | never invent requested state |

### Vendor-specific authority

**Tuya**
- prefer official state/message report where semantic mapping is explicit;
- use bounded status readback for write reconciliation and reconnect gaps.

**GDI**
- until an official event API is qualified, polling/readback is the public authoritative path after writes.

## 11. Events and #546

### 11.1 State reports are not transient events

A cloud report such as:
- lamp turned on;
- temperature changed;
- lock current state changed

updates a canonical TAG when it represents durable/current state.

Do not route all cloud messages through a transient-event model.

### 11.2 Genuine transient event candidates

If vendor API exposes:
- button press;
- double click;
- hold;
- scene invocation with no durable state;
- one-shot device notification;
- other bounded action event

then future support should depend on #546 `DRIVER-TRANSIENT-EVENT-01`.

Do not fake a persistent TAG for an event merely to fit current Runtime.

### 11.3 Current vendor impact

Tuya Message Service makes future transient events plausible, but each event type must be classified.

Intelbras GDI public documentation reviewed does not yet establish an event stream.

Therefore #546 is:
- **not a blocker** for first state/status connector scope;
- a dependency only for genuine transient events.

## 12. Rich commands and #546

### 12.1 Ordinary state writes

Keep `Runtime.WriteAsync(TAG)` for:
- switch on/off;
- lamp state;
- dimmer value when one scalar is enough;
- lock state only when the product contract truthfully models it as writable state;
- other bounded one-value state.

### 12.2 Rich command candidates

Use future Rich Command only if the vendor operation cannot be represented as one ordinary canonical value, for example:
- invoke scene with parameters;
- pulse/open-for duration;
- atomic multi-property color transition;
- bounded parameterized device action.

### 12.3 Camera/video

Tuya/Intelbras stream creation is not a generic Rich Command escape hatch.

Video belongs in the Media Source architecture.

### 12.4 Security/admin operations

Do not expose raw vendor:
- user enrollment;
- credential management;
- biometric administration;
- factory reset;
- firmware upgrade;
- account linking;
- payment/plan actions

as normal process Rich Commands.

## 13. Polling and quota architecture

### 13.1 No shared fixed poll interval

The product must not define one global "cloud polling interval".

Inputs differ:
- vendor plan;
- endpoint grouping;
- device count;
- message availability;
- commercial quota;
- device criticality;
- post-write reconciliation need.

### 13.2 Common policy

Each driver should expose bounded configuration and calculate safe estimates.

Required behavior:
- minimum safe interval;
- maximum configured concurrency;
- jitter;
- exponential/backoff;
- post-write bounded readback;
- quota/error backoff;
- startup/reconnect spreading;
- no tight auth loop;
- no polling while terminal commercial/auth state is known.

### 13.3 Cost visibility

Engineering diagnostics should be able to expose an estimate such as:

`estimated monthly calls`

when enough configuration is known.

This is especially important for GDI because polling directly consumes a paid monthly request quota.

No driver may purchase additional quota automatically.

## 14. Error taxonomy

A small **semantic** taxonomy is useful across cloud drivers, but it should remain within existing diagnostic/error contracts rather than create a new cloud framework.

Recommended categories:
- ConfigurationInvalid;
- DnsFailure;
- TlsFailure;
- NetworkUnavailable;
- VendorUnavailable;
- AuthenticationFailed;
- AuthorizationRevoked;
- SubscriptionRequired;
- SubscriptionExpired;
- QuotaExceeded;
- RateLimited;
- RegionMismatch;
- DeviceOffline;
- DeviceUnavailable;
- CommandRejected;
- CommandUnconfirmed;
- ApiContractChanged;
- UnsupportedFeature.

Driver retains vendor error code safely in protocol details where useful.

Do not expose secret-bearing raw bodies.

## 15. Security convergence

### Common mandatory controls

- Protected Material Authority;
- TLS only;
- no insecure downgrade;
- secret redaction;
- no credential in URL;
- no secret in project/package;
- no private/mobile API fallback;
- bounded retries;
- explicit authorization failure;
- explicit credential rotation/replacement UX;
- least vendor authorization scope available;
- selected import;
- safe diagnostics;
- cancellation/timeouts.

### Tuya-specific

- HMAC-SHA256 signing;
- timestamp/clock health;
- nonce where used;
- endpoint/data-center validation;
- access/refresh token lifecycle;
- Message Service secret/consumer security.

### GDI-specific

- Bearer token protection;
- no Mibo password storage;
- one Data Source per linked-token/account scope;
- unknown token expiry/scopes remain gates;
- no Runtime control of payment/auto-purchase settings.

## 16. Data protection / legal / commercial matrix

| Concern | Tuya | Intelbras GDI |
|---|---|---|
| Brazil service availability | confirmed | confirmed |
| region publicly documented | yes; Brazil current Eastern America | no public data-region contract established |
| production subscription | required | paid plan required |
| free/trial production | no; Trial commercial use prohibited | no public free production path established |
| account type | developer/project; individual/enterprise context | company/CNPJ |
| exact production pricing fully public | incomplete | no |
| redistribution/product integration terms fully qualified | no | no |
| LGPD review | required | required |
| vendor commercial review | required | required |

Both remain:

`LEGAL_REVIEW_REQUIRED`

`COMMERCIAL_REVIEW_REQUIRED`

before production release.

## 17. Diagnostics convergence

Cloud drivers must consume #500.

Recommended safe `protocolDetails` examples:

### Tuya
- dataCenter;
- endpoint;
- authorizationMode;
- tokenState;
- subscriptionState;
- messageConsumerState;
- lastMessageAt;
- quota/rate category;
- deviceOnline;
- writeTruth = event/readback.

### GDI
- endpoint;
- linkedAccountLabel/reference;
- tokenResolved;
- planState if obtainable;
- monthlyUsage if obtainable;
- pollingInterval;
- lastInventorySync;
- deviceOnline;
- quota category;
- writeTruth = readback.

No secret material in diagnostics.

## 18. HA behavior

HA = High Availability.

Cloud drivers must preserve existing external-effect authority rules:
- only the authoritative Runtime node may issue writes/commands;
- passive nodes must not duplicate cloud side effects;
- token refresh/message consumer ownership must not create competing writers/consumers without explicit design;
- failover requires full authoritative state resync.

For Tuya Message Service, consumer ownership/offset/subscription behavior must be designed so HA failover does not silently duplicate application-side effects.

For polling, startup/failover should jitter/resync rather than trigger a fleet-wide request storm.

No new HA framework is justified specifically for cloud connectors.

## 19. L0-L4 convergence matrix

### L0 — deterministic contracts

Common:
- endpoint/config validation;
- Protected Material request/redaction;
- DTO parsing;
- capability mapping;
- error classification;
- quota/backoff policy;
- process-truth state machine.

Tuya:
- signing vectors;
- token/refresh;
- region mapping;
- Message Service payload normalization.

GDI:
- Bearer header/redaction;
- authenticated Swagger DTOs once legitimately accessed;
- account/inventory identity;
- polling/quota estimator.

### L1 — official-shaped fake peers

Both:
- DNS/timeout/TLS category;
- auth failure;
- quota/rate;
- device offline;
- command accepted + no authoritative state;
- stale/changed readback;
- malformed/changed API response;
- vendor outage;
- reconnect/resync.

Tuya additionally:
- token expiry/refresh;
- invalid signature/clock;
- region mismatch;
- duplicate/out-of-order message;
- message-channel loss.

GDI additionally:
- linked/shared inventory;
- token rejection/rotation;
- plan unavailable;
- no-event polling-only path.

### L2 — official software/service peer

**Tuya**
- official development/test project is available in principle;
- Trial/test environment can support development/debug within current terms.

**GDI**
- no anonymous public sandbox identified;
- requires legitimate company/CNPJ + plan + linked Mibo account/token.

Therefore:
- Tuya L2 is materially easier.
- GDI L2 remains commercial-access-gated.

### L3 — canonical EliteSCADA Runtime

For both:
- activate Data Source;
- selected Equipment/TAG/Command;
- Runtime.WriteAsync;
- protected material;
- diagnostics;
- quality/staleness;
- write readback;
- cross-protocol TAG Gateway behavior where state semantics apply;
- HA external-effect authority.

### L4 — real hardware/service

Both require legitimate vendor account/service and supported hardware.

No customer production credential is the default test fixture.

Tuya:
- official cloud project + real Tuya-supported device.

GDI:
- paid GDI account/token + supported Mibo device/hub.

## 20. Testability comparison

| Dimension | Tuya | Intelbras GDI |
|---|---|---|
| public API docs | strong | useful public manual; full Swagger login-gated |
| public auth details | strong | partial |
| token lifecycle | documented | not public |
| official message test environment | yes | not identified |
| development/trial route | yes, non-commercial | paid company path |
| region details | documented | not public |
| public resource limits | substantial | plan exists; exact quota login-gated |
| L2 accessibility | better | commercial gate |

Result:

**Tuya is the lower-risk first implementation target.**

## 21. Implementation-order decision

**TUYA_FIRST**

Then:

**INTELBRAS_GDI_AFTER_VENDOR_QUALIFICATION**

Not:
- PARALLEL_AFTER_FOUNDATION, because no new generic foundation is recommended;
- INTELBRAS_FIRST, because its runtime/commercial contract is less publicly complete and L2 requires paid company access;
- BOTH_WAIT, because Tuya already has enough official technical surface to justify a gated DEV after Main accepts commercial/legal scope;
- NEITHER_JUSTIFIED, because both have official integration paths.

### Why Tuya first

1. API maturity/documentation is broader.
2. Token lifecycle and signing are documented.
3. Region behavior is documented.
4. Message Service and test channel are documented.
5. Trial/development path exists.
6. L2 can be planned without a customer production account.
7. Implementation will prove the shared existing EliteSCADA contracts before a second cloud vendor is added.

### Why GDI second

GDI remains valuable for the Brazil/Mibo market, but first close:
- paid-plan access;
- exact quota/QPS;
- token lifecycle;
- commercial embedding rights;
- event/push question;
- authenticated Swagger/version contract.

## 22. Product release recommendation

### Tuya first-release candidate

**YES, WITH GATES**

Initial bounded scope:
- project/account authorization model explicitly selected by Main;
- inventory + selected import;
- common state capabilities;
- reads/status;
- bounded writes with authoritative reconciliation;
- diagnostics;
- polling fallback;
- Message Service only when dependency/reliability review is accepted.

Do not include every Tuya Industry/Smart Home API.

### Intelbras first-release candidate

**CONDITIONAL / FOLLOW-ON**

Mibo GDI IoT only:
- lamps;
- selected sensors;
- bounded residential lock state/control after security review.

Do not include in same driver:
- Izy;
- professional CFTV;
- professional access control;
- arbitrary camera media as TAGs.

## 23. Exact blockers before future DEV

### Tuya

1. Main selects first auth/product shape:
   - project-level;
   - end-user authorization;
   - staged support for both.
2. Commercial/legal/LGPD review.
3. Exact intended production plan and current API/QPS qualification.
4. Message Service dependency/reliability decision.
5. Brazil legacy-region UX/test case.

### Intelbras GDI

1. Legitimate company/CNPJ test access and paid-plan authorization.
2. Authenticated Swagger snapshot/version evidence.
3. Token lifetime/refresh/revoke/scopes answer.
4. Exact plan quota/QPS/error/backoff data.
5. Official event/push answer or explicit polling-only acceptance.
6. Commercial embedding/redistribution + LGPD/data-region review.
7. Supported Mibo L4 hardware.

## 24. Common implementation slices if Main later releases DEV

These are sequencing recommendations only, not implementation authorization.

### Slice A — vendor-neutral reuse audit

Do not code a new cloud foundation.

Confirm direct reuse of:
- Protected Material;
- diagnostics #500;
- discovery candidates;
- canonical capabilities;
- Runtime write truth;
- HA effect authority.

### Slice B — Tuya L0/L1

Build only vendor-specific:
- signing/token/region;
- DTOs;
- inventory/status/command;
- capability mapping;
- fake peer;
- error/quality mapping.

### Slice C — Tuya L2/L3

Use official development/test project, then canonical Runtime.

### Slice D — Tuya events

Only after the base HTTP driver is truthful.
Classify each message:
- state update;
- availability diagnostic;
- genuine transient event.

Depend on #546 only for the third category.

### Slice E — GDI commercial qualification

Before implementation:
- vendor questions;
- paid test access;
- Swagger capture;
- terms/data review.

### Slice F — GDI L0/L1/L2/L3

Use the same canonical product boundaries, but implement GDI-specific auth/polling/DTO logic directly.

## 25. Why not parallel implementation

Parallel Tuya + GDI implementation before Tuya proves the cloud path would:
- duplicate unresolved UX decisions;
- risk premature shared abstractions;
- consume two commercial/vendor qualification tracks;
- make it harder to distinguish real commonality from coincidental similarity.

A sequential first connector is a deliberate architecture tool:
- prove existing abstractions;
- measure duplication;
- extract only what the second connector actually repeats.

## 26. Decision summary

### Vendor decisions

**TUYA = GO_WITH_GATES**

**INTELBRAS = GO_WITH_GATES**

### Foundation

**CLOUD_CONNECTOR_FOUNDATION = NOT_JUSTIFIED_YET**

Use existing EliteSCADA foundations and vendor-specific implementations.

### Data Source

- Tuya: project + region/data center + authorization scope.
- GDI: linked Mibo account/token.

### Import

**SELECTED_IMPORT_REQUIRED**

### Process truth

**COMMAND_ACCEPTANCE_IS_NOT_PROCESS_TRUTH**

State becomes canonical only from authoritative report/readback.

### Events

- state changes -> TAG;
- genuine transient events -> future #546 event contract if adopted;
- no fake persistent TAG.

### Rich commands

- ordinary one-value state -> `Runtime.WriteAsync(TAG)`;
- parameterized/stateless operation -> future #546 rich command only when necessary;
- no raw cloud action bypass.

### Diagnostics

**REUSE #500**

### Protected material

**REUSE PROTECTED MATERIAL AUTHORITY**

### Implementation order

**TUYA_FIRST**

then:

**INTELBRAS_GDI_AFTER_VENDOR_QUALIFICATION**

## 27. Next <= 3

1. Main decides whether to accept:
   - no new CloudConnectorFoundation;
   - TUYA_FIRST;
   - vendor-specific Data Source/auth implementations on existing common product contracts.
2. If accepted, Main may open a future Tuya DEV lane only after the Tuya gates in section 23 are explicitly staged/accepted.
3. Await a new `SIGA` for **Final Continuation / revalidation + Main handoff**. Do not begin it in this checkpoint.

## 28. Current official-source revalidation

Revalidated 2026-10-06:

### Tuya
- current Cloud Services API reference still exposes IoT Core, Smart Home APIs, Message Service, and separates Legacy APIs;
- current pricing still states Trial is development/debug only and commercial use is prohibited;
- IoT Core renewal remains required after validity expiry;
- current Message Service documents production/test messaging rules, subscriptions, Pulsar SDK integration, and queue monitoring;
- current data-center mapping still lists Brazil and the migration from Western America to Eastern America.

Primary official sources:
- https://developer.tuya.com/en/docs/cloud
- https://developer.tuya.com/en/docs/iot/membership-service?id=K9m8k45jwvg9j
- https://developer.tuya.com/en/docs/iot/manage-messages?id=Ka49p7loog3ze
- https://developer.tuya.com/en/docs/iot/oem-app-data-center-distributed?id=Kafi0ku9l07qb

### Intelbras
- current GDI manual still requires valid CNPJ and states GDI is company-only;
- API Plan and Video Plan remain the documented commercial model;
- linked Mibo accounts remain the device authorization model;
- Bearer token examples remain current;
- authenticated Swagger remains at the documented GDI portal;
- public manual still states GDI supports Mibo devices only and excludes Izy.

Primary official source:
- https://app-mibo.intelbras.com.br/manual-gdi.html

## 29. Declarations

RESEARCH CHECKPOINT 3 COMPLETE  
DOCS_ONLY  
NO PRODUCT CODE CHANGED  
NO CLOUD FRAMEWORK IMPLEMENTED  
NO SCHEMA CHANGED  
NO DRIVER SDK CHANGED  
NO EVENT RUNTIME CHANGED  
NO COMMAND RUNTIME CHANGED  
NO PRIVATE API USED  
NO MOBILE API SCRAPED  
NO CREDENTIAL REVERSE ENGINEERING  
NO CLOUD CREDENTIAL CREATED  
NO CUSTOMER CREDENTIAL USED  
NO COMMERCIAL PLAN PURCHASED  
NO COMMERCIAL TERMS ACCEPTED ON BEHALF OF PRODUCT OWNER  
NO DEPENDENCY CHANGED  
NO CI CHANGED  
HA = HIGH AVAILABILITY  
HAB = HOME ASSISTANT BRIDGE  
NO MERGE PERFORMED

**STOP AFTER CHECKPOINT 3 — FINAL HANDOFF NOT STARTED**
