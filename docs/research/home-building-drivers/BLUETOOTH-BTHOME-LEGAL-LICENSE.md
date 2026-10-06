# BLUETOOTH / BTHOME LEGAL AND LICENSE RESEARCH

Status: CHECKPOINT 3
Lane: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER
Contract: C-HOME-BLUETOOTH-BTHOME-RESEARCH-01
Owner issue: #545
Access date: 2026-10-06

## 1. Purpose

This document records legal/license facts and review gates for the preferred native BTHome architecture:

PREFERRED_V1_ARCHITECTURE =
managed local Bluetooth sidecar

Linux:
BlueZ D-Bus

Windows:
Windows Runtime Bluetooth advertisement APIs

Protocol:
BTHome v2

This is engineering research, not legal advice.

Where a commercial-distribution interpretation is not explicit from the primary source:

LEGAL_REVIEW_REQUIRED

is recorded rather than guessed.

## 2. BTHome UUID / service-name license

The official BTHome v2 format page links a license statement for the BTHome service identifier.

Primary license statement:

https://bthome.io/images/License_Statement_-_BTHOME.pdf

The statement is dated 2022-10-05 and grants a perpetual and irrevocable license to anyone to use:

- Bluetooth service identifier UUID 0xFCD2;
- custom service name BTHome;

in software and products only to implement the BTHome protocol as defined at bthome.io.

Engineering conclusion:

BTHOME_UUID_0xFCD2_USE = PERMITTED_FOR_BTHOME_IMPLEMENTATION

BTHOME_NAME_USE = PERMITTED_FOR_BTHOME_IMPLEMENTATION

This is strong direct evidence that EliteSCADA may recognize/use the assigned BTHome UUID and refer to the BTHome service in an implementation.

## 3. Scope caution: protocol name versus branding

The license statement explicitly covers the UUID and Custom Service Name "BTHome" for implementing the BTHome protocol.

It does not, by itself, explicitly grant:

- arbitrary BTHome logo use;
- certification claims;
- "official BTHome" endorsement claims;
- ownership of third-party artwork;
- broad copying of the specification website content into EliteSCADA documentation.

Therefore future product wording should prefer factual compatibility language such as:

- "BTHome v2";
- "BTHome v2 receiver";
- "compatible with BTHome v2 advertisements";

and avoid:

- "BTHome Certified";
- "Official BTHome";
- use of logos/marks not explicitly licensed.

Record:

LEGAL_REVIEW_REQUIRED — BTHOME-BRANDING

before packaging/marketing assets use any BTHome logo or endorsement-style wording.

## 4. BTHome specification/document content

The public BTHome website provides the protocol format required for implementation.

The UUID/name license statement does not explicitly state a general copyright license for reproducing all specification prose/tables/artwork.

Engineering rule:

- implement protocol facts;
- keep source URLs and version/access dates;
- do not copy substantial specification prose/tables/images into proprietary product assets;
- write independent EliteSCADA documentation;
- quote only minimal material when needed.

If product legal wants to redistribute substantial BTHome documentation content:

LEGAL_REVIEW_REQUIRED — BTHOME-SPEC-CONTENT-REDISTRIBUTION

## 5. bthome-ble reference implementation

Repository:

https://github.com/Bluetooth-Devices/bthome-ble

Current observed release:

v3.24.0
2026-07-21

License:

MIT

Checkpoint role:

- independent reference implementation;
- L2 oracle;
- regression/reference evidence.

Decision:

BTHOME_BLE_PYTHON = NOT_SELECTED_PRODUCT_RUNTIME_DEPENDENCY

Therefore the preferred architecture does not need to redistribute this Python library.

If future DEV copies or bundles code from bthome-ble:

- comply with MIT notice/copyright requirements;
- review transitive dependencies separately;
- do not assume bthome-ble behavior overrides the normative BTHome specification.

## 6. Tmds.DBus.Protocol candidate

Candidate Linux .NET dependency:

Tmds.DBus.Protocol

Current observed package:

0.95.1
updated 2026-09-04

License:

MIT

Sources:

https://www.nuget.org/packages/Tmds.DBus.Protocol/0.95.1
https://github.com/tmds/Tmds.DBus

Engineering conclusion:

LICENSE_PROFILE = PERMISSIVE_CANDIDATE

