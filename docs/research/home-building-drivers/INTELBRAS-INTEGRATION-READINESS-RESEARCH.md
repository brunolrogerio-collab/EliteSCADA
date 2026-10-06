# Intelbras Integration Readiness Research

**Issue:** #547 — HOME-RESEARCH-06 — Tuya Cloud + Intelbras official API readiness dossier  
**Contract:** C-HOME-CLOUD-TUYA-INTELBRAS-RESEARCH-01  
**Order:** HOME-CLOUD-TUYA-INTELBRAS-RESEARCH-01  
**Lane:** research/home-tuya-intelbras-cloud  
**Research date:** 2026-10-06  
**State:** RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

## 1. Scope and decision

This document covers **Checkpoint 2 — Intelbras only**.

It intentionally does not perform the Tuya/Intelbras convergence analysis reserved for Checkpoint 3.

Research authority is restricted to current official Intelbras material:
- Intelbras-owned product/manual sites;
- Intelbras official support/forum statements when product documentation is incomplete;
- Intelbras public API/integration documentation.

No private mobile API, reverse engineering, captured app traffic, extracted credential, unofficial cloud endpoint, or community implementation is used as product authority.

### Preliminary decision

**INTELBRAS = GO_WITH_GATES**

The historical research label **GDI** remains current in official Intelbras material. The official current product is:

**GDI — Gestão de Dispositivos IOT / Plataforma de APIs para Casa Inteligente Intelbras**

The current public GDI client manual describes an official cloud API intended for companies to integrate and control Intelbras smart-home devices programmatically.

This is a materially stronger result than a generic "contact Intelbras for API" posture.

The API is viable for a bounded Mibo integration, but the product scope is narrower than "all Intelbras":
- GDI supports **Mibo** devices documented by the platform;
- the public GDI manual explicitly states **Izy devices are not supported**;
- CFTV and professional access-control families expose separate integration surfaces and must not be folded into a generic HOME driver merely because the manufacturer is Intelbras.

The gates are:
1. paid-plan/commercial/legal qualification;
2. token lifecycle/scopes/event model/rate-limit qualification where public documentation is incomplete;
3. strict product boundary: Mibo GDI only for first HOME cloud integration, with Izy and professional security/access APIs treated separately.

## 2. GitHub authority at research start

Revalidated after Tuya Checkpoint 1:

- issue: #547
- branch: research/home-tuya-intelbras-cloud
- branch HEAD before Intelbras document: 1cc0bdf8d66b0e86f8be000fe74d91be2c0ca7f1
- integration: wave15/corrections-integration@77b08d60333685b4ba06aa649e3125f23b475dcf
- merge-base: 77b08d60333685b4ba06aa649e3125f23b475dcf
- ahead/behind before Intelbras document: 1 / 0
- only existing branch delta: TUYA-CLOUD-READINESS-RESEARCH.md
- no new Main order after Checkpoint 1
- state remains: ACTIVE / RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER
- terminology remains: HA = High Availability; HAB = Home Assistant Bridge

The final branch HEAD/tree after this document is recorded in the #547 checkpoint comment.

## 3. Official Intelbras API/product identified

### 3.1 Current official name

The official current client manual is titled:

**GDI - Plataforma de APIs para Casa Inteligente Intelbras**

and defines GDI as:

**Gestão de Dispositivos IOT**

The manual states that GDI allows programmatic integration and control of home-automation devices including cameras, locks, sensors, and smart lamps without requiring the Mibo Smart app as the runtime control surface.

This validates that "GDI" is not merely a historical research codename.

### 3.2 Current API shape

Publicly documented API base:

`https://api-casainteligente.intelbras.com.br`

The current manual states:
- HTTP API;
- JSON body;
- current documented examples use POST;
- authentication uses `Authorization: Bearer {token}`;
- a Swagger documentation UI is available after login at the GDI/open Casa Inteligente portal;
- the portal provides API usage/consumption management and token management.

Examples in the public manual include:
- list devices;
- query whether a device is online;
- switch a lamp;
- create camera video stream;
- remotely control a lock.

### 3.3 Architecture category

GDI is a **cloud API**, not a documented LAN/local hub API.

The public manual describes GDI as an intermediary between the application and Intelbras devices connected to the cloud.

Therefore the candidate is:

**intelbras.gdi / Mibo Cloud Data Source**

