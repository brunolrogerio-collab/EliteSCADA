# LAST CHANGE — EliteSCADA

**Date:** 2026-09-05 BRT  
**Operational state:** **WAVE 14 ACTIVE / C24 ACCEPTED + INTEGRATED / POST-DEMO COMPATIBILITY CORRECTIONS NEXT / C11 PRESERVED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only commits after an accepted product SHA do not redefine accepted product bytes.

## Current accepted product authority

Wave 14 integration branch:

`wave14/corrections-integration`

Accepted integration product merge:

`40a491c2de2403f2934b8bae647c35072d5c2496`

Accepted C24 exact product/test SHA:

`ff5eac6ad12865b174b6ca602a13d01165e57b36`

C24 exact-SHA evidence is fully green:

- Preview Licensing CI #365 / `33996011571` — SUCCESS;
- Interop Lab Smoke #242 / `33996011614` — SUCCESS;
- Wave 14 C03 DNP3 Adapter #120 / `33996011523` — SUCCESS;
- EliteSCADA CI #1415 / `33996011599` — SUCCESS, including Chromium E2E;
- L3 Seven-Driver Lab #321 / `33996011595` — SUCCESS;
- Wave 11 Active HMI Runtime #343 / `33996011620` — SUCCESS additional compatibility evidence.

Implementation PR #278 merged **only** into `wave14/corrections-integration`. Validation-only PR #279 was closed **without merge**.

## C24 correction

C24 fixed the generic clean First Project inconsistency exposed by C11:

- built-in `dynamo.pump.standard` no longer depends on historical DEMO-only template `pump.standard`;
- built-in Dynamo library remains 8 generic built-ins;
- clean First Project Save/Publish is regression-tested through normal product paths;
- Publish/import validation was not weakened;
- legacy Chromium E2E now asserts serialized `templateKey: null`, the actual JSON representation of no external template dependency.

A previous C24 SHA failed Chromium only because the test incorrectly expected JavaScript `undefined` instead of serialized JSON `null`. The failure was diagnosed from the Playwright artifact before correction; the complete matrix was then rerun on `ff5eac6...` and passed.

## Binding Product Owner sequence after C24

Issue #280 and its Product Owner clarification supersede the older sequence that would immediately sync/freeze C11 after C24.

Current sequence:

1. C24 accepted — **DONE**;
2. implement and exact-SHA accept compatibility-affecting post-DEMO corrections;
3. only then sync/adapt the EEE C11 branch to the accepted product contracts;
4. export/version/freeze the canonical `EliteSCADA-EEE-Demo.escadapkg`;
5. run final C11 gates, Preview and Product Owner fresh-Codespace visual homologation;
6. only after final Wave14 acceptance resume Wave13 #205/#207.

C11 therefore remains intentionally **NOT ACCEPTED / NOT FROZEN / NOT DEMO-READY** during these corrections.

A premature C24->C11 sync PR #281 was closed without merge after this newer sequence was revalidated. C11 remains at pre-sync SHA:

`41d24d89c3b9d2b881215255e44023fabde262f3`

## Next correction packages

Package numbering is live-revalidated and C25 is free.

Coordinator split:

- **C25 — Application Engineering Lock**: generic optional application/IP protection affecting Engineering visibility and package metadata/state;
- **C26 — Restore-first bootstrap**: generic clean-install `Restaurar backup` path combining application restore, Authority restore and optional license attachment without disposable user/project creation.

Both derive from issue #280 and must remain generic product corrections.

### C25 binding semantics

- optional lock; no password means normal Engineering;
- Import and Export do **not** require the Engineering Lock password;
- lock secret is not an Authority credential;
- password presence and `locked` state are distinct;
- locked application exposes restricted Engineering administration only, retaining Authority administration, licensing, Import/Export, solution replacement and explicit unlock;
- wrong password stays locked and leaks no protected Engineering content;
- package carries lock metadata/state, but the whole `.escadapkg` is not encrypted;
- cryptographic/storage architecture must be deliberately security-reviewed; do not ship a pretend-secure fixed symmetric key merely because it is convenient.

## Permanent governance

- #212 remains OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization;
- #263 remains C11 implementation DRAFT -> integration only;
- #266 remains C11 validation-only -> `main` and **MUST NEVER MERGE**;
- #273/#274 remain design-only and must not be merged blindly onto moving product state;
- no force-push/rebase/destructive cleanup;
- diagnose every red before rerun;
- no EEE-specific workaround for a generic product requirement;
- backend Active revision remains Runtime authority;
- authorization/security/licensing remain backend/host-owned and fail closed;
- Alarm / Operational Event / Audit remain distinct.

Read next: `docs/CURRENT-COORDINATOR-HANDOFF.md`, issue #280 and `docs/WAVE14-COORDINATOR-HANDOFF-2026-09-05-C24-POST-DEMO-SEQUENCE.md`.
