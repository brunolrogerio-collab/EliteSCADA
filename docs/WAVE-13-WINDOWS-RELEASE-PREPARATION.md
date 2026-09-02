# Wave 13 — Windows x64 Release + Authenticode Preparation

**Status:** PREPARATION COMPLETE / IMPLEMENTATION NOT STARTED  
**Prepared:** 2026-09-01 BRT  
**Issue:** #205 — Wave 13 — Signed Windows x64 package + Authenticode release verification  
**Entry product-code baseline:** accepted Wave 12 `main` `63bced02426fcb84b26028913f6c68feb3457d80`

Wave 12 issue #201 is complete/accepted. This document prepares Wave 13 for the next Coordinator. It does not start implementation and deliberately does not create a Wave 13 implementation branch.

A later documentation-only `[skip ci]` preparation merge may advance live `main` beyond the accepted product-code SHA above. The Coordinator must therefore branch from live `main` only after the required repository/CI audit.

## Objective

Produce and verify the first controlled Windows x64 EliteSCADA release package with Authenticode signing and trusted timestamping, while preserving all accepted product, security, licensing, Engineering and Runtime contracts through Wave 12.

Wave 13 is a release-engineering and trust-boundary wave. It is not a feature-expansion wave.

## Entry conditions

- Wave 12 issue #201 complete/accepted: **SATISFIED**.
- Accepted Wave 12 product-code baseline known: **SATISFIED** — `63bced02426fcb84b26028913f6c68feb3457d80`.
- Post-merge universal CI on that exact baseline: **SATISFIED** — EliteSCADA CI #1096 / `33576603185` SUCCESS.
- Post-merge L3 on that exact baseline: **SATISFIED** — L3 #92 / `33576603158` SUCCESS.
- Wave 13 coordination issue #205 exists: **SATISFIED**.
- Wave 13 implementation branch: **INTENTIONALLY NOT CREATED** until the next Coordinator revalidates live GitHub state.

## Mandatory first slice: audit and release design

Before writing package/signing code, inspect the real repository and record the findings in issue #205 or a dedicated Wave 13 ledger. At minimum determine:

1. the current Windows publish entry points and all user-facing/runtime executable artifacts;
2. the package contents required for the .NET host, React assets, Pyodide/client scripting assets, configuration and built-in resources;
3. the launch/installation model and whether an installer is actually required for the first Wave 13 acceptance artifact;
4. the exact Windows runtime target (`win-x64`) and self-contained/framework-dependent decision based on the product's real deployment needs;
5. which PE files must be Authenticode-signed and in what order relative to packaging;
6. the release identity/version source and how it appears in binaries/package/manifest;
7. the controlled code-signing authority, certificate-chain requirements and trusted RFC3161 timestamp provider;
8. how an unsigned CI-built candidate moves to the protected signing boundary without exposing signing credentials;
9. deterministic package manifest contents, including SHA-256 for distributed artifacts;
10. verification commands/tests that reject unsigned, incorrectly signed, untimestamped, modified or unexpected artifacts;
11. focused package smoke covering product launch, Web UI, Demo/licensed-mode surface, graphical License Generator and Active HMI Runtime path;
12. dependency-license review, especially DNP3 disposition for any artifact intended for commercial distribution.

Do not hard-code an installer technology, certificate provider or signing service until this audit proves it fits the current repository and release environment.

## Locked Windows trust contract

`PROJECT GOAL.md` already locks the permanent direction:

- production Preview/installable Windows executables and installers are Authenticode-signed with an authorized organizational code-signing identity;
- signatures include a trusted timestamp;
- publisher identity is preserved and verified before publication;
- unsigned internal/early Preview builds may exist only when explicitly identified as unsigned/untrusted development artifacts;
- compilation alone is not a trust claim;
- signing credentials never enter source control or normal build artifacts;
- protected signing service or hardware-backed keys are preferred;
- SmartScreen reputation is separate from Authenticode validity.

Wave 13 must implement this boundary without weakening it for CI convenience.

## Signing credential boundary

Private code-signing key material must not be committed to the repository, embedded in binaries/packages, printed in logs or copied into ordinary GitHub Actions artifacts.

