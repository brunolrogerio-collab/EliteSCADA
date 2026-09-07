# Wave 14 C25 — Post-Demo Consolidated Corrections — Execution Log

**Status:** ACTIVE / NOT ACCEPTED / NOT INTEGRATED / C25.0-C25.7 COMPLETE / C25.8 GREEN INTERMEDIATE INFRASTRUCTURE BUT FINAL ACCEPTANCE REMEDIATION REQUIRED / C25.9 AUDIT OPEN-BLOCKED  
**Coordinator package:** C25  
**Tracking issue:** #282  
**Implementation PR:** #283  
**Implementation branch:** `wave14/c25-post-demo`  
**Integration target:** `wave14/corrections-integration`

> GitHub live state is the sole project authority. Revalidate live refs, PR state and exact-SHA CI before every decision or mutation. Historical detailed ledger revisions remain preserved in Git history; this file is the current resumable authority.

## 1. Permanent governance

- #283 remains OPEN/DRAFT and may target only `wave14/corrections-integration`.
- #212 remains OPEN/DRAFT and MUST NOT merge to `main` without later explicit Product Owner authorization.
- Never alter `main` directly.
- No force push, destructive rebase, branch deletion or unrelated cleanup.
- Diagnose every CI red before rerun; no blind reruns.
- Never weaken tests, validation, authentication, authorization, licensing, lifecycle, package or Runtime authority for green CI.
- Backend Active Revision remains Runtime application authority.
- Alarm / Operational Event / Audit remain distinct.
- No EEE-specific workaround for a generic product gap.
- C11 remains frozen at `41d24d89c3b9d2b881215255e44023fabde262f3` until C25 is accepted, integrated only into integration and post-merge exact-SHA revalidated.
- #266 remains validation-only and MUST NEVER MERGE.
- Wave 13 issue #205 and PR #207 remain paused until the approved post-C25/main sequence reaches them.

## 2. Accepted baseline beneath C25

C25 branch base:

`c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`

Accepted C24 product authority beneath C25:

`40a491c2de2403f2934b8bae647c35072d5c2496`

C24 remains ACCEPTED / INTEGRATED / POST-MERGE REVALIDATED.

## 3. Checkpoint matrix

### C25.0 — Bootstrap + full architecture/code audit

**COMPLETE**

### C25.1 — Engineering Lock domain/package/security

**COMPLETE**

### C25.2 — Engineering Lock backend enforcement

**COMPLETE**

### C25.3 — Engineering Lock UI/lifecycle/package

**COMPLETE**

### C25.4 — Restore-first / System Recovery

**COMPLETE**

### C25.5 — Runtime session UX

**COMPLETE**

### C25.6 — Reusable Resource Libraries

**COMPLETE / EXACT-SHA GREEN / NOT INTEGRATED**

Exact validated product/test SHA:

`1f17367defa03f903e68f585d068b4f23f82bef9`

- C25 #275 / `34052101709` — SUCCESS;
- C03 #280 / `34052101702` — SUCCESS.

C25.6 closed `.escadalib` semantics including association without Working mutation, selective dependency-aware use, project-owned incorporated content, safe disassociation and self-contained `.escadapkg` with no Runtime library dependency.

### C25.7 — Distributed Runtime Foundation

**COMPLETE / EXACT-SHA GREEN / NOT INTEGRATED**

Exact validated product/test SHA:

`233002ded306c971858b356e1d2a50a89921da37`

- C25 #300 / `34062152621` — SUCCESS;
- C03 #294 / `34062152648` — SUCCESS.

C25.7 proves the canonical Server Runtime foundation, Runtime Session Lease, Viewer/Interactive effective-capability reduction, voluntary View Only server-side enforcement and topology-neutral `.escadapkg` behavior. Production HA/replication/failover remains future-Wave work.

### C25.8 — Contextual multilingual Help/manual

**GREEN INTERMEDIATE INFRASTRUCTURE / FINAL ACCEPTANCE REMEDIATION REQUIRED**

Initial implementation:

`9ab78d00ea908636e1d2f05f7df716ee15f197f7` — `feat(w14-c25): add contextual multilingual help`

Diagnosed correction:

`c5cf2dca1090acc6ebb278d31276d508304a6381` — `fix(w14-c25): align contextual help translation contract`

Exact-SHA validation on corrected intermediate SHA:

- C25 #304 / `34068529435` — SUCCESS;
- C03 #296 / `34068529398` — SUCCESS.

Delivered at this green intermediate checkpoint:

- local/offline contextual Help;
- stable language-neutral Topic IDs;
- pt-BR / en / es;
- canonical locale persistence;
- same-origin `/api/help`;
- contextual UI navigation;
- canonical Engineering Data Source catalog-derived Driver/source topics;
- actual Server Script API allow-list;
- backend Help contract tests;
- Playwright locale/topic tests.

The first implementation SHA failed due one C# translation-record constructor mismatch. The failure was diagnosed before correction; no blind rerun or test weakening occurred. `c5cf2dca...` corrected that contract mismatch and closed exact-SHA green.

#### Driver/source inventory rule

