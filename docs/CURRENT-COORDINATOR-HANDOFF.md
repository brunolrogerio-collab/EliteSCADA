# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-02 BRT
**Status:** **WAVE 12 COMPLETE / ACCEPTED; TEST PREVIEW #208 IMPLEMENTED / REAL CODESPACE VALIDATION PENDING; WAVE 13 #205/#207 ACTIVE UNDER SEPARATE COORDINATION**

> GitHub/main/CI is implementation truth. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the mutable resume point. Never resume from chat alone.

## 1. Coordination split

Development Lead direction on 2026-09-02 releases Wave 13 from the temporary Preview pause and separates the work into two concurrent coordination fronts:

- **Temporary Browser Test Preview:** issue #208 / draft PR #210 / branch `preview/codespaces-test-preview`;
- **Wave 13 Windows release/signing:** issue #205 / draft PR #207 / branch `wave13/windows-release-signing`.

The Preview coordinator remains focused on #208/#210 and does not coordinate or develop Wave 13. Wave 13 may proceed under another coordinator without waiting for Preview acceptance.

Neither coordinator may assume the other branch has merged. Before merge/release decisions, re-check live `main`, open PRs/issues and exact-head Actions evidence.

## 2. Mandatory resume protocol

Before changing code, read:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this handoff;
4. `docs/ROADMAP.md`;
5. `docs/CI-VALIDATION-POLICY.md`;
6. live `main`, open PRs/issues and exact Actions state.

Then follow the workstream-specific material.

### Preview coordinator

7. `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`;
8. issue #208;
9. draft PR #210 and branch `preview/codespaces-test-preview`.

### Wave 13 coordinator

7. `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`;
8. `docs/WAVE-13-WINDOWS-RELEASE-AUDIT.md`;
9. `docs/WAVE-13-SIGNING-BOUNDARY.md`;
10. issue #205;
11. draft PR #207 and branch `wave13/windows-release-signing`;
12. inspect #208/#210 only for concurrent repository/launch changes that may affect package assumptions.

If repository state differs from copied prose, GitHub/main/CI wins.

## 3. Accepted foundation

Wave 11 issue #194 is **CLOSED / COMPLETED**.  
Wave 12 issue #201 is **COMPLETE / ACCEPTED / CLOSED**.

Accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Exact post-merge acceptance evidence on that SHA:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Accepted lifecycle authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; Working edits remain isolated until Save/Publish/Activate. Accepted Wave 11/12 architecture must not be reopened without a demonstrated defect.

Owner-test package from Wave 11 remains:

`EliteSCADA-Wave11-Demo.escadapkg`  
SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`

## 4. Temporary Browser Test Preview state

Tracking: issue #208 and draft PR #210.

Implementation head last validated:

`208ac69b5638ace8557a700d34dd16571360c8f6`

Exact-head evidence:

- Test Preview #4 / `33594259242`: **SUCCESS**;
- EliteSCADA CI #1122 / `33594259232`: **SUCCESS**.

The Preview implementation has zero product-code diff and provides devcontainer/Compose infrastructure, private TimescaleDB, Web-only forwarding, protected secret injection, validated Wave 11 Demo fixture, `Launch Test Preview` task, normal package import + Save/Publish/Activate bootstrap and official Demo licensing verification.

Final acceptance is **not complete** until a fresh real GitHub Codespace successfully starts and its forwarded private Web URL opens the real EliteSCADA with representative browser behavior.

Administrative username:

`EliteSCADA`

Protected secret name:

`ELITESCADA_PREVIEW_ADMIN_PASSWORD`

Never commit or echo the supplied password into source, docs, workflows, images, package fixtures, logs or normal artifacts.

## 5. Wave 13 active handoff state

Issue #205 is **ACTIVE / RELEASED FOR SEPARATE COORDINATION**.

Draft PR #207 remains draft on branch `wave13/windows-release-signing` because active implementation does not make the release complete or merge-ready.

The Wave 13 coordinator integrated live documentation-only `main` `056148bb17c0fd6cb78bd21339b3f9614d38ad68` through merge commit `dbef6557e24297d273caeb19dfe5aabc17fb0b43`. Its exact other parent is the previously validated repository-side checkpoint `a287c4f2a4e4c571a7c5ad4b25efb1c98132e5ab`.

Current fully validated repository-side implementation checkpoint before the latest documentation-only synchronization:

`9f26a2bc02ae77017e266c52ff128dc39eece4b4`

Exact validation on `9f26a2bc...`:

- Wave 13 Windows Release #27 / `33643546191`: **SUCCESS**;
- EliteSCADA CI #1134 / `33643546119`: **SUCCESS**;
- L3 Seven-Driver Lab #102 / `33643546111`: **SUCCESS**;
- Wave 11 Active HMI Runtime #64 / `33643546139`: **SUCCESS**.

Repository-side implementation now includes:

- self-contained `win-x64` product and separate graphical License Generator authority candidate;
- packaged React/Pyodide hosting and focused login/Demo/machine-request/Dynamo/Driver/`.escadapkg` smoke;
- `windows-2025`/PostgreSQL 17 packaged regression covering two saved Revisions, Working isolation, explicit Publish/Activate boundaries, persisted Active HMI projection and forced-restart recovery;
- retained unsigned-candidate provenance plus signing-only PE delta validation;
- deterministic signed-byte manifest and separate product/authority ZIP roles;
- fail-closed Authenticode, exact publisher, cryptographically bound RFC3161, hash/content and safe-ZIP verification with negative cases.

Normal CI remains keyless and produces only an explicitly `UNSIGNED` signer-input artifact. Windows #27 produced artifact `9851917252`, 111,162,549 bytes, with uploaded-artifact SHA-256 `ee2297cde3675114822b0be01e305590c7d78b46927a58e64938baa004f9c709`. This is signer-input transport evidence, not a signed release. The protected signing service/hardware-backed key and exact organizational certificate Subject remain external acceptance blockers.

Existing Wave 13 locks remain intact:

- controlled Windows x64 release package;
- Authenticode with trusted timestamp;
- protected organizational signing boundary;
- deterministic/fail-closed manifest and signed-byte verification;
- no private code-signing material in source/GitHub/normal artifacts/logs;
- commercial DNP3 distribution remains gated pending appropriate license or approved/revalidated replacement.

## 6. CI / merge rules

- EliteSCADA CI remains the universal Coordinator gate for PRs to `main` when product code changes;
- specialized validation complements but never replaces universal CI;
- diagnose failures before rerun;
- do not weaken authentication, authorization, Runtime authority or tests to manufacture green evidence;
- integration uses expected-head protection;
- validate post-merge `main` when product code changes;
- documentation-only coordination changes may use `[skip ci]` according to repository policy.

## 7. Explicit exclusions for the Preview coordinator

Do not include in #208/#210 unless separately authorized:

- permanent/public production hosting;
- new Drivers/protocols;
- unrelated HMI/Engineering feature work;
- Wave 13 release-signing development;
- Wave 14 owner-validation execution;
- Wave 15 feedback/corrections;
- Linux `.deb` implementation;
- physical Driver L4 claims.
