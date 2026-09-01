# Wave 12 — Hardening Audit

**Status:** ACTIVE — REMEDIATION IN PROGRESS  
**Audit date:** 2026-09-01 BRT  
**Issue:** #201 — Wave 12 — Hardening  
**Branch:** `coordination/wave12-hardening`  
**Draft PR:** #202  
**Branch base:** `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`

This is the active finding and remediation ledger. A finding closes only when its root cause is fixed with focused regression evidence and exact-SHA validation, or an explicit residual-risk disposition is recorded.

Latest validated Wave 12 product-code checkpoint:

`b737fb88cc58f53000e4f859e127473de10f4a51`

Evidence:

- EliteSCADA CI #1078 / `33566810710`: **SUCCESS**;
- L3 Seven-Driver Lab #74 / `33566810666`: **SUCCESS**;
- Preview Licensing CI #127 / `33566810696`: **SUCCESS**;
- Wave 11 Active HMI Runtime #25 / `33566810758`: **SUCCESS**.

## 1. Existing controls that remain locked

- HMI Runtime resolves persisted Active Engineering and fails closed on project/revision/package inconsistency.
- Activation validates the candidate before publishing Active and preserves the previous Active on failure.
- `.escadapkg` import validates paths, duplicates, checksums/lengths, assets and aggregate resource limits.
- Runtime View, Engineering Modify, TAG writes and administration remain separately authorized.
- Script execution uses bounded queues, cancellation/timeout boundaries and sanitized diagnostics.
- Core Engineering mutations use workspace serialization and compare-and-swap version checks.
- Local-identity logical mutations are serialized across the complete read/validate/write sequence, including PostgreSQL multi-process cooperation.
- No test, security or lifecycle boundary may be weakened merely to make CI green.

## 2. Findings

| ID | Severity | Surface | Required disposition | State |
|---|---|---|---|---|
| W12-RT-001 | High | Realtime | Isolate slow/stalled realtime clients with bounded outbound work and deterministic eviction. | **FIXED / REGRESSION / VALIDATED** |
| W12-PER-001 | High | Persistence Save | Derive persisted payloads from one canonical snapshot and serialize snapshot/persist/AcceptSave. | **FIXED / REGRESSION / VALIDATED** |
| W12-ING-001 | High | Engineering ingress | Bound JSON/CSV request bodies with fast/streaming rejection, strict decoding and sanitized client errors. | **FIXED / REGRESSION / VALIDATED** |
| W12-PKG-001 | High | `.escadapkg` | Enforce export resource limits symmetric with the importer. | **FIXED / REGRESSION / VALIDATED** |
| W12-PER-002 | High | Persistence Apply | Serialize Apply with the Working mutation lease and require caller-observed version/CAS. | **FIXED / REGRESSION / VALIDATED** |
| W12-AUTH-001 | High | Local identities | Prevent concurrent lost updates and preserve the last-enabled-administrator invariant across the entire logical mutation. | **FIXED / REGRESSION / VALIDATED** |
| W12-AUTH-002 | Medium | Login limiting | Bound/expire unique remote-key entries without weakening lockout. | **ACTIVE NEXT** |
| W12-AUD-001 | High | Audit durability | Select and implement an explicit product-safe audit-outage contract; silent protected-action audit loss is unacceptable. | Pending design disposition |
| W12-API-001 | Medium | Requests/diagnostics | Normalize request validation/provider failures and sanitize deterministic diagnostics. | Pending |

## 3. Closed remediation details

### W12-RT-001

Realtime delivery now isolates clients with bounded per-client outbound work and stalled/overflowing client eviction so one client cannot block the TAG event consumer or later clients.

Validation uncovered a regression in session-revocation close semantics: premature lifetime cancellation produced browser close 1006 rather than required 1008 Policy Violation. The root cause was fixed without weakening the E2E assertion. Commit `25444267e20b668a22191a662d6eeb4bef4b88d5` was green in EliteSCADA CI #1071, L3 #67, Preview Licensing #120 and Wave 11 Runtime #18.

