# LAST CHANGE — EliteSCADA

**Date:** 2026-09-01 (BRT)  
**Operational state:** **WAVE 12 #201 — COMPLETE / ACCEPTED / CLOSED; WAVE 13 #205 — IMPLEMENTATION IN BRANCH**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. Live GitHub refs and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA without superseding that product baseline.

## 1. Accepted product baseline

Wave 12 Hardening is **COMPLETE / ACCEPTED**.

Final accepted Wave 12 product-code `main` baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

This baseline contains the Wave 12 implementation integrated through PR #203 plus the post-merge Modbus CI timing stabilization integrated through PR #204.

Accepted runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; mutable Working never drives HMI Runtime directly.

## 2. Final Wave 12 acceptance evidence

Exact `main` SHA `63bced02426fcb84b26028913f6c68feb3457d80`:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Pre-merge stabilization head `8d9950f56cf4cac8d835f448df8f77dc6a780928` also passed:

- EliteSCADA CI #1095 / `33576006577`: **SUCCESS**;
- L3 Seven-Driver Lab #91 / `33576006594`: **SUCCESS**.

The first post-Wave-12 merge SHA `be710e630da63639af9a0fc63458f9bd92068746` failed EliteSCADA CI #1094 only because two existing Modbus loopback happy-path tests used a runner-sensitive 500 ms request timeout. The failure was diagnosed before any rerun. PR #204 changed only those two healthy-path test timeouts to 2 s; production code and explicit timeout/fault assertions were not weakened.

## 3. Wave 12 accepted findings

All identified findings are **FIXED / REGRESSION / VALIDATED**:

- W12-RT-001 — realtime client isolation and preserved WebSocket 1008 revocation semantics;
- W12-PER-001 — atomic/serialized persistence Save;
- W12-ING-001 — bounded JSON/CSV Engineering ingress;
- W12-PKG-001 — `.escadapkg` export/import resource-limit symmetry;
- W12-PER-002 — Persistence Apply mutation lease + caller-observed workspace CAS;
- W12-AUTH-001 — serialized local-identity logical mutations and last-enabled-administrator invariant;
- W12-AUTH-002 — bounded login-limiter key lifecycle without active lockout eviction;
- W12-API-001 — deterministic request validation and typed/sanitized historical failures;
- W12-AUD-001 — durable pre-mutation audit admission for unsafe `/api` mutations, failing closed on audit-store outage before endpoint execution.

Historical detail remains in `docs/WAVE-12-HARDENING-AUDIT.md` and issue #201.

## 4. Wave 13 live entry audit

Wave 13 issue #205 remains the active coordination surface:

`Wave 13 — Signed Windows x64 package + Authenticode release verification`

Live `main` was revalidated immediately before implementation branch creation:

`fd694d936131919e5325dd9479d84d74759100a5`

At that checkpoint:

- no open PR existed;
- open issues were Wave 13 #205 and deferred L4 #178;
- latest product-code validation remained Wave 12 `63bced...` / EliteSCADA CI #1096 / L3 #92;
- the `fd694d...` advance was documentation-only `[skip ci]`.

The packaging/signing audit was persisted first in issue #205 comment `5503088761` and then in `docs/WAVE-13-WINDOWS-RELEASE-AUDIT.md`.

## 5. Wave 13 implementation branch

Active branch:

`wave13/windows-release-signing`

Branch base:

`fd694d936131919e5325dd9479d84d74759100a5`

Wave 13 is now **IMPLEMENTED IN BRANCH / NOT MERGED / NOT ACCEPTED**.

Initial W13-S1 foundation currently includes:

- `release/release-identity.json` as the release-engineering identity source;
- initial version `0.1.0-preview.13`, RID `win-x64`, ZIP distribution contract;
- `scripts/release/Build-WindowsReleaseCandidate.ps1` for self-contained product and graphical License Generator candidate publish;
- React/Vite build with pinned Pyodide payload copied into the product candidate;
- explicit fail-closed exclusion of Step Function DNP3 content from the customer package while the commercial gate remains uncleared;
- `scripts/release/Test-WindowsReleaseCandidate.ps1` for required files, DNP3 exclusion, private-key-material exclusion and PE presence checks;
- `.github/workflows/wave13-windows-release.yml` on `windows-latest`, producing a clearly named `EliteSCADA-Wave13-UNSIGNED-win-x64` candidate artifact and smoke-testing product host plus License Generator.

The normal workflow intentionally does **not** contain Authenticode credentials and does **not** represent its candidate as signed or releasable.

## 6. Audited release architecture

Initial package decision: versioned Windows x64 ZIP, not MSI/MSIX/WiX/Inno yet. Installer technology remains deferred until the product actually requires installation/service/update/uninstall semantics.

Product target: `win-x64` self-contained.

Signing boundary:

- normal CI builds unsigned candidates only;
- protected organizational signing service or hardware-backed key signs required PE artifacts outside normal CI;
- no PFX/private key/password belongs in source control, normal Actions secrets/artifacts, logs or product packages;
- licensing private material and Authenticode private material remain separate trust domains.

Required release order:

`build -> publish -> protected signing -> signature/publisher/timestamp verification -> final manifest over signed bytes -> package -> verify again`

DNP3 remains excluded from the initial customer package while Step Function I/O `dnp3` 1.6.0 lacks recorded commercial-distribution clearance.

## 7. Remaining Wave 13 slices

1. finish W13-S1 by giving the packaged Web payload a production serving path from the product distribution, preserving required COOP/COEP behavior;
2. W13-S2 deterministic signed-byte manifest and fail-closed Authenticode/publisher/timestamp/hash/unexpected-content verifier with negative tests;
3. W13-S3 protected-signing handoff and signed-artifact verification workflow without private key material in normal CI;
4. W13-S4 focused packaged-product regression for login, Demo/machine request, `.escadapkg`, assets/Dynamos/Pyodide, persistence/configuration, supported Drivers and Active HMI Runtime authority;
5. W13-S5 exact-head universal + affected specialized validation, expected-head merge and post-merge release evidence.

## 8. CI / acceptance status

The new branch has not yet been accepted or merged. Universal `EliteSCADA CI` remains mandatory for any PR to `main`; Wave 13 Windows validation complements it. Preview Licensing CI and L3 must be invoked as conservative release overrides when the branch reaches integration readiness.

Do not declare Wave 13 complete until final required PE files are Authenticode-valid with trusted timestamp, final signed-byte hashes are recorded, the packaged product smoke/regression passes, exact-head CI is green, post-merge `main` is validated and acceptance evidence is persisted.

## 9. Explicit exclusions

Do not begin Wave 14 owner validation, Wave 15 corrections, Linux packaging or physical L4 work as part of Wave 13.

Do not include commercially gated DNP3 in the customer release until Step Function commercial licensing or an approved/revalidated replacement is recorded.
