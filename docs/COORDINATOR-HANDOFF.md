# COORDINATOR HANDOFF — EliteSCADA

Date: 2026-08-30 (BRT)  
Stage: **DRIVER CONVERGENCE ACTIVE / COMMON LAB MERGED / WAVE 11 DEFERRED**

This is the start-here operational checkpoint for a new Coordinator chat.

## Mandatory startup

Before planning or changing code:

1. read `PROJECT GOAL.md`;
2. read `LAST CHANGE.md`;
3. read this file;
4. read `docs/CHAT-WORK-ASSIGNMENTS.md` and `docs/DRIVER-AND-INTEROP-LAB-STATUS.md`;
5. re-fetch live `main`, issue #174, PR #175, targeted Driver heads/PRs and exact Actions state.

GitHub live state wins over stale prose. On long-lived Driver PRs, operational authority is live `head_sha` + exact Actions + current coordination documents. PR bodies remain useful historical context but may lag worker branches.

## Stable product checkpoint

Wave 10 is **CLOSED / MERGED / POST-MAIN GREEN**. Issues #149, #150, #151 and #152 are closed as completed.

The common seven-peer interoperability lab is **MERGED** through PR #173:

- main merge: `a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`;
- exact validated functional lab head: `3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`;
- Interop Lab Smoke #42: SUCCESS;
- EliteSCADA CI #886: SUCCESS after failed Modbus timing jobs were rerun on the unchanged functional SHA;
- common peers on main: MQTT, CIP, OPC UA, IEC-104, DNP3, Siemens S7 and BACnet/IP.

## Active shared integration authority

Issue: **#174 — Driver Convergence v1 — shared runtime, Engineering and mainline integration**  
Branch: `coordination/driver-convergence-v3`  
Draft PR: **#175 — Driver convergence v3 — shared host contracts**  
Exact audited PR head: `06c7d408c76926bf5d37dfec4be20ea6044f52b1`  
Exact-head normal CI: **#895 SUCCESS**

Implemented shared-host foundation on #175:

- fail-closed Driver module registry keyed by stable DriverType;
- runtime planner/factory component registry;
- protocol-neutral Data Source readiness contract;
- scoped host-owned protected-material resolver/lease seam;
- focused fail-closed shared-contract tests.

### Critical audit finding: schema v15 is only partial

PR #175 also contains a Communication TAG binding scaffold:

- `CommunicationTagBinding` exists;
- `TagPhysicalValueTransform` exists;
- `TagDefinition.CommunicationBinding` exists;
- `TagEngineeringDto.CommunicationBinding` exists;
- export mapper prefers `CommunicationBinding.PortableAddress` as compatibility Address;
- `CommunicationTagBindingEngineeringValidator` exists and declares introduction at schema v15.

But **do not treat schema v15 as implemented or accepted yet**. At audited head `06c7d408...`:

- `EngineeringExchangeService.CurrentSchemaVersion` is still `14`;
- `TagEngineeringHandler.Preview` does not invoke the new binding validator;
- `TagEngineeringHandler.Apply` does not carry `dto.CommunicationBinding` into `TagDefinition`, so Apply drops the binding;
- preview materialization paths also omit the rich binding;
- TAG CSV fidelity has not been implemented for rich binding;
- no end-to-end v15 JSON/CSV/Preview/Apply/re-export/package/revision/PostgreSQL test proves the public lifecycle.

CI #895 proves the current scaffold/build/test state, **not** completion of the v15 round-trip gate.

## Immediate next Coordinator work

Finish the schema-v15 binding slice coherently on PR #175 **before** adapting MQTT:

1. bump canonical Engineering schema to v15 while preserving <=v14 import compatibility;
2. wire `CommunicationTagBindingEngineeringValidator` into TAG Preview;
3. preserve `CommunicationBinding` in Apply and all canonical TagDefinition materialization paths;
4. keep `Address == CommunicationBinding.PortableAddress` during compatibility migration;
5. extend TAG CSV fidelity where applicable without creating another Driver address grammar;
6. prove JSON/CSV Preview/Apply/re-export, `.escadapkg`, immutable revision and PostgreSQL persistence;
7. prove malformed binding and plaintext protected material fail closed;
8. keep `TagValueSelector` as the sole generic bit selector;
9. keep ADR-007 physical transform before typed decode/bit selection;
10. run exact-head normal CI.

