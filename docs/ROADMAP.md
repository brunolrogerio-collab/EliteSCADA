# EliteSCADA Roadmap

**Status date:** 2026-09-02 (BRT)
**Active direction:** **TEST PREVIEW #208/#210 — REAL CODESPACE VALIDATION PENDING; WAVE 13 #205/#207 — ACTIVE UNDER SEPARATE COORDINATION**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable resume point: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Wave 12 preparation: `docs/WAVE-12-HARDENING-PREPARATION.md`.  
Wave 12 accepted ledger: `docs/WAVE-12-HARDENING-AUDIT.md`.  
Temporary browser Test Preview: `docs/TEMPORARY-BROWSER-TEST-PREVIEW.md`, issue #208, draft PR #210.
Wave 13 preparation: `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md`.  
Wave 13 issue: #205; draft implementation PR: #207.
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
- Temporary browser Test Preview #208/#210 is **IMPLEMENTED / AUTOMATED VALIDATION GREEN / REAL CODESPACE VALIDATION PENDING**.
- Preview head `208ac69b5638ace8557a700d34dd16571360c8f6`: Test Preview #4 / `33594259242` **SUCCESS** and EliteSCADA CI #1122 / `33594259232` **SUCCESS**.
- Wave 13 #205/#207 is **ACTIVE / RELEASED FOR SEPARATE COORDINATION**; PR #207 remains draft until its own acceptance gates are met.
- Wave 13 repository-side checkpoint `a287c4f2a4e4c571a7c5ad4b25efb1c98132e5ab`: Windows #22, EliteSCADA CI #1118, L3 #97 and Wave 11 Active HMI Runtime #56 are **SUCCESS**.

## Coordination model

Development Lead direction on 2026-09-02 authorizes Preview and Wave 13 to proceed in parallel under different coordinators.

- Preview coordinator: issue #208 / PR #210.
- Wave 13 coordinator: issue #205 / PR #207.
- Neither workstream blocks the other.
- Neither coordinator may assume the other branch has merged.
- Before merge/release decisions, live `main`, open PRs/issues and exact-head Actions must be revalidated.

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
Test Preview Temporary browser Preview via Codespaces / Launch Test Preview              VALIDATION PENDING
Wave 13      Signed Windows x64 package + Authenticode release verification              ACTIVE / SEPARATE COORDINATOR
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation                                           AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Wave 12 final acceptance

Final accepted product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Final post-merge evidence:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

No accepted Wave 11/12 architecture is reopened by either current workstream without a demonstrated defect.

## Temporary browser Test Preview direction

Issue #208 / PR #210 remains active for its coordinator.

The target is a temporary development/homologation environment using GitHub Codespaces that starts the real EliteSCADA stack and provides an authenticated temporary browser URL without local installation.

Implemented direction includes the .NET backend, React/Pyodide frontend, PostgreSQL/TimescaleDB, validated Wave 11 Demo package, normal persisted Engineering lifecycle bootstrap and Web-only temporary exposure.

Only the required Web port is intended to be forwarded. Database/internal service ports remain private. The environment makes no production availability, durability or security claim and is not a supported customer deployment target.

A dedicated administrative test account named `EliteSCADA` uses protected secret `ELITESCADA_PREVIEW_ADMIN_PASSWORD`. Its value must never be committed to the repository or emitted in normal artifacts/logs.

Automated validation is green. Remaining acceptance gate: a fresh real Codespace must start successfully and provide a working private Web URL with representative browser validation.

## Wave 13 active direction

Issue #205 remains open and PR #207 remains draft. Development is **ACTIVE under separate coordination**.

The Wave 13 coordinator re-audited live `main` `056148bb17c0fd6cb78bd21339b3f9614d38ad68`, issue #205, PR #207, exact current CI and the signing/package audit. That documentation-only `main` is being incorporated over validated implementation checkpoint `a287c4f2a4e4c571a7c5ad4b25efb1c98132e5ab`. Concurrent Preview changes matter only after they actually reach `main` or otherwise affect the exact branch being evaluated.

Implemented repository-side scope includes the self-contained Windows x64 unsigned candidate, packaged React/Pyodide product, separate graphical License Generator authority role, packaged-product regression, signed-return derivation checks, deterministic signed-byte manifests, role-specific ZIPs and fail-closed Authenticode/publisher/RFC3161/hash/content verification with negative cases.

Wave 13 remains blocked from acceptance by a real protected or hardware-backed organizational signing authority, the exact certificate Subject and a real RFC3161-timestamped signed return. Its security and DNP3 commercial-distribution gates remain unchanged.

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
- Wave 13 requires Authenticode + trusted timestamp release verification;
- SmartScreen reputation is separate from signature validity;
- Linux `.deb` remains specified/not started until Development Lead authorization;
- commercial packaging cannot include/enable DNP3 without an appropriate commercial license or approved/revalidated replacement.

## Future distribution tracks

`docs/LINUX-DEBIAN-DISTRIBUTION.md` remains **SPECIFIED / NOT STARTED**. Debian 12 `amd64` remains the first planned target, followed by Debian 13. No Linux implementation belongs in the temporary Test Preview or Wave 13 unless explicitly authorized.

Step Function I/O `dnp3` 1.6.0 remains a commercial-distribution gate because its public licensing is non-commercial/non-production.
