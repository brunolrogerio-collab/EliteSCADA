# EliteSCADA Roadmap

**Status date:** 2026-09-01 (BRT)  
**Active direction:** **WAVE 13 — PREPARED / NOT STARTED**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable resume point: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Wave 12 preparation: `docs/WAVE-12-HARDENING-PREPARATION.md`.  
Wave 12 accepted ledger: `docs/WAVE-12-HARDENING-AUDIT.md`.  
Wave 13 preparation: `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`.  
Wave 13 issue: #205.  
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
- Wave 13 issue #205 and preparation document exist; implementation has **NOT STARTED**.

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
Wave 13      Signed Windows x64 package + Authenticode release verification              PREPARED / NOT STARTED
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

## Wave 13 prepared direction

Issue #205 is **OPEN / PREPARED / NOT STARTED**. No implementation branch exists by design.

Wave 13 is responsible for the controlled Windows x64 release package, Authenticode signatures, trusted timestamping and deterministic release verification. The first implementation step is an audit/design slice over the existing Windows publish/package surfaces, user-facing executable artifacts, package layout, signing boundary and verification contract.

The next Coordinator must create the implementation branch only after re-reading live `main` and exact current CI.

## Quality locks

- canonical Engineering/backend authority;
- Runtime derives from persisted Active Engineering, never mutable Working;
- security is enforced in the backend;
- no Driver-to-Driver coupling or canonical TAG/cache/event bypass;
- licensing remains host-owned;
- private licensing/signing keys never enter GitHub, normal CI or distributed product builds;
- no test weakening to manufacture green evidence;
- EliteSCADA CI is the universal merge gate even without GitHub branch protection;
- specialized CI is impact-based and never substitutes for the universal gate;
- protected unsafe API mutations fail closed before execution when durable append-only audit admission cannot be persisted;
- post-action audit failures do not masquerade as process-command failures that could trigger unsafe client retries;
- Wave 13 requires Authenticode + trusted timestamp release verification;
- SmartScreen reputation is separate from signature validity;
- Linux `.deb` remains specified/not started until Development Lead authorization;
- commercial packaging cannot include/enable DNP3 without an appropriate commercial license or approved/revalidated replacement.

## Future distribution tracks

`docs/LINUX-DEBIAN-DISTRIBUTION.md` remains **SPECIFIED / NOT STARTED**. Debian 12 `amd64` remains the first planned target, followed by Debian 13. No Linux implementation belongs in Wave 13 unless explicitly authorized.

Step Function I/O `dnp3` 1.6.0 remains a commercial-distribution gate because its public licensing is non-commercial/non-production.
