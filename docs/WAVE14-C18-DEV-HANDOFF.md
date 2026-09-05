# W14-C18 — DEV Handoff — Embeddable Alarm + Event Browser HMI Objects

**State:** **RELEASED / IMPLEMENTATION AUTHORIZED**  
**Release date:** 2026-09-04 BRT  
**Coordinator branch:** `wave14/corrections-integration`  
**Integration PR:** `#212` — DRAFT / DO NOT MERGE TO `main`  
**Package branch:** `wave14/c18-hmi-alarm-event-browsers`  
**AUTHORIZED C18 PRODUCT BASE:** `568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`  
**C11 implementation:** **LOCKED**

GitHub is the official development memory. Revalidate live refs before changing product code.

## 1. Release authority

The previous HOLD is closed. C12–C17 have converged on one exact combined-green integration product SHA:

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

Exact combined gates on that product SHA:

- EliteSCADA CI #1347 / `33882503111` — **SUCCESS**;
- Wave 11 Active HMI Runtime #275 / `33882503088` — **SUCCESS**;
- Preview Licensing CI #297 / `33882503272` — **SUCCESS**;
- L3 Seven-Driver Lab #252 / `33882503050` — **SUCCESS**;
- Interop Lab Smoke #174 / `33882503053` — **SUCCESS**.

EliteSCADA CI #1347 initially hit the known IEC-104 T2 timing test once. The affected backend job was rerun once only after diagnosis: unchanged IEC-104 test/product lineage plus prior identical transient evidence. The rerun passed Backend build/test/smoke and the downstream Chromium end-to-end job passed. No product code was changed to obtain green.

The accepted C17 convergence correction candidate is:

`705ac0a689d6ec4b3462f85e2082410f1d8b3baa`

It also passed all five exact-candidate gates before composition:

- EliteSCADA CI #1346 / `33881471883`;
- Wave 11 Active HMI Runtime #274 / `33881471880`;
- Preview Licensing CI #296 / `33881471818`;
- L3 Seven-Driver Lab #251 / `33881471893`;
- Interop Lab Smoke #173 / `33881471846`.

C17's latent New Data Source stale-draft identity race is therefore corrected and the C16 contracts remain accepted.

## 2. Branch start rule

The existing C18 branch was intentionally parked at historical checkpoint:

`1dcd80a4df448ced3a228d3f5b9057fa26ef547c`

That parked SHA is no longer the development authority.

C18 product work is authorized from the exact product base:

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

The package branch may of course advance with C18 implementation commits after this base. Later documentation-only commits do not redefine the authorized product base.

Do not use `main` or the old parked `1dcd80...` as product authority.

## 3. Package purpose

C18 closes:

- `C11-P2-BROWSER-01` — configurable embeddable Alarm Browser;
- `C11-P2-BROWSER-02` — configurable embeddable Event Browser;
- `C11-P2-I18N-HIST-01` — related Historical/Browser visible chrome that remains English-only.

Normal Engineering must allow:

`Engineering palette/object -> configure canonical properties -> Save -> Publish -> Activate -> render inside Screen or Popup`

No hidden package editing, DEMO-only React page, global Runtime route substitute, DOM/CSS injection, private runtime wiring or historical DEMO path counts as acceptance.

## 4. Mandatory reading

Before changing product code, revalidate:

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. `docs/CURRENT-COORDINATOR-HANDOFF.md`;
4. this file;
5. `docs/WAVE14-C11-PRE-DEMO-CORRECTION-PACKAGES.md`;
6. `docs/WAVE14-C11-CANONICAL-DEMO-REQUIREMENTS.md`;
7. C11 consolidated audit and HMI-object clarification on `wave14/c11-pass2-product-gap-audit`;
8. `docs/WAVE14-C14-OPERATIONAL-EVENTS.md`;
9. `docs/WAVE14-C15-HMI-TREND-MULTIPEN.md`;
10. `docs/CI-VALIDATION-POLICY.md`;
11. live issue #211 and draft PR #212.

If copied context conflicts with live GitHub, GitHub wins.

## 5. Architecture authority

Preserve:

