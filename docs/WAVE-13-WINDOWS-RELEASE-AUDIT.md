# Wave 13 — Windows x64 Packaging / Signing Audit

**Audit date:** 2026-09-01 BRT  
**Audited live main:** `fd694d936131919e5325dd9479d84d74759100a5`  
**Wave issue:** #205  
**State:** IMPLEMENTATION STARTED AFTER AUDIT / AUDIT CORRECTED BY LIVE PUBLISH EVIDENCE

## Repository state at entry

- Accepted Wave 12 product-code baseline remains `63bced02426fcb84b26028913f6c68feb3457d80`.
- Exact accepted evidence remains EliteSCADA CI #1096 / `33576603185` SUCCESS and L3 Seven-Driver Lab #92 / `33576603158` SUCCESS.
- Live `main` at audit/branch creation was documentation-only `fd694d936131919e5325dd9479d84d74759100a5`.
- No open PR existed at audit time.
- Open issues were Wave 13 #205 and deferred L4 #178.
- The Wave 13 implementation branch was created only after the audit was first persisted to issue #205.

## Current Windows publish surfaces

Before Wave 13, the only explicit Windows x64 publish path was `.github/workflows/preview-licensing-ci.yml` for `src/Scada.LicenseGenerator/Scada.LicenseGenerator.csproj`:

- target `win-x64`;
- self-contained;
- single-file;
- resulting `EliteSCADA.LicenseGenerator.exe` supports `--smoke-test`.

There was no equivalent Windows publish/package path for the actual EliteSCADA product host plus Web UI.

## Product host and PE inventory

`src/Scada.Api/Scada.Api.csproj` is the ASP.NET Core product host/composition root. Before Wave 13 it had no Windows RID, self-contained publish or package contract.

The graphical `EliteSCADA.LicenseGenerator.exe` is an authority-side application. It remains a separate release artifact/trust role rather than being presented as part of the customer runtime authority.

A live Wave 13 `dotnet publish Scada.Api -r win-x64` corrected an important initial audit assumption: DNP3 **is in the transitive product graph**.

The dependency path is:

`Scada.Api -> Scada.DriverHost -> Scada.Drivers.Dnp3.StepFunction -> Step Function I/O dnp3 1.6.0`

`Scada.DriverHost/Engineering/CommunicationDriverRuntimeComposition.cs` also explicitly registers the DNP3 planner/runtime factory. Therefore a single-file publish may embed DNP3 content inside `Scada.Api.exe` even if no standalone file name reveals it.

The earlier idea of proving DNP3 exclusion by scanning package filenames is withdrawn as technically invalid.

## Web / Pyodide payload

The React/Vite application lives in `web/scada-web`.

Its build contract includes `scripts/sync-pyodide.mjs`, which copies pinned Pyodide `314.0.6` assets from `node_modules/pyodide` into `public/pyodide` before Vite build. The release package must contain the resulting Pyodide/static assets rather than assuming CDN or Node availability on the target machine.

Vite development mode supplies COOP/COEP headers and proxies `/api`, `/health`, `/openapi` and `/ws` to the backend. Wave 13 adds a packaged Web hosting path to the ASP.NET product host that activates only when a built `wwwroot/index.html` is present. Normal Vite development remains unchanged. The packaged path preserves COOP `same-origin` and COEP `require-corp`, serves static/Pyodide assets and applies SPA fallback only outside reserved backend routes.

## Installation / package decision

Wave 13 does not yet have a demonstrated need for MSI/MSIX/WiX/Inno lifecycle semantics. Selecting an installer before launch/storage/service/update requirements exist would add installation policy without product need.

The initial Wave 13 acceptance artifact is therefore a versioned ZIP distribution:

- Windows x64;
- self-contained .NET product host;
- built React/Pyodide static payload;
- no target-machine Node/Vite dependency;
- no target-machine .NET runtime dependency;
- installer technology remains deferred until service registration, Start Menu integration, upgrade or uninstall semantics justify it.

## Runtime model decision

The product publish target is `win-x64` self-contained. This removes target-machine framework drift from the first controlled acceptance package and gives verification a closed runtime payload.

## Release identity

Before Wave 13, Web declared `0.1.0` while .NET had no single release-engineering identity source. Wave 13 introduces `release/release-identity.json` for:

- product/release version;
- runtime identifier;
- package format;
- audited DNP3 dependency presence;
- DNP3 commercial-license gate;
- commercial-distribution authorization state.

The initial Wave 13 identity deliberately records:

- `dnp3IncludedInProductGraph: true`;
- `dnp3CommercialGate: blocked`;
- `commercialDistributionAuthorized: false`.

Build/package/manifest tooling consumes that identity rather than inventing independent states.

## Authenticode signing boundary

Normal CI builds unsigned candidates only.

Private Authenticode key material is prohibited from:

- source control;
- normal GitHub Actions secrets;
- normal CI artifacts;
- product packages;
- logs.

The intended signing authority is a protected organizational signing service or hardware-backed key outside normal CI. License-signing private material and Authenticode private material remain separate trust domains.

Correct release order:

`build Web -> publish win-x64 binaries -> protected Authenticode signing -> verify signature/publisher/RFC3161 trusted timestamp -> finalize manifest over signed bytes -> verify -> deterministic ZIP package`

Hashes describe the signed bytes, never the unsigned candidate.

## Manifest / verifier contract

The deterministic release manifest includes or requires:

- schema/process version;
- product/release version;
- exact source SHA;
- runtime identifier and package format;
- exact artifact paths and roles;
- SHA-256 and size of final signed bytes/payload;
- PE classification based on file content;
- Authenticode requirement for every PE;
- expected publisher identity;
- trusted timestamp requirement with `RFC3161` protocol;
- DNP3 dependency/commercial-gate state.

Verification is fail-closed for:

- missing required file;
- undeclared package content;
- unexpected PE executable/module;
- hash mismatch;
- missing/invalid Authenticode;
- wrong publisher;
- missing trusted timestamp;
- missing RFC3161 timestamp token;
- private signing material in the release;
- commercial-distribution authorization while the DNP3 gate is blocked.

SmartScreen reputation is explicitly outside this cryptographic acceptance claim.

## DNP3 disposition — corrected by CI evidence

The accepted runtime currently includes Step Function I/O `dnp3` 1.6.0 transitively through `Scada.DriverHost`. Wave 13 will not silently redesign the accepted seven-driver runtime composition merely to make release packaging convenient.

Therefore the first controlled signed package may contain the currently accepted DNP3 implementation, but it is **not a commercially distributable package** while the licensing gate remains blocked.

Authenticode validity does not clear this commercial gate.

Before commercial distribution with DNP3, one of the following remains mandatory:

1. obtain and record an appropriate commercial license from Step Function; or
2. replace the dependency under Development Lead approval and revalidate the DNP3 Driver/runtime contract.

No release tooling may label a blocked package as commercially authorized.

## Implementation slices

### W13-S1 — publish/package foundation

- single release identity;
- Windows x64 self-contained product publish;
- Web + Pyodide inclusion and production serving path;
- exact source-SHA candidate metadata;
- explicit DNP3 commercial gate;
- normal CI artifact clearly marked `UNSIGNED`;
- Windows product-host and License Generator smoke foundation.

### W13-S2 — manifest and fail-closed verification

- deterministic signed-byte manifest tooling;
- SHA-256/content verification;
- content-based PE discovery;
- Authenticode/publisher verification;
- RFC3161 timestamp-token verification;
- missing/tampered/unexpected/unsigned negative tests.

### W13-S3 — protected signing handoff

- unsigned CI candidate artifact;
- explicit protected signing input/output contract;
- no private key material in normal CI;
- signed-return finalization and verification workflow.

### W13-S4 — packaged-product regressions

- product host launch;
- Web UI availability;
- local login;
- Demo Mode and machine-request/licensing surfaces;
- graphical License Generator startup;
- `.escadapkg` Open/Save;
- built-in assets/Dynamos/Pyodide;
- configuration/persistence behavior;
- supported Driver composition;
- preserved `Working -> Revision -> Published -> Active -> HMI Runtime` authority.

### W13-S5 — exact-head integration / acceptance

- EliteSCADA CI universal gate;
- affected specialized workflows as conservative release overrides;
- Windows release-specific validation;
- expected-head merge protection;
- post-merge `main` validation;
- final package SHA-256, source SHA, publisher/certificate/RFC3161 timestamp and workflow evidence persisted.

## Current acceptance limitation

Repository-side release engineering can establish the unsigned candidate, manifest/verifier, package composition and protected-signing boundary without holding a private Authenticode key.

Wave 13 cannot be declared accepted until a real protected signing authority is configured, an exact expected publisher identity is known, required PE files are returned correctly Authenticode-signed with RFC3161 trusted timestamps, and the resulting signed package passes the full verifier and packaged-product regressions.