Then converge Drivers in evidence order:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

Protocol branches are source/evidence lines, not merge trains. Re-port/adapt narrowly against current `main`; do not merge historical Driver branch baggage wholesale.

## Driver matrix at handoff audit

| Driver | Product head | Product CI | Evidence / active gate |
| --- | --- | --- | --- |
| D10 MQTT | `acd46cd9a4a49e324f2037a1994e6f579a0bae3f` | #865 GREEN | Broad Mosquitto/HiveMQ/TLS/auth/negative-security/restart/freshness evidence green. First convergence candidate after v15. |
| D6 IEC-104 | `d597ef5ed1885b63dcd0b3568287bc1e34330bee` | #798 GREEN | lib60870 L2 #7 13/13 green. Second convergence candidate. |
| D5 CIP | `18ff6dc989a65c1f8b006f83c08d8394a5510914` | #785 GREEN | independent CIP L2 #6 green. Third convergence candidate. |
| D9 OPC UA | `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6` | #869 GREEN | Driver product-path L2 against common open62541 still active. |
| D7 DNP3 | `ac0dd6944f53d19447f3353addd404c02da7249c` | #697 GREEN | PR #167 active/red on canonical Int32 -> Double mismatch. Fix product; never relax assertion. |
| D8 Siemens S7 | `0c37b922b44f591ebd143470abf3ebaa6b4bffae` | #789 GREEN | Driver product-path L2 against common python-snap7 active. |
| D4 BACnet/IP | `de3357750f79266e43588e7bb26d66093f8cf3d5` | #860 GREEN | Driver product-path discovery/RP/RPM/WP/COV/recovery L2 active against common BACpypes peer. |

## PR hygiene completed during handoff

Closed as completed/superseded validation evidence, **without merge**:

- #148 OPC UA standalone lab, superseded by merged common lab #173;
- #160 MQTT two-broker evidence;
- #161 MQTT trusted TLS/auth evidence;
- #162 MQTT negative-security evidence;
- #163 MQTT broker-restart evidence;
- #164 MQTT live-freshness evidence;
- #165 CIP L2 evidence;
- #166 initial IEC-104 validation, superseded by #168;
- #168 final IEC-104 L2 13/13 evidence.

Keep open:

- #175 Coordinator convergence;
- Driver handoff PRs #108, #109, #111, #128, #135, #146, #169;
- #167 DNP3 validation because the product defect remains unresolved.

## Shared architectural locks

- Current `main` wins implementation conflicts; `PROJECT GOAL.md`/ADRs win locked future intent.
- Canonical Engineering remains authoritative and versioned; Preview/Apply/revisions/package fidelity are mandatory.
- No plaintext protected material in Engineering/packages.
- `Address == CommunicationBinding.PortableAddress` during v15 compatibility migration.
- Stable TAG-bit identity remains `TagId + TagValueSelector`; `.NN` is authoring/display only.
- ADR-007 byte/word transform precedes bit selection.
- Drivers never call sibling Drivers and never bypass TAG/cache/event architecture.
- Runtime readiness is Data Source/protocol readiness, not every point being Good.
- L0/L1/L2/L3/L4, normal CI, licensing and conformance are distinct claims.
- Never weaken an interoperability assertion to obtain green CI.
- Wave 11 remains deferred until Driver convergence closes or product priority is explicitly changed.

## Repository governance note

At the audit start, GitHub reported `main` as **not protected** by a branch-protection rule. Do not silently change repository governance during Driver convergence, but treat direct pushes/merges to `main` as a process risk and preserve exact-head CI discipline manually unless branch protection is separately authorized.

## Handoff rule

Before ending any Coordinator task, update `LAST CHANGE.md` with actual **MERGED / IMPLEMENTED IN PR / SPECIFIED-NOT IMPLEMENTED** state and keep `docs/ROADMAP.md` consistent. If a worker branch has moved, refresh these checkpoints only after re-reading live SHA and Actions state.