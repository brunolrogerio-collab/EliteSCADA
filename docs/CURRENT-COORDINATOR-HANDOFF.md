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
8. issue #205;
9. draft PR #207 and branch `wave13/windows-release-signing`;
10. inspect #208/#210 only for concurrent repository/launch changes that may affect package assumptions.

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

Draft PR #207 remains draft on branch `wave13/windows-release-signing` because unpausing does not make it complete or merge-ready.

Incoming Wave 13 coordination must first re-audit:

- live `main`;
- current exact-SHA CI;
- issue #205;
- PR #207 and its complete diff/history;
- `docs/WAVE-13-WINDOWS-RELEASE-AUDIT.md` on the branch;
- concurrent Preview #208/#210 state for any launch/repository changes that actually reach `main`.

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
