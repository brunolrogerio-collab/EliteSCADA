# LAST CHANGE — EliteSCADA

**Date:** 2026-09-02 (BRT)
**Operational state:** **WAVE 12 #201 — COMPLETE / ACCEPTED / CLOSED; TEST PREVIEW #208/#210 — PARALLEL / REAL CODESPACE VALIDATION PENDING; WAVE 13 #205/#207 — ACTIVE IN DRAFT / NOT ACCEPTED**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. Live GitHub refs and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA without superseding that product baseline.

## 1. Accepted product baseline

Wave 12 Hardening is **COMPLETE / ACCEPTED**.

Final accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Exact post-merge evidence:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Accepted Runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; mutable Working never drives HMI Runtime directly.

## 2. Parallel coordination authorized on 2026-09-02

The Development Lead released Wave 13 from the temporary Preview pause. The two workstreams may proceed independently and in parallel:

- Temporary Browser Test Preview: issue #208, draft PR #210, branch `preview/codespaces-test-preview`;
- Wave 13 Windows release/signing: issue #205, draft PR #207, branch `wave13/windows-release-signing`.

Neither workstream may assume the other branch has merged. Only live `main` changes become shared package/launch assumptions.

## 3. Live repository state and Wave 13 integration

Live `main` audited before resuming Wave 13:

`056148bb17c0fd6cb78bd21339b3f9614d38ad68`

That SHA is documentation-only and records the parallel coordination model. It descends from the original Wave 13 branch base:

`fd694d936131919e5325dd9479d84d74759100a5`

The live-`main` integration commit is:

`dbef6557e24297d273caeb19dfe5aabc17fb0b43`

Its exact parents are the previously validated Wave 13 checkpoint `a287c4f2a4e4c571a7c5ad4b25efb1c98132e5ab` and live `main` `056148bb17c0fd6cb78bd21339b3f9614d38ad68`.

Current fully validated Wave 13 repository-side implementation checkpoint before this documentation-only synchronization:

`9f26a2bc02ae77017e266c52ff128dc39eece4b4`

PR #207 remains **draft**. The integration and subsequent packaged-regression work do not accept or merge the release into `main`.

Open coordination surfaces observed at resume time are Wave 13 #205, Preview #208/#210 and deferred physical L4 #178.

## 4. Wave 13 repository-side implementation

The draft branch currently provides:

- `release/release-identity.json` as the single release-engineering identity source;
- `win-x64` self-contained, single-file product and graphical License Generator publishes;
- React/Vite, pinned Pyodide and packaged Web hosting beside `Scada.Api.exe`, preserving COOP/COEP and reserved API routes;
- separate customer-product and License Generator authority artifact roles;
- normal Windows CI, pinned to `windows-2025`, that produces only a clearly named `UNSIGNED` signer-input candidate and receives no Authenticode private material;
- packaged regression for Web/Pyodide, local login, Demo limits, stable machine request, eight built-in Dynamos, Demo screen, Runtime Driver surface and `.escadapkg` export/inspect/import preview;
- isolated PostgreSQL 17 regression of the actual packaged `Scada.Api.exe`: coherent seven-TAG Demo Runtime fixture, first Save/Publish/Activate, mutable Working isolation, second Revision lineage, Published isolation, explicit second Activate, Active HMI projection and recovery after a forced host restart;
- signed-return comparison that requires non-PE identity and permits PE differences only in Authenticode checksum/Security Directory/final certificate-table append;
- deterministic signed-byte manifest with hashes, roles, exact publisher, signer certificate and cryptographically bound RFC3161 evidence;
- separate deterministic product and License Generator ZIP creation and trusted-hash verification;
- fail-closed checks and negative cases for wrong source SHA, unsigned/missing/tampered/unexpected content, invalid signing-return deltas, traversal, duplicate/case-colliding and Windows-unsafe ZIP paths;
- explicit release identity stating that DNP3 is transitively present, its commercial gate is blocked and `commercialDistributionAuthorized` is `false`.

Correct release order:

`build -> publish -> retain exact unsigned candidate -> protected signing -> compare signed return -> verify Authenticode/publisher/RFC3161 -> manifest signed bytes -> role-specific ZIPs -> verify trusted package hashes and content`

## 5. Exact repository-side validation

Exact SHA `9f26a2bc02ae77017e266c52ff128dc39eece4b4`:

- Wave 13 Windows Release #27 / `33643546191`: **SUCCESS**;
- EliteSCADA CI #1134 / `33643546119`: **SUCCESS**;
- L3 Seven-Driver Lab #102 / `33643546111`: **SUCCESS**;
- Wave 11 Active HMI Runtime #64 / `33643546139`: **SUCCESS**.

Windows #27 verified 115 manifest artifacts, all release negative cases and the PostgreSQL-backed packaged lifecycle/restart regression. It produced unsigned signer-input transport evidence only:

- artifact ID `9851917252`;
- name `EliteSCADA-Wave13-UNSIGNED-win-x64`;
- compressed size 111,162,549 bytes;
- uploaded artifact ZIP SHA-256 `ee2297cde3675114822b0be01e305590c7d78b46927a58e64938baa004f9c709`.

That digest is **not** a final signed-release hash and establishes no Authenticode acceptance.

Preview head `208ac69b5638ace8557a700d34dd16571360c8f6` independently passed Test Preview #4 / `33594259242` and EliteSCADA CI #1122 / `33594259232`; its branch has not merged into `main` and is not part of the current Windows candidate.

## 6. Remaining Wave 13 acceptance blockers

Wave 13 cannot be accepted until all of the following exist:

1. an organizationally controlled protected signing service or hardware-backed Authenticode key;
2. the exact public certificate Subject/publisher expected by verification;
3. SHA-256 Authenticode plus trusted RFC3161 timestamp on every returned PE, without rebuilding;
4. successful signed-return derivation, final manifest and both role-specific package verifications;
5. repetition of the now-green PostgreSQL/package regression against the final signed product bytes;
6. trusted product/authority ZIP SHA-256, certificate/timestamp and workflow evidence persisted in issue #205;
7. exact-head universal and affected specialized gates, expected-head merge and post-merge `main` validation.

## 7. Exact next action — Wave 13 coordinator

1. obtain the Development Lead's protected signing-authority choice and exact public certificate Subject;
2. submit the exact retained unsigned candidate from `9f26a2bc...` to that protected authority without rebuilding it;
3. return only the Authenticode/RFC3161-signed PE bytes and complete the provider-neutral derivation, manifest and package verification flow;
4. run the full packaged-product regression against the final signed product bytes;
5. persist trusted product/authority ZIP hashes, certificate/timestamp evidence and exact workflow/source SHA in issue #205;
6. require exact-head universal and affected specialized gates, then merge only with expected-head protection and validate post-merge `main`;
7. keep PR #207 draft until a real signed return and final package evidence satisfy every gate.

## 8. Security, licensing and scope locks

- No PFX, private key, certificate password or equivalent Authenticode material belongs in source, normal GitHub Secrets, normal CI artifacts, logs or product files.
- Authenticode and EliteSCADA license signing are separate trust domains.
- Step Function I/O `dnp3` 1.6.0 remains transitively present. Authenticode does not grant commercial clearance; commercial distribution remains unauthorized until an appropriate Step Function commercial license or approved/revalidated replacement is recorded.
- Do not begin Wave 14, Wave 15, Linux `.deb` implementation or physical L4 work under Wave 13.