If selected:
- preserve MIT notices as required;
- run normal dependency/SBOM/vulnerability review;
- pin exact approved version;
- verify transitive packages;
- revalidate license at implementation time.

No dependency was added by this research.

## 7. BlueZ

Repository:

https://github.com/bluez/bluez

Current observed release/configuration version:

5.87

The repository exposes mixed licensing, including GPL-2.0 and LGPL-2.1 licensed components/files.

The exact license depends on the BlueZ component/source file.

Therefore it is incorrect to summarize all BlueZ code as one permissive library.

### 7.1 Preferred architecture avoids linking/copying BlueZ code

Preferred Linux design:

EliteSCADA sidecar
-> D-Bus protocol
-> host-installed org.bluez / bluetoothd

The product does not need to:

- copy BlueZ source;
- statically link BlueZ libraries;
- embed bluetoothd;
- fork BlueZ;
- redistribute BlueZ code as part of the sidecar.

This IPC/process boundary is intentionally favorable from both architecture and license perspectives.

### 7.2 D-Bus API use

The preferred implementation consumes the public D-Bus API exposed by a separately installed/running BlueZ service.

Engineering conclusion:

BLUEZ_DISTRIBUTION_BOUNDARY =
HOST_OS_SERVICE / IPC

not:

LINKED_PRODUCT_LIBRARY

### 7.3 Distribution gate

If the EliteSCADA installer/container image later bundles:

- bluetoothd;
- BlueZ utilities;
- BlueZ libraries;
- copied BlueZ source;

then:

LEGAL_REVIEW_REQUIRED — BLUEZ-REDISTRIBUTION

before shipping.

Review must determine exact applicable licenses/notices/source obligations for the specific files/binaries distributed.

### 7.4 Raw HCI

Raw HCI does not eliminate Bluetooth licensing/compliance questions and increases product complexity.

It is not selected merely to avoid BlueZ licensing.

## 8. Windows Runtime / Windows SDK

Preferred Windows backend uses OS/SDK APIs including:

Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher

Microsoft documents WinRT use from desktop applications and provides Windows SDK metadata/build support for .NET desktop apps.

Current Windows SDK documentation observed:

- Windows SDK 10.0.28000 is current for Windows 11 development;
- Windows SDK contains WinRT metadata, headers, libraries and build tools;
- .NET 6+ desktop apps can reference Windows SDK targeting through the Windows-specific Target Framework Moniker.

Engineering architecture:

- call the OS-provided API;
- do not redistribute the Windows Bluetooth stack;
- do not ship private/undocumented Windows Bluetooth components.

No separate third-party Bluetooth runtime is selected for Windows.

### 8.1 Product distribution

Normal Windows SDK / .NET build and deployment license compliance still applies.

If future packaging bundles Microsoft redistributables or Windows App SDK runtime packages, use Microsoft's documented redistributable mechanism and license terms for the exact component/version.

Record:

LEGAL_REVIEW_REQUIRED — WINDOWS-REDISTRIBUTABLES

only if the Bluetooth implementation introduces new Microsoft redistributable payload beyond the product's already-approved Windows packaging.

Using the OS WinRT API alone does not justify shipping private Windows binaries.

## 9. Bluetooth SIG trademarks / qualification

Bluetooth SIG owns the Bluetooth word mark, figure mark and combination mark.

Current Bluetooth SIG guidance states that companies using Bluetooth trademarks on products/marketing are subject to membership, qualification and Brand Guide requirements.

Therefore distinguish:

A. technical implementation that consumes an operating system Bluetooth API;

from:

B. marketing a product using Bluetooth trademarks/logos as a qualified Bluetooth product.

### 9.1 EliteSCADA software integration

The planned software is a receiver/application running on already qualified host Bluetooth hardware/OS APIs.

This research does not design or manufacture a Bluetooth radio/controller.

Even so, product marketing/trademark wording needs review.

Record:

LEGAL_REVIEW_REQUIRED — BLUETOOTH-SIG-BRANDING

before using:
- Bluetooth logo;
- Bluetooth figure mark;
- qualification/certification wording;
- packaging statements implying EliteSCADA itself is a qualified Bluetooth end product.

### 9.2 Safe engineering documentation direction

Engineering docs can use necessary factual protocol terminology while preserving correct trademark spelling.

