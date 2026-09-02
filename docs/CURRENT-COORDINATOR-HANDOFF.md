# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-01 BRT  
**Status:** **WAVE 12 COMPLETE / ACCEPTED; WAVE 13 #205 PREPARED / NOT STARTED**

> GitHub/main/CI is implementation truth. `PROJECT GOAL.md` governs permanent product intent. `LAST CHANGE.md` is the mutable resume point. Never resume from chat alone.

## 1. Mandatory resume protocol

Read in this order before changing code:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this handoff;
4. `docs/ROADMAP.md`;
5. `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`;
6. issue #205 — Wave 13;
7. `docs/CI-VALIDATION-POLICY.md`;
8. live `main`, open PRs/issues and exact Actions state;
9. for historical Wave 12 diagnosis only, `docs/WAVE-12-HARDENING-AUDIT.md` and issue #201.

If repository state differs from copied prose, GitHub/main/CI wins.

## 2. Accepted foundation

Wave 11 issue #194 is **CLOSED / COMPLETED**.  
Wave 12 issue #201 is **COMPLETE / ACCEPTED / CLOSED**.

Accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Exact post-merge acceptance evidence on that SHA:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Accepted lifecycle authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; Working edits remain isolated until Save/Publish/Activate. Accepted Wave 11/12 architecture must not be reopened without a demonstrated defect.

Owner-test package from Wave 11 remains:

`EliteSCADA-Wave11-Demo.escadapkg`  
SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`

## 3. Wave 12 closure history that matters

Wave 12 implementation was integrated through PR #203. Initial post-merge `main` SHA `be710e630da63639af9a0fc63458f9bd92068746` failed EliteSCADA CI #1094 in two existing Modbus happy-path loopback writes at the identical 500 ms request timeout.

The failure was diagnosed before rerun. `ModbusTcpTransport` starts its request timeout after acquiring its transport serialization gate; the failing tests injected no server delay and did not define a 500 ms latency product contract. Explicit timeout/reconnect/degraded-path tests remained green.

PR #204 changed only the two healthy-path test configurations from 500 ms to 2 s and the matching diagnostics expectation. No production code or fault-path assertions changed.

Exact #204 head `8d9950f56cf4cac8d835f448df8f77dc6a780928` passed EliteSCADA CI #1095 and L3 #91. Squash merge produced accepted `main` `63bced...`, which then passed #1096 and L3 #92.

This history exists to prevent a future Coordinator from reducing timeouts indiscriminately or treating #1094 as an unresolved product defect.

## 4. Wave 13 preparation state

Issue #205: **OPEN / PREPARED / NOT STARTED**.  
Preparation: `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`.

No Wave 13 implementation branch has been created. This is intentional.

A documentation-only preparation commit may advance live `main` beyond accepted product-code `63bced...`. Therefore do not create the implementation branch from a copied SHA. Re-read live `main` immediately before branch creation.

## 5. Wave 13 objective

Produce and verify the first controlled Windows x64 release package with Authenticode signing and trusted timestamping while preserving accepted product/security/runtime/licensing contracts.

Wave 13 is release engineering, not a new feature wave.

The first implementation slice must be audit/design, not blind packaging:

- inventory current Windows publish/package surfaces and user-facing executable artifacts;
- define package layout and launch/installation contract;
- identify which PE artifacts require signing;
- define signing authority, certificate and timestamp boundary;
- define deterministic manifest/hash verification;
- define package smoke/regression checks for product launch, Demo/licensing and Active Runtime;
- define DNP3 disposition for any distributable artifact.

## 6. Signing/security locks

- Production Preview/installable Windows executables/installers must be Authenticode-signed with an authorized organizational identity and trusted timestamp.
- Private signing keys/certificates must never be committed, embedded, copied into normal GitHub Actions secrets/artifacts or included in packages/logs.
- Prefer protected signing service or hardware-backed key.
- Verification must fail closed on missing/invalid signatures, missing trusted timestamp, hash mismatch or unexpected package contents.
- SmartScreen reputation is separate from Authenticode validity; do not claim a warning-free reputation merely because signing succeeds.
- Licensing private material remains separate from release code-signing material and remains controlled by the License Generator environment.

## 7. CI / merge rules

- EliteSCADA CI remains the universal Coordinator gate for PRs to `main`;
- add/execute Windows release-specific validation according to actual Wave 13 impact, but never as a substitute for universal CI;
- diagnose failures before rerun;
- do not weaken assertions, authorization, signing verification or architecture to obtain green;
- integration uses expected-head protection;
- validate post-merge `main` before declaring Wave 13 complete;
- keep exact artifact SHA-256/signature/timestamp evidence with the accepted release checkpoint.

## 8. Explicit exclusions and gates

Do not include in Wave 13 unless separately authorized:

- new Drivers/protocols;
- unrelated HMI/Engineering feature work;
- Wave 14 owner-validation execution;
- Wave 15 feedback/corrections;
- Linux `.deb` implementation;
- physical Driver L4 claims.

Commercial DNP3 inclusion remains blocked until Step Function I/O `dnp3` 1.6.0 has an appropriate commercial license or an approved/revalidated replacement.
