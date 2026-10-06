# BLUETOOTH ADAPTER HOST RESOURCE RESEARCH

Status: CHECKPOINT 2 COMPLETE
Lane: RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER
Contract: C-HOME-BLUETOOTH-BTHOME-RESEARCH-01
Order: HOME-BLUETOOTH-BTHOME-EXECUTION-RESEARCH-01
Owner issue: #545
Parent: #472
Related foundation: #543
Access date for public sources: 2026-10-06

## 1. Checkpoint scope

This document covers CHECKPOINT 2 only:

- whether BluetoothAdapter should be a first-class Host Resource;
- Linux BlueZ/D-Bus versus raw HCI;
- Windows BLE advertisement APIs and service/runtime implications;
- container / edge deployment;
- shared scan versus exclusive adapter ownership;
- multiple adapters and adapter affinity;
- HA High Availability external-effect ownership boundary;
- comparison of first implementation architectures A-D;
- PREFERRED_V1_ARCHITECTURE;
- contract deltas required from the common Host Resource foundation.

This checkpoint does not implement Bluetooth, Host Resource changes, sidecar code, Runtime changes, schemas, UI, tests or CI.

## 2. Live EliteSCADA state revalidated

GitHub live was revalidated before this checkpoint.

### #545

State remains:

ACTIVE / RESEARCH_ONLY / DOCS_ONLY / NO_PRODUCT_CODE / NO_MERGE_BY_RESEARCHER

Authorized research branch:

research/home-bluetooth-bthome

Release base:

wave15/corrections-integration@77b08d60333685b4ba06aa649e3125f23b475dcf

Checkpoint-1 branch state before this work:

- HEAD: e6977174865f86d49e26b916856aa1e4cdbe352f;
- merge-base: 77b08d60333685b4ba06aa649e3125f23b475dcf;
- ahead 1 / behind 0;
- only BTHOME-EXECUTION-RESEARCH.md changed.

### #543

#543 currently owns:

- SIDECAR-LIFECYCLE-01;
- HOST-RESOURCE-GENERALIZATION-01.

Its published Checkpoint 1 implemented an exclusive lease by stable physical identity and consumes the existing:

CommunicationDriverRuntimeServices.CanOwnExternalEffects

authority seam.

Bluetooth protocol remains explicitly outside #543.

This research does not edit #543.

The current exclusive-only lease model is relevant because Bluetooth scanning is not universally an exclusive hardware operation. That produces a research contract delta later in this document.

## 3. Public/current sources

### Linux / BlueZ

1. BlueZ org.bluez.Adapter1 D-Bus API
   https://github.com/bluez/bluez/blob/master/doc/org.bluez.Adapter.rst
   Accessed 2026-10-06.

2. BlueZ management protocol
   https://github.com/bluez/bluez/blob/master/doc/mgmt-protocol.rst
   Accessed 2026-10-06.

3. BlueZ D-Bus security policy
   https://github.com/bluez/bluez/blob/master/src/bluetooth.conf
   Accessed 2026-10-06.

4. BlueZ current releases
   https://github.com/bluez/bluez/releases
   Current release observed: 5.87, released 2026-07-07.

5. BlueZ bluetoothctl scan documentation
   https://github.com/bluez/bluez/blob/master/doc/bluetoothctl-scan.rst
   Accessed 2026-10-06.

### Windows

6. Microsoft BluetoothLEAdvertisementWatcher
   https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementwatcher
   Accessed 2026-10-06.

7. Microsoft BluetoothLEAdvertisementWatcher.Start
   https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementwatcher.start
   Accessed 2026-10-06.

8. Microsoft Bluetooth LE Advertisements
   https://learn.microsoft.com/en-us/windows/uwp/devices-sensors/ble-beacon
   Accessed 2026-10-06.

9. Microsoft BluetoothAdapter
   https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothadapter
   Accessed 2026-10-06.

10. Microsoft BluetoothLEAdvertisementReceivedEventArgs
    https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementreceivedeventargs
    Accessed 2026-10-06.

11. Microsoft Device information properties
    https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/device-information-properties
    Accessed 2026-10-06.

12. Microsoft Call Windows Runtime APIs in desktop apps
    https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-apis-desktop-apps
    Accessed 2026-10-06.

### Container

13. Docker running containers / runtime privilege
    https://docs.docker.com/engine/containers/run/
    Accessed 2026-10-06.

14. Docker container run reference
    https://docs.docker.com/reference/cli/docker/container/run/
    Accessed 2026-10-06.

### Candidate implementation dependencies / references

15. Tmds.DBus.Protocol
    https://www.nuget.org/packages/Tmds.DBus.Protocol
    Current observed version: 0.95.1, updated 2026-09-04, MIT.

