# Matter + Z-Wave Legal / Certification Research

Research date: **2026-10-06**  
Issue owner: **#542 — HOME-RESEARCH-03**  
Contract: **C-HOME-MATTER-ZWAVE-RESEARCH-01**  
Scope: **RESEARCH_ONLY / DOCS_ONLY / NOT LEGAL ADVICE**

This document separates:

- open-source software licensing;
- protocol specification access;
- product certification;
- trademark/logo rights;
- radio/regulatory compliance.

These are independent obligations.

**LEGAL_REVIEW_REQUIRED** before product marketing, logo use, certification claims, commercial redistribution packaging decisions or regional RF deployment.

---

# 1. Executive legal posture

## Matter

Engineering interoperability may proceed using the selected permissively licensed open-source stacks.

However:

- Apache-2.0 does not make EliteSCADA a Matter Certified product;
- the preferred current Matter(.js) Server successor states that it is Beta and not yet officially re-certified by the Connectivity Standards Alliance;
- Matter software/controller certification exists and is actively used by controller products;
- Matter logos/certification wording require Alliance authorization/certification/registration as applicable.

Disposition:

**ENGINEERING GO_WITH_GATES / CERTIFICATION LEGAL TRACK REQUIRED BEFORE CERTIFIED MARKETING**

## Z-Wave

Engineering interoperability may proceed using:

- Z-Wave JS — MIT;
- Z-Wave JS Server — Apache-2.0;
- a supported controller.

However:

- open-source license does not grant Z-Wave certification marks;
- Z-Wave certification includes technical and market certification;
- current Alliance process ties product certification and mark use to membership/licensing rules;
- exact certification obligations for EliteSCADA as software plus an external certified USB controller require direct legal/Alliance classification.

Disposition:

**ENGINEERING GO_WITH_GATES / CERTIFICATION SCOPE MUST BE CONFIRMED BEFORE Z-WAVE PRODUCT CLAIMS**

---

# 2. Open-source license matrix

| Component | Research version | License | Intended role |
|---|---|---|---|
| Matter(.js) Server | 1.4.0 | source/container identified Apache-2.0; npm metadata discrepancy noted | managed Matter sidecar |
| matter.js | 0.17.9 | Apache-2.0 | underlying Matter stack |
| connectedhomeip | v1.6.1.0 | Apache-2.0 | reference / L2 oracle |
| Z-Wave JS Server | 3.10.1 | Apache-2.0 | managed Z-Wave sidecar |
| Z-Wave JS | 15.31.0 | MIT | underlying Z-Wave stack |

## Common obligations

Before redistribution:

- preserve applicable copyright notices;
- preserve LICENSE files;
- preserve NOTICE obligations where applicable;
- build SBOM;
- review transitive dependencies;
- record exact shipped package/image/runtime versions;
- review container/base-image licenses;
- review Node runtime distribution terms;
- do not strip upstream attribution.

No selected primary stack introduces a known copyleft requirement into EliteSCADA proprietary product code based on the current research versions.

This conclusion applies only to the exact identified components, not every transitive package.

---

# 3. Matter specification status

Current public Connectivity Standards Alliance download page, accessed 2026-10-06, lists:

**Matter 1.6.1**

including:

- Core Specification;
- Application Clusters Specification;
- Device Type Library;
- Standard Namespace.

Current preferred stable Matter(.js) Server research baseline states Matter 1.6.0 support.

Therefore the first implementation must explicitly pin the feature/specification window.

Do not claim full Matter 1.6.1 implementation merely because the public specification exists.

---

# 4. Matter certification program

Current CSA certification material states that successful certification allows products to be recognized as Certified Products and use the associated certified-product logo.

Current certification programs include:

- End Products;
- Compliant Platforms;
- Software Components.

A Software Component may be:

- a User Interface Component/application; or
- an underlying software component/library.

Software Components operate against defined Supported Operating Environments.

This is directly relevant because EliteSCADA Matter architecture is software-controller oriented.

---

# 5. Current evidence that Matter controller software can be certified

The current CSA Certified Product directory contains controller Software Components.

Examples observed 2026-10-06:

## 1Home MController

- Product Type: Software Component;
- Certified: 2026-01-27;
- Certificate ID: CSA26005SWC60243-M2;
- Specification Version: 1.4.

## LG ThinQ Local Controller

- Product Type: Software Component;
- Certified: 2026-05-26;
- Certificate ID: CSA26023SWC60261-M2;
- Specification Version: 1.5.

