# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-29  
**Merged product state:** **WAVE 08 CLOSED / POST-MERGE GREEN**  
**Active development state:** **08-FOLLOW-A — WORKER SLICES DELIVERED + GREEN / COORDINATOR INTEGRATION PENDING**  
**CI mode:** **NORMAL — Actions authorized with conservative usage**

## Mandatory resume reading

Before any action read current `main`:

- `PROJECT GOAL.md`
- `LAST CHANGE.md`
- `docs/ROADMAP.md`
- `docs/PARALLEL-WORK.md`
- `docs/DEVELOPMENT-WAVES.md`
- `docs/CHAT-WORK-ASSIGNMENTS.md`
- `docs/CI-USAGE-POLICY.md`
- `docs/V0.1-FULL-PRODUCT-VALIDATION-PLAN.md`
- `docs/COORDINATOR-HANDOFF.md`
- `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`
- `docs/VISUAL-BOOLEAN-CONDITIONS-AND-ANALOG-FILL.md`
- `docs/WAVE-09-HISTORICAL-DATA-BROWSER-ALARM-HISTORIAN-CONTEXT.md`
- `docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`
- current assignment `MustReadSpecific`.

Then verify live GitHub branch/PR/head/CI. GitHub is operational truth.

## Wave 08 — CLOSED