16. Bleak
    https://bleak.readthedocs.io/en/latest/
    Current documented release observed: 3.0.2, 2026-05-02, MIT.
    Reference only for architecture comparison / possible independent fixture.
    Not selected as a product dependency by this research.

## 4. Primary conclusion

BluetoothAdapter SHOULD be a first-class Host Resource.

However, its ownership semantics differ materially from controller resources such as:

- ZWaveController;
- ZigbeeCoordinator.

Those controller resources generally have one protocol owner that controls a network/controller state.

A BLE adapter used only for BTHome advertisement reception is different:

- the OS may coordinate scanning;
- multiple clients may legitimately share one physical scan procedure;
- another OS service may use Bluetooth concurrently;
- scanning can be read/observation-oriented;
- global adapter power/reset/pairing operations are disruptive and are not equivalent to passive/shared observation.

Therefore:

BLUETOOTH_ADAPTER = FIRST_CLASS_HOST_RESOURCE

but:

BLUETOOTH_SCAN_OBSERVATION != EXCLUSIVE_CONTROLLER_OWNERSHIP

The resource contract needs explicit ownership mode/capability semantics.

## 5. Proposed Host Resource model

Future conceptual model:

ResourceKind = BluetoothAdapter

Required common fields:

- ResourceId;
- ResourceKind;
- Locator;
- PhysicalIdentity;
- Model;
- Firmware/driver metadata;
- Capabilities;
- Availability;
- Ownership/lease capability;
- current lease state;
- current scan state;
- last enumeration time;
- replacement/re-enumeration evidence.

### 5.1 ResourceId

ResourceId is an EliteSCADA logical stable ID.

It must not equal:

- hci0/hci1;
- Windows DeviceId;
- USB bus path;
- enumeration order;
- current default adapter index.

Those are locators/evidence, not the project identity.

### 5.2 Locator

Linux examples:

- /org/bluez/hci0;
- /org/bluez/hci1.

Windows example:

- BluetoothAdapter.DeviceId / DeviceInformation.Id.

Locator is explicitly replaceable/transient.

### 5.3 PhysicalIdentity

PhysicalIdentity should be a normalized fingerprint assembled from the strongest evidence available.

Linux candidate evidence:

- BlueZ Adapter1 Address;
- BlueZ Adapter1 AddressType;
- Modalias;
- manufacturer/version;
- udev/device metadata where available;
- USB VID/PID/serial/path evidence for external dongles when available.

Windows candidate evidence:

- BluetoothAdapter BluetoothAddress;
- BluetoothAdapter DeviceId;
- System.Devices.ContainerId;
- System.Devices.DeviceInstanceId;
- System.Devices.HardwareIds;
- manufacturer/model properties where available.

No single one is declared universally stable without L4 qualification.

The correct product rule is:

LOCATOR != PHYSICAL_IDENTITY != RESOURCE_ID

### 5.4 Capabilities

Candidate resource capabilities:

- SupportsLowEnergy;
- SupportsPassiveScan;
- SupportsActiveScan;
- SupportsExtendedAdvertising;
- SupportsCodedPhy;
- ExposesRemoteAddressType;
- SupportsAdapterSpecificScanAffinity;
- SupportsDuplicateAdvertisementDelivery;
- SupportsSharedObservation;
- SupportsExclusiveMutation;
- CanPowerCycle;
- CanReset;
- CanEnumerateFirmware;
- CanEnumerateDriver;
- CanObserveServiceData;
- CanObserveRSSI.

Do not expose a capability unless the actual backend proves it.

## 6. Linux architecture

### 6.1 Preferred Linux API: BlueZ D-Bus

Preferred Linux backend:

org.bluez.Adapter1 over the system D-Bus.

Reasons:

- it is the supported userspace service abstraction;
- it coordinates with bluetoothd and other clients;
- it exposes per-adapter objects;
- it provides discovery sessions;
- it supports LE-only discovery filtering;
- it exposes adapter address/address type;
- it exposes current discovery state;
- it supports multiple adapters as separate object paths;
- it explicitly defines sharing semantics.

### 6.2 Shared discovery is normative BlueZ behavior

BlueZ StartDiscovery creates a discovery session for a client.

The documentation explicitly states:

- each client may request one discovery session per adapter;
- the physical discovery procedure is shared between discovery sessions;
- StopDiscovery releases only the caller's session;
- scanning stops only after all client sessions have ended.

SetDiscoveryFilter is also client-scoped.

When multiple clients set filters:

- BlueZ merges filters internally;
- notifications are delivered to clients;
- each client must verify that received updates match its own desired filter.

This is direct evidence that BLE discovery/scan through BlueZ is not an exclusive-adapter operation.

### 6.3 Duplicate data

BlueZ SetDiscoveryFilter has:

DuplicateData

which controls duplicate detection for ManufacturerData / ServiceData updates.

For BTHome:

- the backend should request the duplicate behavior required by event/state semantics;
- the EliteSCADA BTHome layer must still do its own protocol-aware duplicate/replay handling;
- BlueZ duplicate suppression must not be treated as sufficient for BTHome button/event correctness.

### 6.4 Adapter enumeration

BlueZ exposes adapters as:

/org/bluez/hci0
/org/bluez/hci1
...

These paths are useful locators.

They are not safe ResourceIds.

The adapter exposes:

- Address;
- AddressType;
- Name/Alias;
- Powered;
- Discovering;
- UUIDs;
- Modalias;
- Roles;
- Manufacturer;
- Version.

These properties are suitable Host Resource evidence/diagnostics.

### 6.5 Multiple adapters on Linux

Linux/BlueZ provides a clean per-adapter object boundary.

Therefore Linux v1 can support:

- enumerate multiple adapters;
- select an adapter by Host Resource;
- run one discovery session per selected adapter;
- observe which adapter produced a Device object/update;
- maintain adapter-affinity diagnostics.

Do not infer "roaming".

A device seen by hci0 and hci1 is two observations of one remote device, not two Equipments.

### 6.6 BlueZ D-Bus versus raw HCI

#### BlueZ D-Bus

Advantages:

- OS-supported service abstraction;
- shared discovery ownership;
- existing permission boundary;
- stable high-level adapter model;
- natural multi-adapter object hierarchy;
- lower privilege than owning controller internals directly;
- coexists with desktop/system Bluetooth consumers.

Disadvantages:

- depends on host bluetoothd/system bus;
- filter behavior is merged across clients;
- exact radio scan parameters may be system-controlled;
- container access to the host system bus is security-sensitive.

#### Raw HCI / management path

Advantages:

- direct controller index;
- lower-level access to scan details;
- potentially precise adapter affinity and HCI diagnostics.

Disadvantages:

- bypasses much of BlueZ client coordination;
- duplicates controller-management responsibilities;
- tends to require broader device/socket/capability access;
- creates conflict risk with bluetoothd;
- increases kernel/HCI compatibility burden;
- makes container security materially worse;
- creates a second ownership layer the product does not need for BTHome.

Decision:

LINUX_V1_BACKEND = BLUEZ_DBUS

RAW_HCI = NOT_DEFAULT / LAB_OR_EXCEPTION_ONLY

Raw HCI may be used later for independent diagnostics, packet capture, or a proven missing capability, but not merely because it appears simpler.

## 7. Linux permissions

The current BlueZ D-Bus policy allows default-context clients to send to org.bluez, while bluetoothd owns org.bluez as root.

Therefore the default Linux design should:

- run bluetoothd as the host system Bluetooth authority;
- access org.bluez through D-Bus;
- avoid running a second bluetoothd;
- avoid raw HCI ownership;
- use the narrowest service/user policy permitted by the target distribution;
- treat distro security frameworks/policies as a deployment qualification item.

Do not require root as a protocol requirement.

If a distribution adds stricter D-Bus / polkit / MAC policy, deployment must express that explicitly.

## 8. Windows architecture

### 8.1 Preferred Windows API

Preferred Windows receive path:

Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher

The API:

- exists on Windows 10 and Windows 11;
- receives BLE advertisements;
- exposes payload filters;
- exposes RSSI;
- exposes active/passive scanning mode;
- exposes watcher status/stopped events;
- supports desktop application use through WinRT.

### 8.2 Desktop / service execution

Microsoft documents that desktop apps can call WinRT APIs.

The BluetoothLEAdvertisementWatcher.Start documentation explicitly discusses applications outside AppContainer, including:

- session 0 services;
- Win32 applications;

in the context of suspend/resume power notifications.

Therefore a Windows service/worker execution model is credible.

However the future implementation must validate:

- actual EliteSCADA service identity;
- required Windows capability/package identity behavior;
- startup before interactive login;
- Bluetooth service readiness;
- suspend/resume;
- controller reset/restart;
- service account permissions.

Do not assume UI-session behavior equals service behavior until L1/L4 qualification.

### 8.3 Windows 10/11 compatibility gate

Basic advertisement watcher exists from Windows 10 initial releases.

For BTHome stable-identity evidence, however, the important property:

BluetoothLEAdvertisementReceivedEventArgs.BluetoothAddressType

was introduced in Windows 10 version 2004 / build 19041.

Therefore recommended first qualification floor for the direct BTHome Windows backend:

WINDOWS_BTHOME_QUALIFICATION_FLOOR = WINDOWS_10_2004_BUILD_19041_OR_LATER

This is a qualification recommendation, not yet a product-wide minimum OS change.

