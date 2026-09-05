# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-05 BRT  
**Status:** **WAVE 14 ACTIVE / C11 IMPLEMENTATION LOCKED / CANONICAL EEE PACKAGE GATE BLOCKED BY GENERIC FIRST-PROJECT BOOTSTRAP GAP / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs, PR state and exact-SHA workflows before taking any action. This handoff is documentation-only and does not redefine accepted product bytes.

## 1. Permanent governance

- repository: `brunolrogerio-collab/EliteSCADA`;
- Wave 14 integration branch: `wave14/corrections-integration`;
- integration PR #212 must remain **OPEN/DRAFT** and must **NOT** merge to `main` without later explicit Product Owner authorization;
- C11 implementation PR #263 targets only `wave14/corrections-integration`, remains DRAFT until exact C11 acceptance;
- C11 validation-only PR #266 targets `main` only to trigger main-scoped workflows and must **NEVER MERGE**;
- Wave13 #205/#207 remains paused;
- diagnose every red before rerun;
- never weaken tests, security, identity, lifecycle or package contracts merely to obtain green;
- no destructive rebase/force-push/branch deletion or unrelated cleanup;
- backend Active revision remains runtime authority;
- Alarm, Operational Event and Audit remain distinct;
- C11 may use only normal generic EliteSCADA project mechanisms. A missing generic capability becomes a separate product correction package, never an EEE-specific workaround.

## 2. Exact accepted integration product authority

Last accepted product-code integration commit before this documentation handoff:

`5962bee401fadd700041e7c61cd430d4b4f28e27`

This merge integrated C23, the embedded production licensing trust anchor, into `wave14/corrections-integration` only.

C23 product behavior:

- production public verification key is compiled into EliteSCADA;
- production `KeyId` is `elite-prod-2026-01`;
- public-key SHA-256 fingerprint is `62244a1ca23f4a03d581e3df8fb46508264e29cd13d8747992710d3b0b4aac72`;
- normal customer installation no longer needs a public PEM file to validate production licenses;
- external verification keys may be additive but cannot replace the built-in production trust anchor;
- License Generator GUI/CLI defaults to the production KeyId;
- the private signing PEM is intentionally outside the repository/product.

Post-merge exact-SHA evidence on `5962bee...` is 5/5 SUCCESS:

- EliteSCADA CI #1408 / run `33975104580`;
- Wave11 Active HMI Runtime #336 / `33975104596`;
- Preview Licensing CI #358 / `33975104578`;
- L3 Seven-Driver Lab #314 / `33975104579`;
- Interop Lab Smoke #235 / `33975104582`.

## 3. C11 current exact state

Canonical branch:

`wave14/c11-canonical-eee-demo`

Exact live C11 head at handoff:

`41d24d89c3b9d2b881215255e44023fabde262f3`

Commit:

`test(c11): prove package from fresh eee-demo project`

This head already contains accepted C23 through normal two-parent sync merge `3c38d5c603e9d8226b338c516644c331edb71ccd`.

### What C11 already proves

The canonical EEE Simulation/HMI was previously implemented and repository-validated using normal product mechanisms:

- `builtin.memory.server` process truth;
- deterministic Server Script lifecycle;
- TAGs, quality, Alarms, Operational Events, Historian and Commands;
- six Screens;
- two Popups;
- reusable EEE pump Dynamo with two independent instances;
- Trend, Alarm Browser and Event Browser;
- Save -> Publish -> Activate -> Active HMI Runtime;
- Runtime viewport fit through accepted C22.

Do not regress these contracts while fixing package portability.

## 4. Why the canonical package gate is currently red

Earlier C11 package candidate `3486a488181201062ba2f6790cd6deb7f5bccb8a` exposed that the package test was building on a workspace contaminated by the historical Wave11 DEMO.

That was correctly fixed at `41d24d89...` by:

- creating an isolated `elitescada_c11` persistence database after the historical Wave11 suite;
- starting the C11 API against that isolated database;
- creating `eee-demo` through the supported `/api/engineering/persistence/projects/first` endpoint;
- proving the fresh project has the built-in Dynamo library but no EEE application Dynamo before C11 import;
- then using the normal Preview -> Apply -> Save -> Publish -> Activate -> Export -> Inspect -> Import Preview route.