Marketing/legal should define the final trademark notice and qualification position.

Do not let engineering research claim certification.

## 10. BTHome versus Bluetooth qualification

BTHome UUID/name license does not replace Bluetooth SIG obligations that may apply to Bluetooth product branding/qualification.

They are separate boundaries:

BTHome:
- service UUID/name implementation license.

Bluetooth SIG:
- Bluetooth specifications, patents, qualification and trademarks.

Do not conflate them.

## 11. Shelly vendor provisioning

Shelly technical documentation is used as public interoperability documentation.

Current Shelly BLU encryption behavior:

- BTHome AES-CCM;
- device-generated 16-byte key;
- pairing/bonding required to read encryption-key characteristic;
- user passkey controls protected pairing/encryption state;
- changing passkey regenerates key in documented flows.

Engineering use:

- implement interoperable commissioning against documented characteristics if Main authorizes it;
- do not copy Shelly application code;
- do not embed vendor credentials;
- do not bypass pairing/authentication;
- do not scrape cloud/private APIs for keys.

### 11.1 Vendor app / key export

There is no universal BTHome key-export method.

If a future vendor requires:
- proprietary application;
- account login;
- cloud token;
- reverse-engineered key extraction;
- undocumented BLE commands;

then that vendor's provisioning automation requires separate technical/legal review.

Record as needed:

LEGAL_REVIEW_REQUIRED — VENDOR-PROVISIONING-RESTRICTION

The first implementation should prefer documented local characteristics and manual protected key import where necessary.

## 12. Shelly device documentation / firmware

Shelly device docs provide protocol behavior and public characteristics.

Compatibility testing may use commercial Shelly hardware.

Do not:
- redistribute Shelly firmware;
- claim Shelly certification/endorsement;
- copy vendor logos into EliteSCADA without marketing permission;
- bundle firmware update images.

Firmware update is outside BTHome v1 scope.

## 13. Hardware manufacturer branding

Compatibility records should identify manufacturer/model factually.

Compatibility statements must not imply partnership.

Recommended form:

"Tested with Shelly BLU H&T ZB, firmware X, on [date]."

Avoid:

"Official Shelly integration"

unless an actual partnership/authorization exists.

## 14. Test captures and secrets

BLE advertising captures may contain:

- device addresses;
- local names;
- sensor values;
- encrypted ciphertext;
- device metadata.

Encrypted key material must not be placed in public fixtures/issues.

Rules:

- synthetic keys are acceptable for public L0 vectors;
- real bindkeys stay in #497 Protected Material authority / secure test secret handling;
- sanitize diagnostics;
- consider device MAC/address as potentially identifying lab data;
- public test fixtures should use synthetic/redacted device identities where feasible.

## 15. Privacy boundary

Passive Bluetooth scanning can observe third-party devices nearby.

Future product/legal/privacy documentation should disclose:

- discovery scans local radio environment;
- only selected/imported devices become project devices;
- unrelated observations are not persisted beyond bounded discovery/diagnostics needs;
- no automatic whole-radio project import.

Engineering default:

SELECTED_IMPORT

not:

PASSIVE_NEIGHBORHOOD_INVENTORY

This is primarily privacy/product design rather than a license issue.

## 16. Supply-chain decision

Preferred v1 runtime dependency shape:

### Product-owned

- EliteSCADA managed sidecar;
- EliteSCADA BTHome decoder;
- OS backend adapters.

### Linux external host service

- BlueZ / bluetoothd, supplied by target Linux distribution.

### Candidate .NET dependency

- Tmds.DBus.Protocol MIT, subject to Main dependency approval.

### Windows

- OS WinRT / Windows SDK targeting.

### Reference/test only

- bthome-ble MIT;
- commercial Shelly devices;
- optional independent capture tools.

No:
- Python runtime dependency;
- bundled Home Assistant;
- bundled ESPHome;
- bundled BlueZ by default;
- GPL Bluetooth library linked into proprietary core.

## 17. SBOM / notice requirements

If Tmds.DBus.Protocol is selected:

- SBOM entry;
- exact version;
- MIT notice as required.

If bthome-ble is used only externally in lab:
- not a production SBOM dependency.

If any third-party test tool is redistributed in a developer/test image:
- include it in that artifact's SBOM/license inventory.