not:
- a direct Zigbee coordinator driver;
- a LAN hub API;
- a generic Intelbras hardware driver;
- an Izy integration.

Official source:
- https://app-mibo.intelbras.com.br/manual-gdi.html

## 4. Relevant portfolio boundary

### 4.1 Mibo — supported by GDI

The GDI public manual currently lists support for Mibo families:

**Cameras**
- all iM cameras with cloud visualization support, as stated by the GDI manual

**Locks**
- MFV 3000
- MFV 7000
- MFR 7000/7001
- MFD 7000

**Sensors**
- MSM 1001
- MSA 1001
- MTU 1001

**Lamps**
- MLS 4100
- ELW 1001

**Hubs**
- MCA 1001
- MCA 1002

The platform requires supported devices to be associated with the linked Mibo Smart account.

### 4.2 Izy — explicitly not supported by GDI

The current GDI manual states explicitly:

**GDI APIs support only Mibo devices. Izy devices are not supported by the platform.**

This is a hard product boundary.

An older official Intelbras support response for IZY Connect also did not publish/open an API/SDK and directed integration inquiries to Intelbras support.

Therefore:
- do not claim GDI is an Izy API;
- do not use Mibo GDI credentials/endpoints for Izy by reverse engineering;
- do not create an `intelbras.cloud` abstraction that pretends all Intelbras home products are covered.

**IZY VIA GDI = NOT SUPPORTED**

Any future Izy direct/cloud integration requires a separate official-interface qualification.

Official sources:
- https://app-mibo.intelbras.com.br/manual-gdi.html
- https://forum.intelbras.com.br/viewtopic.php?t=77094

### 4.3 Professional access control — separate integration family

Intelbras publishes official API integration material for professional access-control devices, including a dedicated API integration document and newer product-specific integration routes.

This is not the same API/product as GDI.

For HOME v1:

**OUT_OF_HOME_V1_SCOPE**

unless Main deliberately opens a separate access-control lane.

Do not map:
- user/face enrollment;
- credential administration;
- access-rule administration;
- professional door-controller administration

into HOME process capabilities merely because GDI also supports some residential smart locks.

Official source:
- https://suporte.intelbras.com.br/images/d/dd/Documento_de_Integrao_via_API_V1.3.pdf
- current Intelbras access-control manuals/integration portal references

### 4.4 CFTV / professional video — separate integration family

Intelbras also maintains separate CFTV APIs/protocol support for many camera/DVR/NVR products.

Those are not equivalent to GDI Mibo camera streaming.

For HOME v1:
- Mibo camera/video functions belong closer to Media Source Core than a process TAG driver;
- professional CFTV/ONVIF/SDK integration is a separate product decision.

**OUT_OF_HOME_V1_SCOPE** for professional CFTV administration.

## 5. Local vs cloud by relevant family

| Family | GDI Cloud | Local API proven by GDI docs | Gateway/hub | Account required | Internet required for GDI |
|---|---|---|---|---|---|
| Mibo Wi-Fi lamps/cameras | Yes | No | depends on device | Yes | Yes |
| Mibo Zigbee locks | Yes | No | MCA 1001/1002 | Yes | Yes |
| Mibo Zigbee sensors | Yes | No | MCA 1001/1002 | Yes | Yes |
| Mibo hubs | represented in GDI inventory | no generic local API documented | hub itself | Yes | Yes |
| Izy | **No GDI support** | not established here | family-specific | family-specific | family-specific |
| Professional access control | separate APIs exist | model/product-specific | not GDI | product-specific | product-specific |
| Professional CFTV | separate APIs/protocols exist | model/product-specific | not GDI | product-specific | product-specific |

Important:
- Zigbee at the physical-device layer does not mean EliteSCADA can directly use the Mibo Zigbee network through GDI.
- GDI remains the cloud boundary.
- Direct native Zigbee support should be evaluated through the generic Zigbee lane, not by assuming access to the Intelbras hub internals.

## 6. Protocol reuse

### 6.1 Mibo Zigbee devices

Official current product material confirms Zigbee on several Mibo sensors and the use of Mibo hubs.

Examples:
- MSM 1001: Zigbee / IEEE 802.15.4;
- MSA 1001: Zigbee / IEEE 802.15.4;
- MTU 1001: Zigbee / IEEE 802.15.4.

This does **not** prove that a device can be paired to an arbitrary third-party coordinator with full supported semantics, nor that Intelbras warrants that topology.

