# EliteSCADA Roadmap

**Status date:** 2026-09-01 (BRT)  
**Active direction:** **WAVE 12 — HARDENING IN PROGRESS / PR #202**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable resume point: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Wave 12 preparation: `docs/WAVE-12-HARDENING-PREPARATION.md`.  
Wave 12 active ledger: `docs/WAVE-12-HARDENING-AUDIT.md`.  
CI policy: `docs/CI-VALIDATION-POLICY.md`.

## Current validated foundation

- Waves 03–10: **COMPLETE / MERGED**.
- Seven communication Drivers shared convergence + L2 + integrated L3: **COMPLETE / ACCEPTED**.
- Demo/hardware-bound licensing and offline License Generator: **IMPLEMENTED / ACCEPTED / MERGED**.
- Pre-Wave-11 owner-usability gate #191: **COMPLETE / ACCEPTED / MERGED**.
- Repository/CI hygiene: **COMPLETE / MERGED**.
- Wave 11 Active Engineering HMI Runtime + owner-test `.escadapkg`: **COMPLETE / ACCEPTED / CLOSED** under issue #194.
- Accepted Wave 11 product-code baseline: `4ccc29cb4bb334dc473d8265f48a9c8601993413`.

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
Wave 12      Hardening                                                                   IN PROGRESS / #201 / PR #202
Wave 13      Signed Windows x64 package + Authenticode release verification              WAITING / REQUIRED
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation                                           AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 12 checkpoint

Branch: `coordination/wave12-hardening`  
Draft PR: #202  
Branch base: `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`  
Latest validated Wave 12 product-code SHA: `012d15554d96af8600953a793cd58f0a5fc11c4d`

Exact checkpoint evidence:

- EliteSCADA CI #1075 / `33565105224`: **SUCCESS**;
- L3 Seven-Driver Lab #71 / `33565105291`: **SUCCESS**;
- Preview Licensing CI #124 / `33565105254`: **SUCCESS**;
- Wave 11 Active HMI Runtime #22 / `33565105207`: **SUCCESS**.

Closed with regression evidence at this checkpoint:

- W12-RT-001 realtime client isolation;
- W12-PER-001 persistence Save atomicity/serialization;
- W12-ING-001 bounded Engineering ingress;
- W12-PKG-001 package resource-limit symmetry;
- W12-PER-002 Persistence Apply lease/CAS parity.

Next High finding: **W12-AUTH-001 local-identity mutation concurrency and last-administrator invariant**.

Remaining afterward: W12-AUTH-002, W12-API-001 and W12-AUD-001.

Wave 12 is not complete and PR #202 must remain unmerged until the ledger is fully fixed or explicitly dispositioned and final exact-SHA CI evidence is green.

## Quality locks

- canonical Engineering/backend authority;
- Runtime derives from Active persisted Engineering, never mutable Working;
- security is enforced in the backend;
- no Driver-to-Driver coupling or canonical TAG/cache/event bypass;
- licensing remains host-owned;
- private signing keys never enter GitHub, CI or distributed product builds;
- no test weakening to manufacture green evidence;
- EliteSCADA CI is the universal merge gate even without GitHub branch protection;
- specialized CI is impact-based and never substitutes for the universal gate;
- Wave 13 retains Authenticode + trusted timestamp release signing;
- Linux `.deb` remains specified/not started until Development Lead authorization;
- commercial packaging cannot include/enable DNP3 without an appropriate commercial license or approved/revalidated replacement.

## Future distribution tracks

`docs/LINUX-DEBIAN-DISTRIBUTION.md` remains **SPECIFIED / NOT STARTED**. Debian 12 `amd64` remains the first planned target, followed by Debian 13. No Linux implementation belongs in Wave 12.

Step Function I/O `dnp3` 1.6.0 remains a commercial-distribution gate because its public licensing is non-commercial/non-production.