- final integration head: `9ea0eace15aa925133005f40e16403a2c0f3deb1`;
- final integration CI #531 / run `33236703599`: **SUCCESS**;
- PR #96: **MERGED**;
- `main` merge commit: `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- post-merge CI #533 / run `33236999366`: **SUCCESS**.

The final Wave 08 polygon Preview/Apply defect was fixed before merge: `core.polygon.points` remains structural geometry and is no longer sent through the scalar visual-property codec. Regression coverage is included.

## Current active work — 08-FOLLOW-A

Canonical product contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

Logical BaseSHA:

`bfd17d035d905e9bcae263f68244cfb2b6453aa2`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

Current integration head remains the shared contract seed:

`9a8cb931cf851e6cad2ddb8d5efcae428db51d01`

Coordinator integration of the three accepted worker deliveries has **not** yet occurred.

### Shared canonical implementation seam frozen

The Follow-A integration branch contains the coordinator-owned seed:

- `TagValueSelectorKind.Bit`;
- `TagValueSelector(Kind, Index)`;
- `TagValueReference(TagId, Selector?)`, where the Guid is authoritative identity;
- `TagDefinition.AddressSelector` for a structured driver-independent selector over the physical source address;
- `TagEngineeringDto.AddressSelector` for the public Engineering representation.

Friendly references such as `Word_status.03` remain authoring/display notation only. They must resolve to canonical TAG Guid + structured bit selector and must never become the only persisted identity.

## Worker deliveries — IMPLEMENTED IN PR / INTEGRATION REQUIRED

### DEV 1 — Core logical bit semantics

- branch: `feature/tag-bit-wave-08-follow-a-core`;
- exact delivery head: `80b0911eda92b96a7c1945307cea402cf9ad4417`;
- canonical PR: #97;
- CI #534: **SUCCESS**.

Delivered logical Int16/Int32/Int64 bit read/write semantics, fixed-width two's-complement behavior, quality/timestamp/context inheritance and focused Core validation.

### DEV 2 — Engineering persistence/validation

- branch: `feature/tag-bit-wave-08-follow-a-engineering`;
- exact delivery head: `bcd65ac4ee81ac399fb2d1882e426d90f419a225`;
- PR: #101;
- CI #537 / run `33250943750`: **SUCCESS**.

Delivered structured `AddressSelector` JSON/CSV/Preview/Apply/Export persistence and validation. Generic Engineering accepts bit indices through 63; protocol-specific limits remain driver/compiler authority. Existing v13 content without selectors remains compatible.

### DEV 3 — Modbus physical register-bit binding

- branch: `feature/tag-bit-wave-08-follow-a-modbus`;
- exact delivery head: `e0365c6dd0d81cd938ccc41e44b208726c73392e`;
- PR: #99;
- CI #535: **SUCCESS**.

Delivered Modbus Holding/Input Register bit reads, Input Register read-only behavior, safe Holding Register read-modify-write, same-authority write coordination, unrelated-bit preservation and shared/coalesced physical register reads where practical.

The three worker slices were reviewed as conceptually compatible. Do not rerun their unchanged heads merely for reassurance.

## Follow-A required final integration outcome

1. canonical integer bit selector by TAG Guid + bit index;
2. Int16/Int32/Int64 low/high/sign-bit correctness;
3. source quality/timestamp/context preserved;
4. logical bit writes preserve all unrelated bits;
5. direct physical Boolean bit binding represented publicly/versionably;
6. Modbus HR/IR bit `0..15` reads correct;
7. HR bit writes preserve unrelated register bits and coordinate EliteSCADA writes;
8. same-register physical reads remain coalesced where practical;
9. Engineering JSON/CSV/Preview/Apply/revision/PostgreSQL/package fidelity;
10. shared Project Reference Tree/Development Monitor consume canonical `TagValueReference`, never private `.NN` parsing;
11. existing whole-register/Coil/DiscreteInput and prior-wave regressions stay green;
12. one coherent integrated CI on the final Follow-A candidate.

## Permanent future-driver bit rule — SPECIFIED

The bit capability is no longer a Modbus-specific direction.

Every future production driver that exposes bit-addressable byte/word/register/integer storage must expose structured bit reads through the public versioned binding/capability contract.

If the underlying protocol/address is writable, the driver must also provide a safe Boolean bit write that preserves unrelated bits, using a native atomic/mask primitive where available or a coordinated read-modify-write strategy otherwise. Intrinsically read-only areas remain explicitly read-only.

Every such future driver must pass common bit conformance tests for range validation, quality propagation, unrelated-bit preservation and concurrent EliteSCADA writes where applicable.

Locked contract: `docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`.

## Wave 09 Reporting — SPECIFIED / NOT IMPLEMENTED

Wave 09 now includes first-class Reporting in addition to Screens/Popups/Dynamos/navigation and Historical Data Browser.

Canonical contract:

`docs/WAVE-09-REPORTING-AND-REPORT-DESIGNER.md`

Locked direction includes:

- versioned Report Engineering;
- Report/Page/Group Header/Footer plus repeatable Detail sections;
- nested groups and deterministic ordering;
- visual Report Designer with typed fields, text, Boolean state, images/resources, barcode, charts, shapes and page breaks;
- grid/snap/alignment/z-order/page/font/border editing;
- protected typed historian/alarm/current-value providers shared with Historical Data Browser/Trends;
- graphical typed query/parameter/filter authoring instead of arbitrary SQL;
- runtime requery with relative/absolute time and other typed parameters without dirtying Engineering;
- count/sum/average/min/max and grouped/time-bucket summaries;
- print preview, page numbering and printing;
- PDF, XLSX, HTML, RTF, Text and CSV export;
- authorization, parameterization, cancellation/timeouts and result/output bounds;
- canonical JSON/Preview/Apply/Working/revision/PostgreSQL/`.escadapkg` fidelity.

Unrestricted report scripting is deliberately not pulled into the first Wave 09 slice. If later required it must use normal sandboxed Script Engineering and respect the Wave 10 event/scripting boundary.

Wave 09 remains **NOT ACTIVE** until 08-FOLLOW-A and 08-FOLLOW-B are green.

## Documentation commits from this task

All changes are documentation/product-contract only and use `[skip ci]`; no Actions were spent.

- `e5f5223e3f8eb6f8f982da523bddeb3a12c25a95` — create Wave 09 Reporting contract;
- `bcfaf69dc46a5a84ff05519e2d576b18fdb17ddc` — make bit-level conformance mandatory for future drivers;
- `ed4868d192f1c623d5d5aac19829e9bb16205b74` — expand Roadmap Wave 09 with Reporting;
- `2fab92471d5d3050ca16adbf3bd6c435b5ff41b5` — align Historical Data Browser with Reporting;
- `7d680463b084a4189d8cb1949422bc4613439a10` — lock Reporting and future-driver bit conformance in product north;
- `f2d89f5b727327ca6566efbd1f2c288a6f32e998` — add Reporting to the authoritative v0.1 plan.

No repository document names or attributes the external benchmark used during product research. The repository records only EliteSCADA's own generic product requirements.

## Ordered work after current point

1. integrate DEV 1/2/3 accepted Follow-A heads into `integration/tag-bit-access-wave-08-follow-a`;
2. implement coordinator-owned canonical Project Reference Tree / Development Monitor / Runtime reference seam;
3. focused integration validation, then one coherent full Follow-A matrix;
4. merge Follow-A only when green and confirm post-merge `main`;
5. execute **08-FOLLOW-B — Typed Visual Expressions + Boolean Conditions + Analog Fill**;
6. only after both follow-ups are green, activate **Wave 09 — Screens/Popups/Dynamos/navigation + Historical Data + Reporting**.
