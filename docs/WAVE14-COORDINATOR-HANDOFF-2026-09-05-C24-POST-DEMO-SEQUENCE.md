# Wave 14 Coordinator Handoff — 2026-09-05 — C24 and post-demo sequencing

> **Authority:** GitHub is the official project memory. This handoff is a coordination snapshot only. Revalidate every branch, PR, SHA and CI state live before acting. Live GitHub always wins.

## 1. Non-negotiable governance

- Repository: `brunolrogerio-collab/EliteSCADA`.
- Integration branch: `wave14/corrections-integration`.
- Integration PR: #212. It must remain **OPEN/DRAFT** and must **NOT** be merged to `main` without later explicit Product Owner authorization.
- Do not alter `main` directly.
- Wave13 PRs #205/#207 remain paused.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose red CI before rerunning. Never weaken tests, contracts, security, identity, lifecycle or validation to obtain green CI.
- C11 must not be declared ACCEPTED, INTEGRATED, FROZEN or DEMO-READY until canonical package, final CI and Product Owner visual homologation are complete.
- #266 is C11 validation-only to `main`: **NEVER MERGE**. Close without merge when appropriate.
- #263 may merge C11 into integration only after full C11 acceptance.
- Generic product defects must remain generic fixes. Do not create EEE-specific workarounds or reimplement EEE as a special service/driver.

At the time this handoff was written, integration was revalidated at `35b3bf7984b8c5753710409295ea9bd5e3ad9b25`, whose product parent is the accepted Wave14 product SHA `5962bee401fadd700041e7c61cd430d4b4f28e27`. Revalidate before any mutation.

## 2. C24 — implementation exists, acceptance is BLOCKED

### Purpose

C24 fixes the clean First Project bootstrap defect exposed by the C11 canonical package gate. `BuiltinDynamoLibrary.Create()` installed `dynamo.pump.standard` with an external `TemplateKey` dependency on `pump.standard`, while that template existed only in historical Demo seeding. A clean First Project therefore correctly failed normal Publish validation with `DYNAMO_TEMPLATE_NOT_FOUND`.

Derived invariant:

> A built-in library installed into a clean First Project must be internally dependency-closed, or any external dependency must itself be a generic built-in installed by the same bootstrap.

### Branch and commits

- Branch: `wave14/c24-first-project-builtin-consistency`.
- Base: exact accepted product SHA `5962bee401fadd700041e7c61cd430d4b4f28e27`.
- Product fix commit: `2f9388b6053880db0e3258ddd17591343950540e` — `fix(c24): keep builtin dynamo library self-contained`.
- Candidate/regression commit: `42513edbfc4a8093c7aa99bb2ca09e405b987e7a` — `test(c24): prove fresh first project can save and publish`.
- Implementation PR: #278, C24 -> `wave14/corrections-integration`, OPEN/DRAFT at handoff time.
- Validation-only PR: #279, C24 -> `main`, OPEN/DRAFT at handoff time, **NEVER MERGE**.

The product change removes only the Demo-only `pump.standard` template dependency from the built-in pump Dynamo. It does not weaken the validator and does not touch security, identity, lifecycle or EEE-specific code.

Regression coverage in `tests/Scada.Drivers.Tests/EngineeringFirstProjectBootstrapTests.cs` proves both:

1. the built-in Dynamo library has exactly 8 built-ins and no external equipment-template dependency;
2. a real clean First Project can Save and Publish through the normal persistence/publication path with built-in bootstrap content only.

### Exact-SHA CI evidence for `42513edbfc4a8093c7aa99bb2ca09e405b987e7a`

Required matrix observed:

- Wave11 Gate #340: SUCCESS.
- Wave14 Preview Licensing CI #362: SUCCESS.
- Wave14 Interop CI #239: SUCCESS.
- Wave11 L3 Lab #318: SUCCESS.
- EliteSCADA CI #1412: **FAILURE**.
- Additional C03 #118: SUCCESS, not part of the required five.

EliteSCADA CI run id: `33985793295`.

