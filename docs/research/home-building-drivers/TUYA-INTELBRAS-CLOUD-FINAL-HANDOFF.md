# Tuya / Intelbras Cloud Research — Final Handoff

**Issue:** #547 — HOME-RESEARCH-06 — Tuya Cloud + Intelbras official API readiness dossier  
**Contract:** C-HOME-CLOUD-TUYA-INTELBRAS-RESEARCH-01  
**Order:** HOME-CLOUD-TUYA-INTELBRAS-RESEARCH-01  
**Branch:** research/home-tuya-intelbras-cloud  
**Final research date:** 2026-10-06  
**State:** RESEARCH_COMPLETE / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

> Exact final branch HEAD/tree are published in the final #547 handoff comment after this document is committed. This avoids self-referential commit metadata inside the document.

## 1. Final decisions

**TUYA = GO_WITH_GATES**

**INTELBRAS = GO_WITH_GATES**

**CLOUD_CONNECTOR_FOUNDATION = NOT_JUSTIFIED_YET**

**IMPLEMENTATION ORDER = TUYA_FIRST**

Follow-on:

**INTELBRAS_GDI_AFTER_VENDOR_QUALIFICATION**

The recommendation is to reuse existing EliteSCADA foundations and implement vendor-specific cloud logic directly inside each future driver. Do not create a generic cloud framework before real duplication proves one is necessary.

## 2. Research artifacts

- `docs/research/home-building-drivers/TUYA-CLOUD-READINESS-RESEARCH.md`
- `docs/research/home-building-drivers/INTELBRAS-INTEGRATION-READINESS-RESEARCH.md`
- `docs/research/home-building-drivers/TUYA-INTELBRAS-CLOUD-CONVERGENCE.md`
- this final handoff document

All repository changes in this lane are documentation-only.

## 3. Final source revalidation summary

### Tuya

Revalidated against current official material on 2026-10-06:

- current Cloud Services API reference still exposes IoT Core, Smart Home APIs, Message Service and explicitly separates Legacy APIs;
- IoT Core pricing page remains last-updated 2026-07-28;
- Trial remains development/debug only and commercial use is prohibited;
- IoT Core still requires renewal after its validity period;
- public monthly allowances remain:
  - Trial: 26,000 API calls / 68,000 messages;
  - Flagship: 224 million API calls / 568 million messages;
  - Corporate: 426 million API calls / 1 billion messages;
- paid overage unit rates outside mainland China remain publicly listed:
  - Flagship: USD 3.15/million API calls and USD 1.24/million messages;
  - Corporate: USD 2.97/million API calls and USD 1.17/million messages;
- exact production base subscription price remains login/service-selection dependent; no public unauthenticated price was established;
- current support, updated 2026-08-19, publishes generic cloud-to-cloud protection of:
  - 500,000 API invocations/day;
  - 500 requests/second;
- endpoint-specific stricter limits and exact rate-limit error/backoff behavior still require qualification;
- Brazil remains mapped to Eastern America Data Center in the current 2026-08-12 data-center table;
- historical account/data-center migration behavior still matters;
- simple/project authorization and user-code authorization remain documented separately;
- OAuth/access tokens remain documented as valid for 2 hours with refresh-token replacement;
- Message Service still exposes independent test/production subscriptions and Pulsar integration;
- Tuya publishes an official C#/.NET Pulsar consumer path;
- the current official Tuya GitHub sample `tuya/tuya-pulsar-sdk-dotnet` targets `net9.0` and references:
  - `DotPulsar 3.4.0`;
  - `Newtonsoft.Json 13.0.1`;
- no dependency/version was adopted by this research lane.

### Intelbras

Revalidated against the current public GDI manual on 2026-10-06:

- GDI remains the current official **Gestão de Dispositivos IOT / Plataforma de APIs para Casa Inteligente Intelbras**;
- API base remains `https://api-casainteligente.intelbras.com.br`;
- public examples remain HTTP POST + JSON + `Authorization: Bearer {token}`;
- GDI remains company-only and requires valid CNPJ;
- API use requires a paid plan;
- plan families remain:
  - API Plan (IoT);
  - Video Plan (Streaming);
