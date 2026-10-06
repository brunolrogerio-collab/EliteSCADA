# Zigbee Legal / License / Certification Research — Checkpoint 3

Status: RESEARCH_ONLY / DOCS_ONLY / NOT LEGAL ADVICE / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

Issue owner: #541 — HOME-RESEARCH-02

Access/revalidation date: 2026-10-06

This document records engineering/legal-review boundaries. It is not legal advice and does not approve marketing claims, certification status, redistribution, trademarks, or firmware redistribution.

---

## 1. Executive classification

### Zigbee2MQTT external bridge

Preferred:

**USER_MANAGED_EXTERNAL**

Research classification:

**USER_MANAGED_OK**

Meaning:
EliteSCADA connects to a separately operated Zigbee2MQTT instance over MQTT and does not redistribute Zigbee2MQTT.

This is an architecture classification, not a legal opinion.

Any bundled, appliance, installer or managed-sidecar distribution of Zigbee2MQTT remains:

**BUNDLED_LEGAL_REVIEW_REQUIRED**

### Native Zigbee sidecar

Core candidate dependencies:

- zigbee-herdsman — MIT
- zigbee-herdsman-converters — MIT
- Node.js — MIT for Node core, with bundled third-party components under additional licenses

Research classification:

**PERMISSIVE_CORE_WITH_NOTICE_AND_TRANSITIVE_REVIEW**

Before release:

**LEGAL_REVIEW_REQUIRED**

for:
- full transitive license inventory;
- notices;
- Node redistribution package;
- coordinator firmware;
- vendor assets;
- trademark/certification claims.

---

## 2. Zigbee2MQTT license

Revalidated official repository:

**GPL-3.0**

Version observed:
2.14.2.

Preferred v1:
do not distribute Zigbee2MQTT.

EliteSCADA:
- stores MQTT endpoint/trust credentials;
- communicates through documented MQTT;
- may document how to connect a user-owned instance;
- does not copy or modify Z2M code.

This is intentionally different from bundling.

---

## 3. Zigbee2MQTT bundling

Examples requiring legal review before product decision:

- EliteSCADA installer downloads/installs Z2M;
- EliteSCADA ships a Z2M container image;
- an EliteSCADA appliance image contains Z2M;
- Z2M is included as a managed local service;
- a modified Z2M build is redistributed;
- Z2M source/code is copied into EliteSCADA.

Disposition:

**DO NOT BUNDLE IN FIRST Z2M RELEASE**

If product later wants bundling:
legal counsel must determine distribution/source/notice/combined-work obligations for the chosen packaging.

---

## 4. zigbee-herdsman license

Official license:

**MIT**

Current observed release:
11.0.0.

MIT redistribution generally requires retaining copyright and permission notice.

Engineering requirement:
ship the upstream MIT license notice in third-party notices.

---

## 5. zigbee-herdsman-converters license

Official license:

**MIT**

Current observed release:
26.117.1.

Engineering requirement:
retain MIT notice.

Device definition/converter code remains upstream copyrighted material even though permissively licensed.

Do not remove attribution simply because it is bundled as data/compiled JS.

---

## 6. Node.js license

Official Node repository states:

Node.js core is licensed under MIT.

The Node LICENSE also includes third-party components with other licenses.

Therefore:

**NODE_LICENSE != ONE-LINE-MIT-ONLY-INVENTORY**

Packaging must preserve:
- Node license;
- third-party notices included by Node;
- any applicable attribution/redistribution requirements.

Do not manually reduce Node notices to one MIT sentence.

---

## 7. npm/transitive dependencies

Native sidecar transitive dependencies must be inventoried at build time.

Required:
- exact lockfile;
- SBOM;
- package name;
- version;
- license expression;
- source;
- required notice;
- exception/manual-review flag.

If unknown/custom/nonstandard license appears:
**LEGAL_REVIEW_REQUIRED**

Do not assume all herdsman dependencies are MIT.

---

## 8. Coordinator firmware

Coordinator firmware is legally separate from:
- EliteSCADA;
- herdsman;
- converters.

Examples:
- TI Z-Stack coordinator firmware;
- Silicon Labs Ember/EZNet firmware;
- SMLIGHT bridge firmware;
- SONOFF bridge/ESP firmware.

Research v1 recommendation:

**DO NOT REDISTRIBUTE COORDINATOR FIRMWARE AS PART OF ELITESCADA**

Use:
- vendor-preinstalled qualified firmware;
- documented admin guidance;
- vendor-supported flashing/update tools where appropriate.

If EliteSCADA later bundles firmware images:
each firmware/license must be reviewed separately.

---

## 9. Firmware version support

EliteSCADA compatibility records may identify tested firmware versions.

This does not grant rights to redistribute firmware.

Support documentation can state:
- tested with version X;
- vendor update may change compatibility;
- requalification required.

---

## 10. Vendor tools

Vendor flashers, web consoles and firmware utilities remain vendor software/services.

