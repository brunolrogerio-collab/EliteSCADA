# Wave 14 C03 — DNP3 unrestricted production adapter

Status: implementation spike in progress. **The commercial blocker is not removed yet.**

## Decision

EliteSCADA will keep `IDnp3MasterSession` as the vendor-neutral production boundary and will replace the restricted Step Function production implementation with an isolated native helper based on OpenDNP3 3.1.2.

The intended runtime boundary is:

`Dnp3Driver -> IDnp3MasterSession -> OpenDnp3 managed adapter -> isolated native OpenDNP3 helper -> DNP3/TCP`

The existing `dnp3py` lab remains an independent peer/outstation and is deliberately not reused as the production Master. This avoids a circular interoperability test and avoids turning EliteSCADA into the maintainer of missing dnp3py Master orchestration, command-status and variation support.

## Production candidate and provenance

OpenDNP3 3.1.2 is pinned to upstream commit `26b4c01e4839bbbda8866655e086471c4917ee53` and is Apache-2.0. Its NOTICE must be retained in distributed notices.

The exact upstream CMake dependency graph at that commit pins:

- ASIO `asio-1-16-0`, archive SHA1 `6BDD33522D5B95B36445ABB2072A481F7CE15402`, Boost Software License 1.0;
- exe4cpp commit `fb878a4de598ba9d6e4338afebf83f96e03af1b8`, archive SHA1 `18B141E8CF09DC8D28CC62DD5FA2920670D501BD`, BSD-3-Clause;
- ser4cpp commit `3c449734dc530a8f465eb0982de29165cc4e23d5`, archive SHA1 `937B759B7CC80180DA26B47037E796B59798A672`, BSD-3-Clause.

TLS is intentionally out of scope for this C03 TCP production adapter. With `DNP3_TLS=OFF`, OpenDNP3 does not link OpenSSL, so obsolete OpenSSL 1.1.1 bytes are not part of the intended Windows distribution graph.

OpenDNP3 is archived/EOL upstream. EliteSCADA therefore owns the maintenance risk of this pinned source integration. The permissive license solves the commercial distribution restriction; it does not make the upstream maintained again.

## Managed/native process boundary

`Scada.Drivers.Dnp3.OpenDnp3` owns one helper process per DNP3 session.

Rules:

- no implicit PATH lookup and no system-installed DNP3 runtime dependency;
- packaged helper path is `native/dnp3/EliteScada.Dnp3Host[.exe]` relative to the application base directory;
- `ELITESCADA_DNP3_HOST_PATH` is an explicit development/test override only;
- stdin/stdout uses a small versioned tab-delimited protocol (`V1`) so the native helper needs no JSON dependency;
- stdout is reserved for protocol messages; native diagnostics go to stderr;
- process commands are correlated by request id and are never retained or replayed across reconnect/host exit;
- unexpected helper exit faults the session and fails all in-flight commands;
- stop is bounded and kills the helper process tree if graceful shutdown does not complete.

The managed spike already carries timestamp, event/static identity, all Elite point kinds, quality dimensions, CROB profile fields and G41 V1/V2/V3/V4 command selection across this process boundary.

## Required native mapping

The native helper must map OpenDNP3 `ISOEHandler` values for:

- Binary;
- DoubleBitBinary;
- Analog;
- Counter;
- FrozenCounter;
- BinaryOutputStatus;
- AnalogOutputStatus.

`HeaderInfo.gv` is encoded by OpenDNP3 as `(group << 8) | variation`, so the helper can preserve the exact DNP3 group/variation supplied by the outstation. `HeaderInfo.isEventVariation`, `flagsValid`, measurement flags and DNPTime must also be carried without normalization that loses source evidence.

Commands must use OpenDNP3 command task results, not merely transport response arrival. Elite success requires both a successful task and successful point status. The helper must support:

- Select-Before-Operate and Direct Operate;
- CROB latch on/off and pulse on/off;
- Trip/Close code, count, on-time and off-time;
- Analog Output G41V1 Int32, G41V2 Int16, G41V3 Float32 and G41V4 Float64.

## Completion gate

Do not mark W14-C03 complete or the commercial blocker removed until all of the following are evidenced on GitHub:

1. native OpenDNP3 helper builds from pinned permissive sources on the supported Windows packaging path;
2. the DriverHost default composition references the OpenDNP3 adapter and no longer references `Scada.Drivers.Dnp3.StepFunction`;
3. Step Function `dnp3` package and assemblies are absent from the commercial publish/package dependency graph;
4. DNP3-specific managed/native tests are green, including command-status failure semantics, timestamps, quality, reconnect and clean shutdown;
5. the independent `dnp3py` L3 peer passes the DNP3 portion of the Seven-Driver Lab;
6. the full Seven-Driver Lab is green;
7. EliteSCADA CI is green;
8. Windows package contents and third-party LICENSE/NOTICE material are inspected and recorded.

This document records the implementation choice and evidence boundary; it is not itself completion evidence.

## Validation-only carry-forward trigger

This branch is a disposable CI overlay whose direct product parent is combined C12–C19 candidate `3fda88061df35ad14755d22881e5d3a9216d1ff5`. This documentation-only note exists solely to trigger the dedicated C03 workflow against byte-identical executable/product code. The overlay must never be merged and must never become product authority.
