# Tuya Cloud Readiness Research

**Issue:** #547 — HOME-RESEARCH-06 — Tuya Cloud + Intelbras official API readiness dossier  
**Contract:** C-HOME-CLOUD-TUYA-INTELBRAS-RESEARCH-01  
**Order:** HOME-CLOUD-TUYA-INTELBRAS-RESEARCH-01  
**Lane:** research/home-tuya-intelbras-cloud  
**Research date:** 2026-10-06  
**State:** RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

## 1. Scope and decision

This document covers **Checkpoint 1 — Tuya only**.

It intentionally does not research or decide Intelbras. It uses official Tuya documentation, official Tuya support, and official Tuya terms as primary authority. Community projects, private/mobile APIs, reverse-engineered endpoints, extracted tokens, and undocumented device APIs are outside scope.

### Preliminary decision

**TUYA = GO_WITH_GATES**

Reason: Tuya exposes a current, documented Cloud Development platform with current Cloud Service APIs, IoT Core, project/device authorization, signed HTTPS requests, device inventory/status/command APIs, and a Message Service based on a Pulsar-style queue. Brazil is explicitly supported in current data-center mapping. However, production depends on a paid IoT Core subscription, account/project and regional binding must be modeled explicitly, stricter endpoint-specific limits/error semantics and Message Service delivery semantics still need qualification, and legal/commercial terms for EliteSCADA redistribution/integration require review.

This is not a recommendation to start product code yet.

## 2. GitHub authority at research start

Validated before research:

- issue: #547
- branch: research/home-tuya-intelbras-cloud
- integration: wave15/corrections-integration
- exact branch/integration HEAD: 77b08d60333685b4ba06aa649e3125f23b475dcf
- exact tree: 6dfc58791f106b5538679c0b2e321b50bcff17a8
- merge-base: 77b08d60333685b4ba06aa649e3125f23b475dcf
- ahead/behind at start: 0 / 0
- state confirmed: ACTIVE / RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER
- terminology confirmed: HA = High Availability; HAB = Home Assistant Bridge

The final branch HEAD after this document commit is intentionally recorded in the #547 checkpoint comment, because embedding the commit that contains this document inside the document would be self-referential.

## 3. Current official Tuya product/API

The current official surface is the **Tuya Developer Platform / Cloud Development** and its **Cloud Services API Reference**.

The current API index exposes:
- IoT Core;
- Smart Home APIs;
- industry APIs;
- general capabilities;
- Message Service;
- extension APIs.

Tuya separately labels historical material as **Cloud Service APIs (Legacy)** and recommends the current version for new development.

For EliteSCADA, the primary candidate is:

**Cloud Development + IoT Core + current Cloud Service APIs + Message Service where justified.**

Historical names such as OpenAPI can still appear in support/docs, but they must not be treated as the canonical product name when the current documentation provides a newer structure.

Official sources:
- https://developer.tuya.com/en/docs/cloud/overview
- https://developer.tuya.com/en/docs/legacy-reference-of-cloud-service-apis
- https://developer.tuya.com/en/cloud-development

## 4. Account and commercial model

### 4.1 Developer account and cloud project

A Tuya developer account is required to create/use Cloud Development projects. Tuya's current support material for cloud-to-cloud integration explicitly starts with registering a developer account and applying for cloud API authorization to obtain project credentials.

Cloud Development projects provide isolation of resources. Tuya documents both Custom and Smart Home project types and recommends Custom development for the generic cloud-development path.

### 4.2 Individual versus enterprise use

Current official Tuya support states that Cloud Development is available to both individual and enterprise developers. It also states that users outside mainland China are not subject to the mainland-China individual identity-verification restriction described there.

This does **not** remove the commercial subscription requirement for production.

### 4.3 IoT Core subscription

IoT Core is the essential cloud service for bidirectional device communication and management. A valid basic resource pack is required to call cloud services and is obtained through an IoT Core subscription.

The public pricing document currently shows Trial, Flagship, and Corporate editions with different resource limits.

Important production rule:

**Trial is for individual developer development/debugging only and commercial use is prohibited.**

Therefore:

- Trial can support legitimate L2 development validation.
- Trial cannot be the production entitlement for EliteSCADA customer deployments.
- Production must qualify a current paid IoT Core plan and applicable service terms.

### 4.4 Public resource limits

Current official pricing, last updated 2026-07-28, lists:

| Resource | Trial | Flagship | Corporate |
|---|---:|---:|---:|
| API calls/month | 26,000 | 224,000,000 | 426,000,000 |
| Messages/month | 68,000 | 568,000,000 | 1,000,000,000 |
| Data centers | 1 | 7 | 7 |
| Maximum devices | 50 | 75,000 | 200,000 |
| Controllable devices | 10 | 30,000 | 75,000 |
| Log backtracking | — | 72 h | 168 h |

The pricing document states that the subscription validity is one year and renewal is required after expiration.

### 4.5 Public overage unit rates

Official source date: 2026-07-28  
Region: Outside Mainland China  
Currency: USD

| Plan | API calls | Messages |
|---|---:|---:|
| Flagship | USD 3.15 / million | USD 1.24 / million |
| Corporate | USD 2.97 / million | USD 1.17 / million |

The Trial column has reference unit values in the table, but Trial does not support paid overage as a production substitute and commercial use is prohibited.

The exact current base subscription amount for the production edition was not established from the public source snapshot used here.

**COMMERCIAL_INFORMATION_NOT_PUBLIC / MUST_QUALIFY** for the exact production subscription amount and any customer-specific commercial agreement that applies to EliteSCADA.

Official sources:
- https://developer.tuya.com/en/docs/iot/membership-service?id=K9m8k45jwvg9j
- https://support.tuya.com/en/help/_detail/K9zsouaplymo6
- https://support.tuya.com/en/help/_detail/K9g7809uyf803

## 5. Brazil and regional binding

### 5.1 Current Brazil mapping

The current official OEM app account/data-center mapping, last updated 2026-08-12, maps:

**Brazil / country code 55 -> Eastern America Data Center**

The documented Cloud API endpoint for Eastern America is:

**https://openapi-ueaz.tuyaus.com**

### 5.2 Migration caveat

Tuya documents a Brazil mapping change dated 2025-11-25:

- new-app/account mapping moved from Western America to Eastern America;
- existing apps before the adjustment remain unaffected.

Therefore EliteSCADA must not infer region only from current country code. Existing customer accounts can require a legacy/previous data-center association.

### 5.3 Project/device visibility

Tuya's official project/device linking documentation explicitly tells developers to switch the cloud project's data center when expected devices do not appear. This proves regional/data-center binding materially affects inventory visibility.

The Data Source must persist a selected/validated Tuya data center/endpoint as configuration, not hide it as an incidental implementation detail.

### 5.4 Data residency / LGPD

For current Brazil mapping, authoritative Tuya account/device traffic is associated with the Eastern America data center rather than a Brazil-local endpoint.

That creates a data-location and international-transfer question for Brazilian deployments.

**LEGAL_REVIEW_REQUIRED** for LGPD, customer notices/agreements, data categories, retention, and any cross-border processing obligations.

This is a product/legal gate, not a legal conclusion.

Official sources:
- https://developer.tuya.com/en/docs/iot/oem-app-data-center-distributed?id=Kafi0ku9l07qb
- https://developer.tuya.com/en/docs/iot/api-request?id=Ka4a8uuo1j4t4
- https://developer.tuya.com/en/docs/iot/link-devices?id=Ka471nu1sfmkl

## 6. Authentication and authorization

Tuya documents two materially different authorization shapes.

### 6.1 Project/simple authorization

For data created by or associated with a cloud project, the simple authorization mode obtains a token using project credentials.

The token request uses:
- client_id;
- client secret for signing;
- timestamp;
- signature;
- grant_type=1.

This is the natural fit for a customer-managed Tuya cloud project/project-level integration.

### 6.2 User authorization / OAuth-style flow

Tuya also documents authorization-code mode for access to user data. The token request uses grant_type=2 plus a user authorization code and the project/app credentials.

This is the relevant family when the product must access data through end-user account authorization instead of only project-owned resources.

### 6.3 Product decision

The evidence does not justify forcing a single authentication model for all future EliteSCADA deployments.

Expected product split:

**A. Project integration**
- customer provides/owns a legitimate Tuya Cloud Development project;
- EliteSCADA stores only protected references to project credentials;
- suitable for integrator/project deployments.

**B. End-user account authorization**
- required when the intended device inventory is owned through a user/app authorization model;
- UX and authorization flow must follow the current official Tuya method, not private Smart Life mobile endpoints.

**C. Both may be required depending on the product/channel.**