Therefore no blanket "reuse native Zigbee instead of GDI" recommendation is made.

Potential future route:
- if a specific Intelbras device is officially interoperable with the future native Zigbee implementation and its semantic behavior is qualified, reuse the generic Zigbee driver;
- otherwise use GDI for the officially supported Mibo cloud path.

### 6.2 Access control / CFTV

Where a professional Intelbras device officially exposes ONVIF, SIP, HTTP API, or another standard/protocol already supported by EliteSCADA, prefer the standard driver/integration rather than creating vendor duplication.

That rule applies outside the GDI HOME scope.

### 6.3 Vendor label is not architecture

"Intelbras" alone does not justify one driver.

Candidate integrations are protocol/product boundaries, for example:
- Intelbras GDI / Mibo Cloud;
- ONVIF through a generic media/video integration;
- separate access-control integration if deliberately authorized;
- native Zigbee when officially compatible and qualified.

## 7. Account and commercial model

### 7.1 Account prerequisites

The current GDI manual requires:
- valid CNPJ;
- active email;
- Intelbras account/login;
- company registration details;
- address information.

It explicitly states that GDI is for companies and accepts company registrations with CNPJ.

### 7.2 Paid plan required

The manual states that using the API requires contracting a plan.

Two current plan families are described:

**API Plan (IoT)**
- intended for IoT device control;
- monthly request quota;
- examples include on/off, status, password management, etc.

**Video Plan (Streaming)**
- video-request quota;
- streaming bandwidth quota in GB.

A customer can contract one or both depending on use case.

### 7.3 Extra packages / auto-purchase

The manual states:
- extra request/streaming packages can be acquired if quota is exhausted;
- when an automatic extra-package option is enabled, an extra package can be purchased automatically when the minimum resource threshold is reached;
- billing uses the registered payment method;
- plan renewal is automatic monthly unless changed/cancelled.

This is a product-safety concern for unattended SCADA polling.

EliteSCADA must never silently enable automatic commercial overage purchasing.

Any future UI should:
- expose current quota/plan state when available;
- warn on estimated polling consumption;
- never trigger an auto-purchase setting without explicit customer action outside normal runtime control.

### 7.4 Exact public price

The public GDI client manual does not expose exact plan prices.

The service/pricing page requires portal login.

Therefore:

**COMMERCIAL_INFORMATION_NOT_PUBLIC**

for:
- exact API Plan monthly price;
- exact request quota per plan;
- exact extra-package price;
- exact Video Plan price;
- exact streaming quota/overage price.

Do not reuse Mibo Cloud consumer-storage prices as GDI API pricing.

### 7.5 Partnership / homologation / NDA

The public GDI onboarding manual presents a self-service company/CNPJ + plan flow.

No separate public mandatory:
- integrator partnership;
- NDA;
- homologation;
- reseller status

was established for basic GDI onboarding.

Do not claim those are unnecessary in every commercial scenario; service-specific terms can still impose obligations.

## 8. Authentication

### 8.1 GDI access token

All documented API requests use:

`Authorization: Bearer {token}`

### 8.2 How the token is created

The public flow is:

GDI company account  
-> add/link Mibo Smart account  
-> redirect to Mibo Smart authentication  
-> user authenticates  
-> user reviews/authorizes requested access  
-> GDI generates a token  
-> token is named/saved in the GDI portal

Multiple Mibo Smart accounts can be linked. Each receives its own token and controls the devices belonging to that linked account.

### 8.3 Security significance

The manual warns that the token gives control over devices linked to that account.

Therefore the token is high-impact Protected Material.

### 8.4 OAuth classification

The flow is authorization-oriented and browser-mediated, but the public GDI manual does not publish a standards-level OAuth contract, authorization endpoint specification, client registration contract, redirect URI model, scopes, PKCE, or refresh-token flow.

Therefore:

**DO NOT LABEL THE GDI FLOW OAUTH IN PRODUCT CONTRACTS WITHOUT VENDOR/API DOCUMENTATION.**

Use the neutral term:
- linked-account authorization;
- GDI Bearer token.

### 8.5 Token lifecycle unknowns

The public manual does not establish:
- token lifetime;
- refresh token;
- automatic token refresh;
- token scopes;
- expiry behavior;
- revoke endpoint;
- number of simultaneously valid tokens;
- API-level rotation contract.

It does explicitly instruct the user to generate a new token if leakage is suspected.

Therefore:

**TOKEN_LIFECYCLE_NOT_PUBLIC / MUST_QUALIFY**

and:

**TOKEN_SCOPE_NOT_PUBLIC / MUST_QUALIFY**

before production code.

## 9. Protected Material

Any future GDI token must be stored through:

**Protected Material Authority**

Never persist the token plaintext in:
- .escadapkg;
- Engineering JSON;
- logs;
- diagnostics;
- URLs;
- telemetry;
- import/export previews.

Suggested canonical project data:
- non-secret GDI Data Source identity;
- linked-account display label;
- opaque protected-material reference.

Suggested Protected Material value:
- Bearer token only.

If future API documentation introduces client secrets, refresh tokens, signing keys, or webhook secrets, those also belong exclusively in Protected Material Authority.

## 10. Device identity

The current public examples use:
- `ns` — device serial/number identifier;
- `idProduto` — product ID.

The device-list endpoint is the documented source for retrieving those values.

The inventory example also distinguishes origin:
- linked;
- shared;
- all.

Recommended separation:

**Cloud authorization identity**
- GDI company account;
- linked Mibo account/token reference.

**Device stable identity**
- `ns` as primary device instance key where the Swagger/API confirms stability;
- `idProduto` as product/model identity, not instance identity.

**Gateway relation**
- preserve explicit hub relation when API response exposes it;
- lock example shows a compound/reference shape involving device/hub/product information.

**EliteSCADA identity**
- Equipment ID remains canonical;
- friendly name is display metadata only;
- Location is protocol-neutral.

Before DEV, inspect the authenticated Swagger/schema and confirm:
- `ns` uniqueness/stability;
- whether shared-device ownership changes identity;
- whether device replacement/re-pairing changes `ns`;
- gateway/sub-device relation fields.

## 11. Inventory and selected import

The documented GDI API includes device listing and can return linked/shared inventory.

Required EliteSCADA flow:

connect GDI company/account authorization  
-> select linked Mibo account/token  
-> list inventory  
-> user selects devices  
-> candidate  
-> semantic capability mapping  
-> Preview  
-> Apply

Do not:
- import all linked/shared devices automatically;
- merge inventories from different tokens without explicit account identity;
- treat "shared" devices as equivalent ownership.

A reasonable Data Source granularity is:

**one GDI Data Source per linked Mibo authorization/token**

because the public manual states that each linked Mibo account has its own token and corresponding device inventory.

## 12. Capability mapping

### 12.1 Proven candidate mappings

From current supported-device scope and public examples, candidate capabilities include:

**Lamps**
- OnOff
- possibly Dimmer/Light/ColorLight only when authenticated Swagger/model metadata explicitly exposes those semantics

**Sensors**
- Occupancy/Motion for MSM 1001 where API exposes a stable motion state
- Contact for MSA 1001 where API exposes open/closed state
- Temperature and Humidity for MTU 1001
- Battery only if API explicitly returns it as a supported status/property

**Locks**
- Lock/unlock only for supported residential lock models and only through explicit API semantics

### 12.2 Security-sensitive features

Residential lock state/control is a valid HOME capability candidate, but:
- credential administration;
- biometrics;
- user enrollment;
- emergency/coercion events;
- audit/access-policy administration

must not be casually projected as generic process TAGs.

Rich/security administration may require separate product contracts.

### 12.3 Cameras

Camera streaming is not a normal scalar process capability.

Route future Mibo camera video through Media Source Core/appropriate visual/media contracts rather than inventing persistent TAGs for video.

### 12.4 Mapping rule

Do not map by product-name string alone.

Use authenticated Swagger/schema + explicit supported model semantics.

## 13. Commands and process truth

Public GDI examples prove command-style operations, including:
- lamp on/off;
- lock control.

However, an HTTP success response is not sufficient to establish physical process truth.

Future write path:

`Runtime.WriteAsync(TAG)`
-> GDI cloud command
-> API acceptance/failure classification
-> authoritative status query/readback
-> reconcile CurrentTagCache

For locks especially:
- do not publish unlocked/locked Good state from the requested command alone;
- require authoritative returned/read state or a defined confirmed event.

If the device is offline:
- command result and physical state must remain distinct;
- quality must degrade appropriately.

## 14. Events / push / subscriptions

The current public GDI client manual reviewed here documents:
- HTTP requests;
- status queries;
- device online queries;
- commands;
- Swagger.