## DIRIGERA Matter Controller

- Product Type: Software Component;
- Certified: 2026-07-09;
- Certificate ID: CSA26028SWC60266-M2;
- Specification Version: 1.4.2.

## Tethral

- Android Matter controller;
- Product Type: Software Component;
- Certified: 2026-08-10;
- Certificate ID: CSA26029SWC60267-M3;
- Specification Version: 1.5.1.

Conclusion:

A software-controller certification path is real, not theoretical.

This does **not** determine which exact certification category EliteSCADA must use.

---

# 6. Matter Software Component / application registration distinction

Current CSA trademark guidance defines:

- Certified Software Component;
- Supported Operating Environment;
- Registration/Registered for certain Applications or Containers using a Certified Software Component after a declaration process.

This creates multiple possible commercial paths:

1. certify EliteSCADA controller functionality as a Software Component;
2. consume a certified Software Component and pursue applicable application/container registration;
3. ship interoperable Matter functionality without certified/logo claims where legally permissible and commercially acceptable.

The selected current Matter(.js) Server successor is not currently presented upstream as re-certified.

Therefore EliteSCADA cannot assume path 2 merely by using it.

**MAIN_DECISION_REQUIRED / LEGAL_REVIEW_REQUIRED**

Select certification strategy after:

- product topology;
- supported OS;
- sidecar packaging;
- commercial regions;
- branding intent

are frozen.

---

# 7. Matter certification application artifacts

Current CSA Certification Tool guidance lists application material including:

- Declaration of Conformity;
- PICS;
- final test report from the test provider;
- Network Transport Attestation for Matter applications;
- Security Attestation for Matter applications.

Matter certification also results in product records/declarations in Alliance systems including the Distributed Compliance Ledger for applicable certified products.

Do not wait until final release to discover these requirements.

If certified product scope is selected, create a certification workstream before late-stage UI polish.

---

# 8. Matter product attestation

Product Attestation Authority infrastructure concerns Matter devices/end products and device attestation credentials.

The CSA maintains current PAA provider information.

EliteSCADA as commissioner/controller must correctly validate device attestation according to selected stack/product policy.

Do not:

- operate production defaults in test-DCL bypass mode;
- silently trust arbitrary DACs;
- claim device certification based only on successful commissioning.

EliteSCADA does not become a PAA merely by commissioning devices.

If EliteSCADA ever manufactures Matter end devices, that would be a separate PKI/PAA legal/operational program.

---

# 9. Matter trademarks and logos

Current CSA trademark/logo guidelines govern:

- certified-product marks;
- product materials;
- marketing materials;
- software component terminology;
- registered application/container terminology.

Rules are independent from Apache-2.0 code rights.

Before using:

- Matter logo;
- Certified Product marks;
- "Matter Certified";
- wording that implies CSA certification;

obtain the appropriate certification/registration/permission.

Use neutral engineering wording until approved:

- "Matter integration";
- "Matter controller integration under development";
- "interoperability tested with..."

Avoid "certified" unless there is an actual certification record covering the shipped product/configuration.

---

# 10. Matter selected-stack maturity and certification boundary

Preferred research stack:

**Matter(.js) Server 1.4.0 + matter.js 0.17.9**

Current upstream research states:

- successor server is Beta/testing;
- not yet officially re-certified by CSA;
- re-certification is intended later.

This is a production/certification gate, not an open-source license gate.

Development may proceed.

Public certified-product marketing must not infer certification from the predecessor Python Matter Server's historical certification.

Certification does not automatically transfer to a successor implementation.

---

# 11. Matter distribution/SBOM gate

Before shipping Matter sidecar:

1. freeze exact server package/image;
2. freeze exact matter.js dependency graph;
3. freeze Node runtime;
4. capture license texts;
5. capture notices;
6. generate SBOM;
7. scan vulnerabilities;
8. verify npm/source license metadata discrepancy;
9. record supported OS/architecture;
10. record whether artifacts are redistributed or downloaded separately.

The current Matter Server npm metadata discrepancy observed in Checkpoint 1 should be resolved by exact-artifact legal inspection before release.

Do not rely on a package sidebar alone.

---

# 12. Z-Wave specification status

Current Z-Wave Alliance developer page, accessed 2026-10-06, identifies:

**2026A**

as the in-force technical specification package.

The Alliance states:

- specifications are updated on a twice-yearly cadence;
- a new released specification becomes certifiable after Certification Portal/Compliance Test Tool implementation;
- current in-force package should be used for new development.

