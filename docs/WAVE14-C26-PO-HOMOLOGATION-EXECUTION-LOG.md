# Wave 14 C26 — PO Homologation Execution Log

Coordinator issue: #286

Branch: `wave14/c26-po-homologation-corrections`

Base authority at C26 start:

`a724ece64a292aa1d1dedd886a72fb28ff8d90fe`

## Status

**EXECUTION ACTIVE — PRODUCT CORRECTIONS — NEW PREVIEW ONLY AFTER ACCEPTED C26 PRODUCT HEAD**

The pre-C26 Preview remains preserved in #285 / `d92e81f821c1a9c376b39bc3684eead54b3f570e` as homologation evidence. It is not the post-C26 Preview.

## Work order

1. C26.1 Runtime transient-failure resilience — P0
2. C26.2 Runtime logical viewport/layout — P0/P1
3. C26.3 Runtime renderer technical-id leakage — P1
4. C26.4 Runtime popup layout — P1
5. C26.5 Alarm/Event/Historian Runtime surface — P1
6. C26.6 Engineering shell workspace usability — P1
7. C26.7 Engineering theme/contrast — P1
8. C26.8 Screen editor functional audit/fixes — P1
9. C26.9 Popup editor functional audit/fixes — P1
10. C26.10 Canonical EEE residual cleanup — P1
11. exact-SHA full validation
12. integrate only into canonical C11 when accepted
13. regenerate canonical package/checksum/provenance
14. create a new post-C26 Preview branch/PR
15. new real Codespace/Product Owner homologation

## C26.1 initial diagnosis

Real Codespace Product Owner homologation demonstrated an intermittent Runtime failure roughly every 10–20 seconds. The observed unavailable surface includes `HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE` and a transport failure such as `Content-Length header of network response exceeds response Body`.

The product currently treats a transient fetch/poll failure as loss of the mounted Active Runtime projection. Recovery remounts the Runtime application and therefore resets visual navigation to the configured startup screen. This violates normal operator continuity expectations.

Required behavior:

- backend persisted Active Revision remains the canonical authority;
- retain the last successfully loaded Active Runtime projection during transient transport/poll failures;
- keep the current selected Runtime screen mounted during degradation and recovery;
- continue retry/recovery without authentication/licensing/authority bypass;
- block only when no valid projection has ever been loaded or when canonical authority genuinely invalidates the projection;
- add regression coverage that navigates away from startup, injects a transient Active Runtime fetch failure, recovers and proves navigation continuity.

## Guardrails

- #212 remains OPEN/DRAFT and is not authorized to merge to `main`;
- #266 MUST NEVER MERGE;
- #263 remains the C11 route only to Wave14 integration;
- never modify `main` directly;
- no force push/destructive rebase/branch deletion;
- diagnose CI red before rerun;
- no validation, test, security, Identity, authorization, Engineering Lock, licensing, lifecycle, package or Runtime authority weakening;
- Runtime/Active must not depend on `.escadalib`;
- Alarm / Operational Event / Audit remain separate;
- Wave 13 #205/#207 remains paused.