The first implementation should select one explicit supported model rather than silently mixing both.

## 7. Request signing and Protected Material

Current request documentation requires signed calls. The documented signing algorithm is HMAC-SHA256 and includes:
- client_id;
- timestamp;
- optional nonce;
- canonical string-to-sign;
- access_token for business API calls.

The canonical string-to-sign includes the HTTP method, SHA-256 content hash, selected signed headers when used, and URL.

The nonce mechanism is documented as a request-uniqueness mechanism.

Future protected values include:
- client secret / access key;
- access token;
- refresh token;
- any message-service access secret;
- any signing secret.

All must converge to **Protected Material Authority**.

They must not be persisted in:
- .escadapkg;
- Engineering JSON;
- logs;
- diagnostics;
- URLs;
- exception messages;
- telemetry payloads.

Only opaque protected-material references belong in canonical Engineering state.

Official sources:
- https://developer.tuya.com/en/docs/iot/authentication-method?id=Ka49gbaxjygox
- https://developer.tuya.com/en/docs/iot/new-singnature?id=Kbw0q34cs2e5g
- https://developer.tuya.com/en/docs/iot/api-request?id=Ka4a8uuo1j4t4

## 8. Token lifecycle

Official refresh-token documentation states:
- access tokens are valid for 2 hours;
- expire_time is returned in seconds;
- refresh returns a new token and refresh token;
- the previous token set is replaced.

Future Runtime rules:

1. Refresh proactively with bounded skew before expiry.
2. Keep clock/time synchronization health visible because request signing uses a millisecond timestamp.
3. On 401/auth expiry, perform one bounded refresh/re-auth path.
4. Never loop indefinitely on an invalid/revoked authorization.
5. Distinguish:
   - token expired;
   - refresh rejected;
   - project deleted/revoked;
   - authorization revoked;
   - regional endpoint mismatch;
   - subscription expired;
   - invalid clock/signature.
6. Repeated terminal auth failure transitions the Data Source to an explicit degraded/fault state and requires operator action.

Official source:
- https://developer.tuya.com/en/docs/cloud/80bb968f1d?id=Ka7kjv3j8jgvr

## 9. Device and project identity

Official Device Management currently exposes stable or semi-stable identifiers including:
- Device ID — unique identifier generated in the cloud after pairing/activation;
- UUID — unique identifier/credential written to the network module;
- Product ID / PID;
- device type;
- gateway/sub-device relation;
- serial number when reported;
- online/binding status.

Friendly device name is user-editable and must **not** be identity.

EliteSCADA should separate:

**CloudAccount/Project identity**
- Tuya project/client identifier;
- authorization scope;
- data center/endpoint.

**Device stable identity**
- primary device ID;
- gateway/sub-device relationship;
- UUID only where appropriate and exposed safely;
- PID as model/product identity, not device instance identity.

**Equipment identity**
- EliteSCADA Equipment ID remains canonical inside the project.

**Location**
- EliteSCADA Location remains protocol-neutral and should not be replaced by a Tuya friendly name.

Official source:
- https://developer.tuya.com/en/docs/iot/total_device_manage?id=Kbrcqbsc89m37

## 10. Inventory and selected import

The safe Engineering flow is:

authenticate  
-> validate project + data center  
-> retrieve accessible device inventory  
-> user selects devices  
-> build discovery candidates  
-> map explicit capabilities  
-> Preview  
-> Apply

Do not auto-import the whole cloud account/project.

For Smart Life app-account linking, Tuya's official documentation supports linking an app account to cloud projects by official QR/account authorization. It also documents project-link limits for an app account.

The presence of official linking does not justify embedding private mobile login/API behavior into EliteSCADA.

**Selected import is mandatory for the first product scope.**

## 11. Status, functions, specifications and commands

The current Device Control API family exposes distinct operations for:
- instruction set by category;
- functions supported by a device;
- specifications/properties;
- device commands;
- latest device status.

This is sufficient to build a bounded capability-mapping layer without guessing from names.

Mapping rule:

**Map only when semantic metadata is explicit and tested.**

Potential canonical capabilities include:
- OnOff;
- Dimmer;
- Light;
- ColorLight;
- Temperature;
- Humidity;
- Occupancy;
- Contact;
- Battery;
- Power;
- Energy;
- Voltage;
- Current;
- Cover;
- Lock;
- Climate;
- Fan.