Future EliteSCADA certification work must pin:

- specification revision;
- CTT version;
- certification program revision.

---

# 13. Z-Wave certification structure

Current Z-Wave Alliance certification process requires two mandatory components:

1. Technical Certification;
2. Market Certification.

Both must pass before a certification number is issued and product certification badges can be used.

Current process also includes:

- Alliance membership;
- Certification Portal;
- Compliance Test Tool;
- independent testing;
- market/logo/documentation review.

The Alliance's 2026 certification material states the program has been updated for the 2026A specification package.

---

# 14. Z-Wave membership and mark use

Current Alliance certification guidance says:

- companies submitting products for certification must join at Manufacturer level or higher;
- Brander membership may brand/adopt certified products but cannot submit its own new product certification;
- certification marks/logo use is tied to Alliance membership/licensing and certification rules.

Exact current commercial membership terms must be confirmed directly with the Alliance before budgeting.

Do not infer logo rights from:

- Z-Wave JS MIT license;
- Z-Wave JS Server Apache-2.0 license;
- ownership of a certified USB stick;
- successful interoperability testing.

---

# 15. Exact Z-Wave product classification remains unresolved

EliteSCADA proposed topology:

**EliteSCADA software -> Z-Wave JS Server -> third-party certified USB controller -> Z-Wave network**

Open legal/certification question:

Does the shipped EliteSCADA software/controller integration itself require certification as:

- Gateway Controller;
- controller software/stack;
- another product category;
- or may it use a separately certified controller without EliteSCADA certification if no certification marks/product claims are made?

Current public sources prove that:

- controllers/gateways are certified product categories;
- product certification is required for products marketed under Z-Wave certification marks;
- Alliance membership/certification infrastructure exists.

Current public research does **not** establish the exact classification of the EliteSCADA software-plus-external-controller architecture.

Therefore:

**LEGAL_REVIEW_REQUIRED / Z-WAVE ALLIANCE CLASSIFICATION REQUIRED**

Ask Z-Wave Alliance Certification team before commercial claim/release.

Do not guess.

---

# 16. Z-Wave product catalog evidence

Current Certified Product Guide includes categories such as:

- All Controllers;
- Computer Controller Interfaces;
- Gateway Controller.

This supports the conclusion that controller/gateway implementations are normal certification subjects.

It does not automatically classify EliteSCADA.

If the final product uses one supported third-party USB controller, that controller's own certification remains separate from EliteSCADA software behavior.

---

# 17. Z-Wave logo/market certification

Current market certification covers branding/materials including:

- product information;
- manuals;
- product/package marks;
- Z-Wave terminology;
- SmartStart wording;
- S2 DSK documentation;
- association groups / Command Classes where applicable;
- correct badges/logos.

Therefore marketing/documentation is part of certification scope, not merely RF/protocol testing.

Before using:

- Z-Wave logo;
- Z-Wave Plus;
- Z-Wave Long Range badges;
- "Z-Wave Certified";

obtain explicit right under current Alliance rules.

Neutral pre-certification wording:

- "Z-Wave JS integration";
- "works with tested Z-Wave controller/device fixtures";
- "Z-Wave support under qualification".

---

# 18. Z-Wave Open Source Specification vs certification rights

The Z-Wave Alliance has opened the specification and publishes current technical specification material.

Open specification access does not mean:

- unrestricted certification-mark use;
- automatic commercial product certification;
- automatic Alliance membership;
- automatic RF regulatory approval.

Likewise, permissive open-source Z-Wave JS licensing does not waive Alliance branding rules.

Keep these legal tracks separate.

---

# 19. RF regulatory compliance is separate from Z-Wave certification

Z-Wave uses region-specific RF bands.

Current Silicon Labs global regions table lists Brazil:

- regulator reference: ANATEL;
- 919.8 MHz / 921.4 MHz;
- ANZ region family.

This table is engineering planning input.

It is **not** a Brazilian product homologation certificate.

Before transmitting or shipping:

- verify exact SKU;
- verify current ANATEL rule;
- verify device/controller homologation status;
- verify importer/manufacturer obligations;
- verify electrical certification where applicable.

A Z-Wave Alliance certification does not replace ANATEL compliance.

---

# 20. Matter regional regulatory compliance

Matter itself is IP-based, but physical radios still use regulated technologies:

- Wi-Fi;
- Bluetooth;
- Thread / IEEE 802.15.4.

A Matter certification does not replace:

- ANATEL;
- FCC;
- CE/RED;
- local electrical safety;
- EMC;
- other jurisdiction requirements.

For EliteSCADA software-only distribution:

- the third-party device/controller vendors normally own their radio certification;
- EliteSCADA must avoid representing an imported/lab SKU as legal for a market without checking.

If EliteSCADA later ships bundled hardware, regulatory responsibility changes materially and needs separate review.

---

# 21. Brazil lab legal posture

Research lab should distinguish:

## Software research

No radio-specific product claim.

## Existing legally acquired third-party equipment

Use within applicable local rules.

## Import of region-specific Z-Wave hardware

Verify:

- frequency;
- SKU;
- ANATEL status;
- importer rules.

## Matter Wi-Fi plug

Tapo P125M Brazil page currently lists 100-125 V.

Do not use it on 220 V solely because the model is sold on a Brazilian website.

Electrical constraints are separate from protocol certification.

---

# 22. Certification does not equal universal compatibility

Even after certification:

- vendor optional features vary;
- firmware bugs exist;
- cluster/Command Class optionality exists;
- region differs;
- sleeping-device behavior differs.

EliteSCADA should still maintain a tested compatibility matrix.

Do not market:

- "supports every Matter device";
- "supports every Z-Wave device";

without evidence.

Better:

- protocol support statement;
- certified/tested stack version;
- explicit qualified-device list;
- documented unsupported features.

---

# 23. Commercial release paths

## Matter Path A — interoperability first, no certification claim

- ship integration after engineering/security/legal approval;
- no Matter certification logo;
- neutral compatibility wording;
- pursue certification later.

Legal team must confirm this is acceptable under applicable agreements/branding.

## Matter Path B — certify EliteSCADA software controller

- Alliance membership;
- select SWC/UIC/SOE scope;
- PICS;
- test provider;
- transport/security attestations;
- certification application;
- logo/marketing after approval.

## Matter Path C — consume certified component

Only if the selected future component is certified and the EliteSCADA topology qualifies for applicable registration/transfer rules.

Current preferred Matter(.js) successor does not provide this shortcut today.

---

# 24. Z-Wave commercial release paths

## Z-Wave Path A — interoperability first, no Z-Wave marks

Potential path subject to Alliance/legal confirmation.

- supported certified USB controller;
- Z-Wave JS;
- no certification badge claim;
- exact tested compatibility matrix.

## Z-Wave Path B — certify EliteSCADA controller/gateway product

- appropriate Alliance membership;
- exact product category confirmed;
- current in-force spec;
- CTT;
- technical certification;
- market certification;
- logo use after approval.

## Z-Wave Path C — certification/adoption model

If Alliance rules allow a certified controller/software component to be adopted/rebranded for this architecture, evaluate after formal classification.

Do not assume this path exists for EliteSCADA without written confirmation.

---

# 25. Dependency redistribution matrix

## Redistributed in EliteSCADA bundle

If bundled:

- include exact license texts;
- include notices;
- record package versions;
- SBOM;
- vulnerability scan;
- source-offer obligations if any transitive component requires them.

## Downloaded/installed by deployment

Still review:

- trust/source;
- pinned hashes;
- update policy;
- license acceptance;
- offline installation;
- reproducibility.

Do not bypass license review by moving a dependency into an installer script.

---

# 26. Trademark-safe documentation rules

Until certification status is approved:

Do:

- spell protocol names correctly;
- identify upstream projects accurately;
- state "integration with";
- state exact tested versions;
- state tested devices.

Do not:

- reproduce certification logos without authorization;
- call EliteSCADA Matter Certified;
- call EliteSCADA Z-Wave Certified;
- imply Alliance endorsement;
- imply all devices are supported.

---

# 27. Required legal artifacts before commercial release

## Common

- SBOM;
- third-party license inventory;
- notices;
- vulnerability report;
- export/distribution review if applicable;
- marketing wording review;
- installer/container distribution terms.

## Matter

- selected certification strategy;
- CSA membership status if certifying;
- PICS;
- test provider;
- Network Transport Attestation;
- Security Attestation;
- certification/registration record;
- DCL/certification declaration handling;
- approved logo usage.

## Z-Wave

- Alliance product classification;
- membership level;
- certification case;
- current in-force spec;
- CTT evidence;
- independent test report;
- market certification;
- approved badge/logo use;
- regional RF compliance.

---

# 28. Recommended timing

Do not block early engineering on completion of formal certification.

Recommended sequence:

1. architecture prototype;
2. security/lifecycle L0-L3;
3. product/legal classification;
4. lab L4;
5. certification preparation;
6. commercial certification/registration where chosen;
7. public certified marketing only after approval.

Do not defer legal classification until after installer/marketing is frozen.

---

# 29. Legal gates

## LEGAL-GATE-01 — Matter certification strategy

Open.

Main/Product must choose:

- no certified claim initially;
- certify EliteSCADA SWC/UIC;
- certified-component/registration strategy.

## LEGAL-GATE-02 — Matter trademark/logo

Open until Alliance-authorized status.

## LEGAL-GATE-03 — Matter package license/SBOM

Required before redistribution.

Especially resolve Matter Server package/source metadata consistency.

## LEGAL-GATE-04 — Z-Wave product classification

Open.

Direct Z-Wave Alliance confirmation required for software + external controller architecture.

## LEGAL-GATE-05 — Z-Wave certification/market marks

Open until certification/licensing path approved.

## LEGAL-GATE-06 — Brazil Z-Wave RF

Open for every controller/device SKU intended for Brazil.

Frequency-family evidence is not homologation.

## LEGAL-GATE-07 — bundled hardware regulatory obligations

Open only if EliteSCADA ships hardware.

Software-only integration has a narrower compliance boundary.

---

# 30. Current decisions safe for engineering

Engineering may safely assume:

- primary upstream stack licenses are permissive;
- Matter certification is separate from source license;
- Z-Wave certification is separate from source license;
- logos/marks require separate authorization;
- current CSA has Software Component certification paths;
- current Z-Wave Alliance has controller/gateway certification categories;
- regional radio law remains separate;
- final certification scope remains a Product/Legal decision.

Engineering must not assume:

- EliteSCADA is certified;
- using certified devices certifies the controller;
- using a certified USB stick certifies EliteSCADA;
- using open-source specs grants trademark rights.

---

# 31. Current official source ledger

Accessed/revalidated **2026-10-06**.

## Connectivity Standards Alliance

Matter 1.6.1 specification downloads:
https://csa-iot.org/developer-resource/specifications-download-request/

Certification programs:
https://csa-iot.org/certification/why-certify/

Certification Tool / required application artifacts:
https://csa-iot.org/certification/tools/certification-tool/

PAA providers:
https://csa-iot.org/certification/paa/

Trademark / Brand / Logo Usage Guidelines, November 2025:
https://csa-iot.org/wp-content/uploads/2022/11/TM_Logo-Use-Guide_Update_November-2025.pdf

Certified controller software examples:
https://csa-iot.org/csa_product/1home-mcontroller/
https://csa-iot.org/csa_product/lg-thinq-local-controller/
https://csa-iot.org/csa_product/dirigera-2/
https://csa-iot.org/csa_product/tethral/

## Z-Wave Alliance

Certification process:
https://z-wavealliance.org/development-process-overview-2/

Current specification:
https://z-wavealliance.org/development-resources-overview/specification-for-developers/

Certification news / 2026A:
https://z-wavealliance.org/zwa-certification-news/

Certification Portal:
https://certification.z-wavealliance.org/

Membership FAQ:
https://z-wavealliance.org/membership-faqs/

Certified Product Guide:
https://products.z-wavealliance.org/

## RF region

Silicon Labs global regions:
https://www.silabs.com/wireless/z-wave/global-regions

## Open-source components

https://github.com/matter-js/matterjs-server
https://github.com/matter-js/matter.js
https://github.com/project-chip/connectedhomeip
https://github.com/zwave-js/zwave-js-server
https://github.com/zwave-js/zwave-js

---

# 32. Checkpoint 3 legal conclusion

Both protocols remain viable for engineering.

Neither open-source license grants certification or trademark rights.

Matter has a clear current Software Component certification ecosystem, including certified controller software, but the selected preferred Matter(.js) successor is not currently re-certified.

Z-Wave has a mature current certification program and certified controller/gateway categories, but the exact legal classification of EliteSCADA software operating through a third-party USB controller remains unresolved and must be confirmed directly with the Z-Wave Alliance before commercial Z-Wave product claims.

Brazil RF/electrical compliance remains independent from protocol certification.

---

**LEGAL_REVIEW_REQUIRED**

**NOT LEGAL ADVICE**

**DOCS_ONLY**

**NO PRODUCT CODE CHANGED**

**NO CERTIFICATION CLAIM MADE**

**NO LOGO RIGHTS CLAIMED**

**NO MERGE PERFORMED**