Main must reconcile it with the product's supported Windows matrix before DEV.

Windows 11 is naturally inside this API range.

### 8.4 Active versus passive scanning on Windows

Windows watcher supports active scanning.

Microsoft states active scanning is needed to receive scan-response advertisements and consumes more power.

BTHome v2 data is carried in advertisement service data and does not require scan responses for the normal direct receive path.

Therefore:

WINDOWS_BTHOME_DEFAULT_SCAN_MODE = PASSIVE

Active scan should be used only when a qualified device/profile proves that required metadata is in scan responses.

### 8.5 Address / address type evidence

Received advertisements expose:

- BluetoothAddress;
- BluetoothAddressType;
- RSSI;
- timestamp;
- advertisement payload.

This supports the Checkpoint-1 identity policy by allowing the backend to distinguish at least the public/random address type reported by Windows.

It still does not create a universal stable remote-device identity.

## 9. Windows adapter enumeration and multiple adapters

BluetoothAdapter provides:

- GetDefaultAsync();
- GetDeviceSelector();
- FromIdAsync();
- DeviceId;
- BluetoothAddress;
- Low Energy support/capability properties;
- GetRadioAsync().

Therefore Windows can enumerate adapter resources.

However the public BluetoothLEAdvertisementWatcher API documented on 2026-10-06 has:

- constructor with no adapter argument;
- constructor with advertisement filter;
- scan parameters for interval/window;
- no documented BluetoothAdapter selection property;
- received advertisement event args do not identify the local receiving adapter.

Conclusion:

WINDOWS_ADAPTER_INVENTORY = SUPPORTED

WINDOWS_ADVERTISEMENT_WATCHER_PER_ADAPTER_AFFINITY = NOT_PROVEN

This is a material v1 limitation.

### 9.1 Required product behavior

Until L4 evidence proves a supported per-adapter receive path:

- Windows must not claim deterministic scan affinity to a selected physical dongle;
- a Windows Host Resource can inventory Bluetooth adapters;
- the actual advertisement receive capability should report:
  ScanAffinity = SystemManagedOrUnspecified;
- multi-adapter receive must be treated as a qualification gap.

A possible v1 Windows shape is:

- require one qualified active BLE adapter for deterministic support;
- allow multiple installed radios but do not promise which one receives advertisements;
- expose this limitation in diagnostics.

Do not use undocumented/private APIs merely to force per-dongle affinity.

## 10. Active versus passive scanning — cross-platform rule

BTHome direct receive does not need active scan as a protocol requirement.

Preferred rule:

- passive/shared observation first;
- active scanning only for a documented device/profile requirement;
- never enable local discoverability merely to receive BTHome;
- do not pair devices unless provisioning/GATT key retrieval explicitly requires it.

Linux BlueZ D-Bus does not expose the same simple per-watcher active/passive toggle as the Windows watcher.

Therefore actual scan mode must be backend-specific and diagnosable.

Do not build protocol semantics around "active scan" being identical across Windows and Linux.

## 11. Exclusive ownership versus shared scanning

### 11.1 Required distinction

Two different operations must not share one lease semantic.

#### Shared observation

Examples:

- receive advertisements;
- read RSSI;
- observe service data;
- maintain last-seen;
- decode BTHome.

Recommended lease:

SHARED_OBSERVATION

#### Exclusive mutation/control

Examples:

- power adapter on/off;
- reset controller;
- reconfigure controller-wide state;
- take raw HCI ownership;
- perform invasive firmware operations;
- operations known to disrupt other clients.

Recommended lease:

EXCLUSIVE_MUTATION

### 11.2 Why exclusive scan ownership is wrong by default

BlueZ explicitly supports multiple discovery clients sharing one physical discovery procedure.

Windows watcher is also system-managed rather than an application claiming a raw controller.

Therefore:

EXCLUSIVE_ADAPTER_OWNERSHIP_FOR_BTHOME_SCAN = REJECT

It would unnecessarily block legitimate OS/client coexistence and misrepresent actual platform semantics.

### 11.3 What EliteSCADA can actually arbitrate

EliteSCADA can arbitrate its own owners.

It cannot promise to globally lock the Bluetooth adapter against:

- Windows;
- BlueZ;
- desktop settings;
- another host process not participating in EliteSCADA Host Resource leases.

Therefore "exclusive" means:

exclusive among EliteSCADA owners for a disruptive operation

not:

global physical exclusivity over every OS consumer.

External contention must be detected and diagnosed.

## 12. Contract delta for #543

The current published #543 Checkpoint-1 Host Resource foundation implements an exclusive lease by stable physical identity.

Bluetooth requires a more expressive ownership model.

Record:

RESEARCH_CONTRACT_DELTA_REQUIRED — BLUETOOTH-ADAPTER-01

