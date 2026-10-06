# KNX + DALI Legal, License and Certification Matrix — Checkpoint 3

Status: RESEARCH_ONLY / LEGAL_REVIEW_REQUIRED  
Issue: #539 — HOME-RESEARCH-01  
Access date: 2026-10-06

## 1. Scope

This document records implementation-relevant licensing, certification and trademark findings.

It is technical research, not legal advice.

No statement here grants EliteSCADA a trademark, certification, redistribution or specification right.

Final commercial/product decisions require legal review.

## 2. Executive legal disposition

KNX implementation:

`GO_WITH_LEGAL_GATES`

DALI gateway-first implementation:

`GO_WITH_BRANDING_DISCIPLINE`

Native DALI:

`NOT_JUSTIFIED / WOULD_EXPAND_CERTIFICATION_AND_INTEROP_SCOPE`

Mandatory legal flags:

1. Falcon license/redistribution review.
2. KNX trademark/marketing review.
3. ETS project import/content handling review.
4. DALI/DALI-2 trademark/logo review.
5. Vendor commissioning-tool redistribution review if packaging is ever proposed.
6. Third-party library licenses remain dependency-specific.

## 3. KNX specification and ecosystem

KNX Association publishes current specification/support material and provides the ETS/Falcon ecosystem.

The research can use public technical documentation to design architecture.

That does not imply unrestricted redistribution of KNX copyrighted materials, ETS assets, product databases or trademarks.

### Certification distinction

KNX Association's current certification prerequisite page specifically describes a company wishing to develop a KNX-compatible product that:

- is configurable in ETS; and
- is branded with the KNX logo.

Such a company must meet membership/IPR/trademark/certification prerequisites.

The proposed EliteSCADA KNX driver is a software client/integration endpoint, not presently a KNX device intended to be downloaded/configured by ETS.

Therefore:

- do not automatically classify EliteSCADA as a KNX-certified device product;
- do not claim that certification is definitely unnecessary either;
- product counsel/KNX Association confirmation should classify the exact commercial presentation before launch.

`LEGAL_REVIEW_REQUIRED — KNX_PRODUCT_CLASSIFICATION`

## 4. KNX trademark

KNX branding is separate from protocol interoperability.

Rules around KNX trademark/logo use must not be inferred from:

- Falcon availability;
- an open-source library license;
- use of a certified KNX/IP interface;
- successful interoperability testing.

EliteSCADA marketing should initially use factual compatibility wording reviewed by legal and avoid the KNX certification logo unless authorization is explicit.

## 5. Falcon 6 license

Current official KNX Tools Software License Agreement contains Falcon-specific terms.

The public license text states, among other things, that Falcon:

- is free of license fees;
- grants a personal, non-exclusive, non-transferable right;
- permits reproduction/distribution of Falcon copies to end users who purchased or obtained the licensee's software product created using Falcon, subject to the agreement;
- permits development on behalf of a third party intending to distribute the developed product;
- leaves support/service responsibility with the licensee.

This makes Falcon substantially more commercially plausible than a simplistic “proprietary SDK = impossible” conclusion.

It does not remove legal review.

Before adding Falcon to EliteSCADA:

- review the complete current agreement, not excerpts;
- confirm NuGet/package redistribution mechanics;
- confirm notices/attribution;
- confirm SaaS/on-prem/container implications;
- confirm use of KNX trademarks in product UI/docs;
- confirm any restrictions tied to use against the KNX system;
- archive the exact agreement version accepted by the company.

`LEGAL_REVIEW_REQUIRED — FALCON_REDISTRIBUTION`

## 6. Alternative KNX stacks

### XKNX

Current repository license:

`MIT`

Implications:

- permissive software license;
- attribution/license notice obligations;
- sidecar/process packaging remains an architectural cost, not a copyleft blocker.

Important:

Do not assume separate ETS/project parser projects use the same license merely because they are in the XKNX ecosystem.

Every parser/package needs its own license audit.

### ChrisTTian667/knx-dotnet

Current repository license:

`MIT`

Licensing is permissive.

The research rejects it for Tier A production on technical maturity/Secure coverage grounds, not license grounds.

### Calimero Core

Current license:

`GPL-2.0 with Classpath Exception`

The Classpath Exception changes normal GPL linkage implications, but distribution still requires exact legal review.

Because Calimero is not the preferred shipped runtime, the cleanest use is:

- external independent L2 test peer;
- separately obtained/run test tooling where allowed.

Do not copy Calimero code into proprietary EliteSCADA core.

`LEGAL_REVIEW_REQUIRED` if Calimero is ever distributed with EliteSCADA.

## 7. ETS licensing

Current KNX support documentation states ETS6 licensing is based on project/device size.

Current observed public prices on 2026-10-06 include:

- ETS6 Professional: EUR 1,000 excluding VAT;
- ETS6 Home: EUR 350 excluding VAT;
- ETS6 Lite: EUR 200 excluding VAT.

Prices can change and are procurement data, not architecture.

For the lab:

`ETS6 Professional recommended`

