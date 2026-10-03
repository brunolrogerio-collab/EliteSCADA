# Tier B Survey

Research date: **2026-10-02**

This is deliberately bounded. It is not a vendor catalog.

## KNX/IP

**Local or cloud:** Local. KNXnet/IP tunneling/routing uses IP, commonly UDP/TCP with port 3671.  
**Open/proprietary:** Standardized KNX ecosystem; specifications/certification/trademark are controlled by KNX Association.  
**API/library:** \`XKNX/xknx\` is a mature Python KNX library; current 2026 activity includes KNX IP Secure and device-management work.  
**Library license:** MIT.  
**Account:** No cloud account required for runtime. ETS/project access is normally part of professional commissioning.  
**Subscription/cost:** No cloud subscription inherently required; ETS and KNX professional/certification ecosystem are commercial considerations.  
**Hardware:** KNX/IP interface/router for TP installations; pure KNX/IP devices need only network reachability.  
**Difficulty:** Medium-high. DPT typing, group addresses, ETS project import, Secure keyring handling and routing/tunneling need deliberate design.  
**Market relevance:** High in professional building automation and directly useful as a DALI gateway path.  
**Tier A recommendation:** **YES. Promote after #472 foundation**, likely before or alongside DALI gateway implementation.

Security:
- support KNX IP Secure where the installation uses it;
- KNX Association documents secure tunneling credentials/keyring behavior;
- do not downgrade a secure project silently.

Potential first architecture:
\`EliteSCADA -> KNX/IP built-in or permissive sidecar -> KNX/IP Interface/Router -> KNX TP/IP\`

Candidate implementation choices:
- native .NET KNXnet/IP client if scope is intentionally bounded;
- small XKNX sidecar (MIT) if project-import/security breadth justifies Python.

Sources:
- https://support.knx.org/hc/en-us/articles/360012026220-Interfaces
- https://support.knx.org/hc/en-us/articles/360000653399-Secure-Tunneling
- https://support.knx.org/hc/en-us/articles/360012630199-KNX-Security-overview
- https://github.com/XKNX/xknx
- https://github.com/XKNX/xknx/blob/main/LICENSE

## Tuya Cloud

**Local or cloud:** Cloud for the surveyed official Cloud Development APIs.  
**Open/proprietary:** Proprietary cloud platform/API.  
**API/library:** Official Tuya Cloud Service APIs / IoT Core.  
**License:** Vendor terms; not an open-source protocol stack.  
**Account:** Yes; cloud developer/project authorization is required.  
**Subscription/cost:** Cloud services are subscription/plan based; exact current plan/pricing must be revalidated during productization.  
**Hardware:** No EliteSCADA host radio if using cloud; devices/gateways remain Tuya ecosystem hardware.  
**Difficulty:** Medium technically, higher commercially/operationally because of account, region, API authorization and cloud dependency.  
**Market relevance:** High consumer-device breadth.  
**Tier A recommendation:** **MAYBE, but not before local integrations.** Promote only if Product Owner accepts cloud/account dependence and current commercial terms.

Recommended scope if promoted:
- cloud project connection;
- device inventory;
- standardized DP/property mapping;
- selected import;
- commands;
- cloud availability/rate-limit diagnostics;
- no claim of offline/local operation.

Risks:
- cloud latency/outage;
- API product/plan changes;
- region/account lifecycle;
- DP semantics vary by product.

Sources:
- https://developer.tuya.com/en/docs/cloud
- https://developer.tuya.com/en/docs/cloud/device-connection-service

## Intelbras GDI

**Local or cloud:** Cloud.  
**Open/proprietary:** Proprietary Intelbras API platform.  
**API/library:** Official HTTP/JSON GDI platform.  
**License:** Vendor commercial/API terms.  
**Account:** Yes; current manual states company registration with valid **CNPJ**.  
**Subscription/cost:** Yes; current manual describes paid API (IoT) and Video plans with monthly quotas. Exact pricing/quotas must be revalidated before implementation.  
**Hardware:** Intelbras cloud-connected supported devices; no EliteSCADA host radio for cloud API path.  
**Difficulty:** Medium technically; commercial/account and product-specific semantics are the major dependencies.  
**Market relevance:** High specifically for the Brazilian residential/security market.  
**Tier A recommendation:** **YES if Brazil-focused cloud integration is a product priority; otherwise keep Tier B.**

Current official GDI manual describes:
- base cloud API;
- Bearer token authorization;
- device control/status;
- account/company onboarding;
- API request quotas;
- optional video streaming plans.

First EliteSCADA scope should exclude video streaming unless a separate security/video product contract is approved. Focus on IoT status/control and canonical capabilities.

Sources:
- https://app-mibo.intelbras.com.br/manual-gdi.html
- official GDI base/API portal linked by that manual.

## Bluetooth / BTHome

**Local or cloud:** Local broadcast BLE.  
**Open/proprietary:** BTHome is a published BLE advertisement format.  
**API/library:** No sidecar is inherently required; parse BLE advertisements directly after F4 BluetoothAdapter exists.  
**License:** Protocol/UUID use is publicly documented; no runtime library dependency is required for an independent parser. Trademark/spec wording should be revalidated before public branding.  
**Account:** No.  
**Subscription/cost:** No.  
**Hardware:** Host Bluetooth adapter or remote BLE proxy/gateway.  
**Difficulty:** Low-medium for receive-only sensors; higher for portable cross-platform scanning and encrypted bindkey management.  
**Market relevance:** Growing, inexpensive sensor ecosystem.  
**Tier A recommendation:** **YES for a bounded receive-first sensor integration after F4 BluetoothAdapter.**

BTHome v2:
- service UUID 0xFCD2;
- compact typed measurements in BLE advertisements;
- optional packet id for dedupe;
- optional AES-CCM encryption with 16-byte pre-shared key.

Security warning from BTHome documentation: encrypted BLE advertisements do not by themselves make the data appropriate for safety-critical actions. First EliteSCADA scope should be **sensor receive only**, not locks/access control.

Potential capability mapping:
- temperature/humidity/illuminance;
- contact/motion;
- battery;
- power/energy where encoded;
- button events.

Sources:
- https://bthome.io/format/
- https://bthome.io/encryption/

## Other candidate considered: BACnet gateway reuse

BACnet is already an industrial/building protocol concern rather than a new home-driver family. For DALI gateway research, reuse an existing/future BACnet driver instead of creating a separate Tier A “home” dossier.

## Summary

| Candidate | Local | Account/subscription | Hardware | Difficulty | Future Tier A |
|---|---|---|---|---|---|
| KNX/IP | Yes | No cloud account | KNX/IP interface/router | Med-High | Yes |
| Tuya Cloud | No | Yes | ecosystem devices/gateway | Medium + commercial | Conditional |
| Intelbras GDI | No | Yes, current manual requires company/CNPJ | ecosystem devices | Medium + commercial | Conditional/BR priority |
| BTHome | Yes | No | Bluetooth adapter | Low-Med | Yes |
