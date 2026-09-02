# EliteSCADA Roadmap

**Status date:** 2026-09-02 (BRT)  
**Active direction:** **TEST PREVIEW #208/#210 — ACTIVE VALIDATION HARNESS; WAVE 14 #211 — ACTIVE EARLY PRODUCT-OWNER VALIDATION; WAVE 13 #205/#207 — PAUSED AT GREEN CHECKPOINT**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable resume point: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Wave 12 accepted ledger: `docs/WAVE-12-HARDENING-AUDIT.md`.  
Temporary browser Test Preview: `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`, issue #208, draft PR #210.  
Codespaces Preview runbook: `docs/CODESPACES-PREVIEW-RUNBOOK.md` on the Preview branch while PR #210 remains unmerged.  
Wave 13 preparation: `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`.  
Wave 13 issue: #205; draft implementation PR: #207.  
Wave 14 Product-owner validation: issue #211.  
CI policy: `docs/CI-VALIDATION-POLICY.md`.

## Current validated foundation

- Waves 03–10: **COMPLETE / MERGED**.
- Seven communication Drivers shared convergence + L2 + integrated L3: **COMPLETE / ACCEPTED**.
- Demo/hardware-bound licensing and offline License Generator: **IMPLEMENTED / ACCEPTED / MERGED**.
- Pre-Wave-11 owner-usability gate #191: **COMPLETE / ACCEPTED / MERGED**.
- Wave 11 Active Engineering HMI Runtime + owner-test `.escadapkg`: **COMPLETE / ACCEPTED / CLOSED** under issue #194.
- Wave 12 Hardening: **COMPLETE / ACCEPTED / CLOSED** under issue #201.
- Accepted Wave 12 product-code baseline: `63bced02426fcb84b26028913f6c68feb3457d80`.
- Exact accepted post-merge evidence: EliteSCADA CI #1096 / `33576603185` **SUCCESS** and L3 #92 / `33576603158` **SUCCESS**.
- Temporary browser Test Preview #208/#210 is active and has already produced successful real browser/login evidence after Codespaces-specific fixes.
- Wave 13 #205/#207 repository-side implementation checkpoint is green but **PAUSED by Development Lead**.
- Wave 14 #211 **Product-owner validation** is **ACTIVE EARLY** through the Test Preview.

## Coordination model

Development Lead direction on 2026-09-02 intentionally changes the original order of work after real owner use exposed product/usability findings before release signing was complete.

Current responsibility split:

- Preview infrastructure/reproducibility: issue #208 / PR #210.
- Product-owner validation and finding ledger: issue #211.
- Windows release/signing: issue #205 / PR #207, paused until owner-validation baseline stabilizes.

The Preview is the test harness, not the product-validation scope itself.

Before any merge/release decision, live `main`, open PRs/issues and exact-head Actions must be revalidated.

## Ordered path to v0.1