### W12-PER-001

Persistence Save uses one canonical Engineering snapshot and holds the workspace mutation lease through durable persistence and AcceptSave, preventing mixed/falsely-clean persisted state.

### W12-ING-001

JSON/CSV preview/apply ingress uses explicit byte limits, Content-Length fast rejection, streaming enforcement, strict UTF-8 and sanitized deterministic errors.

### W12-PKG-001

Package export enforces the Engineering-size, payload-count, manifest-size and aggregate uncompressed limits that import enforces, preventing self-generated packages that the product itself rejects.

### W12-PER-002

Persistence Apply now acquires the canonical Working mutation lease and requires `x-elitescada-workspace-version`. Stale caller state returns conflict rather than silently replacing newer Working edits.

First exact-head CI at `329083a9f3273907306f4a17a99f527b382a303a` (#1074) deterministically exposed one E2E caller that did not yet provide the newly required header. The test was corrected, not weakened, at `012d15554d96af8600953a793cd58f0a5fc11c4d` by reading post-checkout `changeVersion`; #1075 and the associated specialized gates all passed.

### W12-AUTH-001

Local-identity logical mutations now hold a store-level mutation lease across the entire read/validate/write sequence. The InMemory store serializes with `SemaphoreSlim`; PostgreSQL uses a dedicated-session advisory lock so cooperating EliteSCADA processes sharing the database cannot interleave protected mutations. User create/update/password-reset and bootstrap paths respect the same mutation boundary, including the last-enabled-administrator invariant.

Focused regressions cover stale-snapshot lost updates, concurrent administrator-invariant preservation and PostgreSQL cross-session advisory-lock behavior. An initial test build exposed xUnit2031 syntax and was corrected without changing semantics. A later universal run exposed an unrelated 100 ms Modbus recovery-test timing margin; the test retained the same failure/recovery assertions while moving the healthy timeout/fault delay to 250/750 ms. Exact-head checkpoint `b737fb88cc58f53000e4f859e127473de10f4a51` then passed EliteSCADA CI #1078, L3 #74, Preview Licensing #127 and Wave 11 Runtime #25.

## 4. Active Slice C — W12-AUTH-002 / W12-API-001

### W12-AUTH-002 confirmed defect

`LocalLoginAttemptLimiter` keeps one `AttemptWindow` per observed remote key in a process-lifetime `ConcurrentDictionary`. Window counters reset after the configured rate window, but expired key entries are never removed, so high-cardinality remote traffic leaves retained state indefinitely.

Required implementation:

1. give expired remote-key windows a bounded lifecycle;
2. never evict an unexpired/actively limited window merely to satisfy a size cap, because that would weaken lockout;
3. make cleanup concurrency-safe so removal cannot split one key across two counters;
4. avoid a full-table scan on every login attempt;
5. add deterministic regressions proving stale keys are reclaimed while an active limited key remains limited;
6. obtain exact-head EliteSCADA CI success before marking the finding fixed.

### W12-API-001

After AUTH-002 is validated, normalize invalid request ranges/limits and provider-dependent failures into deterministic sanitized client responses, with focused regression coverage.

## 5. Remaining Slice D — audit outage contract

Resolve W12-AUD-001 with an explicit fail-closed or durable-spool contract appropriate to protected mutations. Merely enlarging a queue or swallowing errors is not a disposition.

## 6. Acceptance / exclusions

- issue #201 stays open until every finding is fixed or explicitly dispositioned;
- PR #202 remains draft/unmerged until Wave 12 acceptance;
- exact-SHA EliteSCADA CI is mandatory for every material product-code checkpoint;
- specialized workflows are impact-based and do not replace the universal gate;
- failures are diagnosed before rerun;
- Wave 13 signing, Linux `.deb`, new Drivers/protocols, owner validation and physical L4 are outside this branch;
- DNP3 commercial inclusion remains gated on an appropriate commercial license or approved/revalidated dependency replacement.