Minimum delta to consider before Bluetooth DEV:

1. Host Resource lease mode/capability:
   - SharedObservation;
   - ExclusiveMutation.

2. Resource capability:
   - SupportsSharedObservation.

3. Resource capability:
   - SupportsAdapterSpecificScanAffinity
   or equivalent backend capability representation.

4. Lease diagnostics that distinguish:
   - EliteSCADA owner contention;
   - OS/external contention;
   - backend unavailable;
   - adapter disappeared.

5. No weakening of existing exclusive-controller semantics for:
   - ZWaveController;
   - ZigbeeCoordinator.

The smallest acceptable implementation could preserve existing exclusive lease behavior as the default and add an explicitly opted-in shared observation lease mode.

This research does not prescribe #543 code structure.

## 13. HA — High Availability ownership

This document does not alter HA.

The future Bluetooth runtime must consume the existing public authority seam:

CommunicationDriverRuntimeServices.CanOwnExternalEffects

or an already-existing equivalent public contract.

Recommended behavior:

- only effective Active may acquire the EliteSCADA scan ownership session that feeds canonical Runtime state/events;
- standby may enumerate resource availability without publishing canonical observations;
- standby must not independently elect itself;
- standby must not start a competing Bluetooth sidecar;
- failover must release/reacquire through the common Host Resource/sidecar lifecycle;
- no new Bluetooth-specific HA election or epoch.

Reason:

Even passive scanning is a host-side external resource activity, and duplicate Active/Standby scanners can produce duplicate transient Events.

This recommendation is about ownership of EliteSCADA effects, not about preventing the OS from sharing the physical radio.

## 14. Multiple EliteSCADA instances on one host

Scenario:

- two EliteSCADA Runtime instances on one machine;
- both see the same Bluetooth adapter;
- OS permits shared scan.

Product rule:

- OS-level shareability does not mean two EliteSCADA owners should publish the same canonical data;
- Host Resource authority still chooses the EliteSCADA owner;
- one authoritative scanner stream feeds canonical Runtime;
- a second non-authoritative owner is denied from owning Runtime effects.

This is compatible with:

SHARED_WITH_OS
+
SINGLE_AUTHORITATIVE_ELITESCADA_OWNER

The shared lease delta is primarily needed to distinguish observation semantics from controller-exclusive semantics, not to authorize duplicate Runtime authorities.

## 15. Container / edge architecture

### 15.1 Linux container facts

A container does not automatically own host Bluetooth hardware or the host BlueZ service.

Possible technical paths:

A. expose host BlueZ system D-Bus to the container;
B. pass physical USB/controller device into the container and run Bluetooth stack there;
C. run a host-side Bluetooth service/sidecar and expose a narrow local IPC contract to the container.

### 15.2 Full host D-Bus mount is not a safe default

The BlueZ system bus is part of the host system D-Bus.

Giving the application container access to the host system-bus socket creates a much broader host IPC boundary than "receive BTHome advertisements".

Even if D-Bus policy restricts services, mounting the complete system bus is a security-sensitive deployment choice.

Therefore:

HOST_SYSTEM_DBUS_MOUNT = ADVANCED / SECURITY_REVIEW_REQUIRED

not the default deployment architecture.

### 15.3 Raw device passthrough

Docker supports specific --device passthrough and capability grants.

Docker also explicitly warns that --privileged grants:

- all Linux capabilities;
- access to all host devices;
- weakened/default-disabled security controls.

Therefore:

docker --privileged = REJECT_AS_DEFAULT

If a future raw-HCI container experiment is necessary:

- expose only the required device;
- add only required capabilities;
- document why each permission exists;
- keep it outside default BTHome deployment.

### 15.4 Preferred container boundary

Preferred container deployment:

HOST
  -> managed local Bluetooth sidecar / host worker
  -> BlueZ D-Bus
  -> BluetoothAdapter

MAIN ELITESCADA CONTAINER
  -> narrow local authenticated/authorized IPC
  -> Bluetooth advertisement observations

Benefits:

- bluetoothd remains host-owned;
- main container stays hardware-unprivileged;
- no full system D-Bus exposure to the main application container;
- no raw HCI in the main container;
- Host Resource identity lives where hardware is visible;
- sidecar lifecycle can reuse #543;
- Windows and Linux can share a normalized scanner contract.

Control plane must remain local-only by default.

## 16. Sidecar process security boundary

If the preferred architecture is implemented as a managed local sidecar:

Sidecar may receive:

- selected adapter ResourceId / locator evidence;
- scan policy;
- BTHome filter UUID 0xFCD2;
- protected key references/material only through approved protected-material injection;
- authority token/state from the host process;
- bounded configuration.

Sidecar returns:

- normalized advertisement observations;
- adapter availability;
- scan lifecycle state;
- source adapter evidence when the OS exposes it;
- sanitized diagnostics/counters.

Sidecar must not:

- expose bindkeys;
- expose arbitrary D-Bus/HCI passthrough to browser/UI;
- create a public network listener by default;
- implement independent leader election;
- mutate canonical Equipment/TAGs directly;
- own historian/alarm/runtime semantics.

## 17. Candidate .NET Linux D-Bus dependency

If a .NET sidecar/backend uses a library rather than hand-writing D-Bus framing, a current candidate is:

Tmds.DBus.Protocol 0.95.1

Observed 2026-10-06:

- current NuGet update 2026-09-04;
- MIT license;
- .NET 6+ / .NET Standard support;
- described as low-level, high-performance, NativeAOT-compatible D-Bus library.

This is a candidate only.

Dependency approval belongs to future product DEV/Main.

Checkpoint 3 must still record legal/license conclusions.

## 18. Bleak as reference / fixture, not preferred product runtime

Current Bleak documentation observed:

- version 3.0.2;
- MIT;
- Linux backend uses BlueZ D-Bus;
- Windows backend uses WinRT;
- cross-platform scanner abstraction.

This independently supports the architectural conclusion that BLE requires OS-specific backends.

However Bleak is Python and adds:

- Python runtime packaging;
- another dependency ecosystem;
- async runtime/process integration;
- product supply-chain surface.

Its docs also currently list Windows 11 as Tier-1 maintained support.

Therefore:

BLEAK = USEFUL_REFERENCE_OR_L2_INDEPENDENT_FIXTURE

BLEAK != SELECTED_PRODUCT_RUNTIME_DEPENDENCY

No Python sidecar is proposed as the default EliteSCADA implementation.

## 19. Architecture comparison

### A — Built-in generic .NET implementation

Meaning:

One in-process .NET implementation attempting to abstract Bluetooth as if one common managed OS API existed.

Windows:
- possible only by calling WinRT/Windows platform APIs.

Linux:
- still needs BlueZ D-Bus or lower-level Linux APIs.

ARM:
- depends on Linux backend and distro.

Container:
- hardware/D-Bus privilege remains inside main product container.

Dependencies:
- Windows SDK;
- Linux D-Bus client or raw native interop.

Licensing:
- manageable, but Linux dependency still exists.

Packaging:
- simplest process count;
- hardest container privilege boundary.

Complexity:
- OS-specific code leaks into main Runtime/DriverHost.

Observability:
- good if implemented carefully.

Security:
- weaker separation for container/hardware access.

Decision:
NOT PREFERRED as architecture label because there is no credible truly generic OS-independent Bluetooth scanner API in base .NET.

### B — Linux BlueZ backend + Windows native backend in-process

Windows:
- WinRT BluetoothLEAdvertisementWatcher.

Linux:
- BlueZ Adapter1 over D-Bus.

ARM:
- viable where BlueZ/distro is qualified.

Container:
- difficult without host D-Bus exposure or device passthrough.

Dependencies:
- Windows SDK;
- Linux D-Bus library.

Licensing:
- favorable candidate libraries exist.

Packaging:
- no extra process;
- OS-specific dependencies in primary host.

Complexity:
- moderate.

Observability:
- direct.

Security:
- acceptable bare-metal;
- awkward in container.

Decision:
TECHNICALLY SOUND BACKEND STRATEGY
but not preferred deployment shape for the full Windows/Linux/container matrix.

### C — Managed local Bluetooth sidecar

Sidecar implementation:

- common managed BTHome scanner contract;
- Windows backend = WinRT watcher;
- Linux backend = BlueZ D-Bus;
- common BTHome frame decoder can be shared with normal managed libraries;
- host-side sidecar when main EliteSCADA is containerized.

Windows:
- local service/worker;
- no browser endpoint.

Linux:
- local service/worker with D-Bus access.

ARM:
- .NET Linux ARM64 is feasible subject to adapter/distro qualification.

Container:
- strongest option because main container need not receive raw hardware/system-bus privileges.

Dependencies:
- common #543 sidecar lifecycle;
- Windows SDK backend;
- Linux D-Bus backend/library;
- local IPC contract.

Licensing:
- controllable; Tmds candidate MIT;
- no Python dependency required.

Packaging:
- additional artifact/process;
- but #543 exists specifically to standardize this lifecycle.

Complexity:
- higher initial integration;
- lower long-term hardware privilege coupling.

Observability:
- explicit lifecycle + adapter diagnostics.

Security:
- strongest privilege separation of the four options when local-only.

Decision:
PREFERRED.

### D — External / user-managed bridge

Examples conceptually:

- Home Assistant;
- ESPHome Bluetooth proxy;
- custom MQTT bridge;
- user service.