```text
Wave 03      Operational lifecycle + Runtime TAG Inspector + acceptance foundation       COMPLETE
Wave 04      Project portability + basic Trends + Administration                        COMPLETE
Wave 05      Canonical Script Engineering                                                COMPLETE
Wave 06      Python Editor + Client Visual sandbox                                       COMPLETE
Wave 07      Visual Runtime Object Model + typed visual Engineering                      COMPLETE
Wave 08      Graphical Editor + Image + Engineering Development Monitor                  COMPLETE
08-FOLLOW-A  TAG Bit Access + Driver Bit-Level Boolean Binding                           COMPLETE
08-FOLLOW-B  Typed Visual Expressions + Boolean Conditions + Analog Fill                 COMPLETE
Wave 09      Screens + Popups + Dynamos + Historical Data + Reporting                   COMPLETE
Wave 10      Python visual events + animation + preview                                  COMPLETE
Driver L3    Seven Drivers concurrently + Gateway + fault/recovery                       PASS / ACCEPTED
Pre-Wave 11  GUI License Generator + Slider + application file + Dynamo library          COMPLETE
Wave 11      Active persisted Engineering HMI Runtime + owner-test package                COMPLETE / CLOSED
Wave 12      Hardening                                                                   COMPLETE / ACCEPTED / CLOSED
Test Preview Temporary browser Preview via Codespaces                                    ACTIVE HARNESS
Wave 14      Product-owner validation                                                    ACTIVE EARLY
Wave 15      Non-blocking feedback/corrections                                           WAITING
Wave 13      Signed Windows x64 package + Authenticode release verification              PAUSED / CHECKPOINT GREEN
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation                                           AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

The numerical order is intentionally not the execution order at this moment. Wave 14 is being advanced before Wave 13 final acceptance because signing a product that is still revealing owner-visible defects would create avoidable rework.

## Temporary browser Test Preview direction

Issue #208 / PR #210 remains active as the temporary development/homologation environment used to exercise the actual EliteSCADA stack from a browser.

Implemented/validated direction includes:

- .NET backend;
- React/Pyodide frontend;
- PostgreSQL/TimescaleDB;
- validated Wave 11 Demo package;
- normal persisted Engineering lifecycle bootstrap;
- Web-only temporary exposure;
- exact .NET SDK 10.0.400;
- disposable `/etc/machine-id` required by normal fail-closed licensing;
- protected `ELITESCADA_PREVIEW_ADMIN_PASSWORD` secret;
- automatic startup through `postAttachCommand`;
- successful actual browser login after environment corrections.

Only the required Web port is intended to be forwarded. Database/internal service ports remain private. The environment makes no production availability, durability or security claim.

A dedicated operational runbook records recovery levels and the real failure patterns already seen during homologation. Manual workarounds that are necessary for successful startup must be converted into repository-controlled automation before Preview acceptance.

## Wave 14 active direction

Issue #211 is the active Product-owner validation ledger.

For each product area, validate the real user workflow through a known exact SHA and classify findings:

- **A — Validation blocker:** prevents meaningful testing; fix during Wave 14 so validation can continue;
- **B — Functional defect:** wrong behavior; fix during Wave 14 when it affects release confidence or later validation;
- **C — Usability defect:** technically works but materially harms owner validation; fix when blocking/material;
- **D — Enhancement/preference:** record for Wave 15 or later.

Representative validation includes authentication/Administration, Engineering navigation, Drivers/Data Sources, TAGs, alarms, Templates/Equipment/Dynamos, Screens/Popups, Scripts/Python/Pyodide, Historian/Trends/Reports, Save/Revision/Publish/Activate, Active HMI Runtime, Demo behavior, `.escadapkg`, restart/recovery, licensing UX and visual/readability defects.

The first confirmed owner finding is a pre-existing Script Engineering contrast problem exposed in the real Codespace. Because the surface was effectively unreadable, its narrow correction is treated as a Wave 14 blocker rather than postponed cosmetic feedback.

## Wave 13 paused direction

Issue #205 remains open and PR #207 remains draft, but further Wave 13 execution is paused.

Preserved fully validated implementation SHA:

`9f26a2bc02ae77017e266c52ff128dc39eece4b4`

Retained exact evidence:

- Wave 13 Windows Release #27 / `33643546191`: **SUCCESS**;
- EliteSCADA CI #1134 / `33643546119`: **SUCCESS**;
- L3 Seven-Driver Lab #102 / `33643546111`: **SUCCESS**;
- Wave 11 Active HMI Runtime #64 / `33643546139`: **SUCCESS**.

Wave 13 remains responsible, when resumed, for the controlled Windows x64 package, Authenticode signatures, trusted timestamping and deterministic release verification.

Before resuming, its coordinator must re-audit live `main`, incorporate the accepted Wave 14 product baseline and rerun the packaging/signing validation against the actual post-owner-validation product. Do not sign the stale pre-validation snapshot merely because its old CI was green.

## Wave 15 direction

Wave 15 remains waiting and should receive non-blocking owner feedback, refinements, redesign requests and enhancements discovered during Wave 14.

Do not use Wave 14 as an excuse to implement every improvement noticed during owner use. Only corrections needed for trustworthy validation or release confidence belong in the active Wave 14 path.

## Quality locks

- canonical Engineering/backend authority;
- Runtime derives from persisted Active Engineering, never mutable Working;
- security is enforced in the backend;
- no Driver-to-Driver coupling or canonical TAG/cache/event bypass;
- licensing remains host-owned and fail-closed;
- private licensing/signing keys never enter GitHub, normal CI or distributed product builds;
- no Preview bootstrap password in repository, workflow YAML, images, packages, logs or normal artifacts;
- temporary Preview exposure is development/homologation only and should expose only the required Web surface;
- no test weakening to manufacture green evidence;
- EliteSCADA CI is the universal merge gate even without GitHub branch protection;
- specialized CI is impact-based and never substitutes for the universal gate;
- protected unsafe API mutations fail closed before execution when durable append-only audit admission cannot be persisted;
- post-action audit failures do not masquerade as process-command failures that could trigger unsafe client retries;
- Wave 13 requires Authenticode + trusted timestamp release verification when resumed;
- SmartScreen reputation is separate from signature validity;
- Linux `.deb` remains specified/not started until Development Lead authorization;
- commercial packaging cannot include/enable DNP3 without an appropriate commercial license or approved/revalidated replacement.

## Future distribution tracks

`docs/LINUX-DEBIAN-DISTRIBUTION.md` remains **SPECIFIED / NOT STARTED**. Debian 12 `amd64` remains the first planned target, followed by Debian 13.

Step Function I/O `dnp3` 1.6.0 remains a commercial-distribution gate because its public licensing is non-commercial/non-production.