Manual/user-facing communication Driver count is **8 total including Modbus**.

Canonical modern runtime composition includes:

1. MQTT;
2. IEC 60870-5-104;
3. Allen-Bradley Logix / EtherNet/IP;
4. OPC UA;
5. DNP3;
6. Siemens S7 ISO;
7. BACnet.

Modbus is the eighth production communication Driver.

Internal Memory and TAG Gateway are separate product concepts. Simulation is not to be presented as a ninth production communication Driver merely because it appears in the Engineering source catalog.

### C25.9 — Integrated regression/audit

**OPEN / BLOCKED BY MANUAL COVERAGE GAP DISCOVERED DURING AUDIT**

The C25.9 audit compared the green C25.8 infrastructure checkpoint against the higher-priority binding consolidated product contract and found that installed-manual topic depth remained incomplete.

Therefore the earlier issue comment stating `C25.8 COMPLETE` is superseded **for final acceptance purposes**. `c5cf2dca...` remains a valid green intermediate infrastructure SHA, but it is not the final C25.8 acceptance candidate.

Missing/insufficient coverage includes the mandatory product manual matrix:

- Getting Started / first startup / authentication;
- Runtime operator/session behavior;
- Working -> Save -> Revision -> Publish -> Activate;
- `.escadapkg` Import/Export/Inspect/Preview/Apply;
- Data Sources;
- TAGs, quality, timestamps, writeability, addressing, scaling vs presentation formatting;
- every production Driver with real configuration/address syntax/limitations derived from shipped contracts;
- Internal Memory and TAG Gateway;
- Scripts including actual APIs, triggers/lifecycle, failure/safety semantics and validated examples;
- Alarms;
- Operational Events;
- Audit as a distinct concept;
- Historian and Trends;
- Reports;
- Screens, Popups, Dynamos, bindings, commands and visual Runtime behavior;
- users/roles/capabilities/security;
- Licensing;
- Backup/System Recovery;
- diagnostics/troubleshooting.

Reusable-library manual coverage is also required:

- `.escadalib` creation/export;
- association != import;
- association alone does not mutate Working;
- selective `Usar` incorporates selected resources plus validated dependency closure;
- disassociation removes catalog availability only;
- incorporated resources become ordinary project-owned canonical content;
- final `.escadapkg` is self-contained;
- Runtime never depends on `.escadalib`.

### C25.10 — Exact final candidate matrix / acceptance

**NOT STARTED**

C25.10 must not begin until the C25.8 remediation is exact-SHA green and C25.9 integrated audit closes.

## 4. Immediate execution order for the next coordinator

1. Revalidate live PR #283 HEAD and exact-SHA C25/C03 workflow results.
2. Read latest issue #282 comments first; they contain the superseding C25.9 finding.
3. Use `c5cf2dca1090acc6ebb278d31276d508304a6381` as the green intermediate Help infrastructure baseline only.
4. Expand the installed manual to cover the complete mandatory topic matrix from `docs/WAVE14-C25-CONSOLIDATED-POST-DEMO-CONTRACT.md`.
5. Enrich Driver topics from canonical descriptor/configuration schema rather than handwritten unsupported claims.
6. Preserve the explicit 8-Driver count; keep Internal Memory and TAG Gateway separate.
7. Add regression tests/gates for mandatory topic presence and reusable-library semantics.
8. Preserve stable Topic IDs and pt-BR/en/es semantic parity.
9. Keep Script documentation bound to APIs actually supported by the build.
10. Run C25 + C03 on one exact replacement SHA; diagnose any red before rerun.
11. Only after that SHA is green and the manual coverage audit is satisfied may C25.8 be finally accepted.
12. Resume/finish C25.9 integrated regression/audit on that replacement SHA.
13. Prepare C25.10 only after C25.9 closes.

## 5. Revalidated guard rails at this handoff

- #212 — OPEN/DRAFT; merged=false; integration head `c2fc96eacc168ea092c2e4d4dcbc79b00faa3155`; no `main` authorization.
- #263 — OPEN/DRAFT; C11 head remains `41d24d89c3b9d2b881215255e44023fabde262f3`.
- #266 — OPEN/DRAFT validation-only; MUST NEVER MERGE.
- issue #205 — OPEN; Wave 13 paused.
- #207 — OPEN/DRAFT; Wave 13 preserved branch head `fda87ba4445127c174f6ea533a6bcabaabc7bb20`.

## 6. Required post-C25 sequence

After explicit Product Owner acceptance of one exact C25 candidate:

1. merge accepted C25 only into `wave14/corrections-integration`;
2. exact-SHA post-merge validate integration;
3. only then synchronize/adapt C11 canonical EEE Demo;
4. revalidate generic product lifecycle/package behavior;
5. export/version/freeze canonical `EliteSCADA-EEE-Demo.escadapkg`, checksum and provenance;
6. launch and keep Preview Codespace active through visual homologation and the later approved main transition;
7. only explicit Product Owner authorization permits #212 -> `main`;
8. validate the resulting new `main` containing accepted Wave 14/C25;
9. only then resume Wave 13 Windows release/signing from that new mainline authority.
