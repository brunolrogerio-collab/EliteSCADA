# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C12–C19 COMBINED AUTOMATED VALIDATION GREEN / REAL FRESH-CODESPACE VISUAL HOMOLOGATION PENDING / C11 IMPLEMENTATION LOCKED / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs and exact-SHA workflows before acting. Documentation-only and validation-overlay commits do not redefine product authority.

## 1. Permanent gates

- backend is canonical authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- lifecycle remains `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- diagnose a red before rerun;
- never weaken tests/security/contracts/identity/lifecycle for green;
- package delivery and CI green are not, by themselves, Product Owner browser acceptance;
- PR #212 stays OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization;
- C11 stays `IMPLEMENTATION LOCKED` until explicit release;
- Wave13 #205/#207 stays paused until final accepted Wave14 bytes.

## 2. C19 accepted and composed

C19 package: **W14-C19 Operational Event Authoring + Server Script Emission Bridge**.

Isolated accepted C19 PRODUCT SHA:

`43d2734b18dc8b78caaa917b9bf6a381ca47b202`

Accepted isolated tree:

`6a20ad793fdd9f3b66b62a954a6d79a5923ac24f`

C19 isolated evidence:

- EliteSCADA CI #1369 / `33930751824` — SUCCESS;
- Wave11 #297 / `33930751831` — SUCCESS;
- Preview Licensing #319 / `33930751817` — SUCCESS;
- L3 #275 / `33930751839` — SUCCESS;
- Interop #196 / `33930751819` — SUCCESS;
- C03 DNP3 #112 / `33930740869` — SUCCESS.

Architecture review accepted the current design:

- Operational Event authoring uses protected Working -> Preview -> CAS -> Apply;
- Python `emit_operational_event` returns requests only and has no event-bus/history authority;
- C# routes through canonical C14 Active Runtime;
- unknown/disabled/stale definitions fail closed;
- Server Script TAG/Server Memory/Operational Event revision serialization uses the single existing `ServerScriptRuntimeManager._revisionGate`;
- the obsolete separate C19 gate/`AsyncLocal` design must never be restored;
- `ServerScriptRunner.py` is packaged as a normal Debug/publish output asset.

PR #257 was merged into `wave14/corrections-integration` with a history-preserving merge commit, never into main.

## 3. Exact current combined product candidate

Integration branch: `wave14/corrections-integration`  
Integration PR: #212 OPEN/DRAFT  
Exact combined C12–C19 PRODUCT CANDIDATE:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

Tree:

`da6b406ac111cb40b99e5b13031601eb71606ddd`

Merge parents:

- previous integration docs head `402ff1f879bf311a1903f1c677dad928eab0c094`;
- C19 docs head `49693517a55ebd8041c6244455b8d6aaac7586a5`, whose product parent is isolated accepted C19 `43d2734...`.

The current integration branch may be ahead of `3fda880...` by documentation-only synchronization commits. Do not promote those docs SHAs to product authority.

## 4. Combined automated evidence on 3fda880

Direct on the exact combined product candidate:

- EliteSCADA CI #1370 / `33934982242` — SUCCESS;
- Wave11 Active HMI Runtime #298 / `33934982300` — SUCCESS;
- Preview Licensing CI #320 / `33934982254` — SUCCESS;
- L3 Seven-Driver Lab #276 / `33934982215` — SUCCESS;
- Interop Lab Smoke #197 / `33934982216` — SUCCESS.

Dedicated C03 carry-forward was required because its PR trigger targets the integration branch, not PR #212 to main and the active connector exposes no workflow-dispatch action.

Validation-only PR #260:

- direct product parent `3fda880...`;
- overlay head `80a4bc7255ca46a0a52c401abed09dc4da0b4e2f`;
- one documentation-only C03 trigger change;
- C03 #113 / `33935067545` — SUCCESS across managed tests, Linux/Windows native builds, real dnp3py interop and Windows commercial publish dependency gate;
- #260 closed without merge.

Therefore combined automated carry-forward is coherent across the six required gates.

## 5. Test Preview automated carry-forward

Historical proven Preview overlay commit `38fce9f72f032f648c6ccda7cc86d00278a7eee6` was a single infrastructure-only commit based on the previous accepted C12–C18 product.

A disposable validation branch was created from `3fda880...` and that exact infrastructure commit was reapplied with history preserved.

Validation harness SHA:

`086b5d6c92ccfa4f5dc7e947c787ffd953acc5e2`

Compare from `3fda880...` contains only seven Preview infrastructure files:

- `.devcontainer/devcontainer.json`;
- `.devcontainer/docker-compose.yml`;
- `.devcontainer/initialize-preview-machine-id.sh`;
- `.github/workflows/test-preview.yml`;
- two historical Preview fixture files;
- `scripts/preview/launch-test-preview.sh`.

No historical Preview product correction was imported.

Validation-only PR #262 targeted `main` solely to trigger the existing `Test Preview` workflow and was then closed without merge.

- Test Preview / `33935493882` — SUCCESS.

The job proved:

- exact .NET SDK/devcontainer contract;
- disposable `/etc/machine-id` with fail-closed licensing preserved;
- Compose/TimescaleDB contract;
- automatic Preview launch configuration;
- backend/frontend setup;
- ephemeral Preview administrator password;
- normal Preview launcher execution;
- browser entry HTTP surface;
- Pyodide asset availability.

## 6. Current blocking boundary: real browser homologation

Automated Test Preview is not final browser homologation.

The runbook requires a fresh Codespace and real visual/Product Owner validation. The current chat tool surface does not expose Codespaces creation/browser interaction, so this evidence cannot be truthfully produced here.

Before declaring the new product freeze, record fresh-Codespace evidence against the current product + Preview harness composition, including at minimum:

- fresh Codespace exact SHA/provenance;
- automatic startup with Web 5173 Private and API/DB internal;
- real login/first-run behavior;
- Engineering readability/usability in Dark/Light;
- representative pt-BR/en/es affected surfaces;
- Save -> Publish -> Activate -> real Active Runtime;
- operator shell/capabilities;
- representative Runtime scaling at canonical resolutions plus mismatched aspect ratio;
- no document scroll/reflow and aligned hit targets;
- Screen navigation, Popups and Dynamos under the same transform;
- representative alarm/event/trend/history behavior;
- visible live simulation behavior appropriate to the current validation fixture.

If the real browser finds a product defect, create new product bytes, diagnose/fix narrowly, rerun the required gates, and repeat the failed browser evidence. Do not freeze `3fda880...` merely because CI is green.

## 7. C11 remains locked

Do not release C11 yet.

After the new exact product freeze, remaining C11 browser evidence still includes:

- Analog Fill visibly live;
- Dynamo operational/bad-quality states;
- two independent Dynamo instances;
- canonical Runtime resolutions/scaling;
- fullscreen/no-scroll/overlay without reflow;
- one integrated living chain `automation -> TAG/quality -> alarm/event/history -> HMI objects -> command`.

Only after every blocker is cleared may the repository explicitly record:

`RELEASE C11 IMPLEMENTATION`

## 8. After C11 release

Build the living deterministic EEE Simulation exclusively using ordinary generic EliteSCADA mechanisms:

Drivers, TAGs, Internal Memory, Server Scripts, Operational Events, Alarms, Historian, Trend, Alarm Browser, Event Browser, Commands, Screens, Popups, Dynamo and Startup/Home.

No EEE-specific service/Driver/private runtime/hidden package/DEMO-only wiring is allowed.

Then perform final Wave14 acceptance. Only after final accepted Wave14 bytes may Wave13 #205/#207 packaging/signing resume.

## 9. PR hygiene

- #212: OPEN/DRAFT, NEVER merge to main without later explicit Product Owner authorization;
- #257: merged only into integration; C19 history now composed;
- #259: historical validation-only DRAFT/CI trigger, NEVER MERGE;
- #260: closed without merge after C03 #113 SUCCESS;
- #262: closed without merge after Test Preview `33935493882` SUCCESS;
- #208/#210: historical/current Preview harness authority, not product authority.

## 10. Mandatory resume order

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. C19 implementation/progress docs;
6. C11 pre-DEMO / Pass-2 audit / HMI clarification / canonical requirements;
7. `docs/CI-VALIDATION-POLICY.md`;
8. `docs/CODESPACES-PREVIEW-RUNBOOK.md`;
9. live issue #211;
10. live PR #212 and relevant validation PR history;
11. revalidate exact refs/workflows;
12. continue from **fresh Codespace / real browser homologation**, not from an already completed packaging fix.

Do not ask the Product Owner to reconstruct decisions already recorded unambiguously in GitHub.
