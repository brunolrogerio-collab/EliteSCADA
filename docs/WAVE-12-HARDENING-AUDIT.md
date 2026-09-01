# Wave 12 — Hardening Audit

**Status:** ACTIVE — INITIAL AUDIT COMPLETE / REMEDIATION IN PROGRESS

**Audit date:** 2026-09-01 BRT

**Issue:** #201 — Wave 12 — Hardening

**Branch:** `coordination/wave12-hardening`

**Branch base:** live `main` `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`

This is the active finding and implementation-slice ledger for Wave 12. A finding closes only after its root cause is fixed with focused regression evidence, or after an explicit residual-risk disposition is recorded. Green CI alone does not close a finding.

## 1. Baseline verification

- Mandatory coordination documents and issue #201 were read before branch creation.
- Live GitHub state at start: no open PRs; open issues #201 and intentionally deferred L4 #178.
- The branch base is documentation-only beyond the accepted Wave 11 product-code SHA `4ccc29cb4bb334dc473d8265f48a9c8601993413`.
- Latest product-code evidence at start: Wave 11 workflow #14 / `33552016447` and EliteSCADA CI #1067 / `33552016454`, both successful at that exact product-code SHA.
- Repository inventory at start: 181 C# test files with 657 `[Fact]`/`[Theory]` declarations and 92 Playwright spec files.
- The coordination environment does not provide the .NET SDK, so local .NET execution is not acceptance evidence. Exact-head GitHub CI remains mandatory.

## 2. Existing controls confirmed

- HMI Runtime resolves persisted Active Engineering and fails closed on project/revision/package inconsistency.
- Activation stages and validates the candidate before publishing it as Active, preserving the previous Active revision on failure.
- `.escadapkg` import already validates entry paths, duplicate payloads, declared checksums/lengths, asset consistency and aggregate import limits.
- Protected backend authorization remains distinct across Runtime View, Engineering Modify, TAG writes and administration.
- Script execution uses bounded queues, cancellation/timeout boundaries and sanitized diagnostics.
- Audit storage is append-only and its buffering/query surfaces are bounded.
- Core Engineering mutation endpoints use workspace serialization and compare-and-swap version checks.

These controls are locks to preserve, not assertions to relax while hardening adjacent paths.

## 3. Findings

| ID | Severity | Surface | Finding and required disposition | State |
|---|---|---|---|---|
| W12-RT-001 | High | Realtime | `TagRealtimeHub.BroadcastAsync` sends to sockets sequentially with no per-client queue or send timeout. One stalled client can block the TAG event consumer and delivery to every later client. Isolate clients with bounded outbound work and disconnect/evict stalled or overflowing clients. | First slice |
| W12-PER-001 | High | Persistence | `EngineeringProjectPersistenceService.SaveCurrentDerivedAsync` exports package and JSON through separate live snapshots; the API Save path does not hold the workspace mutation lease across snapshot/persist/accept-save. A concurrent mutation can create mixed or falsely clean persisted state. Derive all payloads from one canonical snapshot and serialize the transaction boundary. | First slice |
| W12-ING-001 | High | Engineering ingress | JSON and CSV preview/apply endpoints read request bodies with unbounded `ReadToEndAsync`. Add explicit byte limits, Content-Length fast rejection, streaming enforcement, strict decoding and sanitized client errors. | First slice |
| W12-PKG-001 | High | `.escadapkg` | Export enforces manifest/compressed-output limits but can emit an Engineering payload, file count or total uncompressed payload that its own importer rejects. Enforce symmetric limits before producing the archive and add boundary regression tests. | First slice |
| W12-PER-002 | High | Persistence Apply | Latest/revision Apply paths can replace Working without the same workspace mutation lease/CAS contract used by canonical Engineering mutations. Serialize and require a caller-observed version to prevent silent lost updates. | Pending |
| W12-AUTH-001 | High | Local identities | Concurrent user update/password reset operations can overwrite each other, and concurrent last-administrator mutations can violate the administrator invariant. Introduce transactional/concurrency protection and invariant-focused tests. | Pending |
| W12-AUTH-002 | Medium | Login limiting | `LocalLoginAttemptLimiter` retains unique remote-key entries indefinitely. Add bounded expiry/cleanup without weakening lockout behavior. | Pending |
| W12-AUD-001 | High | Audit durability | `BufferedAuditSink` rejects overflow, while `ApiAuditService` logs/swallow failures and protected mutations continue. Product-safe behavior during sustained audit unavailability must be explicitly selected, implemented and regression-tested; silent protected-action audit loss is not acceptable. | Pending design disposition |
| W12-API-001 | Medium | Requests/diagnostics | Invalid history ranges and persistence limits can reach provider-dependent failures and inconsistent 500 responses. Normalize validation and ensure diagnostics remain actionable without exposing protected values. | Pending |

## 4. Ordered remediation slices

### Slice A — Immediate isolation and atomicity

1. W12-RT-001: bounded per-client realtime delivery and deterministic stalled-client eviction.
2. W12-PER-001: one canonical Engineering snapshot plus serialized Save boundary.
3. W12-ING-001: bounded JSON/CSV request-body reader and endpoint regression coverage.
4. W12-PKG-001: symmetric export/import resource limits and boundary tests.

### Slice B — Mutation concurrency

1. W12-PER-002: persistence Apply lease/CAS parity.
2. W12-AUTH-001: local-identity concurrency and last-administrator invariant.

### Slice C — Availability and diagnostics

1. W12-AUTH-002: bounded login-attempt key lifecycle.
2. W12-API-001: request validation and sanitized deterministic errors.

### Slice D — Audit outage contract

Resolve W12-AUD-001 with an explicit fail-closed or durable-spool contract appropriate to protected mutations. The choice must preserve actor authority, ordering expectations and operational recovery; merely increasing a queue or suppressing the error is not a disposition.

## 5. Acceptance and exclusions

- Every fixed finding requires a focused regression test and exact-head EliteSCADA CI success.
- Failures are diagnosed before rerun; assertions, authorization and architecture boundaries are not weakened to obtain green status.
- Specialized workflows run when the actual diff affects their documented surfaces or when structural impact requires a manual integration gate.
- Wave 13 Authenticode/signing, Linux `.deb`, new Drivers/protocols, owner validation and physical L4 are outside this Wave 12 branch.
- Issue #201 remains open until all findings are closed or explicitly dispositioned, continuity documents are synchronized and the accepted post-merge SHA is recorded.