- backend canonical authority and backend-side authorization;
- host-owned fail-closed licensing;
- no Preview-only bypass;
- no Driver-to-Driver coupling;
- canonical TAG/Data Source identities;
- Active revision as Runtime project authority;
- lifecycle `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- accepted canonical visual-object schema / Property Inspector / Runtime rendering pattern from C05/C07/C15;
- C14 Operational Event semantics distinct from Alarm and Audit;
- C16 persisted Startup/Home and Popup X/Y contracts;
- pt-BR / en / es for affected visible chrome.

Do not hard-code DEMO behavior, EEE-specific browsing logic, fixed project identities or private product paths.

## 6. Alarm Browser required surface

Alarm Browser must be a first-class visual object insertable into Screen and Popup, with persisted practical configuration such as current/historical view, active/returned, acknowledged state, severity, Area/Equipment/TAG, text/time filters, visible columns, sort and bounded result controls where supported by canonical contracts.

ACK/shelve/unshelve or other alarm mutations must use backend-authorized product endpoints. Client rendering never substitutes backend authorization.

## 7. Event Browser required surface

Event Browser must be a first-class Screen/Popup visual object consuming the accepted C14 Operational Event model and protected query path.

It must not reinterpret operational events as alarms merely to reuse alarm UI. Persisted filtering/presentation should support relevant C14 dimensions including type/category, source, Area/Equipment/TAG, user/operator, operation/command, time/text filters, visible columns, sort and bounded result controls.

Operational Event remains distinct from Audit history.

C14 does **not** define `/api/commands/{id}/execute` as an automatic Operational Event emitter. `CommandId` / `CommandKey` are optional occurrence context. C18 must not add Command -> Operational Event coupling merely to create E2E fixture data.

For Event Browser acceptance, create/query Operational Events through the canonical C14 flow:

`Engineering definition -> Active Runtime emission -> IScadaEventBus -> durable history -> protected operational.events query`.

Use an existing generic runtime emission path where appropriate, or a test harness/fixture that exercises the accepted C14 runtime contract without adding product-only bypasses. The Event Browser itself remains only a consumer of the canonical dataset.

## 8. Common visual-object contract

Both objects require:

- canonical visual object identities;
- insertion from normal Engineering UI;
- X/Y/width/height composition through accepted visual contracts;
- persisted canonical configuration;
- schema-driven Property Inspector wherever practical;
- deterministic independent multiple instances;
- Active Runtime rendering from persisted Active revision;
- loading, empty/no-data and backend-failure states;
- no hidden JSON/package edits for normal use.

Reuse C15 infrastructure only where genuinely common. Do not copy Trend-specific semantics into browser objects.

## 9. Historical / i18n ownership

Affected visible Historical/Browser strings must exist in:

- `pt-BR`;
- `en`;
- `es`.

Do not translate persisted technical identifiers, TAG paths, canonical enum wire values, IDs or backend keys.

## 10. Backend/query rules

Reuse protected backend query APIs and extend them only when a real generic product capability is missing. Do not fetch unbounded history and disguise the gap with client-side filtering.

Authorization remains backend-side for protected history and alarm state-changing actions.

## 11. Explicit non-scope

C18 does not own:

- redesign of C14 Event model/storage except a narrow proven integration defect;
- C15 Trend behavior/Multi-Pen;
- C16 Operational Command, Startup/Home or Popup X/Y;
- reopening the now-corrected C17 Data Source race;
- EEE Simulation physics or DEMO process screens;
- physical Modbus PLC mapping;
- Preview/Codespaces infrastructure;
- Wave13 packaging/signing.

## 12. Acceptance

Exact C18 candidate HEAD must pass:

- EliteSCADA CI;
- Wave 11 Active HMI Runtime;
- Preview Licensing CI;
- L3 Seven-Driver Lab;
- Interop Lab Smoke;
- package-specific browser tests proving authored Screen and Popup instances.

Acceptance must prove real Save/Publish/Activate/Active Runtime lifecycle, independent configurations, canonical alarm/event data, authorized and denied alarm actions where applicable, and pt-BR/en/es chrome.

Diagnose failures before rerunning. Do not weaken tests, authorization, event/alarm semantics or visual-object contracts to manufacture green.

## 13. Delivery boundary

Package PR must target `wave14/corrections-integration`, never `main`.

PR #212 remains Coordinator-owned and DRAFT.

At delivery report branch/base/candidate SHA, changed subsystems, exact workflow run IDs, architecture decisions and known limitations.

## 14. Current marker

**C18 RELEASED / IMPLEMENTATION AUTHORIZED**

**Exact authorized product base:**

`568e93eb4dc4ba1fdc41455cfd6935e8831f09a4`

**Current package PR:** `#254`

**Current package branch:** `wave14/c18-hmi-alarm-event-browsers`

C11 remains locked until C18 converges, C10 convergence cycle 2 establishes a new exact product freeze, affected C11 findings are revalidated, and the Coordinator explicitly releases C11 implementation.
