# EliteSCADA Roadmap

**Status date:** 2026-09-01 (BRT)  
**Active direction:** **WAVE 12 — HARDENING IN PROGRESS**

**Wave 11:** **COMPLETE / ACCEPTED / CLOSED — issue #194**  
**Wave 12:** **issue #201 IN PROGRESS — AUDIT COMPLETE / IMPLEMENTATION STARTED**

Authoritative product intent: `PROJECT GOAL.md`.  
Mutable coordination state: `LAST CHANGE.md`.  
Operational handoff: `docs/CURRENT-COORDINATOR-HANDOFF.md`.  
Wave 12 preparation: `docs/WAVE-12-HARDENING-PREPARATION.md`.  
Wave 12 active audit: `docs/WAVE-12-HARDENING-AUDIT.md`.

Future Linux/Debian distribution: `docs/LINUX-DEBIAN-DISTRIBUTION.md`.  
CI policy: `docs/CI-VALIDATION-POLICY.md`.

## Current validated foundation

- Waves 03 through 10: **COMPLETE / MERGED**.
- Seven communication Drivers: shared convergence **COMPLETE / MERGED**, independent L2 **7/7 PASS**, integrated L3 **PASS / ACCEPTED**.
- Demo/hardware-bound licensing: **IMPLEMENTED / ACCEPTED / MERGED**.
- Pre-Wave-11 owner-usability gate #191: **COMPLETE / ACCEPTED / MERGED** through PR #193.
- Repository/CI hygiene: **COMPLETE / ACCEPTED / INTEGRATED** through PRs #196 and #197.
- Wave 11 Active Engineering HMI Runtime: **IMPLEMENTED / MERGED / POST-MAIN VALIDATED / ACCEPTED** through PR #199 and issue #194.
- Wave 11 owner-test `.escadapkg` handoff: **IMPLEMENTED / MERGED / POST-MAIN VALIDATED / ACCEPTED** through PR #200.
- Latest validated product-code `main`: `4ccc29cb4bb334dc473d8265f48a9c8601993413`.
- Post-main Wave 11 workflow #14 / `33552016447`: **SUCCESS**.
- Post-main EliteSCADA CI #1067 / `33552016454`: **SUCCESS** including backend build/tests/runtime smoke, Web build and Chromium E2E.

