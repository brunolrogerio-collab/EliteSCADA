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

Last validated Wave 13 implementation checkpoint before incorporating live `main`:

`a287c4f2a4e4c571a7c5ad4b25efb1c98132e5ab`

PR #207 remains **draft**. The current integration combines the exact parents above; it does not accept or merge the release into `main`.

Open coordination surfaces observed at resume time are Wave 13 #205, Preview #208/#210 and deferred physical L4 #178.

## 4. Wave 13 repository-side implementation

The draft branch currently provides:

- `release/release-identity.json` as the single release-engineering identity source;
- `win-x64` self-contained, single-file product and graphical License Generator publishes;
- React/Vite, pinned Pyodide and packaged Web hosting beside `Scada.Api.exe`, preserving COOP/COEP and reserved API routes;
- separate customer-product and License Generator authority artifact roles;
- normal Windows CI that produces only a clearly named `UNSIGNED` signer-input candidate and receives no Authenticode private material;
- packaged regression for Web/Pyodide, local login, Demo limits, machine request, eight built-in Dynamos, Demo screen, Runtime Driver surface and `.escadapkg` export/inspect/import preview;
- signed-return comparison that requires non-PE identity and permits PE differences only in Authenticode checksum/Security Directory/final certificate-table append;
- deterministic signed-byte manifest with hashes, roles, exact publisher, signer certificate and cryptographically bound RFC3161 evidence;
- separate deterministic product and License Generator ZIP creation and trusted-hash verification;
- fail-closed checks and negative cases for wrong source SHA, unsigned/missing/tampered/unexpected content, invalid signing-return deltas, traversal, duplicate/case-colliding and Windows-unsafe ZIP paths;
- explicit release identity stating that DNP3 is transitively present, its commercial gate is blocked and `commercialDistributionAuthorized` is `false`.

Correct release order:

`build -> publish -> retain exact unsigned candidate -> protected signing -> compare signed return -> verify Authenticode/publisher/RFC3161 -> manifest signed bytes -> role-specific ZIPs -> verify trusted package hashes and content`

## 5. Exact validation before live-main integration

Exact SHA `a287c4f2a4e4c571a7c5ad4b25efb1c98132e5ab`:

- Wave 13 Windows Release #22 / `33585606355`: **SUCCESS**;
- EliteSCADA CI #1118 / `33585606437`: **SUCCESS**;
- L3 Seven-Driver Lab #97 / `33585606347`: **SUCCESS**;
- Wave 11 Active HMI Runtime #56 / `33585606366`: **SUCCESS**.

Windows #22 produced unsigned signer-input transport evidence only:

- artifact ID `9829991641`;
- name `EliteSCADA-Wave13-UNSIGNED-win-x64`;
- 115 files;
- compressed size 111,162,421 bytes;
- artifact ZIP SHA-256 `7daea9d5797090b097f8d3b518c998196abb8db7acd7ebd4f9f2e7e427c2ead5`.

That digest is **not** a final signed-release hash and establishes no Authenticode acceptance.

Preview head `208ac69b5638ace8557a700d34dd16571360c8f6` independently passed Test Preview #4 / `33594259242` and EliteSCADA CI #1122 / `33594259232`; its branch has not merged into `main` and is not part of the current Windows candidate.

## 6. Remaining Wave 13 acceptance blockers

Wave 13 cannot be accepted until all of the following exist:

1. an organizationally controlled protected signing service or hardware-backed Authenticode key;
2. the exact public certificate Subject/publisher expected by verification;
3. SHA-256 Authenticode plus trusted RFC3161 timestamp on every returned PE, without rebuilding;
4. successful signed-return derivation, final manifest and both role-specific package verifications;
5. final signed-artifact regression including configuration/persistence and the complete canonical Active-HMI path;
6. trusted product/authority ZIP SHA-256, certificate/timestamp and workflow evidence persisted in issue #205;
7. exact-head universal and affected specialized gates, expected-head merge and post-merge `main` validation.

## 7. Exact next action — Wave 13 coordinator

1. publish the live-`main` integration to draft PR #207 without changing the trust boundary;
2. require Wave 13 Windows Release, EliteSCADA CI, L3 Seven-Driver Lab and Wave 11 Active HMI Runtime on the exact integration head;
3. diagnose any failure before rerun or correction;
4. record the exact integration SHA and Actions evidence in issue #205 and the PR;
5. continue provider-neutral repository work only where it does not invent the organizational signing authority;
6. obtain the Development Lead's signing-authority choice and exact public certificate Subject before provider-specific signing integration;
7. keep the PR draft until a real signed return and final package evidence satisfy every gate.

## 8. Security, licensing and scope locks

- No PFX, private key, certificate password or equivalent Authenticode material belongs in source, normal GitHub Secrets, normal CI artifacts, logs or product files.
- Authenticode and EliteSCADA license signing are separate trust domains.
- Step Function I/O `dnp3` 1.6.0 remains transitively present. Authenticode does not grant commercial clearance; commercial distribution remains unauthorized until an appropriate Step Function commercial license or approved/revalidated replacement is recorded.
- Do not begin Wave 14, Wave 15, Linux `.deb` implementation or physical L4 work under Wave 13.