Backend/build/runtime-smoke portion is green and includes the new C24 regression. The failure is in Chromium integration/E2E; Firefox is skipped after Chromium fails.

Original Chromium failing job: `101359092828`.
Latest rerun Chromium failing job: `101378014398`.

### Important process note

A Chromium rerun was accidentally triggered before the original red had been fully diagnosed. That was contrary to Wave14 governance and must **not** be used to wave the failure away. The rerun also failed at the same candidate SHA. Therefore there is no evidence supporting a transient/flaky classification.

The exact Playwright spec/assertion from the failed Chromium log was not durably recovered before this handoff. The next coordinator must fetch and diagnose job `101378014398`, and compare `101359092828` if useful, **before another rerun**.

### C24 status at handoff

**NOT ACCEPTED. NOT INTEGRATED. DO NOT MERGE #278 YET. DO NOT CLOSE #279 AS SUCCESS YET.**

Next C24 action:

1. Diagnose the repeated Chromium failure from job `101378014398`.
2. If deterministic product/test defect, make the smallest valid fix on the C24 branch, producing a new candidate SHA, then run the entire required five-gate matrix from scratch.
3. Do not weaken or skip E2E to make it green.
4. Only when the exact candidate is 5/5:
   - close #279 **without merge**;
   - merge #278 only into `wave14/corrections-integration`, preserving history;
   - capture the resulting integration SHA;
   - keep #212 DRAFT and unmerged;
   - require the post-merge exact integration SHA to pass the required product CI before marking C24 accepted.

## 3. Product Owner decision — reorder post-demo work BEFORE final EEE canonicalization

This decision supersedes the previous sequencing that would immediately finish/freeze the canonical EEE `.escadapkg` after C24.

Reason: the post-demo corrections below may change application/package/bootstrap contracts. Freezing the EEE package first could deliberately produce a canonical demo file that becomes incompatible one correction later.

New order:

1. **Finish and accept C24.**
2. **Implement and accept the post-demo product corrections that can affect application/package/bootstrap compatibility.**
3. **Only then sync/adapt the EEE demo to the newly accepted product contracts.**
4. Export/version the final canonical EEE `.escadapkg` from that final contract.
5. Run final C11 exact-SHA gates, Preview validation and Product Owner visual homologation.
6. Only after all acceptance conditions are met may #263 merge C11 to integration; #266 remains validation-only and must close without merge.

C11 therefore remains deliberately **not accepted, not frozen and not demo-ready** while post-demo compatibility-affecting corrections are developed.

## 4. Post-demo decision — Application Engineering Lock for IP protection

The old idea of an optional password required to Import/Export an `.escadapkg` is **superseded**. Do not implement Import/Export password gating.

The intended feature is an optional **Application Engineering Lock** whose sole purpose is intellectual-property protection for the developer/integrator, especially in OEM/serialized-machine scenarios.

Required semantics:

- The feature is optional. Most projects may never use it.
- The password is **not** required to Export the application.
- The password is **not** required to Import the application.
- It is not an Authority/user credential.
- An unlocked application exposes normal/full Engineering functions.
- A locked running application exposes a restricted Engineering surface that still permits customer/system administration without exposing or allowing editing of application engineering.
- The restricted surface must retain at least:
  - Authority login/password/profile administration;
  - licensing administration;
  - application Import/Export;
  - importing another solution;
  - ability to unlock the currently running application with that application's Engineering Lock password.
- Presence of a stored password and the current lock flag are separate state:
  - an application may contain a configured password while `locked=false`;
  - a configured password does not automatically mean the application is currently locked;
  - if no lock password exists, there is no password-based application lock to authenticate against;
  - Engineering UI must support the legitimate lock/unlock lifecycle according to the active state and authorization rules.
- The exported application carries the protected password verifier/secret metadata, but **the entire `.escadapkg` is not encrypted**. This is an IP-access barrier, not whole-package confidentiality.

### Cryptography is intentionally NOT frozen yet

The Product Owner suggested storing the Engineering Lock secret encrypted in the export with a key available to the EliteSCADA build. Treat that as the desired product behavior, **not as approval of a fixed/compiled symmetric key architecture**.

