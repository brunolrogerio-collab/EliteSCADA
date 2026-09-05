# LAST CHANGE — EliteSCADA

**Date:** 2026-09-04 BRT  
**Operational state:** **WAVE 14 #211 ACTIVE / C12–C19 AUTOMATED-GREEN PRE-DEMO BASELINE / C11 REVALIDATION RESUMES / CODESPACE PRODUCT OWNER HOMOLOGATION DEFERRED UNTIL AFTER CANONICAL DEMO / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only and validation-overlay commits do not redefine product-code authority.

## Product Owner sequencing decision

Product Owner clarified on 2026-09-04 BRT that the real fresh-Codespace visual homologation will be performed **after creation of the new canonical EEE DEMO**.

Therefore fresh Codespace homologation is no longer a pre-DEMO blocking gate. It remains mandatory before final Wave14 Product Owner acceptance, but it must validate the completed product together with the new canonical DEMO rather than the historical/current Preview fixture.

Do not claim that this homologation has already occurred.

## Current pre-DEMO product baseline

C19 isolated accepted PRODUCT SHA:

`43d2734b18dc8b78caaa917b9bf6a381ca47b202`

C19 was composed into `wave14/corrections-integration` through history-preserving merge of PR #257.

Exact combined C12–C19 PRODUCT SHA used as the current **pre-DEMO engineering baseline**:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

Tree:

`da6b406ac111cb40b99e5b13031601eb71606ddd`

This SHA has coherent automated acceptance evidence and is frozen as the baseline for C11 revalidation / canonical DEMO preparation. It is **not final Wave14 Product Owner acceptance**.

## Combined automated validation

Direct on exact product SHA `3fda880...`, all SUCCESS:

- EliteSCADA CI #1370 / `33934982242`;
- Wave11 Active HMI Runtime #298 / `33934982300`;
- Preview Licensing CI #320 / `33934982254`;
- L3 Seven-Driver Lab #276 / `33934982215`;
- Interop Lab Smoke #197 / `33934982216`.

Dedicated C03 carry-forward:

- Wave 14 C03 DNP3 Adapter #113 / `33935067545` — SUCCESS;
- documentation-only validation PR #260 had direct product parent `3fda880...` and was closed without merge.

Automated Preview harness carry-forward:

- harness SHA `086b5d6c92ccfa4f5dc7e947c787ffd953acc5e2`;
- Test Preview run `33935493882` — SUCCESS;
- validation-only PR #262 closed without merge.

The Test Preview result proves the harness/startup path, not the deferred final visual homologation.

## C19 architecture state

Preserve:

- Operational Event Engineering authoring through protected Preview/CAS/Apply;
- generic `emit_operational_event(definition_id, message=None, context=None)`;
- C14 Active Runtime as canonical event authority;
- `ServerScriptRuntimeManager._revisionGate` as the single revision-serialization gate for Script TAG/Server Memory/Operational Event access;
- `ServerScriptRunner.py` as a normal Debug/publish output asset.

Never restore the obsolete separate bridge gate/`AsyncLocal` design.

## Immediate route

1. Resume C11 revalidation now against the frozen pre-DEMO baseline `3fda880...`.
2. Reconcile remaining C11 evidence with the Product Owner sequencing decision. Do not treat fresh Codespace homologation itself as a pre-DEMO blocker.
3. C11 remains `IMPLEMENTATION LOCKED` until the Coordinator explicitly determines that the implementation-release conditions are satisfied and records `RELEASE C11 IMPLEMENTATION`.
4. After C11 release, construct the living deterministic canonical EEE DEMO exclusively with ordinary generic EliteSCADA mechanisms.
5. Then Product Owner performs the fresh Codespace visual homologation on the product **with the canonical DEMO present**.
6. Any defect found in that homologation creates the appropriate correction/revalidation loop before final Wave14 acceptance.
7. Only after final Wave14 acceptance resume Wave13 #205/#207 packaging/signing.

## Hard boundaries

- PR #212 remains OPEN/DRAFT and must NEVER merge to `main` without later explicit Product Owner authorization;
- C11 is not implicitly released by this sequencing change;
- Wave13 #205/#207 remains PAUSED;
- no weakening tests/security/identity/authorization/licensing/lifecycle;
- diagnose any red before rerun;
- no EEE-specific service/Driver/package/private runtime/DEMO-only bypass;
- Alarm / Operational Event / Audit remain distinct;
- backend Active revision remains canonical authority.

## Mandatory references

Read in order:

1. `PROJECT GOAL.md`;
2. this file;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. C11 pre-DEMO / Pass-2 / HMI clarification / canonical DEMO requirements;
6. C19 implementation/progress docs;
7. `docs/CI-VALIDATION-POLICY.md`;
8. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
9. live issue #211 and PR #212.
