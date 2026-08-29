# LAST CHANGE — EliteSCADA

> Operational handoff. Resume from GitHub, not chat history.

**Handoff date:** 2026-08-29  
**Merged product state:** **WAVE 08 CLOSED / POST-MERGE GREEN**  
**Active development state:** **08-FOLLOW-A — INTEGRATED CANDIDATE / CI IN PROGRESS / NOT MERGED**  
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

Then verify live GitHub branch/PR/head/CI. GitHub is operational truth.

## Wave 08 — CLOSED

- PR #96 merged;
- `main` product merge: `bfd17d035d905e9bcae263f68244cfb2b6453aa2`;
- integration CI #531: **SUCCESS**;
- post-merge CI #533: **SUCCESS**.

## Current active work — 08-FOLLOW-A

Canonical contract:

`docs/TAG-BIT-ACCESS-AND-BIT-BINDING.md`

Integration branch:

`integration/tag-bit-access-wave-08-follow-a`

Exact integration head at this handoff:

`4e8a3c76753c1ead815c790407601852c6f888e3`

Central PR:

**#105 — FOLLOW-A: TAG bit access and Modbus bit-level binding — OPEN / DRAFT**

Exact-head CI:

**#541 / run `33255556172` — IN PROGRESS** at handoff.

Do not edit the integration head merely to provoke another run. Read #541 result/logs first.

## What is already integrated

The three reviewed worker slices are already in the train. Do not integrate them again:

- Core logical bits: worker head `80b0911eda92b96a7c1945307cea402cf9ad4417`, CI #534 SUCCESS;
- Engineering persistence/validation: `bcd65ac4ee81ac399fb2d1882e426d90f419a225`, CI #537 SUCCESS;
- Modbus physical bit binding: `e0365c6dd0d81cd938ccc41e44b208726c73392e`, CI #535 SUCCESS.

Current integration includes:

- `TagValueReference(TagId, Selector?)` and `TagValueSelector(Bit, Index)`;
- Int16/Int32/Int64 bit read/write semantics;
- source quality/timestamp/context inheritance;
- structured `AddressSelector` Engineering persistence;
- Modbus HR/IR selected-bit read support;
- safe coordinated HR RMW preserving unrelated bits;
- stable visual `BindingEngineering.tagReference` / backend binding `TagReference`;
- concrete binding validation preferring stable TagId + selector over friendly path;
- Project Reference bit capability without expanding every integer TAG into permanent bit nodes;
- friendly `.NN` resolution to a Boolean derived reference;
- Binding Editor on-demand bit authoring and stable identity/deduplication;
- Development Monitor stable TagId-based bit entries;
- shared web TAG bit projection preserving Quality/timestamps.

Friendly `Word_status.03` is display/authoring syntax only. Persisted authority is TagId + bit index.

## Git topology note

Current `main` contains later product-documentation changes that are not yet reconciled into the Follow-A branch, including:

- Wave 09 Reporting;
- mandatory PDF and XLSX report exports;
- future-driver bit-conformance rule;
- DNP3 queued as future/post-v0.1 driver.

Before final Follow-A merge, reconcile current `main` into the integration train without discarding either code or documentation.

## Permanent future-driver bit rule

Future drivers exposing bit-addressable word/byte/register/integer storage must publish structured bit capability. Where writable, a bit write must preserve unrelated bits using native atomic/mask support or coordinated RMW. Read-only protocol areas remain read-only.

Future drivers include MQTT, OPC UA, BACnet, S7, Allen-Bradley and **DNP3** as applicable. DNP3 is not active now.

## Wave 09 — SPECIFIED / NOT ACTIVE

Wave 09 includes Screens/Popups/Dynamos/navigation, Historical Data Browser and Reporting.

Reporting requires mandatory export to:

- **PDF (`.pdf`)**;
- **Microsoft Excel (`.xlsx`)** with typed cells where practical.

Wave 09 remains blocked until 08-FOLLOW-A and 08-FOLLOW-B are green.

## Next ordered actions

1. inspect CI #541 on exact head `4e8a3c76753c1ead815c790407601852c6f888e3`;
2. if failed, fix only the proven cause and retest coherently;
3. if green, complete Follow-A acceptance review;
4. reconcile current `main` into integration;
5. obtain final exact-head green evidence if the tested tree changed;
6. merge Follow-A and verify post-merge `main`;
7. close Follow-A in docs/assignments;
8. activate **08-FOLLOW-B — Typed Visual Expressions + Boolean Conditions + Analog Fill**;
9. do not activate Wave 09 before Follow-B closes.