A previous fixed/embedded-key idea was explicitly not accepted as a closed cryptographic design. Before implementation is considered complete, define and security-review a mechanism that does not pretend a client-shipped static decryption key is strong secret protection. Preserve the required user-facing semantics while choosing the actual storage/verifier/key architecture deliberately.

Do not conflate this application-lock secret with Authority credentials or the Security Authority backup encryption model.

Canonical tracking issue: #280.

## 5. Post-demo decision — Restore Backup directly from fresh-install bootstrap

On a fresh or cleaned installation, the user should not be forced to create a throwaway local user and empty project before restoring an existing system.

Add a **Restaurar backup** path to the initial bootstrap screen where the product currently offers creation of the first user/project.

The restore flow must support, in one recovery surface:

- import/restore of the application;
- import/restore of Authority, including the Authority-import password/credential required by that backup mechanism;
- optional attachment/import of an already available license file;
- license file is not mandatory to perform the restore.

Goal: restore a complete existing system directly from clean installation without first creating disposable application/user state.

This modifies/extends the earlier recovery design in PR #273. PR #273 is DESIGN ONLY, was based on older C11 state and must not be merged blindly. Revalidate its assumptions against the accepted product before implementation.

Existing recovery concepts worth preserving/revalidating include application `.escadapkg`, native DB/Historian backup, separately protected Security Authority backup, and clean-install recovery bootstrap. The new Product Owner decision changes the entry UX by making restore a first-class option before creation of a new user/project.

Canonical tracking issue: #280.

## 6. Other pending post-demo design work

PR #274 remains DESIGN ONLY and was based on older C11 state. Revalidate before implementation.

Current intended direction includes:

- Runtime identity UX with discrete current username and actions `Trocar usuário` / `Sair`;
- Runtime-only users must never gain Engineering;
- switch-user flow invalidates the old session first, blocks stale UI, then reloads backend identity/capabilities;
- detailed contextual Help/Manual with stable Help IDs, especially Drivers/Sources/TAG/addressing/Scripts/Reports/HMI;
- manual follows active language, Help IDs remain stable, and a versioned local/offline manual is preferred.

## 7. C11 state to preserve while post-demo work proceeds

Historical C11 branch: `wave14/c11-canonical-eee-demo`.
Historical C11 head before C24 sync: `41d24d89c3b9d2b881215255e44023fabde262f3`. Revalidate live.

- #263: C11 -> integration, OPEN/DRAFT historically; do not merge until full final acceptance.
- #266: C11 -> main validation-only; NEVER MERGE.
- At `41d24...`, Preview #360, Interop #237, Elite #1410 and L3 #316 were green; Wave11 #338 failed only at the new clean canonical package portability/bootstrap gate that exposed the generic C24 defect.

After C24 and the compatibility-affecting post-demo corrections are accepted into integration:

1. sync accepted integration into C11 normally, preserving both histories, no rebase;
2. adapt the EEE application to the final accepted package/recovery/Engineering-Lock contracts;
3. run #266 validation-only gates on the exact C11 SHA;
4. require the full five-gate matrix green, especially canonical Wave11;
5. fetch the exact `EliteSCADA-EEE-Demo` artifact from that Wave11 run;
6. validate `.escadapkg`, `.sha256` and provenance, including `projectKey=eee-demo`, `activeProjectKey=eee-demo`, exact generator SHA and exact source-product SHA;
7. version the exact exported bytes/checksum/provenance without manually editing the package;
8. update Preview to the canonical EEE app;
9. run final gates;
10. perform real Product Owner visual homologation in Codespaces;
11. only then may C11 be called accepted/frozen/demo-ready and #263 be merged to integration. Close #266 without merge.

## 8. Expected coordinator operating mode

When the Product Owner says **`siga`**, continue autonomously through the next safe tasks instead of stopping after each small step. Pause only for a real technical/governance blocker, an action requiring Product Owner authorization by policy, or an explicit request to pause.

Do not rely on this document as authority over live GitHub. Its purpose is to make the next revalidation fast and unambiguous.
