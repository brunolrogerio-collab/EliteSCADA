# LAST CHANGE — EliteSCADA

**Date:** 2026-09-05 BRT  
**Operational state:** **WAVE 14 ACTIVE / C11 CANONICAL PACKAGE GATE BLOCKED BY GENERIC FIRST-PROJECT BOOTSTRAP INCONSISTENCY / C23 INTEGRATED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA CI before acting. Documentation-only commits do not redefine product-code authority.

## Accepted integration product authority

Last accepted product-code integration commit:

`5962bee401fadd700041e7c61cd430d4b4f28e27`

It integrates C23, which embeds the production licensing public verification key in EliteSCADA and makes `elite-prod-2026-01` the License Generator default. The private signing key remains outside the product/repository.

Post-merge exact-SHA evidence on `5962bee...` is 5/5 SUCCESS:

- EliteSCADA CI #1408 / `33975104580`;
- Wave11 Active HMI Runtime #336 / `33975104596`;
- Preview Licensing CI #358 / `33975104578`;
- L3 Seven-Driver Lab #314 / `33975104579`;
- Interop Lab Smoke #235 / `33975104582`.

## Current C11 head

`wave14/c11-canonical-eee-demo`

Exact head:

`41d24d89c3b9d2b881215255e44023fabde262f3`

This C11 head already contains accepted C23 through normal sync merge `3c38d5c603e9d8226b338c516644c331edb71ccd` and updates the canonical package gate so `eee-demo` is created in an isolated database through the supported First Project endpoint.

Exact-SHA gate state:

- Preview Licensing CI #360 — SUCCESS;
- Interop Lab Smoke #237 — SUCCESS;
- EliteSCADA CI #1410 — SUCCESS;
- L3 Seven-Driver Lab #316 — SUCCESS;
- Wave11 Active HMI Runtime #338 — **FAILURE**.

The historical Wave11 lifecycle passed 22/22. The red is isolated to the new C11 package portability gate.

## Diagnosed red — generic product gap

The fresh First Project bootstrap is internally inconsistent.

`SaveFirstProjectAsync` clears the legacy workspace and seeds the built-in Dynamo library plus initial Developer role. Built-in Dynamo `dynamo.pump.standard` references template `pump.standard`, but First Project bootstrap does not seed that template.

At C11 Publish, normal validation therefore returns:

`DYNAMO_TEMPLATE_NOT_FOUND`

This is a **generic First Project / built-in library product defect**, not an EEE Demo defect.

Do not rerun Wave11 #338 blindly and do not patch C11 by adding historical/demo entities merely to satisfy the validator.

## Immediate route

1. revalidate live state and package numbering;
2. open a narrow generic correction package from current accepted integration product bytes, provisionally C24;
3. make a newly created First Project self-consistent with its built-in Dynamo/template dependencies;
4. add regression coverage for normal First Project Save/Publish;
5. run exact-SHA product gates;
6. integrate only into `wave14/corrections-integration` after acceptance;
7. sync accepted correction into C11 by normal merge;
8. rerun C11 package portability;
9. after green, verify/export/version canonical `EliteSCADA-EEE-Demo.escadapkg`, checksum and provenance;
10. update Preview to consume the canonical package;
11. perform Product Owner fresh-Codespace visual homologation;
12. only after final Wave14 acceptance resume Wave13 #205/#207.

## Post-DEMO Product Owner notes preserved

- System Recovery / Backup & Restore design is in DRAFT PR #273.
- Runtime session UX + contextual manual design is in DRAFT PR #274.
- Runtime must keep the current user name visible beside the discreet session icon/control.
- Help/manual must be multilingual and follow the active EliteSCADA UI locale. The same stable Help ID resolves to the equivalent localized topic, e.g. pt-BR UI -> pt-BR help, English UI -> English help.
- Optional password protection for application `.escadapkg` remains a deferred concept. **Before implementing it, ask the Product Owner about the flaw he explicitly identified in the proposed design.** Do not lock or code the crypto architecture before that discussion.

See `docs/WAVE14-PRODUCT-OWNER-PENDING-DESIGN-NOTES.md` and `docs/CURRENT-COORDINATOR-HANDOFF.md` for the complete continuation state.

## Hard boundaries

- PR #212 remains OPEN/DRAFT and must NEVER merge to `main` without later explicit Product Owner authorization;
- PR #263 remains DRAFT and must not merge until C11 exact acceptance;
- PR #266 is validation-only and must NEVER MERGE;
- Wave13 remains PAUSED;
- no force-push/rebase/destructive cleanup;
- diagnose red before rerun;
- no EEE-specific workaround for a generic product defect.
