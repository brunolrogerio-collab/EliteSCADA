# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26  
**Last coordinator synchronization:** 2026-08-27

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies the real assigned branch, PR/head and CI.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH IN PR** = research/specification exists only in an open PR and is not product implementation.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Integrate the first Interface Product Development checkpoint

**Branch:** `main` + `feature/interface-product-development`

**Status:** `PAUSED BY PRODUCT OWNER UNTIL NEXT siga`

**Objective:**

Turn the merged worker UI primitives plus the coordinator product shell into one coherent industrial application experience across Runtime, Engineering and Audit. The product owner explicitly paused continuation of this coordinator implementation until giving the next `siga`, while authorizing isolated research spikes for future protocol work in parallel.

**AllowedScope:** coordinator-owned shared/central frontend shell/routing, `main.tsx`, `AppNavigation.tsx`, global/interface CSS, `EngineeringApp.tsx`, central localization/integration, browser tests, CI, assignment board, roadmap/handoff documentation and worker integration.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no new production MQTT/OPC UA/BACnet/S7/Allen-Bradley/Driver Module runtime during this block;
- no completion/handoff of the provisional Windows presentation package unless reprioritized;
- no frontend-only security decisions;
- no private Engineering truth;
- no production graphical Screen/Popup/Dynamo editor or Python engine/editor ahead of the locked prerequisite chain.

**MustReadSpecific:**