It does **not** publicly document:
- webhook callbacks;
- MQTT;
- WebSocket event stream;
- server-sent events;
- push subscription API;
- event replay;
- event acknowledgement;
- delivery guarantees.

Therefore:

**EVENT_API_NOT_PUBLIC / MUST_QUALIFY**

The first bounded implementation can be viable as HTTP + polling/readback if quotas make that operationally acceptable.

Do not invent an event endpoint based on mobile-app behavior.

## 15. Polling and rate/quotas

### 15.1 Monthly quota

The API Plan has a monthly request quota.

Exact public numeric quota for current plans was not available in the public manual.

### 15.2 Per-second rate limit

No current public generic per-endpoint QPS/rate-limit table was established for GDI.

Therefore:

**RATE_LIMIT_NOT_PUBLIC / MUST_QUALIFY**

### 15.3 Runtime implications

Polling must be designed from plan economics and quota:
- poll per device, not blindly per TAG when response grouping permits;
- use slower baseline intervals;
- allow post-write bounded readback;
- back off on quota/rate-limit errors;
- jitter restart/reconnect polling;
- calculate estimated monthly calls in Engineering diagnostics/configuration.

Example risk formula:

`monthly requests ~= devices × polls_per_hour × 24 × 30 + write/readbacks + inventory/health`

Do not choose an interval before current plan quotas are known.

## 16. Reliability / outage model

Suggested future behavior:

| Condition | Required behavior |
|---|---|
| DNS/TLS failure | cloud communication fault; no insecure fallback |
| Internet loss | GDI unavailable; age data to stale/non-Good |
| Intelbras cloud outage | bounded retry/backoff; preserve last value with degraded quality |
| Token rejected | AuthFailed; do not loop infinitely |
| Linked Mibo authorization removed | explicit authorization fault |
| Paid plan inactive/expired | commercial/entitlement fault |
| Request quota exhausted | QuotaExceeded; stop retry storm |
| Device offline | device-scoped non-Good/offline state |
| HTTP command accepted, no readback | pending/uncertain; do not claim physical truth |
| Shared device removed from account | remove from live inventory/reconcile configuration warning, never silently retarget |
| API version retired | explicit compatibility fault; no undocumented fallback |

## 17. Diagnostics

Reuse #500/common communication diagnostics conceptually.

Useful GDI details:
- API base endpoint;
- linked Mibo account display/reference;
- token present/resolved state, never token value;
- plan/entitlement state if API/portal exposes it;
- monthly request consumption if API exposes it;
- last successful request;
- last inventory sync;
- last device readback;
- device online status;
- last write result;
- response/error category;
- HTTP round-trip latency;
- polling interval;
- quota/rate-limit state;
- authentication failure count.

Never expose:
- Bearer token;
- Mibo account password;
- payment information;
- private mobile credentials.

## 18. Security

Confirmed:
- HTTPS API base;
- Bearer-token authentication;
- browser-mediated Mibo account authorization;
- tokens are account-scoped in practice by linked Mibo account;
- tokens provide control over linked devices.

Required:
- Protected Material only;
- no token in URL;
- no token logging;
- no secret echo in diagnostics;
- no automatic fallback to private/mobile API;
- no Mibo account password storage by EliteSCADA;
- explicit token rotation/revocation UX once vendor lifecycle is qualified;
- strict device-selection/import confirmation.

Unknown publicly:
- token cryptographic form;
- token expiry;
- refresh;
- formal scopes;
- IP allowlist;
- webhook signing;
- certificate pinning;
- rotation API.

Do not invent these.

## 19. Brazil / geography / data residency

GDI onboarding requires a Brazilian CNPJ and uses Intelbras Brazil infrastructure/domains.

Therefore:
- **Brazil availability: CONFIRMED**
- GDI is clearly a Brazil/company-oriented commercial product.

The public GDI manual reviewed does not publish:
- cloud region;
- data-center country;
- customer-selectable region;
- residency guarantee;
- cross-region behavior.

Therefore:

**DATA_REGION_NOT_PUBLIC / MUST_QUALIFY**

Because Mibo smart-home data may include:
- camera streams;
- access/lock events;
- occupancy;
- account/device identifiers,

a production integration requires:

**LEGAL_REVIEW_REQUIRED / LGPD_REVIEW_REQUIRED**

for data categories, controller/processor roles, retention, and international transfer where applicable.

## 20. Legal / commercial

The GDI manual requires the customer to accept terms during plan contracting.

