# LAST CHANGE — EliteSCADA

**Date:** 2026-09-02 (BRT)  
**Operational state:** **WAVE 12 #201 — COMPLETE / ACCEPTED / CLOSED; TEST PREVIEW #208/#210 — ACTIVE VALIDATION HARNESS; WAVE 14 #211 — ACTIVE EARLY PRODUCT-OWNER VALIDATION; WAVE 13 #205/#207 — PAUSED AT GREEN CHECKPOINT**

> Mutable Coordinator resume point. `PROJECT GOAL.md` governs permanent product intent. Live GitHub refs and exact-SHA CI override copied prose. Documentation-only `[skip ci]` commits may advance `main` beyond the latest validated product-code SHA without superseding that product baseline.

## 1. Accepted product baseline

Wave 12 Hardening remains **COMPLETE / ACCEPTED**.

Final accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Accepted runtime authority remains:

`Working -> saved Revision -> Published -> Active -> HMI Runtime`

Runtime uses persisted Active Engineering only; mutable Working never drives HMI Runtime directly.

Exact post-merge Wave 12 evidence:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

## 2. Direction change authorized on 2026-09-02

Real owner use through the Codespaces Test Preview exposed multiple product/usability findings before Wave 13 signing was complete.

Development Lead direction therefore changes the order of work:

- Wave 13 #205 / PR #207 is **PAUSED**, preserving its already-green repository-side checkpoint;
- Wave 14 #211 **Product-owner validation** is now **ACTIVE EARLY**;
- Preview #208 / PR #210 remains the temporary browser test harness used to execute that validation;
- fixes required to continue meaningful owner validation may be handled during Wave 14;
- non-blocking refinements/enhancements should be retained for Wave 15 rather than expanding Wave 14 without limit.

This is an intentional ordering change, not a rejection of Wave 13 implementation.

## 3. Wave 13 preserved checkpoint

Issue #205 and draft PR #207 remain open but paused.

Preserved fully validated implementation SHA:

`9f26a2bc02ae77017e266c52ff128dc39eece4b4`

Validation retained:

- Wave 13 Windows Release #27 / `33643546191`: **SUCCESS**;
- EliteSCADA CI #1134 / `33643546119`: **SUCCESS**;
- L3 Seven-Driver Lab #102 / `33643546111`: **SUCCESS**;
- Wave 11 Active HMI Runtime #64 / `33643546139`: **SUCCESS**.

Current Wave 13 branch head after documentation synchronization:

`fda87ba4445127c174f6ea533a6bcabaabc7bb20`

No signing/merge/release work should advance while Wave 14 owner validation is actively changing the product baseline. Before Wave 13 resumes, re-audit live `main` and incorporate accepted Wave 14 corrections.

DNP3 commercial distribution remains unauthorized independently of signing status.

## 4. Test Preview state

Tracking:

- issue #208;
- draft PR #210;
- branch `preview/codespaces-test-preview`.

The Preview has moved from pure infrastructure bring-up into real owner homologation. Real Codespace evidence already confirmed:

- exact .NET SDK 10.0.400;
- disposable `/etc/machine-id` required by normal fail-closed licensing;
- protected admin secret injection;
- Web port 5173 reachable after successful bootstrap;
- actual EliteSCADA login through the browser;
- a pre-existing Script Engineering contrast defect discovered by owner use.

Operational runbook is versioned on the Preview branch at:

`docs/CODESPACES-PREVIEW-RUNBOOK.md`

It records when to use browser reload, Preview restart, Rebuild Container or a fresh Codespace, plus the real 502/SDK/machine-id/password diagnostic patterns already encountered.

Administrative username:

`EliteSCADA`

Protected secret name:

`ELITESCADA_PREVIEW_ADMIN_PASSWORD`

Never commit or echo the supplied password into source, docs, workflows, images, packages, logs or normal artifacts.

## 5. Wave 14 active state

Issue #211 — **Wave 14 — Product-owner validation** — is the active product-validation tracker.

Use the real browser Preview to exercise the product area by area. Findings are classified as:

- **A — Validation blocker:** prevents meaningful continuation; may be corrected immediately during Wave 14;
- **B — Functional defect:** wrong behavior; fix in Wave 14 when it affects release confidence or later validation;
- **C — Usability defect:** works technically but materially harms owner validation; fix when blocking/material;
- **D — Enhancement/preference:** record for Wave 15 or later.

The first confirmed owner finding is the pre-existing Script Engineering contrast defect discovered in the real Codespace. Its correction started in PR #210 because the surface was effectively unreadable and blocked meaningful validation.

## 6. Exact next action

1. re-check live `main`, #208, #210, #211 and exact current CI before code changes;
2. keep Wave 13 #205/#207 paused;
3. finish making the Preview reproducible enough for repeated owner validation;
4. validate one EliteSCADA product area at a time through the real browser UI;
5. record each finding in Wave 14 #211 with severity/classification and exact exercised SHA;
6. correct only blockers/material defects needed to keep validation trustworthy;
7. require universal `EliteSCADA CI` plus impact-specific gates for product-code corrections;
8. transfer non-blocking feedback to Wave 15 rather than losing it;
9. establish an accepted Wave 14 product baseline before Wave 13 signing resumes.

Do not resume Wave 13 merely because its old branch is green: final signing must target the post-owner-validation product, not a stale pre-validation snapshot.

## 7. Windows installer request rule

Development Lead direction: whenever a Windows installer/build is requested during this validation period, the installer must be generated from the **latest corrected product build/baseline produced by the ongoing Wave 14 owner-validation work**, not from the preserved pre-validation Wave 13 product snapshot.

Operationally:

1. identify the latest product SHA that contains the corrections already accepted for owner testing;
2. validate that exact product SHA with the required universal and impact-specific gates;
3. apply/rebase the proven Wave 13 Windows packaging machinery onto that corrected product baseline;
4. generate the requested Windows test installer/candidate from those corrected bytes;
5. record the exact product SHA and packaging SHA used for the installer;
6. never silently fall back to `9f26a2bc...`, `fda87ba...` or another stale Wave 13 product snapshot merely because its release workflow is already green.

The preserved Wave 13 branch is the authority for the **Windows packaging/signing mechanism**, while the evolving accepted Wave 14 baseline is the authority for the **product content** to be packaged.

A test installer requested before final Authenticode/commercial-release acceptance may remain a non-commercial test candidate according to the Development Lead's request, but it must still contain the latest corrected product content and must not be represented as the final signed/commercial release.