Windows/Linux/ARM/container:
- depends on external bridge.

Dependencies:
- shifts burden to external system.

Packaging:
- easiest for EliteSCADA.

Complexity:
- lowest internal implementation, highest external operational dependency.

Observability:
- reduced end-to-end ownership.

Security:
- depends on external bridge/auth.

Product fit:
- valid interoperability option;
- not native direct-BTHome support.

Decision:
NOT PREFERRED FOR NATIVE BTHOME.
May coexist as separate integration path.

## 20. Preferred v1 architecture

PREFERRED_V1_ARCHITECTURE = C — MANAGED LOCAL BLUETOOTH SIDECAR

with OS backends:

LINUX_BACKEND = BLUEZ_DBUS
WINDOWS_BACKEND = WINRT_BLUETOOTHLEADVERTISEMENTWATCHER

and common managed BTHome decoder/normalizer.

Important clarification:

Architecture C uses architecture-B-style OS-native backends internally.

The choice of C is about process / privilege / deployment ownership.

Why C wins:

1. BluetoothAdapter is a host resource with OS-specific hardware visibility.
2. Containers should not receive broad host-system or raw-device privilege.
3. #543 already establishes managed sidecar lifecycle semantics.
4. Linux BlueZ D-Bus is naturally host-scoped.
5. Windows WinRT can run in a service/desktop process outside the UI.
6. Bluetooth scan sharing does not require the main Runtime to own the raw controller.
7. Sidecar restart/recovery can be independent of canonical Runtime state ownership.
8. The same normalized advertisement contract can serve Windows/Linux while keeping backend quirks isolated.

## 21. Proposed sidecar/backend contract shape

Conceptual only:

BluetoothScannerStartRequest
- ResourceId
- requested scan mode
- BTHome service filter
- duplicate-delivery policy
- authority token/context
- requested adapter affinity

BluetoothAdapterSnapshot
- ResourceId
- Locator
- PhysicalIdentityEvidence
- Availability
- OS backend
- model
- firmware/driver
- capabilities
- scan affinity capability
- current scan state

BluetoothAdvertisementObservation
- ResourceId / local adapter evidence when available
- receiver timestamp
- remote address
- remote address type
- RSSI
- service UUID
- raw bounded service data
- advertisement flags/type metadata
- source backend
- no decrypted key material

The main BTHome protocol layer then:

- validates 0xFCD2;
- decrypts via Protected Material authority integration;
- enforces replay;
- maps state/event semantics;
- feeds canonical Runtime authorities.

Exact process boundary for decryption may be decided later, but keys must never cross unprotected channels.

## 22. Multiple adapters

### Linux

Supported architecture:

- one Host Resource per BlueZ adapter;
- adapter-specific discovery sessions;
- user/project chooses selected adapter(s);
- each observation records local adapter ResourceId.

### Windows

Current public watcher limitation:

- adapters can be enumerated;
- advertisements can be watched;
- per-watcher local adapter selection is not documented.

Therefore:

Windows v1 must not promise deterministic per-adapter affinity.

### Cross-adapter dedup

If the same remote BTHome frame is received by multiple adapters:

- do not create duplicate Equipment;
- preserve adapter observation metadata for diagnostics/coverage;
- deduplicate canonical state/event according to protocol identity/counter/packet evidence;
- transient events require event-aware dedup;
- never use RSSI alone to merge identity.

### Adapter failover

Do not assume roaming.

Future failover may be allowed only when:

- project explicitly selects an alternate adapter set;
- remote device stable identity is already proven;
- duplicate/event semantics remain safe;
- authority remains singular.

## 23. Adapter disappear / re-enumeration

Future backend must handle:

- USB unplug;
- radio disable;
- bluetoothd restart;
- Windows Bluetooth service/radio reset;
- OS suspend/resume;
- adapter appearing under a new locator;
- wrong physical adapter appearing at an old locator.

Expected Host Resource behavior:

- mark unavailable;
- stop accepting observations from missing resource;
- retry enumeration with bounded backoff;
- match reappeared hardware by PhysicalIdentity evidence;
- do not silently bind a different physical adapter to the old ResourceId;
- resume scan only after authority and identity checks pass;
- expose lifecycle in common diagnostics.

## 24. Diagnostics

Reuse #500.

Bluetooth protocol details should feed the existing diagnostics ladder.

Adapter-level details:

- adapter available;
- backend type;
- locator;
- physical identity confidence;
- driver/firmware;
- address/address type;
- scan state;
- scan mode;
- scan affinity capability;
- shared/exclusive lease state;
- sidecar health;
- D-Bus/WinRT error;
- adapter restart/re-enumeration count.

Observation-level details:

- last advertisement;
- advertisements/sec;
- duplicates;
- queue saturation;
- decrypt failures;
- malformed packets;
- replay drops;
- unknown object ids;
- device last seen;
- RSSI;
- missing bindkey.

No second diagnostics framework.

## 25. Security conclusions

1. Do not use --privileged for normal container deployment.
2. Do not expose raw HCI to the main application container by default.
3. Do not mount broad host system D-Bus into the main container as the normal design.
4. Prefer host-local sidecar with narrow IPC.
5. BlueZ remains the Linux Bluetooth authority.
6. WinRT remains the Windows Bluetooth authority.
7. Bindkeys remain protected material.
8. Sidecar has no public/browser listener by default.
9. Shared scan must not weaken singular EliteSCADA Runtime authority.
10. OS sharing is accepted; external contention is diagnostic, not silently ignored.

## 26. Open questions after Checkpoint 2

1. Windows deterministic per-adapter scan affinity remains unproven.
2. Exact Windows service packaging/capability behavior requires L1/L4 qualification.
3. Linux distro-specific D-Bus/polkit/MAC policies require lab qualification.
4. Exact Linux passive-versus-active radio behavior under chosen BlueZ discovery flow needs packet/lab evidence.
5. Adapter physical identity persistence across USB ports/reinstall/reboot needs real hardware evidence.
6. Secure host-side sidecar IPC transport and packaging must reuse/finalize #543 rather than create a parallel lifecycle.
7. Container deployment needs one concrete supported topology and installer/runtime UX.
8. Scale/queue limits remain Checkpoint 3.
9. Legal/redistribution review remains Checkpoint 3.
10. Real hardware compatibility matrix remains Checkpoint 3.

## 27. Main / contract decisions

### Required

RESEARCH_CONTRACT_DELTA_REQUIRED — BLUETOOTH-ADAPTER-01

Need:
Host Resource lease semantics capable of representing shared observation separately from exclusive mutation.

Also represent backend scan-affinity capability.

### No HA change

No HA internals change requested.

Reuse:
CommunicationDriverRuntimeServices.CanOwnExternalEffects

### No Protected Material change

No new vault.

Reuse #497.

### No diagnostics change

No new diagnostics framework.

Reuse #500.

## 28. Checkpoint-2 decision

BluetoothAdapter Host Resource:

YES

Scan ownership:

SHARED_OBSERVATION
+
SINGLE_AUTHORITATIVE_ELITESCADA_OWNER
+
EXCLUSIVE_MUTATION_WHEN_REQUIRED

Linux:

BLUEZ_DBUS_PREFERRED
RAW_HCI_NOT_DEFAULT

Windows:

WINRT_BLUETOOTHLEADVERTISEMENTWATCHER_PREFERRED
WINDOWS_10_2004_BUILD_19041_PLUS_FOR_FULL_ADDRESS_TYPE_EVIDENCE

Container:

HOST_SIDE_MANAGED_BLUETOOTH_SIDECAR_PREFERRED
NO_PRIVILEGED_DEFAULT
NO_RAW_HCI_MAIN_CONTAINER
NO_BROAD_SYSTEM_DBUS_MAIN_CONTAINER_DEFAULT

Architecture:

PREFERRED_V1_ARCHITECTURE = MANAGED_LOCAL_BLUETOOTH_SIDECAR

with BlueZ D-Bus on Linux and WinRT advertisement watcher on Windows.

## 29. Effect on preliminary BTHome decision

Checkpoint 1:

BTHOME = GO_WITH_GATES

Checkpoint 2 does not downgrade that decision.

It strengthens the architecture gate:

BTHOME = GO_WITH_GATES

New/confirmed gates:

- BLUETOOTH-ADAPTER-01 lease-mode contract;
- managed sidecar lifecycle availability from #543;
- Windows adapter-affinity limitation explicitly accepted or solved;
- container topology approved;
- Windows/Linux real adapter qualification;
- StableDeviceIdentity gate from Checkpoint 1;
- transient-event contract from #546.

## 30. Next checkpoint

After a new SIGA only:

CHECKPOINT 3

Research and document:

- scale/product qualification targets;
- diagnostics details;
- L0/L1/L2/L3/L4 validation plan;
- concrete hardware lab matrix;
- legal/license/trademark boundaries;
- current hardware availability;
- final architecture risks.

Do not execute Checkpoint 3 in this session.

## 31. Checkpoint declarations

DOCS_ONLY
NO PRODUCT CODE CHANGED
NO HOST RESOURCE CODE CHANGED
NO BLUETOOTH IMPLEMENTATION
NO DEPENDENCY CHANGED
NO CI CHANGED
NO HA INTERNALS CHANGED
HA = HIGH AVAILABILITY
HAB = HOME ASSISTANT BRIDGE
NO MERGE PERFORMED
