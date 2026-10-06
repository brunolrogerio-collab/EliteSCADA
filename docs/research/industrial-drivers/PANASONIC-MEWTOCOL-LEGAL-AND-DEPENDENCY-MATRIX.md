# Panasonic MEWTOCOL — Legal and Dependency Matrix

**Issue:** #553 — INDUSTRIAL-RESEARCH-02  
**Checkpoint:** 3  
**Research date:** 2026-10-06  
**Status:** `RESEARCH_ONLY / DOCS_ONLY / NOT_LEGAL_ADVICE`

This document records implementation-facing legal/licensing findings. It is not a substitute for counsel.

## 1. Panasonic documentation/content

Panasonic Industry Europe site terms state that site content is protected by copyright and generally may not be reproduced except as permitted by the site terms or with prior written permission.

Engineering implication:

- use official manuals as factual protocol references;
- do not redistribute Panasonic PDFs inside EliteSCADA;
- do not copy long manual text, screenshots, diagrams or branded artwork into product help;
- link to official documentation instead;
- write original EliteSCADA protocol documentation and test descriptions;
- keep source/version/date provenance in research and compatibility records.

Recommended implementation posture:

`FUNCTIONAL_FACTS_ONLY / NO_VENDOR_DOCUMENT_REDISTRIBUTION`

The driver should be an original implementation based on protocol behavior, not copied vendor source code or copied vendor documentation.

## 2. Panasonic Mediapool assets

Panasonic Industry Mediapool terms state that downloaded files remain Panasonic property and constrain use of those assets.

Therefore:

- do not package Panasonic product photos, CAD files, logos, screenshots or marketing assets in EliteSCADA without explicit permission;
- do not recolor/modify Panasonic media assets for the driver catalog;
- use EliteSCADA-owned generic driver iconography.

Disposition:

`NO_PANASONIC_MEDIA_ASSETS_IN_PRODUCT_WITHOUT_PERMISSION`

## 3. Panasonic name / trademarks

Panasonic global terms state that rights in trademarks, logos and product names belong to Panasonic or other right holders and that unauthorized trademark use is restricted.

Product naming therefore requires counsel/brand review.

### Recommended compatibility wording

Use factual compatibility language and avoid endorsement claims.

Acceptable direction, subject to legal approval:

- `MEWTOCOL TCP — Panasonic FP compatible`
- `MEWTOCOL Serial — Panasonic FP compatible`

Internal stable IDs may remain:

- `panasonic.mewtocol.tcp`
- `panasonic.mewtocol.serial`

but even internal/public schema naming should be reviewed because it contains the mark.

Never use language such as:

- `Official Panasonic driver`
- `Panasonic certified`
- `Developed with Panasonic`

unless an actual authorization/certification exists.

Do not use Panasonic logos.

Recommended product disclaimer direction:

`Panasonic and related product names are trademarks of their respective owners. EliteSCADA is not affiliated with or endorsed by Panasonic unless explicitly stated otherwise.`

Exact wording is for legal review.

Status:

`LEGAL_REVIEW_REQUIRED_FOR_PUBLIC_PRODUCT_NAMING`

This legal gate does not block protocol engineering research.

## 4. MEWTOCOL naming

MEWTOCOL is a Panasonic protocol/product term used throughout official manuals.

The implementation should use the term only to identify compatibility with the documented protocol, subject to the same naming/legal review above.

Avoid creating a logo or stylized mark derived from Panasonic branding.

## 5. Panasonic software tools

### Control FPWIN Pro7

Panasonic currently lists:

- Full/Update version 7.7.4.1;
- Free Basic version 7.7.4.1;
- Windows 10/11 support.

The full version requires a separate license.

Research/lab rule:

- user/lab installs Panasonic software separately;
- do not bundle or redistribute FPWIN Pro with EliteSCADA;
- do not automate license bypass;
- do not depend on FPWIN Pro at Runtime.

Use only as independent lab/reference tooling.

### FP Data7

Panasonic currently lists FP Data7 V1.12.0.

Same rule:

- separately obtained vendor software;
- lab/reference only;
- not redistributed;
- not a Runtime dependency.

### Control Configurator WD

Useful for configuring supported Panasonic Ethernet devices/cassettes.

Same rule:

`LAB_TOOL_ONLY / NO_REDISTRIBUTION / NO_RUNTIME_DEPENDENCY`

## 6. Production dependency decision

Recommended implementation:

`ELITESCADA_OWNED_DOTNET_CODEC_SESSION`

Expected dependencies:

- existing .NET networking/runtime;
- existing EliteSCADA host serial abstraction from #469;
- no new MEWTOCOL package.

Benefits:

- avoids external protocol-library licensing risk;
- supports exact EliteSCADA diagnostics/process-truth semantics;
- supports FP7 bounded scope without waiting for a third-party library;
- keeps future MEWTOCOL7 work isolated.