- monthly quota concept remains explicit;
- extra packages remain documented;
- optional automatic extra-package purchase remains documented and must never be controlled silently by EliteSCADA Runtime;
- plans remain monthly auto-renewing;
- exact plan prices and exact numeric current quotas remain login-gated/not public in the open manual;
- multiple Mibo accounts can be linked, each with its own token;
- Swagger remains available after login;
- GDI remains **Mibo-only**;
- Izy remains explicitly unsupported by GDI;
- no public GDI token expiry/refresh/scopes contract was established;
- no public GDI webhook/MQTT/WebSocket/SSE/event-stream contract was established;
- no public generic GDI QPS table was established;
- no public GDI data-region/residency contract was established;
- public Mibo Cloud terms do not replace a GDI-specific commercial/legal review for embedding a connector in proprietary SCADA software.

## 4. TUYA final dossier

### Official product/API

Candidate:

**Tuya Developer Platform / Cloud Development + IoT Core + current Cloud Service APIs**

Use current APIs, not Legacy API documentation as the implementation baseline.

### Brazil / region

Current public mapping:
- Brazil (+55) -> Eastern America Data Center.

Region/data center must be explicit in the Data Source because historical account mappings can differ after Tuya data-center changes.

### Commercial requirements

Production requires a valid paid IoT Core entitlement.

Trial:
- suitable only for development/debug;
- commercial use prohibited.

Exact base subscription price:
- **COMMERCIAL_INFORMATION_NOT_PUBLIC / LOGIN_OR_SALES_QUALIFICATION_REQUIRED**

### Authentication

Two current official product shapes:

1. project/simple authorization;
2. user authorization/code mode.

Main must deliberately choose the first supported EliteSCADA product shape.

### Token model

- client ID;
- client secret/signing secret;
- access token;
- refresh token;
- two-hour token lifetime;
- HMAC-SHA256 signed API requests;
- timestamp and optional nonce.

All secret material must use Protected Material Authority.

### Identity

Separate:
- project/account authorization identity;
- region/data center;
- stable vendor Device ID;
- product ID/model metadata;
- gateway/sub-device relation;
- canonical EliteSCADA Equipment ID;
- canonical EliteSCADA Location.

Friendly name is never identity.

### Inventory

Required:

`authorize -> inventory -> select -> candidate -> preview -> apply`

No whole-project automatic import.

### Capabilities

Map only from explicit vendor specification/function semantics.

Do not convert DP/function names to canonical capabilities through loose string matching.

### Commands / process truth

`API_ACCEPTED != PROCESS_TRUTH`

Stateful write:

`Runtime.WriteAsync(TAG) -> Tuya dispatch -> authoritative event/status/readback -> CurrentTagCache`

### Events

Official Message Service:
- Pulsar-based;
- production/test subscriptions;
- state/data/offline-style messages;
- persistent messaging intent;
- official C# path exists.

Still qualify:
- exact duplicate/redelivery/order/retention behavior needed by EliteSCADA;
- HA consumer ownership;
- dependency/license/packaging posture.

### Rate limits / quotas

Public:
- monthly IoT Core API/message allowances;
- 500,000 cloud-to-cloud calls/day generic application traffic;
- 500 requests/second generic API traffic protection.

Still qualify:
- stricter endpoint-specific limits;
- exact error/backoff semantics.

### Outage model

Cloud/DNS/TLS/internet failure:
- bounded backoff;
- quality/freshness degradation;
- no indefinite Good quality.

Terminal authorization/subscription errors:
- stop blind retry loops;
- expose explicit diagnostic state.

### Security

Required:
- TLS only;
- request signing;
- clock-health diagnostics;
- region endpoint validation;
- Protected Material;
- secret redaction;
- no mobile/private API fallback;
- explicit rotation/revocation handling.

### Legal/commercial

**LEGAL_REVIEW_REQUIRED**

**COMMERCIAL_REVIEW_REQUIRED**

Tuya general terms explicitly allow service-specific additional provisions. Production connector rights, branding, redistribution, data processing, SLA and subscription terms must be qualified.

### L0-L4

L0:
- signing/token/region/DTO/mapping/error/redaction.

L1:
- fake official-shaped peer including auth expiry, invalid signing, region mismatch, quota, offline, accepted/no-state, message duplicates/out-of-order, outage.

L2:
- legitimate official development/test project.

L3:
- canonical EliteSCADA Runtime and diagnostics.

L4:
- legitimate official cloud project + supported real device.

### Architecture

**Built-in .NET connector**

No mandatory sidecar.

### Final decision

**TUYA = GO_WITH_GATES**

## 5. INTELBRAS final dossier

### Official API/program

Current official product:

**GDI — Gestão de Dispositivos IOT / Plataforma de APIs para Casa Inteligente Intelbras**

