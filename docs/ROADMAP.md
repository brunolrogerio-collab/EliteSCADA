# EliteSCADA Roadmap

**Status date:** 2026-09-02 (BRT)  
**Active direction:** **TEMPORARY BROWSER TEST PREVIEW #208 — PLANNED / NEXT; WAVE 13 #205/#207 — PAUSED**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable resume point: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Wave 12 preparation: `docs/WAVE-12-HARDENING-PREPARATION.md`.  
Wave 12 accepted ledger: `docs/WAVE-12-HARDENING-AUDIT.md`.  
Temporary browser Test Preview: `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`, issue #208.  
Wave 13 preparation: `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`.  
Wave 13 issue: #205; preserved draft implementation PR: #207.  
CI policy: `docs/CI-VALIDATION-POLICY.md`.

## Current validated foundation

- Waves 03–10: **COMPLETE / MERGED**.
- Seven communication Drivers shared convergence + L2 + integrated L3: **COMPLETE / ACCEPTED**.
- Demo/hardware-bound licensing and offline License Generator: **IMPLEMENTED / ACCEPTED / MERGED**.
- Pre-Wave-11 owner-usability gate #191: **COMPLETE / ACCEPTED / MERGED**.
- Repository/CI hygiene: **COMPLETE / MERGED**.
- Wave 11 Active Engineering HMI Runtime + owner-test `.escadapkg`: **COMPLETE / ACCEPTED / CLOSED** under issue #194.
- Wave 12 Hardening: **COMPLETE / ACCEPTED / CLOSED** under issue #201.
- Accepted Wave 12 product-code baseline: `63bced02426fcb84b26028913f6c68feb3457d80`.
- Exact accepted post-merge evidence: EliteSCADA CI #1096 / `33576603185` **SUCCESS** and L3 #92 / `33576603158` **SUCCESS**.
- Temporary browser Test Preview issue #208 is **PLANNED / NEXT**.
- Wave 13 issue #205 remains open; implementation exists only in preserved draft PR #207 and is **PAUSED** by Development Lead direction.

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
Test Preview Temporary browser Preview via Codespaces / Launch Test Preview              PLANNED / NEXT
Wave 13      Signed Windows x64 package + Authenticode release verification              PAUSED
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation                                           AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 12 final acceptance

Final accepted product-code `main` baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Final post-merge evidence:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Wave 12 implementation entered `main` through PR #203. The first post-merge universal run #1094 exposed two runner-sensitive 500 ms Modbus happy-path test timeouts. The cause was diagnosed, PR #204 adjusted only those two healthy-path timing margins, exact-head #1095 and L3 #91 passed, and post-merge `main` #1096 and L3 #92 passed. No production or explicit fault-path contract was weakened.

## Temporary browser Test Preview direction

Issue #208 is **OPEN / PLANNED / NEXT** and `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md` records the required direction.

The target is a temporary development/homologation environment, preferably GitHub Codespaces, that starts the real EliteSCADA stack and provides an authenticated temporary browser URL without requiring a local installation.

The intended environment includes the .NET backend, React/Pyodide frontend, PostgreSQL/TimescaleDB, automatic loading of a validated Demo `.escadapkg`, activation through the normal persisted Engineering lifecycle, and browser access to Engineering/HMI Runtime with simulated TAGs, alarms, trends and other available Preview surfaces.

Only the required Web port should be exposed. Database/internal service ports remain private. This environment makes no production availability, durability or security claim and is not a supported customer deployment target.

A dedicated administrative test account named `EliteSCADA` is required. Its password is supplied separately by the Development Lead and must be injected through protected Codespaces/GitHub secret material such as `ELITESCADA_PREVIEW_ADMIN_PASSWORD`; it must never be committed to this public repository or normal artifacts/logs.

Preferred reusable operator path: **Launch Test Preview**.

## Wave 13 paused direction

Issue #205 remains open and draft PR #207 preserves the implementation/audit work completed so far. Development is intentionally **PAUSED** while #208 is the active coordination direction.

Do not merge or expand #207 during this pause. When Wave 13 resumes, re-read live `main`, issue #205, PR #207, current exact-SHA CI and the signing/package audit before continuing. Do not assume the paused branch is automatically current after intervening Preview work.

Wave 13 remains responsible for the controlled Windows x64 release package, Authenticode signatures, trusted timestamping and deterministic release verification. Its security and DNP3 commercial-distribution gates remain unchanged.

## Quality locks

- canonical Engineering/backend authority;
- Runtime derives from persisted Active Engineering, never mutable Working;
- security is enforced in the backend;
- no Driver-to-Driver coupling or canonical TAG/cache/event bypass;
- licensing remains host-owned;
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

`docs/LINUX-DEBIAN-DISTRIBUTION.md` remains **SPECIFIED / NOT STARTED**. Debian 12 `amd64` remains the first planned target, followed by Debian 13. No Linux implementation belongs in the temporary Test Preview or Wave 13 unless explicitly authorized.

Step Function I/O `dnp3` 1.6.0 remains a commercial-distribution gate because its public licensing is non-commercial/non-production.