A Tuya code/DP/function name must not become a canonical Capability by string matching alone. Mapping should be based on documented category/specification/function semantics and explicit converter tables.

Official source:
- https://developer.tuya.com/en/docs/cloud/device-control?id=K95zu01ksols7

## 12. Process truth and write reconciliation

Tuya exposes a command API and a separate current-status API. Tuya Message Service can also report device status/data changes.

Therefore:

**API command success is not process truth.**

Required future flow:

Runtime.WriteAsync(TAG)  
-> signed Tuya command request  
-> API acceptance/error classification  
-> authoritative device/cloud state update  
-> event reconciliation when available  
-> otherwise bounded status readback  
-> CurrentTagCache update

Rules:
- do not publish the requested value as Good merely because an HTTP/API request succeeded;
- if the device is offline, retain/transition quality according to freshness and reported state;
- if command is accepted but no authoritative state arrives before timeout, expose pending/degraded command outcome rather than invented process truth;
- readback/event correlation must tolerate eventual consistency.

## 13. Events / push / message service

The current official Message Service documentation states:
- it receives device registration, data reports, and status changes;
- the currently supported service type is **message queue**;
- it provides production and test environments;
- it supports default and additional subscriptions;
- filtering rules can use device ID, product ID, and user ID;
- monitoring includes production rate, consumption rate, and accumulated messages.

Tuya documents the queue service as based on Apache Pulsar concepts and provides SDK examples.

### No inbound public webhook requirement identified

For the current official Message Service path researched here, EliteSCADA acts as a queue consumer. A public inbound HTTP webhook is not required by that path.

Therefore NAT/reverse-proxy/public-certificate requirements that would exist for an inbound webhook are not an inherent Tuya requirement for this design.

### Delivery semantics still need qualification

The retrieved official public pages do not establish a complete contract for:
- exactly-once delivery;
- duplicate guarantees;
- strict ordering;
- replay horizon;
- retention under disconnect;
- redelivery behavior across all failure modes.

Future implementation must therefore:
- be idempotent;
- tolerate duplicates;
- tolerate out-of-order timestamps;
- retain a bounded last-seen message identity/time where useful;
- perform authoritative resync/status readback after reconnect or detected gaps.

A message data identifier is present in documented message examples and may support deduplication, but its exact uniqueness/replay contract must be qualified before relying on it as the sole dedupe authority.

Official sources:
- https://developer.tuya.com/en/docs/iot/manage-messages?id=Ka49p7loog3ze
- https://developer.tuya.com/en/docs/iot/Pulsar-SDK-get-message-go?id=Katu40rexevky
- https://developer.tuya.com/en/cloud-development

## 14. Polling fallback

If Message Service is unavailable, not subscribed, or unsuitable for the first implementation, official status APIs permit bounded polling/readback.

Polling must be designed from resource limits, not per-TAG naive loops.

Do not implement:
N devices × every TAG × fixed short interval.

Prefer:
- device-level grouped status reads where the API allows;
- event-first reconciliation;
- adaptive/backoff polling;
- slower background refresh for stable state;
- immediate bounded readback only after writes when required;
- jitter on reconnect/startup.

Risks:
- monthly API quota;
- endpoint QPS/rate limits;
- reconnect storms;
- latency;
- cost/resource-pack overage;
- device and cloud outage amplification.

## 15. Rate limits and quotas

Two different limits must be separated.

### 15.1 Public subscription resource limits

Monthly API-call and message allowances are public and plan-dependent. See section 4.

### 15.2 Generic rate protection and endpoint-specific limits

Current official Tuya support, updated 2026-08-19, publishes generic cloud-to-cloud traffic protection:

- application traffic: **500,000 API invocations per day**;
- API/interface traffic: **500 requests per second**;
- an API invocation counts once it reaches Tuya, regardless of success or failure.

These generic traffic-protection limits are separate from the monthly IoT Core API/message resource-pack allowances in section 4.

This does **not** prove that every intended endpoint can safely consume the full generic ceiling. Individual APIs, plans, or services may apply stricter controls, and the exact current 429/vendor-error/backoff contract for the selected endpoints must still be qualified.

Therefore:

**GENERIC_RATE_LIMIT_PUBLIC / ENDPOINT_SPECIFIC_LIMITS_MUST_QUALIFY**

Before DEV, qualify at minimum:
- token/auth endpoints;
- inventory/device list;
- device status reads;
- device commands;
- any stricter plan/endpoint-specific controls;
- message subscription/consumer constraints.