### Product families

HOME scope:
- Mibo devices supported by GDI.

Explicit exclusion:
- Izy is not supported by GDI.

Separate product families:
- professional CFTV;
- professional access control;
- security administration.

Do not create one monolithic Intelbras driver.

### Local/cloud distinction

GDI is cloud-to-cloud.

No generic local MCA hub API was established from GDI documentation.

Physical Zigbee use by some Mibo devices does not make GDI a local Zigbee integration.

### Commercial access

Requires:
- company account;
- valid CNPJ;
- paid API and/or Video plan.

Exact open-web price/quota:
- **COMMERCIAL_INFORMATION_NOT_PUBLIC**

A legitimate L2 test requires deliberate company/commercial access.

### Authentication

Public flow:
- link Mibo Smart account;
- authorize GDI;
- GDI generates token;
- API uses Bearer token.

Each linked Mibo account has its own token.

Do not store Mibo passwords in EliteSCADA.

Do not label the public flow OAuth without vendor protocol documentation.

### Token lifecycle

Not public:
- expiry;
- refresh;
- scopes;
- revoke API;
- rotation contract.

**TOKEN_LIFECYCLE_NOT_PUBLIC / MUST_QUALIFY**

### Identity

Current public examples expose:
- `ns`;
- `idProduto`;
- linked/shared inventory origin.

Authenticated Swagger must qualify exact stable identity semantics before DEV.

### Events

No public official:
- webhook;
- MQTT;
- WebSocket;
- SSE;
- event subscription

was established.

First bounded implementation can be polling/readback-first if commercial quotas make it acceptable.

### Commands / process truth

Public examples include lamp and lock operations.

Still:

`HTTP_SUCCESS != PHYSICAL_STATE_CONFIRMED`

Use authoritative status/readback before canonical TAG state becomes Good.

### Security

- HTTPS;
- high-impact Bearer token;
- Protected Material only;
- no token logging;
- no Mibo password storage;
- no private/mobile API fallback;
- no Runtime control of payment/auto-purchase settings.

### Legal/commercial

**LEGAL_REVIEW_REQUIRED**

**COMMERCIAL_REVIEW_REQUIRED**

Need vendor confirmation for:
- third-party proprietary SCADA embedding;
- resale/redistribution;
- homologation/partnership;
- branding;
- API deprecation/SLA;
- LGPD/data region.

### Test access

L0/L1 are possible without credentials using official-shaped fakes.

L2:
**BLOCKED — LEGITIMATE COMPANY/COMMERCIAL ACCESS REQUIRED**

L4:
**BLOCKED — VENDOR SERVICE + SUPPORTED MIBO HARDWARE REQUIRED**

### Vendor questions

1. token expiry/refresh/revoke/scopes;
2. event/push API;
3. exact plan quota/QPS/error/backoff;
4. proprietary SCADA embedding rights;
5. data region/LGPD roles/retention;
6. versioning/deprecation/sandbox/homologation policy.

### Architecture

**Built-in .NET GDI/Mibo connector**

No sidecar justified.

### Final decision

**INTELBRAS = GO_WITH_GATES**

## 6. Convergence

### Common patterns

Both reuse:
- Protected Material;
- Data Source;
- discovery/candidate/preview/apply;
- Equipment/TAG/Command;
- current-value quality/freshness;
- #500 CommunicationDriverDiagnostic;
- Runtime write/readback truth;
- HA external-effect authority.

### Shared foundation decision

**NO NEW CLOUD FRAMEWORK**

Reason:
- reusable product authorities already exist;
- remaining mechanics differ materially by vendor.

Only extract shared cloud helpers after real code proves duplication.

### Data Source granularity

Tuya:
- project + region/data center + authorization scope.

GDI:
- linked Mibo account/token.

No automatic Data Source per device.

### Selected import

Required for both.

### Process truth

For both:

`COMMAND_ACCEPTANCE_IS_NOT_PROCESS_TRUTH`

### #546 dependency

Not a blocker for basic state/status cloud connectors.

Use future #546 only for:
- genuine transient device events;
- parameterized/stateless rich operations that cannot honestly be modeled as one canonical TAG value.

No raw vendor API bypass.

### Media

Camera/video belongs in Media Source Core, not scalar TAG payloads.

### Diagnostics

Reuse #500.

No second CloudDiagnostics system.

### HA

HA = High Availability.