Do not place a raw PFX/private key in normal CI merely because GitHub Secrets can hide a string. The preferred architecture is an explicit protected signing boundary such as a trusted signing service or hardware-backed key controlled by the release authority. Normal CI may build unsigned candidates and verify public signature evidence after controlled signing.

License-signing private material and Authenticode code-signing material are separate trust domains. Do not reuse or conflate them.

## Package integrity / verification direction

The accepted Wave 13 artifact set should have a deterministic manifest recording, at minimum:

- product/version/release identity;
- source product-code commit SHA;
- package file names/roles;
- SHA-256 hashes;
- expected signing status for executable artifacts;
- signature subject/publisher identity as appropriate;
- trusted timestamp verification evidence;
- build/signing verification version or schema.

Verification must fail closed if a required file is missing, an unexpected executable appears, a hash differs, Authenticode is absent/invalid, or a required timestamp cannot be validated.

The exact release artifact hash and signature/timestamp evidence belong in the final Wave 13 acceptance record.

## Product regression direction

Packaging must not silently change accepted behavior. Focused checks should preserve at least:

- product startup and Web UI availability;
- trusted local login/security boundary;
- Demo mode entitlement and 300-minute-session contract (without waiting 300 minutes in a release smoke; use existing regression coverage plus focused package behavior);
- machine request/license installation surface and graphical License Generator usability;
- `.escadapkg` Open/Save and canonical Engineering authority;
- `Working -> Revision -> Published -> Active -> HMI Runtime` authority;
- required built-in assets/Dynamos/Pyodide/static Web resources;
- configuration and persistent-state paths appropriate for the Windows package;
- supported Driver/module load behavior without claiming physical L4 validation.

## CI / merge direction

EliteSCADA CI remains the universal PR gate. Wave 13 should add or use a focused Windows release workflow where justified by the implementation, preferably on a Windows runner for actual publish/signature/package verification.

Specialized release/signing validation never substitutes for universal CI.

Before merge:

- validate exact PR head;
- diagnose any failure before rerun;
- preserve signing/security assertions rather than weakening them;
- merge with expected-head protection.

After merge, validate the resulting `main` before closing Wave 13.

## Explicit non-goals

- no new external protocol/Driver family;
- no unrelated HMI/Engineering features;
- no redesign of accepted canonical Engineering/Runtime authority without a demonstrated defect;
- no Wave 14 owner-validation execution;
- no Wave 15 feedback/correction work;
- no physical L4 claims;
- no Linux `.deb` implementation unless explicitly authorized by the Development Lead;
- no private signing keys/certificates in repository, normal GitHub Actions secrets/artifacts or distributed product files.

## DNP3 commercial-distribution gate

Step Function I/O `dnp3` 1.6.0 remains non-commercial/non-production under its public license. A signed package is not automatically a commercially releasable package.

Before any commercial Wave 13 artifact includes/enables DNP3, the Development Lead must either record an appropriate Step Function commercial license or approve a replacement dependency and require Driver revalidation. An explicitly non-commercial/internal validation package must be labeled accordingly and must not be represented as clearing the commercial-distribution gate.

## Coordinator start protocol

The next Coordinator must:

1. re-read live `main`, open PRs/issues and current Actions;
2. read `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/CURRENT-COORDINATOR-HANDOFF.md`, `docs/ROADMAP.md`, this file, issue #205 and `docs/CI-VALIDATION-POLICY.md`;
3. confirm Wave 12 remains accepted and no later repository changes invalidate the entry assumptions;
4. audit current publish/package/signing surfaces;
5. persist the Wave 13 implementation slices and acceptance evidence plan;
6. only then create a dedicated Wave 13 implementation branch from live `main`;
7. keep exact artifact hashes, signatures, timestamps, workflow IDs and source SHAs as release evidence.

## Acceptance direction

Wave 13 is complete only when the controlled Windows x64 artifact is built from a known source baseline, required binaries are Authenticode-signed with trusted timestamp, package integrity/signature verification is automated and fail-closed, focused Windows package smoke passes, universal and affected specialized CI are green on exact heads, post-merge `main` is validated, and continuity docs are ready for Wave 14 product-owner validation.