For customers:

EliteSCADA Runtime must not require an ETS license merely to execute normal KNX process traffic.

ETS remains the KNX planning/commissioning authority.

## 8. ETS project export/import

KNX current documentation states:

- ETS6 exports `.knxproj`;
- export licensing is checked according to the ETS license/project size;
- export files receive integrity and source signatures;
- import is version/license constrained;
- public Project Scheme Documentation describes the XML scheme and lists external-tool import as a use case.

This supports the technical feasibility of a future read-only metadata importer.

### Data/content caution

A `.knxproj` can contain more than simple group-address strings.

Depending on export options/project:

- product/catalog information;
- application programs;
- DCA-related files;
- manufacturer data;
- topology/project metadata;
- signatures.

Do not treat the entire archive as freely redistributable EliteSCADA content.

Preferred first importer scope, after legal approval:

- consume user-supplied project file locally;
- extract only metadata required to materialize bindings/equipment suggestions;
- do not re-publish embedded manufacturer binaries/catalogs;
- preserve source/project attribution/fingerprint as needed;
- do not silently copy the file into ordinary portable project exports.

`LEGAL_REVIEW_REQUIRED — ETS_PROJECT_IMPORT`

## 9. ETS keyring

KNX officially documents use of exported keyrings outside ETS and Falcon SDK, including derivation/decryption details for:

- backbone keys;
- tunnel passwords;
- authentication codes;
- management passwords;
- tool keys;
- group keys.

This establishes technical legitimacy of external consumption.

It also confirms the keyring is highly sensitive protected input.

Rules for EliteSCADA:

- never log raw keyring;
- never persist decrypted keys in Engineering JSON;
- never include raw keyring in `.escadapkg`;
- store only protected host references/fingerprints in portable configuration;
- require destination-host reprovisioning when appropriate.

Whether the original keyring archive may be retained encrypted by the host should be a deliberate security/legal policy decision.

## 10. DALI-2 certification

DALI Alliance operates independently verified DALI-2 certification.

Its current Product Database says certification validity is tied to the exact product identity, including:

- brand;
- GTIN;
- hardware version;
- firmware version;
- valid unique ID conditions.

Therefore EliteSCADA laboratory evidence must record those fields for every DALI-2 device used as certification evidence.

Do not write:

“this product family is DALI-2 certified”

unless the exact tested/shipped identity matches the current database record.

Prefer:

“lab unit X, GTIN Y, HW Z, FW W is listed as DALI-2 certified in DALI Alliance product ID N on date D.”

## 11. DALI trademarks and logos

DALI Alliance states that:

- DALI, DALI-2, D4i, DALI+ and DiiA wordmarks/logos are trademarks;
- trademark usage is reserved for DALI Alliance members according to its guidelines;
- certified DALI-2 products qualify for DALI-2 trademark use under the program.

Gateway-first architecture does not make EliteSCADA itself a DALI-2 certified product.

Therefore:

- do not place the DALI-2 certification logo on EliteSCADA merely because it interoperates with a certified gateway;
- do not present EliteSCADA as “DALI-2 certified” without its own applicable certification;
- describe the gateway/gear certification factually;
- get legal approval for trademarked word/logo presentation.

`LEGAL_REVIEW_REQUIRED — DALI_BRANDING`

## 12. Gateway-first certification impact

Gateway-first has a significant legal/certification advantage:

EliteSCADA does not implement the DALI electrical/bus protocol.

It talks to:

- Modbus;
- BACnet;
- KNX.

The professional gateway owns:

- DALI application-controller behavior;
- DALI physical bus;
- DALI addressing;
- DALI certification for its product;
- DALI commissioning.

This does not eliminate interoperability testing, but it avoids making the first EliteSCADA release itself a new native DALI application controller.

This is one reason native DALI remains `NOT_JUSTIFIED`.

## 13. Native DALI future legal gate

If EliteSCADA later ships native DALI bus/controller hardware or a certifiable DALI application-controller product, Main must reopen:

- DALI Alliance membership;
- certification test sequence;
- exact IEC 62386 parts;
- trademark rights;
- hardware/firmware identity;
- electrical compliance;
- bus power/interface requirements;
- interoperability claims.

Do not inherit certification from a USB/DALI adapter or third-party transceiver.

## 14. Vendor commissioning tools

Tools include:

- ETS;
- Theben DCA/web commissioning;
- Intesis MAPS;
- LOYTEC L-INX Configurator;
- ABB i-bus Tool;
- Schneider DCA;
- Lunatone DALI Cockpit or equivalent.

Current research uses their documented behavior as external commissioning authority.

Do not bundle, redistribute, reverse engineer or automate their proprietary binaries unless the applicable license explicitly permits it.

The first Runtime architecture does not need to redistribute them.

## 15. Vendor APIs

Public protocol documentation does not automatically grant:

- trademark rights;
- certification;
- bundled SDK redistribution;
- firmware redistribution;
- use of private/internal endpoints.

For direct gateway APIs:

- use only documented public endpoints;
- retain vendor notices where required;
- audit auth/TLS;
- audit API/SDK license separately.

The current recommendation avoids vendor SDK dependencies for first DALI support by using standard Modbus/BACnet/KNX providers.

## 16. Open-source attribution matrix

| Component | Current observed license | Product disposition |
| --- | --- | --- |
| XKNX | MIT | sidecar fallback / L2 peer |
| knx-dotnet | MIT | not selected technically |
| Calimero Core | GPL-2.0 + Classpath Exception | L2 peer; legal review if distributed |
| Falcon 6 | KNX Tools Software License Agreement | preferred implementation with legal gate |

No code from these projects has been copied by this research lane.

## 17. Certification/marketing wording recommendations

Safe factual style pending legal review:

- “Connects to supported KNX/IP interfaces using KNXnet/IP.”
- “Supports configured KNX Secure operation when enabled and provisioned.”
- “Integrates DALI lighting through supported professional Modbus/BACnet/KNX gateways.”
- “Validated with DALI-2-certified gateway/control-gear models listed in the interoperability matrix.”

Avoid without explicit authorization:

- “KNX Certified EliteSCADA”
- “DALI-2 Certified EliteSCADA”
- KNX logo on product;
- DALI-2 logo on product;
- blanket “works with all KNX/DALI products.”

## 18. Compatibility claims

Compatibility must be evidence-based.

Distinguish:

### Protocol capability

Example:

“supports KNXnet/IP secure tunneling.”

### Tested model

Example:

“validated with Weinzierl KNX IP Interface 732 secure.”

### Downstream DALI device

Example:

“validated through Theben P64 with Tridonic product ID 3356.”

Never convert one successful device into a claim covering all devices sharing a protocol label.

## 19. Documentation retention

For every dependency/certification decision, archive in product compliance records:

- URL;
- organization;
- document title;
- version;
- access date;
- exact package/product version;
- license text/hash where appropriate;
- approval decision;
- attribution requirements;
- renewal/revalidation trigger.

Time-sensitive facts should be rechecked before release.

## 20. Release legal gates

### KNX implementation gate

Must have:

- Falcon agreement reviewed if Falcon selected;
- trademark wording approved;
- ETS importer scope approved if importer is in v1;
- protected keyring policy approved;
- third-party notices defined.

### DALI gateway gate

Must have:

- exact gateway model;
- exact vendor documentation;
- exact DALI Alliance certification record where certification is claimed;
- marketing wording that does not imply EliteSCADA DALI-2 certification.

### Native DALI

No release.

`NOT_JUSTIFIED`

## 21. Official sources revalidated 2026-10-06

KNX:

- Requirements prior to product certification: https://support.knx.org/hc/en-us/articles/4416981176466-Requirements-prior-to-product-certification
- KNX labelling requirements: https://support.knx.org/hc/en-us/articles/4605579760146-Labelling-requirements
- Project export: https://support.knx.org/hc/en-us/articles/360020990259-Project-export
- Project Scheme Documentation: https://support.knx.org/hc/en-us/article_attachments/360024169360
- Use keyring outside ETS & Falcon SDK: https://support.knx.org/hc/en-us/articles/360001582259-Use-keyring-outside-ETS-Falcon-SDK
- Secure Tunneling: https://support.knx.org/hc/en-us/articles/360000653399-Secure-Tunneling
- KNX Tools Software License Agreement, current observed agreement: https://support.knx.org/hc/en-us/article_attachments/18554600032786
- ETS6 licenses/prices: https://support.knx.org/hc/en-us/articles/21546945829010-ETS6-and-other-KNX-software-licenses-types-and-prices

DALI:

- Certification overview: https://www.dali-alliance.org/dali2/
- Certification status: https://www.dali-alliance.org/dali2/status.html
- Product Database: https://api.dali-alliance.org/products
- Trademarks and logos: https://www.dali-alliance.org/trademarks/
- Colour/DT8: https://www.dali-alliance.org/dali/colour.html

Open source:

- XKNX: https://github.com/XKNX/xknx
- Calimero Core: https://github.com/calimero-project/calimero-core
- knx-dotnet: https://github.com/ChrisTTian667/knx-dotnet

## 22. Legal checkpoint disposition

`KNX = GO_WITH_LEGAL_GATES`

`DALI_GATEWAY_FIRST = GO_WITH_BRANDING_DISCIPLINE`

`ETS_IMPORT = OPTIONAL_V1 / LEGAL_REVIEW_REQUIRED`

`FALCON = PREFERRED_WITH_LICENSE_REVIEW`

`DALI_LOGO_ON_ELITESCADA = NOT_AUTHORIZED_BY_RESEARCH`

`KNX_LOGO_ON_ELITESCADA = NOT_AUTHORIZED_BY_RESEARCH`

`NATIVE_DALI = NOT_JUSTIFIED`

DOCS_ONLY  
NO PRODUCT CODE CHANGED  
NO DEPENDENCY CHANGED  
NO CI CHANGED  
NO MERGE PERFORMED