## 7. OpenLogics/MewtocolNet finding

Current GitHub repository:

`OpenLogics/MewtocolNet`

Research on 2026-10-06 found:

- repository presents itself as a community, non-Panasonic product;
- TCP and Serial supported;
- register read/write and polling/batching features;
- README states FP7 is currently unsupported;
- README states testing has covered only a few PLC types;
- current GitHub repository identifies its license as `GPL-3.0`.

NuGet package:

`Mewtocol.NET 0.8.1`

Research found:

- last package update: 2023-11-13;
- NuGet metadata reports `MIT`;
- .NET Standard 2.0;
- TCP/Serial;
- package dependency includes System.IO.Ports.

### License inconsistency

Current source repository:
`GPL-3.0`

Published 0.8.1 NuGet metadata:
`MIT`

Do not assume one license applies to the other artifact without exact-version source/license provenance.

Production disposition:

`REJECT_AS_V1_PRODUCTION_DEPENDENCY`

Reasons:

1. license provenance mismatch;
2. package age;
3. explicit FP7 gap;
4. limited tested-device claims;
5. built-in implementation is small enough and better aligned with EliteSCADA contracts.

### Optional lab use

If someone wants to use the library as non-required L2 corroboration:

- freeze exact package/source version;
- record exact license file;
- keep it outside EliteSCADA product dependencies;
- obtain legal approval if source or binary use creates uncertainty;
- do not copy source/test vectors into EliteSCADA.

Not required for acceptance.

## 8. Other community dependencies

No permissive, current, maintained MEWTOCOL library with a stronger evidence base was identified during this checkpoint that would displace the built-in implementation recommendation.

Future DEV must not add a protocol dependency merely because a package exists.

Any candidate requires:

- exact license;
- release/update history;
- supported PLC matrix;
- FP7 behavior;
- TCP + serial behavior;
- source availability;
- CVE/security posture;
- .NET target compatibility;
- deterministic testability.

## 9. Protocol patents / certification

This research did not identify a Panasonic certification program or an explicit protocol-implementation license requirement for MEWTOCOL-COM.

That absence is **not** a legal conclusion.

Before commercial release, counsel should confirm:

- compatibility naming;
- trademark/nominative-use language;
- whether any regional patent/licensing concern applies;
- whether published protocol documentation may be used for the planned implementation in target jurisdictions.

Status:

`NO_KNOWN_CERTIFICATION_GATE_FOUND / LEGAL_CONFIRMATION_REQUIRED`

## 10. Security-related documentation reuse

Panasonic manuals and product pages should inform the security guidance, but EliteSCADA should publish its own original text:

- legacy plaintext protocol;
- trusted/segmented OT network;
- firewall restrictions;
- VPN/private network for remote access;
- no direct public exposure.

Do not reproduce vendor diagrams or paragraphs.

## 11. Compatibility matrix / evidence redistribution

EliteSCADA may publish its own original test results:

- model;
- firmware;
- transport;
- observed protocol behavior;
- pass/fail;
- latency/quality;
- sanitized frame metadata.

Do not attach proprietary Panasonic project files, software binaries or manual extracts unless licensing explicitly permits it.

## 12. Final legal/dependency disposition

| Topic | Decision |
|---|---|
| Official manuals | reference only; no redistribution |
| Panasonic logos/media | do not use without permission |
| Public compatibility naming | legal review required |
| FPWIN Pro / FP Data7 | lab tools only; no redistribution |
| MewtocolNet current source | GPL-3.0; no production dependency |
| Mewtocol.NET 0.8.1 | NuGet says MIT; provenance mismatch vs current source; no production dependency |
| New protocol package | none |
| Production architecture | EliteSCADA-owned .NET codec/session |
| Runtime vendor software | none |
| Sidecar | none |

## 13. Sources checked

Official:

- https://industry.panasonic.eu/terms-service
- https://mediapool.industry.panasonic.eu/
- https://holdings.panasonic/global/terms-of-use.html
- https://industry.panasonic.eu/products/automation-devices-solutions/programmable-logic-controllers-plc/plc-software/programming-software-control-fpwin-pro
- official FP0R / FP-XH / FP7 / FP0H product pages and manuals referenced by the execution dossier.

Dependency metadata:

- https://github.com/OpenLogics/MewtocolNet
- https://www.nuget.org/packages/Mewtocol.NET/

Access date:
2026-10-06.

## 14. Current decision

`DEPENDENCY = BUILT_IN`

`PUBLIC_NAMING = LEGAL_REVIEW_REQUIRED`

`NO_VENDOR_ASSET_REDISTRIBUTION`

`DOCS_ONLY`  
`NO PRODUCT CODE CHANGED`  
`NO DEPENDENCY CHANGED`  
`NO CI CHANGED`  
`NO MERGE PERFORMED`
