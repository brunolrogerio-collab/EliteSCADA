# Preview/Demo Licensing Acceptance Evidence — 2026-08-31

Status: **IMPLEMENTED / VALIDATED**

This document supersedes the historical `SPECIFIED / NOT IMPLEMENTED` implementation-gap snapshot in `docs/LICENSING-AND-DEMO-MODE.md`. That document remains the product contract; this file records the implementation evidence that now satisfies it.

## Implemented product behavior

- No license installed => Demo entitlement.
- Engineering may contain more than 200 TAGs.
- Demo Run permits <=200 TAGs and blocks >200 before entering candidate runtime activation.
- A denied activation does not replace or dispose the previous active runtime.
- Every successful explicit Demo Run receives a fresh continuous runtime allowance.
- Demo continuous runtime is limited to 300 minutes using monotonic elapsed-time accounting.
- Demo expiry disposes the active runtime through its normal lifecycle, leaves the application/Engineering host alive, exposes an expiry diagnostic and requires a later explicit Run to start again.
- Valid signed licenses remove the Demo time limit and apply 500 / 1000 / 1500 / 3000 / 5000 / Unlimited TAG tiers.
- Invalid, tampered, expired, unknown-key or wrong-machine installed licenses fail closed and block Run.
- Machine request codes are versioned and contain a SHA-256 machine fingerprint rather than raw machine identifiers.
- License signatures use RSA-PSS with SHA-256.
- Product runtime contains verification/public-key material only; production private signing material is external.

## Product surfaces

Protected API:

- `GET /api/licensing/status`
- `GET /api/licensing/request`
- `POST /api/licensing/install`
- `DELETE /api/licensing/license`

Minimal host-served management UI:

- `GET /licensing`

The page exposes readable Demo/license/runtime status, copyable request code, validated license installation and license removal. Sensitive request/license data continues to come from the protected API endpoints.

## Runtime architecture

`ProductLicensedRuntimeCoordinator` decorates the existing proven transactional `IEngineeringRuntimeCoordinator` rather than putting licensing into Drivers.

Entitlement is evaluated before the inner activation path. Therefore a capacity/license rejection cannot stage, commit or replace the existing active runtime. Demo expiry swaps the expired runtime for a fresh empty coordinator and disposes the old runtime normally, allowing a later explicit Run without restarting the host process.

Drivers remain unaware of license files and machine identity.

## Offline License Generator

Project:

`src/Scada.LicenseGenerator`

Windows x64 publish produces the controlled single-file executable:

`EliteSCADA.LicenseGenerator.exe`

The generator accepts the versioned machine request, TAG tier, key id, optional expiration/license id and an explicit external private-key path. It fails closed when request/key material is invalid.

Operational key creation, deployment and rotation are documented in:

`docs/licensing/OFFLINE-LICENSE-OPERATIONS.md`

## Automated coverage

Core licensing tests cover:

- machine request round-trip;
- signed-license round-trip;
- tampered payload;
- wrong hardware;
- missing license => Demo;
- all licensed tiers and Unlimited;
- invalid installed license state;
- monotonic Demo session behavior;
- expired license.

Runtime/API-layer tests cover:

- denied Demo activation preserving previous active runtime;
- Demo expiry disposing runtime and later explicit Run using a fresh coordinator;
- invalid license installation rejection;
- matching signed license installation acceptance.

The normal Driver test assembly is part of the dedicated licensing CI so the runtime decorator is tested against the same product references used by Driver/runtime integration tests.

## Validation checkpoint

Implementation head before this documentation-only acceptance commit:

`90727bb8bf94fe7912a3c998cfb8655840410205`

Preview Licensing CI run `#32`: **SUCCESS**

Successful steps:

1. restore full solution;
2. build full product solution;
3. build License Generator;
4. Core/licensing tests;
5. runtime licensing/Driver tests;
6. publish Windows x64 self-contained single-file License Generator;
7. upload License Generator artifact.

The immediately preceding exact-head artifact validation on `d5dec0098256f53dd9febbdd1e3fea107fb5cafe`, CI `#26`, produced `EliteSCADA-LicenseGenerator-win-x64` (artifact id `9764935636`) with GitHub-recorded digest:

`sha256:f6b68a68170231d419c911bdc101f1ff780ca0638f4679f9c50c322c56be2246`

Final integration rule: the documentation acceptance head must also be green before PR #185 is merged into `coordination/driver-convergence-v3`.

## Stage result

Once the final documentation head is green, issues #183/#184 may be closed as completed and PR #185 may be integrated into the coordinator branch. The established Driver sequence then resumes:

`PR #175 controlled main merge -> exact post-main CI -> L3 #180 -> Wave 11`