Do not copy them into EliteSCADA unless license permits and legal review approves.

The product may link/document vendor instructions where appropriate.

---

## 11. Vendor logos and images

Do not copy:
- SONOFF logos;
- SMLIGHT logos;
- Philips Hue marks;
- IKEA product images;
- Aqara logos;
- Zigbee2MQTT branding

into product assets merely because they are visible on websites.

Default:
- plain text vendor/model name where nominative identification is needed;
- product-owned generic device icons;
- link to vendor documentation where permitted.

Use of third-party logos/images:

**LEGAL_REVIEW_REQUIRED / PERMISSION_REQUIRED AS APPLICABLE**

---

## 12. Zigbee trademark

The Connectivity Standards Alliance owns Zigbee branding/trademarks.

Current CSA brand guidance permits using the Zigbee name/wordmark to identify use of Zigbee technology subject to brand rules.

Engineering/product writing should:
- write **Zigbee** with capital Z;
- use it as an adjective, e.g. "Zigbee coordinator";
- avoid altered spelling/logo art;
- never imply certification that has not occurred.

Marketing/legal should review the final wording.

---

## 13. Zigbee logos

Current CSA guidance distinguishes ordinary technology references from certification logos.

If EliteSCADA is not certified:
- do not use a Zigbee Certified Product logo;
- do not state "Zigbee Certified Product";
- do not imply certification.

Current CSA guidance states uncertified products may not use Zigbee logos in a way that implies certification.

Disposition:

**NO ZIGBEE CERTIFIED LOGO UNTIL FORMAL CERTIFICATION APPROVAL**

---

## 14. Certification is not inherited

Using:
- a certified chipset;
- a certified coordinator;
- a compliant platform;
- an upstream certified device

does not automatically make the entire EliteSCADA product/service certified.

CSA guidance states that an entire product/service cannot be labeled Zigbee Certified merely because a component is certified.

Therefore:

**NO DERIVED CERTIFICATION CLAIM**

---

## 15. CSA certification path

CSA currently describes:
- End Product certification;
- Compliant Platform certification;
- Software Component certification;
- certification testing through authorized providers;
- membership/manufacturer-ID steps.

If Product wants:
- Zigbee Certified positioning;
- official certification logo;
- formal interoperability claim tied to CSA certification

then:

**CSA_CERTIFICATION_REVIEW_REQUIRED**

The correct program depends on final product architecture and marketing scope.

Engineering research does not decide that classification.

---

## 16. Native EliteSCADA certification question

Native EliteSCADA acts as:
- gateway/control software;
- Trust Center/network manager through sidecar;
- host of a third-party coordinator.

Whether this should be certified as:
- software component;
- end product/service;
- another CSA program

must be determined with CSA/legal after the architecture is frozen.

Do not assume that "software only" means certification is irrelevant.

---

## 17. Zigbee2MQTT certification question

A user-managed Zigbee2MQTT bridge is external software.

EliteSCADA should describe:
- "Zigbee2MQTT integration";
- "connects to a user-managed Zigbee2MQTT instance"

without implying:
- Zigbee2MQTT is certified by EliteSCADA;
- EliteSCADA is Zigbee Certified;
- all devices interoperate.

---

## 18. Hardware resale/bundling

If EliteSCADA later sells a kit containing a coordinator:

separate review is needed for:
- regional radio/electrical compliance;
- importer/reseller obligations;
- warranty;
- vendor terms;
- certification marks;
- labeling;
- packaging;
- support responsibility.

The vendor showing CE/FCC/RoHS on a product page is not a substitute for EliteSCADA legal review of resale in each target market.

---

## 19. Device compatibility claims

Allowed engineering statement:

"Qualified with EliteSCADA build X, coordinator Y firmware Z, stack versions A/B on date D."

Avoid:
- "all Zigbee devices";
- "works with all Zigbee 3.0";
- "all Zigbee2MQTT devices";
- "universal Zigbee compatibility".

CSA certification and upstream converter support are different from EliteSCADA qualification.

---

## 20. Compatibility list provenance

A future public compatibility list should clearly distinguish:

- EliteSCADA QUALIFIED;
- upstream Zigbee2MQTT LISTED;
- upstream converter KNOWN;
- untested.

Do not mirror the entire Zigbee2MQTT supported-device catalog as an EliteSCADA supported-device list.

---

## 21. External/custom converters

zigbee-herdsman-converters is MIT.

But customer-provided external converter JavaScript can have:
- unknown copyright/license;
- security risk;
- executable code implications.

Production default:

**ARBITRARY_EXTERNAL_CONVERTERS_DISABLED**

If custom converter support is later offered:
- customer responsibility terms;
- code signing/review;
- license declaration;
- sandbox/security model;
- support boundary

require separate review.

---

## 22. Documentation excerpts

Do not copy large upstream docs/device descriptions into EliteSCADA Help.