If BlueZ binaries are included in an official EliteSCADA image:
- legal/license inventory becomes mandatory before release.

## 18. Current legal/license matrix

| Component | Role | Current evidence | Product distribution decision | Gate |
| --- | --- | --- | --- | --- |
| BTHome UUID/name | Protocol identity | perpetual/irrevocable implementation-use statement | use permitted for BTHome implementation | branding review for logos/endorsement |
| BTHome spec website | protocol documentation | public current specification | implement facts; avoid wholesale reproduction | review if redistributing content |
| bthome-ble | independent reference parser | MIT | test/reference only | MIT notice if code bundled/copied |
| Tmds.DBus.Protocol 0.95.1 | candidate D-Bus client | MIT | candidate production dependency | normal dependency approval |
| BlueZ bluetoothd | Linux host Bluetooth service | mixed GPL/LGPL source tree | use host service over D-Bus | legal review if bundled/redistributed |
| Windows WinRT Bluetooth APIs | Windows backend | OS/Windows SDK API | call OS API | review new redistributables if any |
| Shelly device docs | interoperability/provisioning | public vendor docs | protocol interoperability evidence | vendor-specific review if undocumented flow needed |
| Bluetooth SIG marks | marketing/trademark | Bluetooth SIG controlled | no unreviewed logo/certification claim | LEGAL_REVIEW_REQUIRED |

## 19. Legal review items before GA

Mandatory before General Availability:

1. Confirm final BTHome marketing wording and whether any logo will be used.
2. Confirm EliteSCADA/Bluetooth SIG membership/qualification/trademark position for marketing.
3. Review exact production third-party dependency SBOM.
4. If BlueZ is bundled in any official appliance/container, review redistribution obligations.
5. Review any Windows redistributable newly added by Bluetooth implementation.
6. Review every automated vendor-specific key provisioning path beyond documented local APIs.
7. Review privacy disclosure for passive radio discovery.

These are GA/legal gates, not blockers to protocol research.

## 20. Legal conclusion

Protocol implementation:

GO_WITH_NORMAL_LEGAL_GATES

No evidence found that BTHome requires a royalty or commercial protocol license for use of UUID/name in a conforming BTHome implementation.

The explicit UUID/name license is favorable.

Preferred architecture further reduces license coupling by:

- using BlueZ through D-Bus rather than linking/copying BlueZ code;
- using Windows OS APIs;
- implementing the BTHome decoder independently;
- keeping MIT reference libraries optional/test-only.

Remaining legal risk is mainly:

- branding/trademarks;
- redistribution choices;
- vendor-specific provisioning;
- privacy/discovery behavior.

## 21. Sources

BTHome format/license link:
- https://bthome.io/format/
- https://bthome.io/images/License_Statement_-_BTHOME.pdf

BTHome reference implementation:
- https://github.com/Bluetooth-Devices/bthome-ble
- https://github.com/Bluetooth-Devices/bthome-ble/releases

BlueZ:
- https://github.com/bluez/bluez
- https://github.com/bluez/bluez/blob/master/doc/org.bluez.Adapter.rst

Tmds:
- https://www.nuget.org/packages/Tmds.DBus.Protocol/0.95.1
- https://github.com/tmds/Tmds.DBus

Microsoft:
- https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-apis-desktop-apps
- https://learn.microsoft.com/en-us/windows/apps/windows-sdk/
- https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementwatcher

Bluetooth SIG:
- https://www.bluetooth.com/develop-with-bluetooth/marketing-branding/
- https://www.bluetooth.com/wp-content/uploads/2023/08/BTLA_w_Brand_Guide.pdf

Shelly:
- https://shelly-api-docs.shelly.cloud/docs-ble/
- https://shelly-api-docs.shelly.cloud/docs-ble/encryption/

All sources revalidated/accessed 2026-10-06.

## 22. Declarations

DOCS_ONLY
NO PRODUCT CODE CHANGED
NO DEPENDENCY CHANGED
NO CI CHANGED
NO THIRD_PARTY_CODE_COPIED
NO BLUEZ_CODE_BUNDLED
NO BLUETOOTH IMPLEMENTATION
NO MERGE PERFORMED
HA = HIGH AVAILABILITY
HAB = HOME ASSISTANT BRIDGE