Public Mibo service terms reference Intelbras General Contracting Conditions and license-specific conditions, but this research did not establish a public, GDI-specific complete legal grant covering:
- proprietary software redistribution;
- embedding the connector in EliteSCADA;
- resale;
- sublicensing;
- brand/logo use;
- support SLA;
- API deprecation commitment;
- data-processing roles for GDI;
- automated commercial overage behavior.

Therefore:

**LEGAL_REVIEW_REQUIRED**

and:

**COMMERCIAL_REVIEW_REQUIRED**

before product release.

No term was accepted and no plan was purchased during this research.

## 21. Testing / sandbox

### L0 — unit/contracts

Can be implemented later without vendor credentials:
- DTO/schema from authenticated official Swagger once legitimately accessed;
- Bearer-token injection/redaction;
- endpoint builders;
- capability mapping;
- error classification;
- polling schedule/quota calculator;
- write/readback state machine.

### L1 — fake official-shaped HTTP peer

Must cover:
- 401/403 token rejection;
- plan/quota error;
- device offline;
- inventory linked/shared;
- command accepted + stale old state;
- command accepted + changed readback;
- timeout;
- malformed response;
- version/endpoint not found;
- cloud unavailable.

### L2 — official GDI

No public anonymous sandbox/test project was identified.

The documented route requires:
- company/CNPJ GDI account;
- paid API plan;
- linked Mibo Smart account;
- token.

Therefore:

**L2 BLOCKED — LEGITIMATE COMPANY/COMMERCIAL ACCESS REQUIRED**

until Main intentionally authorizes a legitimate test subscription/account.

### L3 — EliteSCADA canonical runtime

Future:
- Data Source;
- selected Equipment;
- TAG/Command;
- Runtime.WriteAsync;
- readback reconciliation;
- quality;
- diagnostics.

### L4 — real hardware

Requires:
- legitimate GDI company account/plan;
- legitimate Mibo account;
- supported real Mibo device/hub as applicable.

Therefore:

**L4 BLOCKED — VENDOR SERVICE + DEVICE ACCESS REQUIRED**

No customer credential should be used as the default development fixture.

## 22. Architecture recommendation

### A. Built-in .NET cloud connector — RECOMMENDED

Why:
- API is conventional HTTPS/JSON;
- auth is a Bearer token once authorization is completed in Intelbras portal;
- no local hardware stack is required;
- no message-broker dependency is proven in the public API;
- straightforward fit with EliteSCADA Protected Material + Driver Runtime.

### B. Managed sidecar — NOT JUSTIFIED

No protocol/runtime complexity discovered so far justifies a sidecar.

### C. User-managed bridge — OPTIONAL FALLBACK ONLY

Possible but unnecessary if the official API remains stable and licensed for the intended product use.

### D. Not justified — REJECTED

A current official public commercial API exists; therefore "no integration path" is incorrect.

### Recommended initial product boundary

**Built-in .NET connector for GDI/Mibo IoT API only.**

Do not include in the same first driver:
- professional CFTV;
- professional access control;
- Izy;
- arbitrary Intelbras cloud services.

## 23. Proposed Data Source model

Recommended granularity:

**one Data Source per linked Mibo account/token**

because:
- each linked Mibo account receives its own token;
- inventory is tied to that token/account;
- multiple Mibo accounts are explicitly supported.

Data Source public configuration:
- API base fixed to canonical official GDI endpoint;
- linked-account display label;
- optional account/project alias;
- polling/readback policy;
- bounded timeouts/backoff;
- quota budget/expected interval.

Protected Material:
- Bearer token reference.

Discovery:
- inventory -> select -> candidate -> preview -> apply.

Do not create a Data Source per device automatically.

## 24. Product scope recommendations

### HOME v1 candidate

Include only bounded Mibo IoT state/control:
- lamps;
- supported environmental/contact/motion sensors;
- selected residential lock state/control with security review.

### Separate media path

Mibo cameras:
- do not treat video as TAG payload;
- route through Media Source Core if/when Main opens that integration.

### Separate security/access path

Professional access-control and CFTV:
- separate lanes/contracts;
- reuse generic ONVIF/SIP/etc. where officially appropriate;
- no monolithic Intelbras driver.

### Izy

**WAIT_API** as a separate sub-family until Intelbras publishes/authorizes an integration surface suitable for third-party commercial use.

This does not change the overall GDI/Mibo Intelbras decision.