Only the active external-effect authority may:
- write;
- command;
- own effectful message-consumer behavior where applicable.

Failover requires full authoritative resync.

### Recommended implementation order

**TUYA_FIRST**

Why:
- stronger public API documentation;
- known auth/token lifecycle;
- explicit region model;
- official message test environment;
- official C# Pulsar path;
- easier L2 route.

Then:

**INTELBRAS_GDI_AFTER_VENDOR_QUALIFICATION**

## 7. Exact blockers

### Tuya

1. Main chooses first auth product shape.
2. Commercial/legal/LGPD review.
3. Exact paid production entitlement.
4. Endpoint-specific rate/error qualification beyond generic public limits.
5. Message Service dependency/license/HA/reliability review.
6. Brazil legacy-region test case.

### GDI

1. Legitimate CNPJ/company test access.
2. Paid API plan.
3. Authenticated Swagger snapshot.
4. Token lifecycle/scopes.
5. Exact plan quota/QPS/error contract.
6. Official event/push answer or polling-only acceptance.
7. Commercial embedding rights.
8. LGPD/data-region review.
9. Supported L4 Mibo hardware.

## 8. Official source register — final access

Accessed 2026-10-06.

### Tuya

- https://developer.tuya.com/en/docs/cloud
- https://developer.tuya.com/en/docs/legacy-reference-of-cloud-service-apis
- https://developer.tuya.com/en/cloud-development
- https://developer.tuya.com/en/docs/iot/membership-service?id=K9m8k45jwvg9j
- https://www.tuya.com/vas/commodity/IOT_CORE_V2
- https://support.tuya.com/en/help/_detail/K9zsouaplymo6
- https://support.tuya.com/en/help/_detail/K9g7809uyf803
- https://support.tuya.com/en/help/_detail/K8sdy1i4g9u0q
- https://support.tuya.com/en/help/_detail/K9g77zet6uxp0
- https://developer.tuya.com/en/docs/iot/oem-app-data-center-distributed?id=Kafi0ku9l07qb
- https://developer.tuya.com/en/docs/iot/authentication-method?id=Ka49gbaxjygox
- https://developer.tuya.com/en/docs/cloud/80bb968f1d?id=Ka7kjv3j8jgvr
- https://developer.tuya.com/en/docs/iot/manage-messages?id=Ka49p7loog3ze
- https://developer.tuya.com/en/docs/iot/message-service?id=K95zu0nzdw9cd
- https://developer.tuya.com/en/docs/iot/Pulsar-SDK-get-message-c?id=Kawpkk5vic1es
- https://github.com/tuya/tuya-pulsar-sdk-dotnet
- https://hotel.console.tuya.com/policies/service

### Intelbras

- https://app-mibo.intelbras.com.br/manual-gdi.html
- https://app-mibo.intelbras.com.br/termos/pt-br.html
- https://manual-mibo.intelbras.com.br/pt-br/manuais/manual.html
- https://loja.intelbras.com.br/sensor-movimento-msm-1001/p
- https://loja.intelbras.com.br/sensor-abertura-msa-1001/p
- https://loja.intelbras.com.br/sensor-temperatura-umidade-mtu-1001/p
- https://loja.intelbras.com.br/central-automacao-mca-1001/p
- https://forum.intelbras.com.br/viewtopic.php?t=77094
- https://suporte.intelbras.com.br/images/d/dd/Documento_de_Integrao_via_API_V1.3.pdf

## 9. Final declarations

RESEARCH_COMPLETE  
DOCS_ONLY  
NO PRODUCT CODE CHANGED  
NO PRIVATE API USED  
NO MOBILE API SCRAPED  
NO CREDENTIAL REVERSE ENGINEERING  
NO CUSTOMER CREDENTIAL USED  
NO CLOUD CREDENTIAL CREATED  
NO COMMERCIAL PLAN PURCHASED  
NO COMMERCIAL TERMS ACCEPTED ON BEHALF OF PRODUCT OWNER  
NO CLOUD FRAMEWORK IMPLEMENTED  
NO SCHEMA CHANGED  
NO DRIVER SDK CHANGED  
NO EVENT RUNTIME CHANGED  
NO COMMAND RUNTIME CHANGED  
NO DEPENDENCY CHANGED  
NO CI CHANGED  
HA = HIGH AVAILABILITY  
HAB = HOME ASSISTANT BRIDGE  
NO MERGE PERFORMED

**RESEARCH TUYA-INTELBRAS -> MAIN COORDINATOR**
