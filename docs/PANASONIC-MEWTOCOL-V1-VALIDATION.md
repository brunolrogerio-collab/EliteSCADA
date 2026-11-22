# Panasonic MEWTOCOL-COM v1 validation record

This record follows the accepted research review in [#553](https://github.com/brunolrogerio-collab/EliteSCADA/issues/553#issuecomment-6027817211). It covers the implementation lane only. Product compatibility remains bounded by the selected profile and awaits human L4 evidence.

## Protocol and profile boundary

- Wire protocol: classic ASCII MEWTOCOL-COM.
- Driver types: `panasonic.mewtocol.tcp` and `panasonic.mewtocol.serial`.
- Transports: TCP and the existing Host Serial coordinator from #469.
- Initial profiles: bounded FP0R F32, FP-XH C14 common subset, and current FP7 R-series Classic COM subset.
- Values: Boolean, UInt16, and Int16. Multiword, string, timer/counter value areas, MEWTOCOL7-COM, MEWTOCOL-DAT, FP-X legacy, and FP7 MC are outside v1.
- The codec is maintained in EliteSCADA and adds no production package dependency.

The address/profile limits and conservative request limits are encoded in `PanasonicMewtocolFamilyCapabilities`. They are derived from the Panasonic manuals listed by the accepted #553 research. The bounded FP7 profile is specifically Classic COM; it does not establish model or firmware compatibility.

### Frozen address and access table

| Area | FP0R F32 | FP-XH C14 common | FP7 R-series Classic COM | Access |
| --- | --- | --- | --- | --- |
| X / Y | `0..109F` | `0..109F` | `0..511F` | X read-only; Y writable |
| R | `0..255F` | `0..255F` | `0..999F` | Read/write |
| L | `0..127F` | `0..127F` | `0..999F` | Read/write |
| T | `0..1007` | `0..1007` | `0..4095` | Read-only |
| C | `1008..1023` | `1008..1023` | `0..1023` | Read-only |
| WX / WY | `0..109` | `0..109` | `0..511` | WX read-only; WY writable |
| WR | `0..255` | `0..255` | `0..2047` | Read/write |
| WL | `0..127` | `0..127` | `0..1023` | Read/write |
| DT | `0..9999` | `0..9999` | `0..9999` | Read/write |
| LD | `0..255` | `0..255` | `0..9999` | Read/write |

X/Y/R/L contact notation uses a decimal prefix and a hexadecimal final nibble. T/C and all word indexes are decimal. The conservative COM command limits are 24 words for standard reads/writes, 509 words for expanded reads, 507 words for expanded writes, and 8 sparse contacts per RCP. Standard frames are capped at 118 characters; expanded frames at 2048. Expanded mode is disabled for FP0R F32.

## Gate disposition

| Gate | Evidence in this lane | Disposition |
| --- | --- | --- |
| L0 | `PanasonicMewtocolProtocolTests`: address/profile bounds, mixed-radix contacts, standard frame/BCC vector, malformed response payload, transfer limits, UInt16/Int16 bounds/transforms, and contiguous planner batching. | Local focused T0 passed on .NET SDK 10.0.400; exact-head T1 remains required. |
| L1 | `PanasonicMewtocolProtocolTests`: independent fake TCP peer and scripted #469 Host Serial transport; timeout, lease reconnect, recovery, and no blind retry assertions. | Local focused T0 passed on .NET SDK 10.0.400; exact-head T1 remains required. |
| L2 | No independently maintained Panasonic software peer with a verified MEWTOCOL-COM endpoint is available in this lane. Fake peers establish L1 only; FPWIN is not treated as an external wire peer without endpoint evidence. | `SKIP_WITH_REASON` accepted by Main for this code PR. |
| L3 | `RuntimeCatalogAndEngineeringToolingRegisterBothTransportTypes` covers both TCP/Host Serial descriptors and the canonical Engineering tooling registry; `HostSerialTransportUsesCoordinatorAndDoesNotBlindRetryAfterTimeout` exercises the #469 serial path. `Coordinator_ActivatesCanonicalTagReadsWritesAndPointReadWithSharedDiagnostics` round-trips Data Source/TAG through `SaveCurrentDerivedAsync` and `PublishRevisionAsync`, activates via `PublishedRuntimeActivationService`, exercises `Runtime.WriteAsync(TAG)`, #500 diagnostics and transient PointRead, then publishes and activates revision 2. Coordinator lifecycle coverage includes staged input acquisition with pre-commit write/event fencing, failed-candidate rollback, and polling beyond a short activation request token. Panasonic lifecycle tests cover zero Standby communication, promotion/demotion, and visible cleanup/Dispose failure. | Local focused T0 passed; exact-head T1 on the refreshed branch remains required. The persistence test uses an in-memory store fixture and leaves production Save/Publish authority in the existing Engineering workflow. |
| L4 | Physical PLC, exact model/firmware, partner disclosure and installed stable EliteSCADA release evidence are not available in this code lane. | Deferred until after Wave 16 as directed by #570. No public model/firmware compatibility claim is made. |

## Runtime safety behavior

- Standby does not connect, poll, or write. Runtime effect authority controls promotion and demotion through the canonical lifecycle.
- A transport/protocol failure after possible write dispatch surfaces an `Unknown` outcome. The driver sends no blind retry or replay.
- A MEWTOCOL acknowledgement means command handling only; later polling supplies process observations.
- `DisposeAsync` is attempted after a stop failure, and cleanup errors remain visible.

## Execution environment

The local workspace has .NET SDK 10.0.400. Focused T0 command:

```text
dotnet test tests/Scada.Drivers.Tests/Scada.Drivers.Tests.csproj --filter 'FullyQualifiedName~PanasonicMewtocolProtocolTests|FullyQualifiedName~EngineeringRuntimeCoordinatorTests' --no-restore -m:1
```

Result: **33 passed / 0 failed / 0 skipped** (24 Panasonic protocol tests and 9 Runtime coordinator tests). This was a local focused run; the refreshed published branch still requires its exact-head `DRIVER_PROTOCOL` T1. Broad CI is outside this lane's request.
