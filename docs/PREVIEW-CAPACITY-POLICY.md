# EliteSCADA Preview — Product Capacity Policy

Status: **IMPLEMENTED IN PR / VALIDATED**  
Functional head: `6d340e8ca3baaabf138c19be2fb947297854e1f6`  
Validation: **EliteSCADA CI #982 — SUCCESS**

## Preview TAG capacity

The externally distributed EliteSCADA Preview edition is limited to:

**200 TAGs per project**

The limit is project-wide. It is not a per-Driver or per-Data-Source quota. TAGs from communication Drivers and internal memory sources all contribute to the same project total.

The purpose is to preserve enough capacity for meaningful product validation while preventing the Preview build from being a practical unrestricted production SCADA installation.

## Authoritative policy

The limit is defined centrally in:

`src/Scada.Core/Product/ProductCapacityPolicy.cs`

Current contract:

- edition: `Preview`;
- `MaxTagsPerProject = 200`;
- issue code: `PRODUCT_TAG_LIMIT_EXCEEDED`.

Do not duplicate the numeric limit in Drivers, UI code, importers or protocol-specific configuration.

## Enforcement boundaries

### Canonical TAG registry

`InMemoryTagRegistry` rejects any `Register` or new-ID `Upsert` that would create the 201st TAG.

Updates to an existing TAG remain allowed while the project is at the 200-TAG limit.

The capacity check occurs before registry mutation.

### Engineering Preview / Apply

`TagEngineeringHandler.Preview` computes the projected resulting project TAG count for the requested import mode.

If the operation would exceed 200 TAGs:

- Preview returns `PRODUCT_TAG_LIMIT_EXCEEDED` as an error;
- `EngineeringExchangeService.Apply` does not mutate the project because Apply always requires a clean Preview;
- a multi-TAG import is rejected atomically rather than partially creating TAGs up to the limit.

### Runtime activation

`EngineeringRuntimeCoordinator` builds each candidate runtime with a new canonical `InMemoryTagRegistry` and publishes the candidate only after candidate construction/start/readiness succeeds.

Therefore an externally manipulated Engineering package that bypasses normal Preview/Apply still cannot activate more than 200 TAGs through the normal product runtime path. Capacity failure occurs in the isolated candidate and the previous active runtime remains intact.

## Boundary behavior locked by tests

`tests/Scada.Core.Tests/PreviewProductCapacityTests.cs` proves:

1. exactly 200 TAGs are accepted;
2. the 201st TAG is rejected;
3. existing TAGs can still be edited when the project is at capacity;
4. a project with 199 TAGs may import one additional TAG;
5. a project with 199 TAGs attempting to import two new TAGs is rejected without creating either TAG.

CI #982 validates these regressions together with the full normal product suite.

## Future product editions

The 200-TAG number is intentionally centralized so it can be revised after Preview validation without changing Driver implementations.

A future licensed/full edition may replace the static Preview policy with an explicit edition/license capability provider. Do not introduce an undocumented environment-variable or command-line bypass into the externally distributed Preview build.

Any future capacity-policy change must preserve fail-closed behavior and add/update the corresponding boundary regressions.

## Security boundary

This is a product-capacity restriction and misuse deterrent, not cryptographic anti-tamper DRM. A party capable of modifying and rebuilding binaries may remove application checks. Stronger distribution control, signing, licensing and anti-tamper measures are separate future product gates.

## L3 interaction

The post-main integrated seven-Driver L3 laboratory must also operate within the 200-TAG project capacity. L3 does not require a large TAG count; it requires all seven Drivers to coexist concurrently and prove acquisition, supported writes/commands, fault isolation and recovery in one runtime.
