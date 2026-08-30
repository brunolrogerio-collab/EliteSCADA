# IEC 60870-5-104 Acceptance Matrix Status Addendum

Date: 2026-08-30
Applies to: `IEC-60870-5-104-ACCEPTANCE-AND-INTEROPERABILITY.md`
Branch: `driver6/iec-60870-5-104`

This addendum corrects acceptance-matrix evidence states that became stale after additional Driver 06 hardening tests were added. It changes **evidence status only**. It does not mark any case `ACCEPTED`, because the .NET suite has still not been executed in the current environment.

Until the Coordinator folds these rows back into the base acceptance document, this addendum is authoritative for the cases listed below.

| ID | Corrected automated evidence | Corrected current state | Notes |
|---|---|---|---|
| `IEC104-A007` | `Iec104ReceiveQueueBoundsTests.Adapter_ReceiveQueueOverflowFaultsSessionInsteadOfDroppingProcessData` | **WRITTEN / NOT EXECUTED** | Real loopback TCP test sends 1025 valid ASDUs with no consumer against queue capacity 1024 and requires fail-closed session termination rather than silent drop. |
| `IEC104-A038` | `Iec104UnsupportedAsduIsolationTests` | **WRITTEN / NOT EXECUTED** | Unsupported Type IDs are isolated from runtime publication and Engineering candidate creation. |
| `IEC104-A091` | `Iec104ReceiveQueueBoundsTests.Adapter_ReceiveQueueOverflowFaultsSessionInsteadOfDroppingProcessData` | **WRITTEN / NOT EXECUTED** | Bounded receive-queue behavior now has a dedicated real-TCP automated case. External burst/soak evidence is still required for production acceptance. |
| `IEC104-A095` | `Iec104NonFiniteShortFloatTests` | **WRITTEN / NOT EXECUTED** | NaN/+Infinity/-Infinity are preserved as IEEE-754 values with semantic `Uncertain`; stronger IEC quality such as IV retains precedence. |

## Related implementation notes

### Receive queue

`Iec104TcpClientAdapter` uses a bounded process-data queue with capacity 1024. Queue admission failure is treated as a protocol/session failure rather than silently discarding an ASDU. External burst testing remains required because a deterministic unit/loopback case cannot establish long-duration resource behavior.

### Unsupported ASDUs

Unknown or unsupported monitored Type IDs are not automatically turned into canonical TAGs or Engineering points. This remains consistent with the Driver 06 rule that Engineering candidates are bounded evidence and canonical TAG creation requires normal Engineering authority.

### Non-finite short float

For `M_ME_NC_1` and `M_ME_TF_1`, a non-finite IEEE-754 short float is representable by the transport/decoder. The first-release policy is therefore to preserve the received value and downgrade semantic quality to `Uncertain`, rather than terminating the IEC-104 session merely because the peer sent NaN or Infinity. Existing IEC quality precedence remains authoritative.

## Execution status

As of this addendum:

- tests are present in source control;
- static review has been performed;
- no .NET 10 compiler/runtime is available in the current Driver 06 execution environment;
- no branch CI status check is attached to the current HEAD;
- therefore these rows are **not ACCEPTED** until execution evidence exists.
