# LAST CHANGE — EliteSCADA

Date: 2026-08-31 (BRT)

## Read first

Stable product intent: [`PROJECT GOAL.md`](PROJECT%20GOAL.md)  
Operational handoff: [`docs/COORDINATOR-HANDOFF-2026-08-31.md`](docs/COORDINATOR-HANDOFF-2026-08-31.md)  
L3 release gate: [issue #180](https://github.com/brunolrogerio-collab/EliteSCADA/issues/180)

Live GitHub refs and exact-SHA Actions evidence override stale SHAs copied into prose.

## Mandatory continuity / chat replacement rule

The repository must contain enough context to resume EliteSCADA coordination safely in a new ChatGPT conversation without relying on the previous chat history.

For every material coordination cycle, decision, blocker, fix, validation run or change of next action:

1. review and synchronize both `PROJECT GOAL.md` and `LAST CHANGE.md`;
2. persist exact branch/SHA/run/issue evidence when it matters to acceptance;
3. never leave a critical decision, blocker, diagnosis or next action only in chat;
4. keep stable product/architecture intent in `PROJECT GOAL.md` and mutable operational state in `LAST CHANGE.md`;
5. when the user says `siga`, continue executing the active sequence until completion or a real external/blocking condition, while maintaining this repository checkpoint.

A new coordinator/chat must begin by reading these files and the active handoff before acting.

## Current checkpoint

- `main`: `f6210a1539741847aab8949a7e453c8cf141162d` at last live audit.
- Driver convergence v3 is **MERGED** to `main` through PR #187.
- Active L3 branch: `coordination/driver-l3-seven-protocol-lab`.
- Active L3 branch HEAD before this documentation checkpoint: `a9a6fb55cb56659e382e7d09085f63505aee27f4`.
- Issue #180: **OPEN / ACTIVE / RELEASE GATE**.
- Wave 11: **BLOCKED** until the full L3 matrix passes on one exact SHA and issue #180 is accepted and closed.

## L3 topology / acceptance authority

The integrated runtime must operate all seven communication Drivers concurrently:

1. MQTT;
2. IEC-104;
3. CIP / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO-on-TCP;
7. BACnet/IP.

The full acceptance contract, including heterogeneous TAG Gateway behavior, fault isolation, recovery, writes where supported, clean shutdown and exact-SHA CI evidence, is issue #180.

Do not weaken a 7/7 assertion to manufacture a pass.

## Current L3 failure history

### Initial BACnet runtime blocker

Original failing L3 SHA:

`65fbb6ee67040610eef4b6ef88073c38e127913b`

Actions run/job:

- run `33434301171`;
- job `99626884954`.

BACnet/IP startup failed because automatic interface selection found both `10.1.0.204` and `172.18.0.1` in CI and could not choose a broadcast/local endpoint deterministically.

### BACnet fix

Commit:

`c906f3cbbb3f0c584d19475c3dbdfbc6a84b5668` — `fix(bacnet): bind explicit local endpoint for L3`

The fix:

- adds optional `localEndpointIp` configuration;
- preserves previous behavior when omitted;
- validates IPv4 input;
- creates an explicit BACnet/IP transport when configured;
- binds the L3 laboratory peer/configuration to `127.0.0.1`.

Validation run:

- L3 run `33443796424`;
- job `99657990531`;
- Gateway slice passed;
- integrated runtime slice still failed.

### Current blocker: diagnostics 6/7 before acquisition

At `c906f3...`, the integrated test fails at the runtime diagnostics assertion:

`Assert.Equal(7, diagnostics.Count)`

Observed:

- expected communication Driver diagnostics: `7`;
- actual: `6`.

This assertion occurs **before deterministic acquisition assertions**. Therefore the current evidence does **not** prove that one protocol failed acquisition; it proves that `Coordinator.Describe()` exposes only six communication Driver diagnostics when seven are configured/expected.

The next diagnostic commit is:

`a9a6fb55cb56659e382e7d09085f63505aee27f4` — `test: diagnose missing L3 communication driver`

Its L3 run is:

- run `33446328679` (`L3 Seven-Driver Lab #13`);
- conclusion: **FAILURE**.

Current technical task is to identify the missing diagnostic/registration entry and correct the real runtime composition/diagnostic defect. Likely classes of defect to verify are duplicate-key overwrite, filtering, or a Driver not registering its diagnostic provider. Do not assume which Driver is missing without evidence.

## Exact execution order from here

1. Extract the missing Driver identity from the diagnostic run or inspect the coordinator registry/`Describe()` implementation and all seven registrations.
2. Compare newer `main` history where useful to avoid recreating an already-correct fix.
3. Apply the minimum correct production/test-fixture fix without weakening 7/7 acceptance.
4. Push one coherent L3 SHA.
5. Validate the **full** L3 workflow on that exact SHA, not only one slice.
6. If any other L3 criterion fails, diagnose and continue on the same gate.
7. Update issue #180 with exact SHA/run evidence.
8. Review/update `PROJECT GOAL.md` and `LAST CHANGE.md` again at the resulting material checkpoint.
9. Close #180 only after the complete acceptance matrix is green.
10. Only after #180 is accepted and closed may Wave 11 begin.

## MERGED

- Wave 10: merged/closed.
- Common seven-peer interoperability laboratory infrastructure: merged through PR #173.
- Driver convergence v3: merged to `main` through PR #187 at `f6210a1539741847aab8949a7e453c8cf141162d`.

## IMPLEMENTED ON ACTIVE L3 BRANCH

- dedicated seven-Driver L3 workflow;
- one-runtime integrated seven-Driver laboratory test harness;
- heterogeneous TAG Gateway L3 slice;
- deterministic BACnet/IP loopback binding for CI;
- additional diagnostic output intended to identify the missing 7th communication Driver entry.

These items are not L3 acceptance by themselves. The release gate remains the full exact-SHA workflow and issue #180.

## SPECIFIED / NOT YET ACCEPTED

- complete L3 seven-Driver integrated acceptance: **NOT YET ACCEPTED**;
- issue #180 closure: **NOT YET**;
- Wave 11 release: **NOT AUTHORIZED**.

## Resume instruction for a new chat/coordinator

Read, in order:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/COORDINATOR-HANDOFF-2026-08-31.md`;
4. issue #180;
5. `.github/workflows/l3-seven-driver-lab.yml`;
6. the latest L3 Actions run on `coordination/driver-l3-seven-protocol-lab`.

Then inspect live branch/main SHAs before any write. Continue the L3 blocker. Do **not** start Wave 11 from documentation assumptions or chat memory.
