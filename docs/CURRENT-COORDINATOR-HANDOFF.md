# EliteSCADA — Current Coordinator Handoff

**Last operational synchronization:** 2026-09-04 BRT  
**Status:** **WAVE 14 ACTIVE / C12–C19 AUTOMATED-GREEN PRE-DEMO BASELINE / C11 REVALIDATION RESUMES / PRODUCT OWNER CODESPACE HOMOLOGATION AFTER CANONICAL DEMO / WAVE13 PAUSED**

> GitHub is the official development memory. Revalidate live refs and exact-SHA workflows before acting. Documentation-only and validation-overlay commits do not redefine product authority.

## 1. Permanent gates

- backend is canonical authority;
- authorization is backend-enforced;
- licensing is host-owned and fail-closed;
- lifecycle remains `Working -> saved Revision -> Published -> Active -> HMI Runtime`;
- diagnose a red before rerun;
- never weaken tests/security/contracts/identity/lifecycle for green;
- PR #212 stays OPEN/DRAFT and must not merge to `main` without later explicit Product Owner authorization;
- C11 stays `IMPLEMENTATION LOCKED` until explicit Coordinator release;
- Wave13 #205/#207 stays paused until final accepted Wave14 bytes.

## 2. Product Owner sequencing decision

Product Owner clarified on 2026-09-04 BRT:

**the real fresh-Codespace visual homologation will be performed after the new canonical EEE DEMO has been created.**

This supersedes the prior ordering that treated fresh Codespace visual homologation as a pre-DEMO blocker.

The deferred Codespace pass remains mandatory before final Wave14 Product Owner acceptance and must exercise the completed product with the canonical DEMO. Do not claim that it has already occurred.

## 3. C19 accepted and composed

C19 isolated accepted PRODUCT SHA:

`43d2734b18dc8b78caaa917b9bf6a381ca47b202`

Accepted isolated tree:

`6a20ad793fdd9f3b66b62a954a6d79a5923ac24f`

Isolated evidence was green across EliteSCADA CI #1369, Wave11 #297, Preview Licensing #319, L3 #275, Interop #196 and C03 #112.

Architecture accepted:

- Operational Event authoring uses protected Working -> Preview -> CAS -> Apply;
- Python `emit_operational_event` has no event-bus/history authority;
- C# routes through canonical C14 Active Runtime;
- unknown/disabled/stale definitions fail closed;
- `ServerScriptRuntimeManager._revisionGate` is the single canonical Script/Active revision gate;
- obsolete separate C19 gate/`AsyncLocal` design must never be restored;
- `ServerScriptRunner.py` is a normal Debug/publish output asset.

PR #257 was merged only into `wave14/corrections-integration`, preserving history.

## 4. Current pre-DEMO product authority

Exact combined C12–C19 PRODUCT SHA:

`3fda88061df35ad14755d22881e5d3a9216d1ff5`

Tree:

`da6b406ac111cb40b99e5b13031601eb71606ddd`

This SHA is now the frozen **pre-DEMO engineering baseline** for C11 revalidation and canonical DEMO preparation. Later branch HEADs may be documentation-only and must not replace this product authority.

This is not final Wave14 Product Owner acceptance.

## 5. Combined automated validation on 3fda880

Direct SUCCESS:

- EliteSCADA CI #1370 / `33934982242`;
- Wave11 Active HMI Runtime #298 / `33934982300`;
- Preview Licensing CI #320 / `33934982254`;
- L3 Seven-Driver Lab #276 / `33934982215`;
- Interop Lab Smoke #197 / `33934982216`.

Dedicated C03:

- C03 DNP3 #113 / `33935067545` — SUCCESS;
- validation-only PR #260 was documentation-only, based directly on `3fda880...`, and closed without merge.

Automated Preview harness:

- harness SHA `086b5d6c92ccfa4f5dc7e947c787ffd953acc5e2`;
- Test Preview `33935493882` — SUCCESS;
- validation-only PR #262 closed without merge.

The automated Preview result proves startup/harness viability, not the deferred Product Owner visual homologation.

## 6. Immediate task: resume C11 revalidation

Fresh Codespace is no longer the immediate blocker.

Resume C11 against product baseline `3fda880...` using the binding C11 documents and current automated/browser-test evidence.

Re-evaluate the remaining evidence list in light of the new sequencing. Distinguish carefully between:

1. evidence needed to decide whether C11 implementation can be released so the canonical DEMO can be built; and
2. visual Product Owner acceptance that is intentionally deferred until the DEMO exists.

Do not implicitly release C11 merely because Codespace moved later. Explicitly record `RELEASE C11 IMPLEMENTATION` only if the implementation-release conditions are actually satisfied.

## 7. Canonical DEMO after C11 release

Build the living deterministic EEE Simulation exclusively through ordinary generic EliteSCADA mechanisms:

- Drivers;
- TAGs;
- Internal Memory;
- Server Scripts;
- Operational Events;
- Alarms;
- Historian;
- Trend;
- Alarm Browser;
- Event Browser;
- Commands;
- Screens;
- Popups;
- Dynamo;
- Startup/Home.

No EEE-specific service, Driver, hidden package, private runtime or DEMO-only wiring.

## 8. Deferred final Product Owner homologation

After the canonical DEMO exists, perform the fresh Codespace / real browser Product Owner pass using `docs/CODESPACES-PREVIEW-RUNBOOK.md`.

That pass should validate the actual completed demonstration chain and visual behavior, including Engineering usability, languages, Save -> Publish -> Activate, operator Runtime, scaling/no-scroll/reflow, Screen/Popup/Dynamo behavior, and live alarm/event/trend/history/command behavior.

Any real defect found there must be classified and corrected before final Wave14 acceptance.

## 9. PR hygiene

- #212: OPEN/DRAFT, NEVER merge to main without later explicit Product Owner authorization;
- #257: merged only into integration;
- #259: closed without merge;
- #260: closed without merge after C03 #113;
- #262: closed without merge after Test Preview;
- #208/#210: Preview harness/history, not product authority.

## 10. Mandatory resume order

1. `PROJECT GOAL.md`;
2. `LAST CHANGE.md`;
3. this file;
4. `docs/NEXT-COORDINATOR-CHAT-HANDOFF.md`;
5. C11 pre-DEMO / Pass-2 audit / HMI clarification / canonical DEMO requirements;
6. C19 implementation/progress docs;
7. `docs/CI-VALIDATION-POLICY.md`;
8. live issue #211 and PR #212;
9. revalidate exact refs/workflows;
10. continue with **C11 revalidation**, not fresh Codespace creation.

Do not ask the Product Owner to reconstruct decisions already committed to GitHub.
