# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live coordination board. GitHub branch/PR/head/CI state is operational truth; if this file briefly lags GitHub, GitHub wins and the coordinator reconciles it.

**Coordination protocol introduced:** 2026-08-26  
**Last coordinator synchronization:** 2026-08-27

## Permanent `siga` protocol

Before any action, every fixed EliteSCADA chat rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, this file, and every document listed in its current `MustReadSpecific`. Then it verifies real branch, PR/head and CI state.

Workers never choose a new task, alter `main`, merge their own PR, work another DEV branch, or broaden their assignment. `WAIT_FOR_COORDINATOR` means stop after delivery.

Repository terminology:

- **MERGED** = official `main` state.
- **IMPLEMENTED IN PR** = functional implementation exists only in an open branch/PR.
- **RESEARCH IN PR** = research/specification exists only in an open PR and is not product implementation.
- **SPECIFIED / NOT IMPLEMENTED** = documented product intent without merged implementation.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`

**CurrentTask:** Continue Interface Product Development after the first integrated checkpoint

**Branch:** `main`

**Status:** `FIRST CHECKPOINT MERGED — CONTINUE_COORDINATION`

**Objective:**

Keep the merged first interface checkpoint stable and continue coordination of the active Interface Product Development block. Review any completed future-protocol research PRs as research-only inputs, preserve product architecture, and decide subsequent interface integration work from current roadmap/repository facts. Do not silently advance production protocol implementation or the deferred Windows validation package.

**AllowedScope:** coordinator-owned shared/central frontend shell/routing, `main.tsx`, `AppNavigation.tsx`, global/interface CSS, `EngineeringApp.tsx`, central localization/integration, browser tests, CI, assignment board, roadmap/handoff documentation, worker integration and merge/reconciliation work.

**ForbiddenScope:**

- no known-failing merge;
- no force-reset/discard of worker commits;
- no new production MQTT/OPC UA/BACnet/S7/Allen-Bradley/Driver Module runtime while the product gate remains closed;
- no completion/handoff of the provisional Windows validation package unless reprioritized;
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
- Interface DEV 3 PR #59: **MERGED** (`UserSessionMenu`).
- Interface DEV 1 PR #60: **MERGED** (`EngineeringEntityBrowser`).
- Interface DEV 2 PR #61: **MERGED** (`RuntimeOperationsOverview`).
- Coordinator PR #58: **MERGED** as `f3cc82f0d45a9f0162105b57ae6c42f643af6160` after exact-head CI #378 green on `af98359c41a432ea34635c10024cf459c453d1eb`.
- PR #58 integrated the persistent localized product shell, authenticated session menu, Runtime operational overview, Engineering Data Source/TAG entity browsing and Chromium integration coverage while preserving Preview/Apply/CAS semantics.
- `integration/interface-validation-preview`: **PARKED / NO PR / DO NOT MERGE YET**.
- PR #53 and PR #54 remain delivered research inputs, not production implementations.
- Future protocol research is authorized; production protocol implementation remains postponed.

**Dependencies:**

- canonical Engineering remains authoritative;
- research may reduce future uncertainty but may not register production Data Sources or alter active runtime composition;
- graphical HMI editor remains blocked by the Script/visual prerequisite chain;
- Windows validation packaging resumes only after the interface reaches a materially useful validation state;
- production additional drivers/protocols remain postponed until the product gate is reopened.

**NextActions on coordinator `siga`:**

1. verify current `main`, open PRs, research branch heads and exact-head CI;
2. inspect post-merge CI for the latest `main` head and fix any regression before new integration;
3. review/merge completed research PRs only when exact-head green and semantically sound, retaining **RESEARCH ONLY / PRODUCTION NOT IMPLEMENTED** classification;
4. continue Interface Product Development from `docs/INTERFACE-DEVELOPMENT.md` and `docs/ROADMAP.md` without regressing the merged first checkpoint;
5. if a new worker/interface split is needed, record it here before a DEV starts it;
6. keep the deferred Windows package and production external protocols behind their product gates.

**AfterCompletion:** `CONTINUE_COORDINATION`

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** MQTT industrial Data Source/driver architecture research

**Branch:** `research/mqtt-industrial-driver`

**Status:** `ASSIGNED — RESEARCH ONLY`

**PullRequest:** none observed at last coordinator synchronization; GitHub wins if this changes.

**Objective:**

Produce an EliteSCADA-specific research/specification spike for a future MQTT Data Source/driver. Define how MQTT fits Data Source -> TAG -> EventBus/Gateway without implementing production runtime. Raw MQTT and Sparkplug B must remain explicitly separated.

**AllowedScope:** exactly one primary research document under `docs/research/mqtt/**`, optional small research-only supporting material there, read-only inspection of EliteSCADA contracts, and current official MQTT/OASIS/Eclipse Sparkplug/library/broker documentation.

**ForbiddenScope:** production source/package/lockfile changes; `Program.cs`; DI/DriverHost/runtime composition; Engineering schema/contracts; API/frontend/workflows; registering or implementing an MQTT Data Source/runtime; changing `main`; self-merge.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`
- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`

**CompletionCriteria:**

1. MQTT 3.1.1/5.0 first-target and compatibility strategy;
2. endpoint/TLS/mTLS/user-secret-ref/client-ID/session/keepalive/reconnect configuration without plaintext secrets;
3. QoS, duplicate/order behavior, retained/LWT/Clean Start/session expiry/topic aliases/subscription options and explicit distinction from TAG quality;
4. Topic Filter -> TAG mapping, wildcard/payload extraction/timestamp policy and writable publish semantics;
5. honest discovery/import UX with no fabricated OPC-UA-style browse;
6. common diagnostics, stale/data-age and independent multi-broker behavior;
7. strict raw MQTT versus Sparkplug B analysis including Birth/Death/metrics/aliases/sequence/state;
8. current .NET client and broker/library/license comparison without adding dependencies;
9. Gateway interaction and multi-Data-Source isolation;
10. CI/broker/lab matrix including reconnect, retained/session recovery, duplicates and security failures;
11. staged production slices plus `INTEGRATION REQUIRED` items;
12. open Draft PR as **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**, include exact-head CI evidence, then stop.

**ExpectedPrimaryDeliverable:** `docs/research/mqtt/MQTT-INDUSTRIAL-DRIVER-RESEARCH.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** Allen-Bradley EtherNet/IP + CIP / Logix driver architecture research

**Branch:** `research/allen-bradley-ethernet-ip`

**Status:** `ASSIGNED — RESEARCH ONLY`

**PullRequest:** none observed at last coordinator synchronization; GitHub wins if this changes.

**Objective:**

Produce a rigorous future-driver research spike for Rockwell Automation / Allen-Bradley Logix-family controllers, centered on EtherNet/IP + CIP SCADA/HMI tag access. Distinguish standard CIP from Rockwell/Logix symbolic-tag services and do not conflate generic implicit I/O with the intended SCADA driver.

**AllowedScope:** one primary research document under `docs/research/allen-bradley/**`, optional research-only supporting material there, read-only EliteSCADA inspection, current ODVA/Rockwell public documentation and library/license sources.

**ForbiddenScope:** production runtime/source/package changes; `Program.cs`; DI/DriverHost; Engineering schema/contracts; API/frontend/workflows; destructive controller engineering; registering a production Allen-Bradley Data Source; `main`; self-merge.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/S7-ISO-CONNECTION.md`
- `docs/research/s7/S7-ISO-CONNECTION-RESEARCH.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`

**CompletionCriteria:**

1. initial ControlLogix/CompactLogix target and explicit classification of Micro800/legacy families;
2. connected/unconnected explicit CIP messaging versus implicit I/O and bounded first scope;
3. endpoint/backplane/slot/bridge routing semantics;
4. controller/program symbolic TAGs, arrays, UDTs, BOOL packing, strings and common Logix types;
5. External Access/constant/writeability fail-closed behavior;
6. browse/tag-list constraints and stable EliteSCADA binding identity;
7. L5X/L5K/import investigation compatible with Preview/Apply, without runtime Studio 5000 dependency;
8. connection/session limits, request sizing, batching, fragmentation, scan/reconnect strategy;
9. CIP Security/FactoryTalk implications and honest unsupported diagnostics;
10. candidate library/license/native-packaging/maintenance comparison;
11. common diagnostics and independent multi-controller behavior;
12. software/emulation plus real CompactLogix/ControlLogix acceptance strategy;
13. explicit exclusion of destructive/safety controller engineering;
14. open Draft PR as **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**, include exact-head CI evidence, then stop.

**ExpectedPrimaryDeliverable:** `docs/research/allen-bradley/ALLEN-BRADLEY-ETHERNET-IP-CIP-RESEARCH.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`

**CurrentTask:** BACnet/IP + BACnet Secure Connect driver architecture research

**Branch:** `research/bacnet-ip-secure-connect`

**Status:** `ASSIGNED — RESEARCH ONLY`

**PullRequest:** none observed at last coordinator synchronization; older research PR #54 remains separate and unchanged.

**Objective:**

Prepare the future BACnet direction with BACnet/IP first and forward compatibility for BACnet/SC, object/property semantics and industrial-quality discovery/import/diagnostics.

**AllowedScope:** one primary research document under `docs/research/bacnet/**`, optional research-only support there, read-only EliteSCADA inspection, current ASHRAE/BACnet International public material and candidate library/license documentation.

**ForbiddenScope:** production BACnet source/package/runtime changes; `Program.cs`; DI/DriverHost; Engineering schema/contracts; API/frontend/workflows; production BACnet Data Source registration; proprietary reverse engineering presented as standard; `main`; self-merge.

**MustReadSpecific:**

- `docs/COMMUNICATION-DRIVER-DIAGNOSTICS.md`
- `docs/TAG-GATEWAY.md`
- `docs/OPC-UA.md`
- `docs/research/opc-ua/OPC-UA-DISCOVERY-IMPORT-RESEARCH.md`
- `docs/ADR-004-ENGINEERING-IMPORT-EXPORT.md`

**CompletionCriteria:**

1. BACnet/IP first-driver scope and explicit BACnet/SC/MS/TP classification;
2. Who-Is/I-Am, device identity, networks/routers and BBMD/Foreign Device behavior;
3. canonical Device Instance + Object Identifier + Property Identifier binding identity;
4. ReadProperty/ReadPropertyMultiple/WriteProperty, COV, polling fallback, segmentation/APDU and resubscribe behavior;
5. Present_Value plus Status_Flags/Reliability/Out_Of_Service/Units quality semantics;
6. explicit write priority 1..16/relinquish fail-closed behavior;
7. browse/import/proprietary-object visibility and Preview/Apply candidate handling;
8. common diagnostics, broadcast limitations and multi-Data-Source isolation;
9. BACnet/SC TLS/WebSocket/certificate trust and hub/failover direction using protected references;
10. current library/license/IP/COV/segmentation/BACnet-SC comparison;
11. simulator/open-stack plus multi-vendor real-device/BBMD/BACnet-SC interoperability strategy;
12. staged production slices and `INTEGRATION REQUIRED` dependencies;
13. open Draft PR as **RESEARCH IN PR / PRODUCTION NOT IMPLEMENTED**, include exact-head CI evidence, then stop.

**ExpectedPrimaryDeliverable:** `docs/research/bacnet/BACNET-IP-SC-RESEARCH.md`

**AfterCompletion:** `WAIT_FOR_COORDINATOR`