Runtime must implement vendor-error/Retry-After-aware backoff where applicable and must never tight-loop against quota/rate-limit errors.

Official references:
- https://developer.tuya.com/en/docs/iot/membership-service?id=K9m8k45jwvg9j
- https://support.tuya.com/en/help/_detail/K8sdy1i4g9u0q

## 16. Failure and quality model

Suggested future classification:

| Condition | Driver/Data Source behavior |
|---|---|
| DNS failure | Communication unavailable; bounded reconnect/backoff; data ages to non-Good after freshness threshold |
| TLS failure | Security/transport fault; no insecure downgrade |
| Internet loss | Cloud unavailable; no indefinite Good quality |
| Tuya outage | Cloud unavailable; bounded backoff; preserve last value with stale/non-Good quality semantics |
| Token expired | bounded refresh path |
| Refresh/revocation failure | AuthFailed; stop infinite renewal loop; operator action |
| Subscription expired | Commercial/entitlement fault; commands blocked; diagnostics explicit |
| API quota exceeded | QuotaExceeded/RateLimited; backoff; no retry storm |
| Region mismatch | Configuration/authorization fault; inventory may be empty; diagnostics show selected endpoint/data center |
| Device offline | device-scoped offline quality; do not fault unrelated online devices |
| Command accepted/no state | pending/readback timeout; do not assert requested value as truth |

## 17. Diagnostics

Reuse the common communications diagnostics direction from #500 conceptually. Do not create a Tuya-only diagnostics framework.

Useful non-secret details:
- cloud endpoint/data center;
- project/data-source identity reference, redacted as needed;
- auth state;
- subscription/entitlement state if obtainable;
- last successful API request;
- last event/message;
- last authoritative device status;
- device online/offline;
- API/monthly resource consumption when available;
- rate-limit/quota category;
- token refresh failure category;
- HTTP/API error category;
- round-trip latency;
- reconnect count;
- message backlog/consumer health where available.

Never expose:
- client secret/access key;
- access token;
- refresh token;
- message service secret;
- signing input containing secrets.

## 18. Security

Confirmed current requirements/capabilities:
- HTTPS Cloud API endpoints;
- HMAC-SHA256 request signing;
- timestamp-based signatures;
- optional nonce in signing flow;
- access token on business requests;
- explicit project/user authorization scope.

Security requirements for EliteSCADA:
- Protected Material Authority only;
- redact secrets from logs/diagnostics/errors;
- no secret in URL;
- no insecure TLS fallback;
- endpoint/data-center allowlist from canonical Tuya endpoints;
- bounded clock-skew diagnostics;
- credential rotation workflow before production;
- authorization revocation handling;
- no private/mobile API fallback.

Not confirmed from the reviewed public material:
- universal IP allowlist requirement;
- universal webhook-signing model, because the selected Message Service is queue-based;
- one global credential-rotation API contract.

Those must not be invented.

## 19. Legal and commercial review

Current Tuya Terms of Use state that service-specific purchases/subscriptions can require additional provisions, and those specific provisions must be accepted for the service.

The public Trial restriction is explicit: commercial use is prohibited.

The research did not establish a complete public legal answer for:
- embedding/reselling a Tuya connector in proprietary EliteSCADA;
- trademark/logo usage in the product;
- all redistribution restrictions;
- customer data-processing obligations;
- SLA/support rights;
- exact paid-service terms applicable to the chosen IoT Core edition.

Therefore:

**LEGAL_REVIEW_REQUIRED**

and

**COMMERCIAL_REVIEW_REQUIRED**

before production release.

Do not show Tuya trademarks/logos or claim certification/partnership without the applicable permission.

Official sources:
- https://hotel.console.tuya.com/policies/service
- https://developer.tuya.com/en/docs/iot/membership-service?id=K9m8k45jwvg9j

## 20. Architecture comparison

### A. Built-in .NET cloud connector — RECOMMENDED

Fit:
- signed HTTPS auth/API calls are straightforward to implement in-process;
- no local hardware stack is required;
- current EliteSCADA Runtime/Driver/Protected Material boundaries are a natural fit;
- simpler packaging than a permanent sidecar.

Gate:
- Message Service is Pulsar-derived; a production-quality .NET consumer dependency/protocol path must pass dependency, license, reliability, and packaging review.

### B. Managed sidecar — NOT JUSTIFIED AS DEFAULT

