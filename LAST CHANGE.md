# LAST CHANGE — EliteSCADA

Date: 2026-08-30 (BRT)

## Current checkpoint

### MERGED

**Wave 10 is CLOSED / MERGED / POST-MAIN GREEN.**

- final product merge: `15daff2cc076f46f9433812babbd5cbb4b8d9554`;
- final integration CI #873: SUCCESS;
- post-main CI #874: SUCCESS.

Wave 10 issues #149, #150, #151 and #152 are now closed as completed.

**Common seven-peer interoperability laboratory is MERGED on `main`.**

PR #173 merge:

`a08cca94795a5afa14bf8af39b8bf2c6f7df71ae`

Exact validated functional lab head:

`3ff2d6393c4e8734b4b1c08abd2bd8466f78f400`

Evidence:

- Interop Lab Smoke #42: SUCCESS;
- EliteSCADA CI #886: SUCCESS after rerunning failed Modbus timing jobs on the unchanged functional SHA;
- no product/Modbus code changed to obtain the green rerun.

Common test-only peers on main: MQTT, CIP, OPC UA, IEC-104, DNP3, Siemens S7 and BACnet/IP.

### IMPLEMENTED IN PR / ACTIVE WORK

**Shared Driver convergence is ACTIVE under issue #174.**

Coordinator branch:

`coordination/driver-convergence-v3`

Draft PR:

`#175 — Driver convergence v3 — shared host contracts`

Exact audited PR head:

`06c7d408c76926bf5d37dfec4be20ea6044f52b1`

Exact normal CI:

**EliteSCADA CI #895 — SUCCESS.**

Implemented and covered by the current shared-contract tests:

1. fail-closed communication Driver module registry keyed by stable DriverType;
2. common runtime planner/factory component registry;
3. protocol-neutral Data Source readiness contract;
4. scoped host-owned protected-material resolver/lease seam;
5. initial fail-closed shared-contract coverage.

PR #175 also contains a **partial Communication TAG binding / schema-v15 scaffold**:

- `CommunicationTagBinding` core contract;
- `TagPhysicalValueTransform` contract;
- optional `TagDefinition.CommunicationBinding`;
- optional `TagEngineeringDto.CommunicationBinding`;
- export mapping from binding `PortableAddress` into compatibility Address;
- `CommunicationTagBindingEngineeringValidator` declaring introduction at schema v15 and rejecting invalid/plaintext-secret-like data.

This scaffold is **IMPLEMENTED IN PR but NOT FUNCTIONALLY COMPLETE**. The handoff audit confirmed:

- `EngineeringExchangeService.CurrentSchemaVersion` is still 14;
- `TagEngineeringHandler.Preview` does not call the new binding validator;
- `TagEngineeringHandler.Apply` omits `dto.CommunicationBinding`, so Apply drops the rich binding;
- preview materialization paths also omit it;
- TAG CSV rich-binding fidelity is not implemented;
- no full JSON/CSV/Preview/Apply/re-export/package/revision/PostgreSQL v15 regression exists.

Therefore CI #895 proves current scaffold compatibility, **not** completion of the public schema-v15 round-trip gate.

Current Driver state at handoff audit:

- D10 MQTT: head `acd46cd9a4a49e324f2037a1994e6f579a0bae3f`, Draft #128, exact CI #865 green; broad independent broker/security/restart/freshness evidence accepted; READY FOR COORDINATOR CONVERGENCE after v15.
- D6 IEC-104: head `d597ef5ed1885b63dcd0b3568287bc1e34330bee`, Draft #146, CI #798 green; independent lib60870 L2 13/13 accepted; READY FOR COORDINATOR CONVERGENCE.
- D5 CIP: head `18ff6dc989a65c1f8b006f83c08d8394a5510914`, Draft #111, CI #785 green; independent CIP L2 accepted; READY FOR COORDINATOR CONVERGENCE.
- D9 OPC UA: head `5ce1f3c912bf3779e892fb136b51b54b0f19a5c6`, Draft #169, CI #869 green; ACTIVE product-path open62541 L2.
- D7 DNP3: head `ac0dd6944f53d19447f3353addd404c02da7249c`, Draft #108, CI #697 green; validation PR #167 remains OPEN/RED on configured Int32 -> canonical Double mismatch; ACTIVE PRODUCT FIX.
- D8 Siemens S7: head `0c37b922b44f591ebd143470abf3ebaa6b4bffae`, Draft #135, CI #789 green; ACTIVE product-path python-snap7 L2.
- D4 BACnet/IP: head `de3357750f79266e43588e7bb26d66093f8cf3d5`, Draft #109, CI #860 green; ACTIVE product-path BACpypes discovery/RP/RPM/WP/COV/recovery L2.

Repository hygiene completed during coordinator handoff:

- Wave 10 issues #149-#152 closed completed;
- validation/superseded PRs #148, #160, #161, #162, #163, #164, #165, #166 and #168 closed **unmerged**, preserving their accepted evidence/history;
- #167 remains open because DNP3 still has an unresolved product defect;
- Driver product handoff PRs and #175 remain open.

`docs/COORDINATOR-HANDOFF.md`, `docs/CHAT-WORK-ASSIGNMENTS.md`, `docs/DRIVER-AND-INTEROP-LAB-STATUS.md` and `docs/ROADMAP.md` were synchronized for the new Coordinator chat.

### SPECIFIED / NOT IMPLEMENTED

**Immediate Coordinator gate: complete canonical Engineering schema v15 before adapting MQTT.**

Required next slice on PR #175:

1. bump canonical Engineering schema to v15 while preserving <=v14 compatibility;
2. invoke `CommunicationTagBindingEngineeringValidator` in TAG Preview;
3. preserve `CommunicationBinding` through Apply and every canonical TagDefinition materialization path;
4. maintain `Address == CommunicationBinding.PortableAddress` during compatibility migration;
5. implement TAG CSV fidelity where applicable without creating a second Driver address grammar;
6. prove JSON/CSV Preview/Apply/re-export, `.escadapkg`, immutable revision and PostgreSQL persistence;
7. prove malformed rich bindings and plaintext protected material fail closed;
8. preserve `TagValueSelector` as the generic bit selector and ADR-007 transform-before-selection semantics;
9. run exact-head normal CI.

After that gate, convergence order remains:

`MQTT -> IEC-104 -> CIP -> OPC UA -> DNP3 -> Siemens S7 -> BACnet/IP`

Remaining shared Coordinator work after the binding foundation includes:

- central Runtime activation through shared registry/planner/factory;
- protected credential/certificate/private-key composition;
- installable module/catalog/loading policy;
- common namespaced operation surface where simple `WriteAsync` is insufficient;
- SourceTimestamp/ServerTimestamp/current/historical late-event policy;
- central Engineering ConnectionTest/Browse/Import/Reconcile registration and protected API/UI;
- exact integrated CI and controlled transitions to `main`.

**Wave 11 remains DEFERRED** until Driver convergence closes or product priority is explicitly changed.

## Governance / CI note

At the start of this handoff audit, GitHub reported `main` as not protected by a branch-protection rule. No repository-governance setting was changed. Treat direct mainline writes as a process risk and preserve exact-head CI discipline manually unless branch protection is separately authorized.

CI mode remains **NORMAL**. Documentation-only checkpoints may use `[skip ci]`. Normal CI, peer/tool readiness, Driver L2, licensing/conformance and L3/L4 acceptance are separate evidence claims. Never weaken a test to hide a real protocol/type defect.