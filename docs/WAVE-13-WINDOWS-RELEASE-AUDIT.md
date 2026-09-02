# Wave 13 — Windows x64 Packaging / Signing Audit

**Audit date:** 2026-09-01 BRT  
**Audited live main:** `fd694d936131919e5325dd9479d84d74759100a5`  
**Wave issue:** #205  
**State:** IMPLEMENTATION STARTED AFTER AUDIT

## Repository state at entry

- Accepted Wave 12 product-code baseline remains `63bced02426fcb84b26028913f6c68feb3457d80`.
- Exact accepted evidence remains EliteSCADA CI #1096 / `33576603185` SUCCESS and L3 Seven-Driver Lab #92 / `33576603158` SUCCESS.
- Live `main` at audit/branch creation was documentation-only `fd694d936131919e5325dd9479d84d74759100a5`.
- No open PR existed at audit time.
- Open issues were Wave 13 #205 and deferred L4 #178.
- The Wave 13 implementation branch was created only after this audit was first persisted to issue #205.

## Current Windows publish surfaces

The repository had one explicit Windows x64 publish path before Wave 13 implementation:

- `.github/workflows/preview-licensing-ci.yml` publishes `src/Scada.LicenseGenerator/Scada.LicenseGenerator.csproj`;
- target is `win-x64`;
- publish is self-contained and single-file;
- resulting `EliteSCADA.LicenseGenerator.exe` has a `--smoke-test` startup path.

There was no equivalent Windows publish/package path for the actual EliteSCADA product host plus Web UI.

## Product host and executable inventory

`src/Scada.Api/Scada.Api.csproj` is the ASP.NET Core product host/composition root. Before Wave 13 it had no Windows RID, self-contained publish or package contract.

The graphical `EliteSCADA.LicenseGenerator.exe` is a separate authority-side application. It must remain a separate artifact and trust domain rather than being mixed into the customer runtime package.

The current DNP3 implementation is isolated in `src/Scada.Drivers.Dnp3.StepFunction` and directly references Step Function I/O `dnp3` 1.6.0. The project is not referenced by `Scada.Api` and is not in `ScadaPlatform.sln`; therefore the first customer package must not silently add it.

## Web / Pyodide payload

The React/Vite application lives in `web/scada-web`.

Its build contract includes `scripts/sync-pyodide.mjs`, which copies pinned Pyodide `314.0.6` assets from `node_modules/pyodide` into `public/pyodide` before Vite build. The release package must therefore contain the resulting Pyodide/static assets rather than assuming CDN or Node availability on the target machine.

Vite development mode currently supplies COOP/COEP headers and proxies `/api`, `/health`, `/openapi` and `/ws` to the backend. The release architecture must preserve the browser security headers needed by the client scripting runtime and provide a packaged production path that does not require Vite on the target machine.

## Installation / package decision

Wave 13 does not yet have a demonstrated need for MSI/MSIX/WiX/Inno lifecycle semantics. Selecting an installer before the launch/storage/service contract exists would create packaging policy before product need.

The initial Wave 13 acceptance artifact is therefore a versioned ZIP distribution:

- Windows x64;
- self-contained .NET product host;
- built React/Pyodide static payload included in the distribution;
- no dependency on target-machine Node/Vite;
- no dependency on target-machine .NET runtime;
- installer selection remains a later decision if service registration, Start Menu integration, upgrades or uninstall semantics justify it.

## Runtime model decision

The product publish target is `win-x64` self-contained. This is deliberate for the first controlled package because it removes target-machine framework drift from acceptance and gives the release manifest a closed runtime payload.

## Release identity

Before Wave 13, Web declared `0.1.0` while .NET had no single release identity source. Wave 13 introduces `release/release-identity.json` as the release-engineering source for:

- product/release version;
- runtime identifier;
- package format;
- DNP3 customer-package disposition.

Build/package/manifest tooling must consume this file rather than inventing separate versions.

## Authenticode signing boundary

Normal CI builds unsigned candidates only.

Private Authenticode key material is prohibited from:

- source control;
- normal GitHub Actions secrets;
- normal CI artifacts;
- product packages;
- logs.

The intended signing authority is a protected organizational signing service or hardware-backed key outside normal CI. License-signing key material and Authenticode key material remain separate trust domains.

Correct release order:

`build Web -> publish win-x64 binaries -> protected Authenticode signing -> verify signature/publisher/trusted timestamp -> finalize manifest over signed bytes -> package -> verify package again`

Hashes must describe the signed bytes, not the unsigned candidate.

## Manifest / verifier contract

The final deterministic release manifest must include at minimum:

- schema version;
- product and release version;
- source SHA;
- runtime identifier;
- package identity;
- exact artifact paths and roles;
- SHA-256 of final signed bytes;
- whether Authenticode is mandatory for each PE artifact;
- expected publisher identity;
- trusted timestamp requirement/evidence;
- verifier schema/process version.

Verification must fail closed for:

- missing required file;
- unexpected package content;
- unexpected PE executable/module;
- hash mismatch;
- missing Authenticode where required;
- invalid signature;
- wrong publisher;
- missing trusted timestamp;
- modified package content.

SmartScreen reputation is explicitly outside this cryptographic acceptance claim.

## DNP3 disposition

The first customer distribution excludes DNP3 while Step Function I/O `dnp3` 1.6.0 remains under its public non-commercial/non-production licensing gate.

A signed package does not clear that commercial gate. DNP3 may enter a commercial customer package only after an appropriate commercial license is recorded or an approved/revalidated replacement is integrated.

## Implementation slices

### W13-S1 — publish/package foundation

- release identity source;
- Windows x64 self-contained product publish;
- Web + Pyodide inclusion;
- deterministic candidate layout;
- DNP3 exclusion checks;
- Windows package smoke foundation.

### W13-S2 — manifest and fail-closed verification

- deterministic manifest tooling;
- SHA-256 verification;
- PE allowlist/discovery;
- Authenticode publisher/timestamp verification;
- negative tamper/missing/unsigned tests.

### W13-S3 — protected signing handoff

- unsigned CI candidate artifact;
- explicit external signing input/output boundary;
- no key material in normal CI;
- signed-artifact verification workflow.

### W13-S4 — packaged-product regressions

- product host launch;
- Web UI availability;
- local login;
- Demo and machine-request/licensing surfaces;
- graphical License Generator smoke;
- `.escadapkg` Open/Save;
- built-in assets/Dynamos/Pyodide;
- configuration/persistence behavior;
- supported Driver loading;
- preserved `Working -> Revision -> Published -> Active -> HMI Runtime` authority.

### W13-S5 — exact-head integration / acceptance

- EliteSCADA CI universal gate;
- Preview Licensing CI and L3 conservative release overrides;
- Windows release-specific validation;
- expected-head merge protection;
- post-merge `main` validation;
- final package SHA-256, source SHA, publisher/certificate/timestamp and workflow evidence persisted.
