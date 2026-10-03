# Driver License / Commercial Distribution Matrix

Research date: **2026-10-02**

This is engineering research, not legal advice. \`LEGAL_REVIEW_REQUIRED\` means a commercial/legal decision must be reviewed before shipping or marketing.

| Target | Stack / dependency | License / owner | Redistribution | Source disclosure | Notices | Sidecar? | External account / subscription | Certification / trademark | Can EliteSCADA installer ship it? | Legal review |
|---|---|---|---|---|---|---|---|---|---|---|
| Native Zigbee | zigbee-herdsman | MIT; upstream authors | Yes under MIT terms | No copyleft disclosure | Preserve MIT notice | Recommended for process isolation/stable contract | No | CSA Zigbee certification separate | Yes, if notices + dependency policy | For Zigbee certification/logo claims |
| Native Zigbee quirks | zigbee-herdsman-converters | MIT | Yes | No | Preserve MIT notice | Part of Zigbee service | No | Device compatibility claims need L4 evidence | Yes, preferably pinned/curated | For branding/certification |
| Zigbee2MQTT Bridge | Zigbee2MQTT 2.14.2 | GPL-3.0 | Yes, with GPL obligations | **Yes for distributed covered work/modifications** | GPL/license/source compliance | **Yes / separate process** | Broker/Z2M deployment dependent | Zigbee certification separate | Prefer user-managed v1; bundled only after compliance plan | **YES** for any bundled distribution |
| DALI gateway | vendor gateway/API | Vendor-specific | Hardware/user-provided; API maps vary | N/A | Vendor-specific | No | Usually no cloud | DALI-2 certification/logo belongs to certified product | Ship profiles/docs, not vendor firmware unless licensed | **YES** for DALI claims/vendor SDK |
| Native DALI | selected interface/stack TBD | TBD + DALI Alliance/IEC rights | TBD | TBD | TBD | Hardware dependent | No | DALI-2 certification separate | Not yet decided | **YES** |
| Home Assistant Bridge | no HA runtime dependency; HA Core reference | HA Core Apache-2.0 | No HA redistribution required | No | N/A unless copying code | No | User-managed HA | Home Assistant marks/logos separate | Yes, built-in client only | Branding/logo review if used |
| Matter Controller | matterjs-server | Apache-2.0; Open Home Foundation | Yes | No copyleft disclosure | LICENSE/NOTICE where applicable | **Yes** | No cloud account | CSA Matter certification separate | Yes when maturity approved | **YES** for Matter Certified/logo claims |
| Matter implementation | matter.js | Apache-2.0 | Yes | No | LICENSE/NOTICE | Inside sidecar | No | CSA certification separate | Yes | Claims/certification |
| Matter reference | connectedhomeip | Apache-2.0 | Yes | No | LICENSE/NOTICE | Test/reference | No | CSA certification separate | Possible but not preferred runtime | Claims/certification |
| ESPHome Native | first-party protocol client | Own code; official public protocol definitions | Yes | N/A | Generated/protobuf notices as applicable | No | No | No known protocol certification claim needed | Yes | Normal dependency/IP review |
| ESPHome reference | aioesphomeapi | MIT | Yes | No | MIT notice if redistributed | No runtime need | No | N/A | Not needed in product runtime | Low |
| ESPHome firmware/runtime code | ESPHome repo | Mixed: Python/other MIT; C++ runtime GPLv3 per repo LICENSE | Redistribution depends on portion | GPL for covered runtime C++ | Required | Avoid embedding | No | N/A | Do not copy runtime C++ into proprietary core | **YES if redistributed** |
| Shelly RPC | public vendor JSON-RPC API | Vendor API/docs | No vendor code required | N/A | N/A | No | No cloud for local RPC | Shelly brand/trademark separate | Yes, first-party client | Branding/SDK only |
| Z-Wave JS Bridge | zwave-js-server 3.10.1 | Apache-2.0 | Yes | No | LICENSE/NOTICE | **Yes** | No | Z-Wave Alliance certification separate | Yes after sidecar/F4 | **YES** for certified/logo claims |
| Z-Wave stack | zwave-js | MIT | Yes | No | MIT notice | Via server | No | Z-Wave certification separate | Yes | Claims/certification |
| KNX/IP Tier B | XKNX | MIT | Yes | No | MIT notice | Optional | No cloud | KNX certification/trademark separate | Yes if chosen | For KNX certification/ETS/IP claims |
| Tuya Tier B | official Cloud APIs | Proprietary vendor terms | API client only | N/A | Vendor terms | Cloud | **Yes / plan dependent** | Vendor branding | Built-in connector possible under commercial terms | **YES** |
| Intelbras GDI Tier B | official GDI HTTP API | Proprietary vendor terms | API client only | N/A | Vendor terms | Cloud | **Yes / paid plan** | Vendor branding | Built-in connector possible under agreement/terms | **YES** |
| BTHome Tier B | independent parser | public protocol format | No runtime dependency | N/A | N/A | No | No | Branding/spec terms should be checked | Yes | Low/branding |

## Notes

### MIT / Apache-2.0

These are permissive open-source licenses, but redistribution still requires their license/copyright/NOTICE obligations where applicable. Dependency manifests/SBOM should record exact versions.

### GPL-3.0

Zigbee2MQTT is the major copyleft boundary in this plan. The preferred v1 product relationship is a documented MQTT integration with a **user-managed** Zigbee2MQTT service.

If EliteSCADA ships a Z2M container/binary:
- ship the GPL license;
- preserve notices;
- satisfy corresponding-source obligations for the shipped covered work;
- provide source for distributed modifications;
- keep proprietary core and GPL component as separate programs communicating over MQTT;
- obtain legal review of the final distribution layout.

### Certification is separate from open-source licensing

MIT/Apache permission to ship a stack does not grant:
- Zigbee Certified;
- Matter Certified;
- DALI-2 Certified;
- Z-Wave Certified;
- KNX Certified

marketing rights. Those programs are controlled by their respective alliances/organizations and may require membership, testing, applications and logo guidelines.

## Official license evidence checked

Accessed 2026-10-02:
- Koenkk/zigbee-herdsman LICENSE — MIT.
- Koenkk/zigbee-herdsman-converters LICENSE — MIT.
- Koenkk/zigbee2mqtt LICENSE — GPL-3.0.
- matter-js/matter.js LICENSE — Apache-2.0.
- matter-js/matterjs-server LICENSE — Apache-2.0.
- project-chip/connectedhomeip LICENSE — Apache-2.0.
- esphome/aioesphomeapi LICENSE — MIT.
- esphome/esphome LICENSE — mixed MIT/GPLv3 statement.
- zwave-js/zwave-js LICENSE — MIT.
- zwave-js/zwave-js-server LICENSE — Apache-2.0.
- home-assistant/core LICENSE.md — Apache-2.0.
- XKNX/xknx LICENSE — MIT.
