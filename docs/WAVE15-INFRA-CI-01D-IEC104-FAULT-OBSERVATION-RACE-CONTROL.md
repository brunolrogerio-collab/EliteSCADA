# Wave 15 — INFRA-CI-01D IEC-104 Fault-Test Observation Race Control

> GitHub live is the sole authority.
> This is a test-infrastructure reliability closeout. It does not authorize an IEC-104 product/driver redesign.

`CONTROL_BRANCH: coord/w15-infra-ci-01d-control`

`MAIN_ORDER_REV: 0001`

`STATE: PREPARED / QUEUED_AFTER_ACTIVE_SCRIPT_CODEX / NO_MUTATION`

`ORDER_ID: INFRA-CI-01D-IEC104-FAULT-OBSERVATION-RACE-V1`

`EXECUTOR: SEQUENTIAL CODEX`

`ACTIVATION_BASE: MUST_BE_CURRENT_wave15/corrections-integration_HEAD_AT_ACTIVATION`

`PLANNED_WORK_BRANCH: work/w15-infra-ci-01d-iec104-fault-observation-race`

`TARGET: wave15/corrections-integration`

`VALIDATION_PROFILE: FOUNDATION_LIFECYCLE`

## 1. Trigger

DEV-LICENSING-UX PR #345, exact old candidate head
`cdf572d644417fe83aee3003a3da3fe171d7ada3`, ran natural T1
`36067628680`.

The run reached real product evidence:
- classifier — SUCCESS;
- Common sanity — SUCCESS;
- Web semantic build — SUCCESS;
- focused Chromium — SUCCESS;
- focused .NET — 692/693.

Only failing test:

`Scada.Drivers.Tests.Iec104TcpFaultInjectionTests.Adapter_OutOfOrderIFrameFaultsBeforePublishingAsdu`

Observed failure:
- test had already observed `ProtocolErrors >= 1`;
- immediate next assertion expected `IsConnected == false`;
- actual `IsConnected == true`.

## 2. Non-causality

Main proved byte-identical lineage between FC0-A release base
`e3ed5138369c576549cb58a7aff9783792f322d3`
and Licensing head
`cdf572d644417fe83aee3003a3da3fe171d7ada3`:

- `tests/Scada.Drivers.Tests/Iec104TcpFaultInjectionTests.cs`
  blob `b9afd137678d225182de2288e879afbacef2579f`;
- `src/Scada.Drivers/Iec60870/Iec104TcpClientAdapter.cs`
  blob `18c836aa7a6935121104020aa63c50966475b2b8`;
- `src/Scada.Drivers/Iec60870/Iec104SequenceState.cs`
  blob `7c0f66374e679faf181f98504f12946e81a6e388`.

No Licensing candidate file participates in this failure.

Classification:

`IEC104_TEST_OBSERVATION_RACE / NOT_LICENSING_CAUSAL / SHARED_TEST_INFRA_DEFECT`

## 3. Proven race

The exact existing code ordering is sufficient to explain the failure.

Test:
1. starts the out-of-order I-frame server;
2. waits with `WaitUntilAsync(() => diagnostics.ProtocolErrors >= 1)`;
3. immediately reads diagnostics;
4. asserts `IsConnected == false`.

Adapter receive-loop catch:
1. catches `Iec104ProtocolException`;
2. increments `_protocolErrors`;
3. then calls `SignalSessionFailure(ex)`.

`SignalSessionFailure`:
1. increments `_sessionFailures`;
2. writes `_lastFailure`;
3. writes `_connected = 0`;
4. writes `_dataTransferStarted = 0`;
5. closes/cancels the session.

Therefore the test wait predicate can become true between adapter steps 2 and 3. The subsequent snapshot may truthfully expose:
- ProtocolErrors = 1;
- IsConnected = true;
- SessionFailures not yet advanced.

That transient is not a product contract violation; it is an insufficient completion predicate in the asynchronous fault-injection test.

## 4. Relationship to prior IEC-104 CI history

FC0-A CI #1568 previously hit a different test from the same file:
`Adapter_T2FlushesPendingReceiveAcknowledgementWithoutFaultingSession`.

That event was diagnosed as a known T2 timing transient and one same-SHA rerun was allowed.

01D is different:
- the exact root cause is now directly visible in the test/adapter ordering;
- do not establish another permanent “rerun until green” policy;
- stabilize the test's completion synchronization instead.

## 5. Authorized correction

Test-only by default.

Primary authorized file:
`tests/Scada.Drivers.Tests/Iec104TcpFaultInjectionTests.cs`

Required minimal correction:
- wait for the **complete session-failure observation**, not merely the first diagnostic counter that is incremented before failure completion;
- a valid completion predicate should require the relevant protocol error plus the failure/disconnected state required by the assertions, e.g. ProtocolErrors >= 1 AND SessionFailures >= 1 AND !IsConnected;
- after that completion barrier, retain all semantic assertions:
  - disconnected;
  - exactly one protocol error;
  - exactly one session failure;
  - zero ASDUs published;
  - failure text contains the sequence diagnostic.

If the executor finds a better existing deterministic session-completion signal, it may use it instead, provided no product behavior is weakened.

Add a repeated/stress regression for this exact fault path if practical so the old observation race is likely to reproduce on RED and cannot silently return.

## 6. Forbidden

Do not:
- modify IEC-104 production code solely to make the old wait predicate pass;
- reorder production diagnostic state without a separately proven product defect;
- add sleeps;
- increase arbitrary timeout values as the fix;
- disable parallelism;
- skip/exclude the test;
- weaken zero-ASDU/fail-closed assertions;
- use blind reruns as acceptance;
- touch Licensing/Authority/Editor/FND product code;
- self-merge.

## 7. Activation / queue

The sequential CODEX executor is currently assigned to:

`ROUTE-SEQUENTIAL-CODEX-TO-SCRIPT-ENGINEERING-25`

Do not preempt it.

01D is queued immediately after the active Script validation because:
- it is small/test-only;
- it affects shared T1 reliability;
- Editor is already Main-accepted and queued, but should consume a stabilized shared gate.

At activation Main must:
1. resolve the active Script CODEX handoff;
2. revalidate current `wave15/corrections-integration` HEAD;
3. create `work/w15-infra-ci-01d-iec104-fault-observation-race` from that exact current HEAD;
4. record exact SHA/tree here;
5. switch state to ACTIVE;
6. route sequential CODEX explicitly.

## 8. Validation

Required evidence:
1. exact target-base diff is test-only;
2. exact failing test passes repeatedly;
3. adjacent `Iec104TcpFaultInjectionTests` pass;
4. full `Scada.Drivers.Tests` pass at least once on the exact candidate;
5. natural Wave 15 T1 green on exact candidate;
6. Main review;
7. protected merge;
8. exact post-merge relevant gate is green.

## 9. Return

Return:

`INFRA-CI-01D CODEX -> MAIN COORDINATOR — CANDIDATE HANDOFF`

Include:
- exact activation base -> candidate SHA/tree;
- changed files;
- before/after synchronization model;
- repeated focused results;
- full Drivers result;
- natural T1;
- explicit no production-code mutation;
- no merge/freeze authority.
