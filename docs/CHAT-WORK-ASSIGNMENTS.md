# CHAT WORK ASSIGNMENTS — EliteSCADA

> Authoritative live execution board. GitHub branch/PR/head/CI state is operational truth. Permanent rules: `docs/DEVELOPMENT-WAVES.md`, `docs/PARALLEL-WORK.md`, `docs/CI-USAGE-POLICY.md`.

**Last coordinator synchronization:** 2026-08-29 — Wave 08 is CLOSED / MERGED / POST-MERGE GREEN. `08-FOLLOW-A` is now ACTIVE under coordinator-owned architecture reconciliation. DEV 1/2/3 remain STOPPED until a new bounded assignment is explicitly published.

## Mandatory `siga`

Every fixed EliteSCADA chat first rereads current `main`: `PROJECT GOAL.md`, `LAST CHANGE.md`, `docs/ROADMAP.md`, `docs/PARALLEL-WORK.md`, `docs/DEVELOPMENT-WAVES.md`, this board, `docs/CI-USAGE-POLICY.md`, `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`, `docs/COORDINATOR-HANDOFF.md`, and current MustReadSpecific documents. Then verify real branch/head/PR/CI and execute only the current authorized assignment.

## Current product gate

`08-FOLLOW-A — TAG BIT ACCESS + DRIVER BIT-LEVEL BOOLEAN BINDING` is **ACTIVE**.

Logical BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

Canonical contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

### Wave 08 closure evidence

Final integration head:

`9ea0eace15aa925133005f40e16403a2c0f3deb1`

- final integration CI #531 / run `33236703599`: **SUCCESS**;
- replacement final PR #96: **MERGED**;
- main merge: `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- post-merge CI #533 / run `33236999366`: **SUCCESS**.

Draft PR #90 was closed unmerged only because the available connector failed while removing Draft state; #96 used the exact same branch/head and merged normally.

Wave 08 delivered graphical Screen Engineering, image assets, dynamic scalar `core.text`, shared Project Reference Tree, `core.polygon`, Development Monitor and final canonical persistence/Preview/Apply coverage.

---

# COORDENADOR - EliteSCADA

**Role:** `COORDINATOR`  
**Wave:** `08-FOLLOW-A`  
**Status:** `ACTIVE — ARCHITECTURE-FIRST / NO WORKER DELEGATION YET`  
**LogicalBaseSHA:** `bfd17d035d905e9bcae263f68244cfb2b6453aa2`  
**IntegrationBranch:** `integration/tag-bit-access-wave-08-follow-a`

**CurrentTask:** reconcile the owner-locked TAG-bit contract with the actual merged Core/Engineering/Runtime/Modbus/reference/catalog architecture; freeze the minimum public implementation surface; then implement or explicitly delegate bounded slices without creating parallel TAG identities, driver-private metadata or visual-only bit syntax.

**MustReadSpecific:**
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `docs/COORDINATOR-HANDOFF.md`
- `docs/INTERNAL-MEMORY-TAGS.md`
- current canonical TAG contracts/registries/current-value models;
- current Engineering import/export/validation/persistence TAG schemas;
- current Modbus point/address/codec/poll/write implementation;
- current Project Reference Tree contracts;
- current Engineering Development Monitor source/catalog model;
- current Client Visual Python TAG/reference surface where relevant.

**AllowedScope:** Follow-A integration branch; canonical TAG reference/selector contracts; TAG Engineering/public DTOs; driver capability/address binding contracts; Modbus bit read/write implementation; Runtime resolution; shared reference/catalog integration; security/Audit adjustments required by bit access; focused and final tests; explicit worker assignments; Follow-A PR/CI/merge.

**ForbiddenScope:** implementing 08-FOLLOW-B expression language/Analog Fill; Wave 09 Screen navigation/Popup/Dynamo/Historical Data Browser implementation; new unrelated protocols; Server Python; monitor-private or visual-private `.NN` parsing; unsafe whole-register overwrite for Boolean register bits.

**CompletionCriteria:**
- logical Int16/Int32/Int64 bit selectors are canonical and stable by TAG identity + bit index;
- quality/timestamp and signed fixed-width semantics are correct;
- direct physical Boolean bit binding is represented publicly/versionably;
- Modbus Holding/Input Register bit reads are correct;
- Holding Register bit writes preserve unrelated bits and coordinate same-register EliteSCADA writes;
- shared/coalesced physical reads are retained where practical;
- import/export/Preview/Apply/revision/PostgreSQL/package fidelity is green;
- Project Reference Tree/Development Monitor can consume the canonical bit seam without a private parser;
- existing whole-register/Coil/DiscreteInput and prior Wave regressions remain green;
- final exact-head CI and post-merge `main` health are green.

**NextActions:**
1. inspect actual merged TAG and reference DTO/model seams;
2. inspect Modbus point/codec/poll-block/write behavior and current address convention;
3. inspect Project Reference Tree + Development Monitor catalog identity contracts;
4. define the minimum selector/binding DTO and runtime resolver on the integration branch;
5. decide whether parallel-safe worker slices exist and update this board before any worker starts;
6. implement focused contract/driver tests first;
7. run a full matrix only at a meaningful integrated checkpoint.

---

# DEV 1 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `08-FOLLOW-A`  
**Status:** `STOPPED / WAIT_FOR_COORDINATOR`  
**PreviousTask:** Wave 08 Canvas / Selection — DELIVERED AND MERGED THROUGH CENTRAL TRAIN  
**PreviousDeliveryHead:** `d6542643014e955b013756fd8ee53a5629b8e82a`

**CurrentTask:** none.

**Authorization state:** NOT AUTHORIZED for Follow-A work until this board grants an explicit AllowedScope/ForbiddenScope/CompletionCriteria.

---

# DEV 2 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `08-FOLLOW-A`  
**Status:** `STOPPED / WAIT_FOR_COORDINATOR`  
**PreviousTask:** Wave 08 Property Inspector — DELIVERED AND MERGED THROUGH CENTRAL TRAIN  
**PreviousDeliveryHead:** `2c974cadafbcc773a4645864440c190f128ea808`

**CurrentTask:** none.

**Authorization state:** NOT AUTHORIZED for Follow-A work until this board grants an explicit AllowedScope/ForbiddenScope/CompletionCriteria.

---

# DEV 3 - EliteSCADA

**Role:** `WORKER`  
**Wave:** `08-FOLLOW-A`  
**Status:** `STOPPED / WAIT_FOR_COORDINATOR`  
**PreviousTask:** Wave 08 Object Palette / Binding — DELIVERED AND MERGED THROUGH CENTRAL TRAIN  
**PreviousDeliveryHead:** `d614a00dda0903a0f7c78641dc0b803dfd8085df`

**CurrentTask:** none.

**Authorization state:** NOT AUTHORIZED for Follow-A work until this board grants an explicit AllowedScope/ForbiddenScope/CompletionCriteria.

## Follow-up ordering

1. `08-FOLLOW-A` — ACTIVE now.
2. `08-FOLLOW-B` — WAITING ON Follow-A.
3. Wave 09 — NOT ACTIVE until both mandatory follow-ups are green.
