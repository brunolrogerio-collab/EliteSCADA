# W14-C13 — Canonical Simulation Quality Contract

**Package:** W14-C13  
**Base:** `2607e03d5445eefe1f434495d0ee81136c6cd220`  
**Finding:** `C11-P2-QUAL-01`

## Objective

Provide a reusable, server-authoritative mechanism for internal/simulated Sources to originate canonical TAG samples with explicit quality without granting Client Visual code authority to spoof the quality of physical Driver TAGs.

## Contract

`Scada.Core.Sources.QualifiedSourceSample` carries:

- process value;
- `TagQuality`;
- optional source timestamp;
- optional server/intermediary timestamp.

`IQualifiedSourceProvider` is an optional capability layered on top of `ISourceProvider`. Ordinary `ISourceProvider.WriteAsync` remains value-only.

`ServerMemorySourceProvider` implements `IQualifiedSourceProvider`. Its existing `WriteAsync` continues to produce `TagQuality.Good`. Explicit quality is available only through `PublishSampleAsync`.

`ServerAuthoritativeSamplePublisher` is the canonical publication boundary used to connect a qualified server Source to `ICurrentTagCache`. Construction fails unless the Source is server-owned and has one server-authoritative value, and publication fails when the TAG is not owned by that Source.

`TagQuality.Unavailable` is added as an explicit canonical quality value. It is appended to the enum so existing numeric values are not renumbered.

## Authority boundary

No HTTP endpoint, Client Visual API, browser contract, Driver write contract or generic TAG-write payload was changed to accept quality.

Physical communication Drivers remain the authority for their own quality. `ServerAuthoritativeSamplePublisher` publishes only through `IQualifiedSourceProvider`; physical Drivers do not gain that capability. A Server Memory Source also cannot publish a qualified sample for a TAG it does not own.

Client Memory remains value-only and client-local.

## Retention semantics

Server Memory retention stores the typed process value, not transient runtime communication quality. After activation/restart a retained value begins `Good` until the server-side Source/automation publishes a new explicit sample. This prevents stale runtime communication state from silently surviving a process restart while retaining the engineered process value.

## Downstream propagation

Qualified publication writes the Source sample, reads back the canonical `TagValue`, then updates `ICurrentTagCache`. This uses the existing `TagValueChanged` event stream. Existing alarm communication semantics already treat every non-`Good` quality as a Communication alarm condition, and historian implementations receive the same `TagValue`, including quality.

The contract therefore supports:

`server Source -> TagValue/quality -> CurrentTagCache -> TagValueChanged -> alarm/realtime/historian/bindings`

without a second simulation-only data plane.

## Tests

`CanonicalSimulationQualityContractTests` covers:

- explicit `Bad` publication;
- explicit `Stale` publication;
- explicit `Unavailable` publication;
- `CurrentTagCache` preservation of quality;
- Communication alarm activation from the same sample;
- historian capture preserving the same quality/value;
- regression that ordinary Server Memory writes remain `Good`;
- rejection of publication to a TAG not owned by the Server Memory Source.

## C12 integration

C12 may consume `QualifiedSourceSample`, `IQualifiedSourceProvider` and `ServerAuthoritativeSamplePublisher` after C13 is integrated. C12 must not duplicate or privately fork this quality contract on its parallel branch.

## Explicit exclusions

This package does not add a scheduler, Server Script host, EEE-specific simulator, browser quality override, Driver-specific quality injection or DEMO shortcut.