- `docs/INTERFACE-DEVELOPMENT.md`
- `docs/INTERFACE-VALIDATION-MILESTONE.md`
- `docs/PYTHON-SCRIPTING-AND-VISUAL-RUNTIME.md`
- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/INTERNAL-MEMORY-TAGS.md`

**ObservedGitHubState:**

- Internal Memory: **MERGED / COMPLETE** through PR #49.
- TAG Gateway: **MERGED / COMPLETE** through PRs #50 and #55.
- Common communication diagnostics: **MERGED / COMPLETE** through PRs #56 and #57.
- Engineering Schema: **v9**.
- Interface DEV 3 PR #59 **MERGED** as `b0b58964f119f83356cf2edc8fecf5939fb905da`; CI #363 green.
- Interface DEV 1 PR #60 **MERGED** as `a7e6105fb65079ad1af8bcb56f8484225ff3dc8c`; CI #359 green.
- Interface DEV 2 PR #61 **MERGED** as `49c9e7261d63047b601f4b3c4f6e788168c8ee5c`; CI #360 green.
- Coordinator PR #58 remains Draft/Open on `feature/interface-product-development`; CI #357 had Web/backend/tests/smoke green and Chromium failure. It remains unmerged and must be reconciled/integrated only after the product owner sends the next `siga` here.
- `integration/interface-validation-preview` remains **PARKED / NO PR / DO NOT MERGE YET**.
- PR #53 and PR #54 remain delivered research inputs, not production implementations.
- Future protocol research is explicitly authorized now, but production protocol implementation remains postponed.

**Dependencies:**

- canonical Engineering remains authoritative;
- research may reduce future uncertainty but may not register production Data Sources or alter active runtime composition;
- graphical HMI editor remains blocked by the Script/visual prerequisite chain;
- Windows validation packaging resumes after the interface reaches a materially useful validation state;
- production additional drivers/protocols remain postponed until the product gate is reopened.

**NextActions after next coordinator `siga`:**

1. reconcile `feature/interface-product-development` with current `main` without discarding PR #58 shell work;
2. integrate merged `UserSessionMenu` into the product shell;
3. integrate merged `EngineeringEntityBrowser` into Engineering without weakening Preview/Apply/CAS semantics;
4. integrate merged `RuntimeOperationsOverview` while preserving the process demo;
5. normalize locale/visual behavior and extend Chromium coverage;
6. run full CI and merge only a reviewed green current head.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** MQTT industrial Data Source/driver architecture research

**Branch:** `research/mqtt-industrial-driver`

**Status:** `ASSIGNED — RESEARCH ONLY`

**PullRequest:** none yet

**Objective:**

Produce an EliteSCADA-specific research/specification spike for a future MQTT Data Source/driver, comparable in rigor to the merged OPC UA and Siemens S7 research. Define how MQTT fits the existing Data Source -> TAG -> EventBus/Gateway architecture without implementing production runtime. Keep raw MQTT and Sparkplug B explicitly separated.

**AllowedScope:**

- exactly one primary research document under `docs/research/mqtt/**` plus small supporting research-only diagrams/tables in that folder if genuinely needed;
- inspect current EliteSCADA source/contracts read-only;
- current official MQTT/OASIS, Eclipse Sparkplug and relevant library/broker documentation;
- compare candidate .NET client libraries and test brokers without adding dependencies.

**ForbiddenScope:**

- production source code, package references or lockfiles;
- `Program.cs`, DI, DriverHost/runtime composition, Engineering schema/contracts, API/frontend/workflows;
- registering an MQTT Data Source;
- implementing MQTT connection/publish/subscribe runtime;
- changing `main` or merging own PR.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`

**ResearchQuestions / CompletionCriteria:**

1. define first production target between MQTT 3.1.1 and MQTT 5.0 while preserving compatibility strategy;
2. define broker endpoint/TLS/mTLS/username-secret-reference/client-ID/session/keepalive/reconnect configuration without plaintext secrets;
3. analyze QoS 0/1/2, duplicate delivery, ordering, retained messages, Last Will, Clean Start/session expiry, topic aliases and subscription options, and state clearly what does **not** equal SCADA TAG quality;
4. define Topic Filter -> TAG mapping, wildcard behavior, payload extraction for scalar/text/JSON/binary, timestamp/source-time policy and writable TAG -> publish semantics;
5. define browse/discovery UX honestly: MQTT has no OPC-UA-style address-space browse; research observed-topic/topic-template/import approaches without fabricating standard discovery;
6. define connection diagnostics using the common Data Source contract and stale/data-age semantics;
7. separate raw MQTT from Sparkplug B; analyze Sparkplug Birth/Death certificates, metrics, aliases, sequence/state and whether Sparkplug should be a mode/module rather than silently changing raw MQTT semantics;
8. compare current .NET MQTT client candidates, licenses, maintenance, MQTT 5/TLS support and packaging implications; recommend laboratory candidates only, not a production dependency by decree;
9. define multi-broker/multi-Data-Source behavior and Gateway interaction;
10. define software CI plus broker integration/lab test matrix, including reconnect, retained/session recovery, QoS duplicates and security failures;
11. record explicit first implementation slices and `INTEGRATION REQUIRED` items;
12. open Draft PR as **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**, include exact-head CI evidence, then stop.

**ExpectedPrimaryDeliverable:** `docs/research/mqtt/MQTT-INDUSTRIAL-DRIVER-RESEARCH.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Allen-Bradley EtherNet/IP + CIP / Logix driver architecture research

**Branch:** `research/allen-bradley-ethernet-ip`

**Status:** `ASSIGNED — RESEARCH ONLY`

**PullRequest:** none yet

**Objective:**

Produce a rigorous future-driver research spike for Rockwell Automation / Allen-Bradley Logix-family controllers, centered on EtherNet/IP + CIP and SCADA/HMI tag access. Research must distinguish standard ODVA EtherNet/IP/CIP behavior from Rockwell/Logix-specific symbolic-tag services and must not imply that EtherNet/IP generic I/O equals the intended SCADA driver.

**AllowedScope:**

- exactly one primary research document under `docs/research/allen-bradley/**` plus research-only supporting material there if necessary;
- inspect current EliteSCADA contracts read-only;
- use current ODVA and Rockwell official public documentation plus candidate library documentation/license sources;
- compare library/test approaches without adding dependencies.

**ForbiddenScope:**

- production runtime/source/package changes;
- `Program.cs`, DI, DriverHost, Engineering schema/contracts, API/frontend/workflows;
- controller program download/upload, mode RUN/STOP changes, firmware, safety programming or other destructive engineering operations;
- registering a production Allen-Bradley Data Source;
- changing `main` or self-merging.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/S7-ISO-CONNECTION.md`
- `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`

**ResearchQuestions / CompletionCriteria:**

1. define initial controller family target, prioritizing ControlLogix/CompactLogix and explicitly classify Micro800/legacy PLC families rather than pretending one path fits all;
2. explain EtherNet/IP/CIP messaging classes relevant to SCADA: explicit connected/unconnected messaging versus implicit I/O, and recommend a bounded first-driver scope;
3. define endpoint/routing path semantics including chassis/backplane/slot/bridge routes where applicable;
4. research controller-scoped/program-scoped symbolic TAG access, arrays, structures/UDTs, BOOL packing, strings and common Logix primitive types;
5. define External Access/constant/writeability handling and fail-closed writes;
6. research symbolic browse/tag-list capabilities, exact service/public-documentation constraints and how stable EliteSCADA binding identity should survive browse/import refresh;
7. research L5X/L5K or other supported Rockwell export/import paths for Engineering candidate import, licensing/tool dependencies and Preview/Apply flow; do not make runtime depend on Studio 5000;
8. analyze connection/session limits, packet/request sizing, batching/multi-service packets, fragmentation and scan/reconnect strategy;
9. cover CIP Security / FactoryTalk policy implications and define honest unsupported/secured-controller diagnostics for first releases;
10. compare current candidate libraries such as libplctag/.NET wrappers and managed EtherNet/IP/CIP alternatives, including licenses, native packaging, maintenance and Logix feature coverage; recommend lab candidates only;
11. define diagnostics mapping to the common Data Source contract and independent multi-controller behavior;
12. define test plan using software/emulation where legally/practically available plus real CompactLogix/ControlLogix hardware acceptance; identify what cannot be credibly CI-only;
13. explicitly exclude destructive controller engineering/safety operations from the SCADA driver;
14. open Draft PR as **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**, include exact-head CI evidence, then stop.

**ExpectedPrimaryDeliverable:** `docs/research/allen-bradley/ALLEN-BRADLEY-ETHERNET-IP-CIP-RESEARCH.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** BACnet/IP + BACnet Secure Connect driver architecture research

**Branch:** `research/bacnet-ip-secure-connect`

**Status:** `ASSIGNED — RESEARCH ONLY`

**PullRequest:** none yet for this task; older research PR #54 remains separate and unchanged

**Objective:**

Use the additional research capacity authorized by the product owner to prepare the future BACnet direction. Focus on BACnet/IP first while designing forward compatibility for BACnet Secure Connect (BACnet/SC), object/property semantics and industrial-quality discovery/import/diagnostics.

**AllowedScope:**

- exactly one primary research document under `docs/research/bacnet/**` plus research-only support files there if necessary;
- inspect current EliteSCADA contracts read-only;
- use current ASHRAE/BACnet International public material and candidate library documentation/licenses;
- compare implementation/test candidates without adding dependencies.

**ForbiddenScope:**

- production BACnet source/package/runtime changes;
- `Program.cs`, DI, DriverHost, Engineering schema/contracts, API/frontend/workflows;
- production BACnet/IP or BACnet/SC Data Source registration;
- proprietary-device reverse engineering presented as standard behavior;
- changing `main` or self-merging.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`

**ResearchQuestions / CompletionCriteria:**

1. define BACnet/IP first-driver scope and explicitly classify BACnet/SC and MS/TP instead of silently conflating transports;
2. research Who-Is/I-Am discovery, device instance identity, network numbers/routers and cross-subnet BBMD/Foreign Device behavior;
3. define canonical object/property binding identity using Device Instance + Object Identifier + Property Identifier, with names/descriptions as metadata rather than sole identity;
4. cover ReadProperty/ReadPropertyMultiple, WriteProperty, COV subscriptions, polling fallback, segmentation/APDU limits and reconnect/resubscribe behavior;
5. cover Present_Value, Status_Flags, Reliability, Out_Of_Service, Units and quality mapping without reducing BACnet object semantics to a naked number;
6. define write priority 1..16/relinquish behavior explicitly and fail closed rather than choosing a dangerous implicit priority;
7. research device/object browse/import UX, proprietary object/property visibility and Preview/Apply candidate handling;
8. define BACnet/IP diagnostics, broadcast/discovery limitations and multi-Data-Source isolation using the common diagnostics contract;
9. research BACnet/SC TLS/WebSocket/certificate trust, primary/failover hub behavior and how secret/certificate references fit EliteSCADA without plaintext credentials;
10. compare current .NET/C BACnet library candidates, licenses, BACnet/IP/COV/segmentation/BACnet-SC coverage and packaging implications; recommend lab candidates only;
11. define interoperability test plan using open BACnet stacks/simulators where appropriate plus real multi-vendor/BTL-class devices, BBMD and BACnet/SC scenarios;
12. record explicit production slices and `INTEGRATION REQUIRED` dependencies;
13. open Draft PR as **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**, include exact-head CI evidence, then stop.

**ExpectedPrimaryDeliverable:** `docs/research/bacnet/BACNET-IP-SC-RESEARCH.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