A sidecar should not be introduced merely because Tuya has a message queue. It adds packaging, lifecycle, diagnostics, upgrade, and security complexity.

Reconsider only if the supported message-consumer path proves materially safer/more supportable out-of-process than in-process.

### C. User-managed external bridge — FALLBACK/ADVANCED ONLY

Possible for customers with their own cloud middleware, but it weakens the turnkey experience and creates another truth boundary. It should not replace an official connector if the built-in path remains viable.

### D. Not justified — REJECTED AT RESEARCH STAGE

The official API maturity, Brazil availability, and documented device/control/message capabilities justify continued product qualification.

### Recommended architecture

**Built-in .NET cloud connector with no mandatory sidecar.**

Initial product should use:
- built-in signed HTTPS for auth/inventory/status/commands;
- selected import;
- Protected Material references;
- authoritative status reconciliation;
- bounded polling fallback;
- Message Service in-process only after dependency/protocol qualification.

## 21. Proposed Data Source model

Recommended granularity:

**one Tuya Data Source per cloud project + data center + authorization scope**

Not one Data Source per device.

Configuration:
- region/data center;
- canonical API endpoint selected from supported Tuya mapping;
- project/client identifier (non-secret);
- authorization mode;
- protected-material references;
- polling/event policy;
- bounded timeout/backoff policy.

Inventory children become Equipment/TAG/Command mappings through selected import.

If end-user account authorization is later supported, the authorization scope must be explicit. Do not silently merge multiple user accounts into one Data Source unless the official authorization model and product UX justify it.

## 22. L0-L4 validation route

### L0 — contract/unit

Validate:
- HMAC signing vectors;
- canonical request construction;
- token DTO/expiry/refresh;
- region/endpoint selection;
- device/specification/status/command DTOs;
- capability mapping;
- error/rate-limit classification;
- Protected Material redaction.

### L1 — fake official-shaped cloud peer

Must cover:
- token refresh;
- expired token;
- invalid signature/clock;
- quota/rate limit;
- offline device;
- command accepted but no authoritative state;
- delayed readback;
- duplicate event;
- out-of-order event;
- cloud unavailable;
- region mismatch;
- subscription/entitlement failure.

### L2 — official development/test access

Available in principle through a legitimate Tuya developer account/project and Trial/test environment.

Constraints:
- Trial is development/debug only;
- no production customer account as the default test;
- no fabricated credentials;
- no commercial use of Trial.

### L3 — canonical EliteSCADA Runtime

Validate:
- Data Source activation;
- Equipment/TAG/Command;
- Runtime.WriteAsync;
- CurrentTagCache reconciliation;
- diagnostics;
- quality degradation/recovery;
- cross-protocol/Gateway behavior where relevant.

### L4 — real supported device

Requires:
- legitimate Tuya developer/project authorization;
- legitimate Tuya-supported test hardware/device;
- correct region/data center;
- official Cloud API only.

**L4 BLOCKED until legitimate account/device access is intentionally provided for product validation.**

## 23. Open uncertainties / gates

1. **Commercial/legal gate**
   - exact current paid IoT Core base subscription for intended Brazil deployment;
   - service-specific paid terms;
   - proprietary connector/resale/trademark/data-processing rights;
   - LGPD/international data transfer review.

2. **Authorization/region product gate**
   - choose first supported product path: project-level integration, end-user authorization, or a deliberately staged combination;
   - validate existing Brazil accounts created before the 2025-11-25 data-center mapping change;
   - define explicit Data Source region/authorization UX.

3. **Runtime/event qualification gate**
   - qualify whether intended endpoints impose stricter limits than the published generic 500,000/day and 500/second protections, plus exact rate-limit error/backoff semantics;
   - qualify Message Service delivery/redelivery/order/retention semantics;
   - review the official C# Pulsar path and its dependency/license/packaging posture, or intentionally ship polling-first.

## 24. Source register

All sources below are official Tuya properties and were accessed on 2026-10-06.