The fresh-project correction worked, but it revealed a **new generic product inconsistency**.

Exact SHA `41d24d89...` workflow state:

- Preview Licensing CI #360 / run `33977314302` — SUCCESS;
- Interop Lab Smoke #237 / `33977314294` — SUCCESS;
- EliteSCADA CI #1410 / `33977314325` — SUCCESS;
- L3 Seven-Driver Lab #316 / `33977314306` — SUCCESS;
- Wave11 Active HMI Runtime #338 / `33977314297` — **FAILURE**.

Wave11 historical lifecycle itself passed 22/22. The failure is exclusively the new C11 canonical package gate.

### Exact failure

Publish returns HTTP 400 because Preview reports:

`DYNAMO_TEMPLATE_NOT_FOUND`

for:

`dynamo.pump.standard`

which references template:

`pump.standard`

The normal First Project bootstrap currently clears the legacy workspace and seeds `BuiltinDynamoLibrary.Create()` plus the initial Developer role, but it does not seed the `pump.standard` template referenced by one built-in Dynamo. The resulting fresh project is therefore internally inconsistent at Publish validation.

This is **not an EEE Demo defect** and must not be hidden by adding historical/demo entities to C11.

## 5. Immediate next product package: generic first-project bootstrap consistency

The next coordinator should treat this as a narrow generic product correction package, provisionally **C24** unless live repository state already assigned another number.

Before writing code, revalidate issue/PR/package numbering live.

Required intent:

1. inspect `BuiltinDynamoLibrary`, first-project bootstrap and normal project/package validation contracts;
2. determine the correct generic invariant for built-in library dependencies;
3. make a newly created first project self-consistent through supported product behavior;
4. do not special-case `eee-demo`;
5. add regression coverage that First Project can Save/Publish without unresolved built-in dependencies;
6. run the standard exact-SHA gates on the correction;
7. integrate only into `wave14/corrections-integration` after acceptance;
8. sync the accepted correction into C11 through a normal merge preserving histories;
9. rerun the exact C11 package gate.

Do **not** rerun Wave11 #338 blindly. The red is diagnosed and needs a product correction first.

## 6. Canonical `.escadapkg` acceptance still pending

No canonical `EliteSCADA-EEE-Demo.escadapkg` artifact was uploaded from run #338 because the package gate failed before artifact upload.

After the generic bootstrap gap is fixed and C11 exact head is green:

1. fetch the exact Wave11 artifact `EliteSCADA-EEE-Demo`;
2. verify package bytes against the generated `.sha256` and provenance JSON;
3. verify provenance `projectKey=eee-demo`, `activeProjectKey=eee-demo`, exact C11 generator SHA and accepted source product SHA;
4. inspect the `.escadapkg` ZIP/manifest without mutating it;
5. version the exported package bytes, checksum and provenance in the repository using existing fixture conventions;
6. update Preview launcher/fixture to consume the canonical EEE package instead of the historical Wave11 DEMO, without overwriting historical provenance;
7. update C11 progress/handoff docs with exact CI IDs;
8. revalidate the final C11 SHA because versioning/docs commits change the SHA;
9. only then proceed to Product Owner fresh-Codespace visual homologation and final C11 acceptance.

C11 PR #263 must not merge before those steps are complete. PR #266 must never merge.

## 7. Post-DEMO design decisions already recorded

### PR #273 — System Recovery / Backup & Restore

Open DRAFT design-only PR. It records:

- application portability remains `.escadapkg`;
- Database/Historian recovery uses supported native DB backup/restore mechanisms;
- Security Authority gets dedicated encrypted Export / Preview / Import with user-supplied master password;
- fresh installation uses Recovery Bootstrap with provisional administrator/workspace;
- bootstrap administrator has precedence on identity/credential collision;
- recovery cannot finalize without usable admin + valid Active application.

Do not merge #273 blindly into a moving C11 branch. Revalidate its base/diff and port the design cleanly after C11 acceptance if needed.

### PR #274 — Runtime session UX / contextual manual / licensing design notes

Open DRAFT design-only PR. It records:

- Runtime current-user identity is already displayed and must remain visible beside the future compact session icon;
- current logout exists;
- future Runtime system popup adds explicit `Trocar usuário` and `Sair` without exposing Engineering to runtime-only users;
- switch-user invalidates old session first and reloads capabilities from backend;
- contextual user/developer manual uses stable Help IDs, web pages, version compatibility and preferably local/offline delivery;
- Driver/TAG/Source/Script/Report/HMI documentation must be detailed and derived from real product contracts.

Product Owner refinement after the original PR text: **the help/manual is obligatorily multilingual and follows the active EliteSCADA UI language.** At minimum, if the system UI is `pt-BR`, the same Help ID resolves to the Portuguese topic; if UI is English, it resolves to the English equivalent. Help IDs remain language-neutral. Language change must preserve semantic topic identity rather than falling back to an unrelated manual home page.

## 8. Pending optional application-package protection concept — STOP BEFORE IMPLEMENTATION

The Product Owner proposed an optional protection mode for application `.escadapkg` export/import, motivated by OEM/serial-machine cases where the customer operates Runtime but the developer wants to protect the engineered application.

Current intent only:

- no password => Import/Export behaves exactly as today;
- developer may optionally export a protected application package with a password;
- this must not reduce normal package portability when protection is unused;
- the original discussion considered keeping the package structure non-encrypted while protecting password/authorization data through product-owned cryptographic material.

**CRITICAL PRODUCT OWNER INSTRUCTION:** the Product Owner subsequently identified a flaw in this proposed design and explicitly instructed the coordinator to **ask about that flaw when implementation is about to begin**.

Therefore:

- this feature is **NOT IMPLEMENTED**;
- the cryptographic architecture is **NOT LOCKED**;
- do not code it, finalize a threat model, or document the earlier password/key idea as accepted security design before asking the Product Owner what flaw was identified;
- preserve existing unprotected `.escadapkg` behavior until a reviewed design exists.

## 9. Product Owner decisions that must not be lost

- Runtime session control must show the current user's name beside the discreet icon/control;
- Help/manual must follow active UI language and resolve the same stable Help ID to the equivalent localized topic;
- optional protected application package remains deferred pending explicit Product Owner flaw discussion;
- production licensing public key is now embedded in EliteSCADA; private signing key never enters repository/product;
- C11 must continue normally after generic product gaps are fixed, without EEE-specific shortcuts;
- Product Owner fresh-Codespace visual homologation remains after canonical package construction and before final Wave14 acceptance.

## 10. PR / branch hygiene at handoff

Revalidate live before acting, but expected state at this handoff:

- #212 — OPEN/DRAFT, integration -> main, NEVER merge without explicit later authorization;
- #263 — OPEN/DRAFT, C11 -> integration, do not merge yet;
- #266 — OPEN/DRAFT validation-only, C11 -> main, NEVER MERGE;
- #273 — OPEN/DRAFT design-only System Recovery;
- #274 — OPEN/DRAFT design-only Runtime session/manual/licensing notes;
- #275 — C23 implementation already merged only into integration;
- #276 — C23 validation-only closed without merge;
- #277 — sync-only C23 -> C11 merged, producing `3c38d5c6...`.

## 11. Mandatory resume order for the next coordinator

1. read `PROJECT GOAL.md`;
2. read `LAST CHANGE.md`;
3. read this file completely;
4. read `docs/WAVE14-C11-CANONICAL-DEMO-IMPLEMENTATION-HANDOFF.md`;
5. read `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md` and Pass-2 audit docs as needed;
6. inspect live issue #211 and PRs #212/#263/#266/#273/#274;
7. revalidate integration and C11 refs live;
8. revalidate exact workflow state for C11 head before taking any action;
9. inspect the diagnosed First Project / built-in Dynamo dependency failure;
10. create the narrow generic correction package on a separate branch from current accepted integration product bytes;
11. after acceptance/integration, sync it into C11 and continue canonical package acceptance.

Do not ask the Product Owner to repeat decisions already recorded here, **except** the intentionally deferred `.escadapkg` password-protection flaw, which the Product Owner explicitly wants to discuss before implementation.