Documentation-only `[skip ci]` commits have advanced `main` beyond the validated product-code SHA without superseding its validation evidence.

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
Wave 09      Screens + Popups + Dynamos + navigation + Historical Data + Reporting       COMPLETE
Wave 10      Python visual events + animation + preview                                  COMPLETE
Driver Lab   Seven-peer reproducible interoperability infrastructure                     COMPLETE / MERGED
Driver L2    Independent product-path protocol evidence                                  7/7 PASS
Drivers      Shared runtime/Engineering convergence                                      COMPLETE / MERGED
DemoLicense  Demo + hardware-bound licensing + offline License Generator                 COMPLETE / ACCEPTED / MERGED
Driver L3    Seven Drivers concurrently + Gateway + fault/recovery                       PASS / ACCEPTED / INTEGRATED
Pre-Wave 11  GUI License Generator + Slider + application file + minimum Dynamo library  COMPLETE / ACCEPTED / MERGED
CI Hygiene   Specialized CI routing + stale PR/issue sanitation + focused ownership      COMPLETE / MERGED
Wave 11      Active persisted Engineering HMI Runtime + owner-test package                COMPLETE / ACCEPTED / CLOSED
Wave 12      Hardening                                                                   IN PROGRESS / #201
Wave 13      Signed Windows x64 package + Authenticode release verification              WAITING / REQUIRED
Wave 14      Product-owner validation                                                    WAITING
Wave 15      Feedback/corrections                                                        WAITING
Preview      EliteSCADA Preview build                                                    FUTURE
Driver L4    Physical hardware/site validation by Development Lead                       AFTER PREVIEW BUILD
FINAL        EliteSCADA v0.1 — Full Product Validation Preview
```

## Future distribution tracks outside the current v0.1 sequence

These are product directions, not active implementation stages. They start only when explicitly authorized by the Development Lead.

```text
DNP3 License Commercial license or approved dependency alternative                       FUTURE / REQUIRED BEFORE COMMERCIAL PACKAGE WITH DNP3
LinuxDeb     Official Linux x64 Debian .deb distribution                                 FUTURE / NOT STARTED / DEVELOPMENT-LEAD TRIGGER
Debian 12    First Linux amd64 packaging/homologation target                             PLANNED FIRST TARGET
Debian 13    Follow-up Linux amd64 homologation target                                   PLANNED AFTER DEBIAN 12
```

The Linux distribution is expected to be a packaging/integration front rather than a product port. Planned scope includes `linux-x64` self-contained publish, React/Pyodide assets served by Kestrel, `/usr/lib/elitescada` + `/etc/elitescada` + `/var/lib/elitescada` layout, Linux-persistent licensing, system user + `elitescada.service`, external PostgreSQL/TimescaleDB configuration, `.deb amd64`, clean-Debian install/upgrade CI, installed Runtime/Web/licensing/Driver validation and SBOM/dependency-license auditing.

Initial Linux packaging should remain multi-file internally; the `.deb` itself is the distribution artifact. Single-file publish is not a first-version requirement because native libraries, Pyodide assets and future installable Drivers benefit from an explicit package layout.

The current Step Function I/O `dnp3` 1.6.0 dependency is a commercial-distribution gate: its public license is non-commercial/non-production. Before any commercial EliteSCADA package includes/enables this Driver, obtain an appropriate commercial license or approve/revalidate an alternative dependency. DNP3 licensing remediation is expected before the Linux `.deb` front unless the authorized package explicitly excludes DNP3.

Full future Linux contract: `docs/LINUX-DEBIAN-DISTRIBUTION.md`.

## Wave 11 accepted result

Wave 11 replaced the hand-authored Runtime Demo as application truth with an HMI Runtime derived from the **active persisted canonical Engineering revision**.

Accepted lifecycle authority:

`Working -> saved Revision -> Published -> Active -> HMI Runtime projection`

Accepted capabilities include:

1. protected backend projection of persisted Active Engineering;
2. deterministic project/revision consistency and fail-closed behavior;
3. canonical Screen/Popup/Dynamo Runtime mount;
4. Active visual assets with integrity validation and explicit Runtime asset authority;
5. protected Slider/TAG writes through the existing Runtime API;
6. explicit Simulation fallback only when no Engineering runtime is active;
7. Active A -> Working isolation -> Active B browser proof;
8. operator Runtime `View` without Working Engineering read authority;
9. real imported PNG served from the Active revision;
10. owner-test portable application package generated through the normal package export boundary and validated through inspect/import-preview.

Final owner-test application:

- `EliteSCADA-Wave11-Demo.escadapkg`;
- post-main artifact id `9817878392`;
- application SHA-256 `13261af59b8707df7d9ef3bbea307cb0c85d945ea8f47315fb693c92c885efa1`.

Issue #194 is **CLOSED / COMPLETED**.

## Wave 12 active boundary

Wave 12 issue #201 is **IN PROGRESS**. After the required live-state review and failure-surface audit, branch `coordination/wave12-hardening` was created from live `main` `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`. No Wave 12 PR or acceptance-CI evidence exists yet.

Wave 12 is a hardening stage over already accepted contracts, focused on:

- fail-closed/recovery behavior;
- authorization and audit boundaries;
- persistence/restart consistency;
- `.escadapkg` integrity and atomic preview/apply behavior;
- runtime resource/fault isolation;
- concurrency/race review;
- diagnostic sanitization;
- regression and CI hardening.

Wave 12 is not a feature-expansion wave. New Drivers/protocols, Authenticode/release signing, owner validation and physical L4 remain outside its scope.

The active finding/slice ledger is `docs/WAVE-12-HARDENING-AUDIT.md`. The first implementation slice addresses realtime WebSocket isolation, exact-snapshot/serialized Engineering persistence saves, bounded JSON/CSV import ingress and `.escadapkg` export/import limit symmetry. Remaining findings must be fixed with regression evidence or explicitly dispositioned before Wave 12 closes.

## CI validation strategy

`EliteSCADA CI` is the universal Coordinator acceptance gate for PRs to `main`.

Specialized validation remains affected-subsystem based:

- `Preview Licensing CI`: licensing, License Generator, product capacity and licensing-sensitive shared paths; manual/release execution remains available.
- `L3 Seven-Driver Lab`: Drivers, DriverHost, communication, Gateway, TAG/event, Driver tests and interoperability lab; manual/cross-cutting/release execution remains available.
- structural impact may require a manual specialized run even when path filters do not select it automatically.

Repository fact: `main` currently has no configured branch protection / required status checks. The universal CI requirement remains an operational merge rule rather than GitHub enforcement.

## Quality locks

- canonical Engineering/backend authority;
- Runtime derives from Active persisted Engineering, never mutable Working/browser-only state;
- schema-v15 `CommunicationBinding` remains the rich communication TAG authority;
- licensing remains host-owned; Drivers do not inspect license/hardware state directly;
- private signing keys never enter GitHub, CI or distributed product builds;
- no Driver-to-Driver coupling or canonical TAG/cache/event bypass;
- no test weakening to manufacture green evidence;
- L2 does not imply L3; L3 does not imply physical L4;
- exact CI evidence is required at material stage transitions;
- Wave 13 retains mandatory Authenticode + trusted timestamp release signing;
- commercial packaging must not silently ship a dependency outside its permitted license terms; DNP3 requires an explicit commercial-license/alternative disposition before commercial inclusion.