## 25. Vendor questions required

Public research resolved the existence and basic operation of GDI. The remaining non-public questions should be asked only if Main wants to advance DEV/commercial qualification.

1. **Token lifecycle**
   - Does a GDI Mibo-account token expire?
   - Is there a refresh flow, revocation endpoint, or documented rotation procedure?
   - Are scopes/permissions configurable per token?

2. **Events**
   - Does GDI expose official webhook, WebSocket, MQTT, push, or subscription APIs for device state/events?
   - If yes, what are delivery/retry/order/replay guarantees?

3. **Rate/plan limits**
   - Current numeric monthly request quotas per API plan?
   - Per-endpoint QPS/rate limits?
   - 429/error semantics and backoff guidance?
   - Current price and extra-package price?

4. **Commercial product rights**
   - Is using GDI from a commercial third-party SCADA product explicitly permitted under the standard plan?
   - Is a partnership, homologation, branding approval, or separate agreement required?

5. **Data protection**
   - Hosting/data region?
   - LGPD controller/processor roles?
   - Retention for API logs/device data?
   - International transfers/subprocessors?

6. **Compatibility/versioning**
   - Supported device/model compatibility endpoint or versioned matrix?
   - API deprecation/version policy?
   - Sandbox/homologation environment?

## 26. Source register

All sources below are official Intelbras properties and were accessed on 2026-10-06.

| Source | Use |
|---|---|
| https://app-mibo.intelbras.com.br/manual-gdi.html | current GDI product/API, onboarding, plans, token flow, examples, supported devices, Mibo-only/Izy exclusion |
| https://manual-mibo.intelbras.com.br/pt-br/manuais/manual.html | current Mibo Smart ecosystem and hub/device context |
| https://loja.intelbras.com.br/sensor-movimento-msm-1001/p | MSM 1001 Zigbee protocol/specs |
| https://loja.intelbras.com.br/sensor-abertura-msa-1001/p | MSA 1001 Zigbee protocol/specs |
| https://loja.intelbras.com.br/sensor-temperatura-umidade-mtu-1001/p | MTU 1001 Zigbee protocol/specs |
| https://loja.intelbras.com.br/central-automacao-mca-1001/p | Mibo hub role |
| https://forum.intelbras.com.br/viewtopic.php?t=77094 | official support statement on IZY Connect integration inquiries |
| https://suporte.intelbras.com.br/images/d/dd/Documento_de_Integrao_via_API_V1.3.pdf | official separate professional access-control API evidence |
| https://manuais.intelbras.com.br/manual-interface-web-linha-bio-t/pt-BR/manual_unificado_web_2.0_pt-BR.html | current access-control integration route evidence |
| https://manuais.intelbras.com.br/manual-defense-ia/pt-BR/manual_pt-BR.html | separate professional security/access software API evidence |
| https://app-mibo.intelbras.com.br/termos/pt-br.html | Mibo contractual/CGI/terms context |
| https://www.intelbras.com/pt-br/ajuda-download/videos/aplicativo-de-casa-inteligente-intelbras-mibo-smart | current Mibo Smart support/download activity |

## 27. Decision

**INTELBRAS = GO_WITH_GATES**

### Why not WAIT_API

A current official API is publicly documented:
- named GDI;
- company onboarding;
- Bearer-token auth;
- paid API plan;
- inventory;
- status;
- device commands;
- supported-device matrix.

### Why not unconditional GO

Important product/commercial/runtime information remains non-public or login-gated:
- exact plan price/quota;
- token expiry/refresh/scopes;
- event/push model;
- exact per-endpoint rate limits;
- data region;
- product redistribution/commercial integration rights;
- sandbox/homologation access.

### Required gates

1. **Commercial/legal/data gate**
   - exact plan/quota/terms;
   - commercial software rights;
   - LGPD/data-region review.

2. **Runtime contract gate**
   - token lifecycle;
   - rate-limit/error contract;
   - event API availability or explicit polling-only design.

3. **Scope gate**
   - first integration is Mibo GDI only;
   - Izy remains outside until official API access is established;
   - cameras/media and professional security/access stay in their appropriate product lanes.

## 28. Declarations

RESEARCH CHECKPOINT 2 COMPLETE  
DOCS_ONLY  
NO PRODUCT CODE CHANGED  
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

**STOP AFTER CHECKPOINT 2 — CLOUD CONVERGENCE NOT STARTED**