| Source | Official date/status | Use |
|---|---|---|
| https://developer.tuya.com/en/docs/cloud/overview | current index, accessed 2026-10-06 | current Cloud Services/API families |
| https://developer.tuya.com/en/docs/legacy-reference-of-cloud-service-apis | current index, accessed 2026-10-06 | legacy/current distinction |
| https://developer.tuya.com/en/cloud-development | current product page, accessed 2026-10-06 | Cloud Development flow + real-time Pulsar messaging statement |
| https://developer.tuya.com/en/docs/iot/membership-service?id=K9m8k45jwvg9j | updated 2026-07-28 | IoT Core plans, quotas, Trial restriction, overage pricing |
| https://support.tuya.com/en/help/_detail/K9zsouaplymo6 | updated 2026-05-13 | individual/enterprise developer availability |
| https://support.tuya.com/en/help/_detail/K9g7809uyf803 | updated 2026-08-19 | developer account/cloud-to-cloud authorization process |
| https://developer.tuya.com/en/docs/iot/oem-app-data-center-distributed?id=Kafi0ku9l07qb | updated 2026-08-12 | Brazil -> Eastern America; migration caveat |
| https://developer.tuya.com/en/docs/iot/api-request?id=Ka4a8uuo1j4t4 | updated 2025-06-10 | regional endpoints and request headers |
| https://developer.tuya.com/en/docs/iot/authentication-method?id=Ka49gbaxjygox | updated 2024-06-13 | simple/project vs user auth |
| https://developer.tuya.com/en/docs/iot/new-singnature?id=Kbw0q34cs2e5g | updated 2026-01-05 | HMAC-SHA256 signing |
| https://developer.tuya.com/en/docs/cloud/80bb968f1d?id=Ka7kjv3j8jgvr | updated 2024-03-22 | 2-hour token + refresh |
| https://developer.tuya.com/en/docs/iot/link-devices?id=Ka471nu1sfmkl | current page, accessed 2026-10-06 | app-account/project linking + data-center troubleshooting |
| https://developer.tuya.com/en/docs/iot/total_device_manage?id=Kbrcqbsc89m37 | updated 2026-01-08 | Device ID, UUID, PID, gateway/sub-device, data isolation |
| https://developer.tuya.com/en/docs/cloud/device-control?id=K95zu01ksols7 | current page, accessed 2026-10-06 | functions/specifications/commands/latest status |
| https://developer.tuya.com/en/docs/iot/manage-messages?id=Ka49p7loog3ze | updated 2026-04-10 | Message Service, queue, subscriptions, test/production |
| https://developer.tuya.com/en/docs/iot/Pulsar-SDK-get-message-go?id=Katu40rexevky | current page, accessed 2026-10-06 | Pulsar consumer/message shape |
| https://developer.tuya.com/en/docs/iot/Pulsar-SDK-get-message-c?id=Kawpkk5vic1es | updated 2025-08-28 | official C#/.NET Message Service consumer path |
| https://github.com/tuya/tuya-pulsar-sdk-dotnet | current repository, accessed 2026-10-06 | current sample targets net9.0 and references DotPulsar 3.4.0 + Newtonsoft.Json 13.0.1; no dependency adopted by this research |
| https://support.tuya.com/en/help/_detail/K8sdy1i4g9u0q | updated 2026-08-19 | generic cloud-to-cloud protection: 500,000 calls/day and 500 calls/second |
| https://hotel.console.tuya.com/policies/service | terms last updated 2021-11-04 | general platform/service terms and service-specific terms |

## 25. Decision

**TUYA = GO_WITH_GATES**

Rationale:
- current official API surface exists and is mature enough for a bounded connector;
- Brazil is explicitly supported;
- project/user authorization models are documented;
- signed HTTP APIs expose inventory, status and commands;
- Message Service provides an official event path;
- no private/mobile API is necessary for the proposed architecture;
- the remaining blockers are qualification/commercial/legal/product-shape gates, not absence of an official API.

Do **not** open DEV until the three gates in section 23 are accepted or deliberately staged by Main.

## 26. Declarations

RESEARCH CHECKPOINT 1 COMPLETE  
DOCS_ONLY  
NO PRODUCT CODE CHANGED  
NO CLOUD CREDENTIAL CREATED  
NO PRIVATE API USED  
NO MOBILE API SCRAPED  
NO CREDENTIAL REVERSE ENGINEERING  
NO CUSTOMER CREDENTIAL USED  
NO COMMERCIAL TERMS ACCEPTED ON BEHALF OF PRODUCT OWNER  
NO DEPENDENCY CHANGED  
NO CI CHANGED  
HA = HIGH AVAILABILITY  
HAB = HOME ASSISTANT BRIDGE  
NO MERGE PERFORMED

**STOP AFTER CHECKPOINT 1 — INTELBRAS NOT STARTED**