Use:
- original product-authored summaries;
- links/references;
- only license-compliant necessary excerpts.

This avoids unnecessary copyright dependency.

---

## 23. Device pictures

Upstream device pages may display vendor/product images.

Do not assume these images inherit the code repository's MIT/GPL license.

Use own/generic illustrations unless image license/permission is explicit.

---

## 24. OTA firmware

Device OTA files can have vendor-specific distribution terms.

Classification:

**ADMIN_FUTURE_SCOPE**
+
**FIRMWARE_LICENSE_REVIEW_REQUIRED**

Do not mirror vendor OTA repositories into EliteSCADA without explicit review.

---

## 25. Open-source notice artifact

Native sidecar release should contain a generated:

THIRD-PARTY-NOTICES

with:
- package;
- version;
- copyright;
- license;
- notice text/location;
- source URL;
- modification status.

Also retain:
- SBOM;
- source/release manifest.

---

## 26. Source-offer obligations

For MIT dependencies:
notice retention is the principal known obligation from the reviewed licenses.

For GPL software such as Zigbee2MQTT:
distribution obligations are materially different.

Therefore product packaging must not treat GPL Z2M as equivalent to MIT herdsman/converters.

---

## 27. Modifications/forks

If EliteSCADA modifies herdsman/converters:

- keep upstream notices;
- record patches;
- record source revision;
- contribute upstream where practical;
- carry a patch manifest.

MIT allows modification, but compatibility/support cost increases.

Preferred:
upstream contributions + minimal maintained patch set.

---

## 28. Security advisories

Legal/license approval does not equal security approval.

Release process should also track:
- Node security releases;
- npm dependency advisories;
- herdsman security fixes;
- converter regressions.

Security update still needs compatibility qualification.

---

## 29. Trademark language examples

Preferred descriptive phrases:

- "Zigbee integration"
- "Zigbee coordinator"
- "supports selected qualified Zigbee devices"
- "connects to user-managed Zigbee2MQTT"
- "Native Zigbee support through a managed coordinator service"

Avoid unless formally approved/certified:

- "Zigbee Certified"
- "official Zigbee gateway"
- "universal Zigbee"
- use of certification logo.

---

## 30. Source record

Revalidated 2026-10-06.

### GPL

https://github.com/Koenkk/zigbee2mqtt/blob/master/LICENSE
- GPL-3.0.

### MIT

https://github.com/Koenkk/zigbee-herdsman/blob/master/LICENSE
- MIT.

https://github.com/Koenkk/zigbee-herdsman-converters/blob/master/LICENSE
- MIT.

### Node

https://github.com/nodejs/node/blob/main/LICENSE
- Node core MIT;
- included third-party license notices.

### CSA / Zigbee

https://csa-iot.org/certification/why-certify/
- certification process and program classes.

https://csa-iot.org/csa-iot_products/
- certified end products/platform concepts.

https://csa-iot.org/wp-content/uploads/2022/11/Zigbee_Brand_Guidelines_November_2025.pdf
- current Zigbee brand/logo guidance.

https://csa-iot.org/wp-content/uploads/2024/01/tm_brand_logo-usage-guide_final_03-2024.pdf
- general trademark/brand guidance.

---

## 31. Legal review gates

### Z2M v1 external

Engineering status:
**GO_WITH_GATES**

Legal gate:
confirm product documentation/installer does not redistribute Z2M.

### Z2M bundled

**BUNDLED_LEGAL_REVIEW_REQUIRED**

### Native sidecar

**LEGAL_REVIEW_REQUIRED BEFORE DISTRIBUTION**

Focus:
- transitive licenses;
- Node third-party notices;
- SBOM;
- sidecar third-party notices;
- coordinator firmware;
- trademarks/marketing.

### Zigbee certification claim

**CSA_CERTIFICATION_REVIEW_REQUIRED**

### Vendor logos/assets

**PERMISSION / LEGAL REVIEW REQUIRED**

---

## 32. Conclusion

Zigbee2MQTT:

GPL-3.0

Preferred:
**USER_MANAGED_EXTERNAL**

Bundling:
**BUNDLED_LEGAL_REVIEW_REQUIRED**

Native:

herdsman MIT
+
converters MIT
+
Node MIT core / mixed bundled third-party notices

Packaging:
**PERMISSIVE_CORE_WITH_NOTICE_AND_TRANSITIVE_REVIEW**

Zigbee marks:
**NO CERTIFICATION CLAIM WITHOUT CSA PROCESS**

Coordinator firmware:
**DO NOT REDISTRIBUTE IN FIRST NATIVE RELEASE**

Vendor assets:
**DO NOT COPY BY DEFAULT**

Final:
**LEGAL_REVIEW_REQUIRED before commercial Native distribution and before any certification/logo claim**

Scope:

DOCS_ONLY

NO PRODUCT CODE CHANGED

NO DEPENDENCY CHANGED

NO CI CHANGED

NO MERGE PERFORMED
