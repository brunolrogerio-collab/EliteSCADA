# Wave 12 — Hardening Audit

**Status:** REMEDIATION COMPLETE — INTEGRATION ACCEPTANCE PENDING  
**Audit date:** 2026-09-01 BRT  
**Issue:** #201 — Wave 12 — Hardening  
**Branch:** `coordination/wave12-hardening`  
**PR:** #202  
**Branch base:** `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`

This is the active finding and remediation ledger. All identified findings have been fixed with focused regression evidence. Wave 12 itself closes only after PR integration and successful post-merge universal CI on `main`.

Latest validated Wave 12 product-code checkpoint:

`29141feab168fa6e33d98b0f36cdd6e79f3811d8`

Evidence:

- EliteSCADA CI #1093 / `33574192584`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #89 / `33574192610`: **SUCCESS**;
- Preview Licensing CI #142 / `33574192572`: **SUCCESS**;
- Wave 11 Active HMI Runtime #40 / `33574192580`: **SUCCESS**.

## 1. Existing controls that remain locked

- HMI Runtime resolves persisted Active Engineering and fails closed on project/revision/package inconsistency.
- Activation validates the candidate before publishing Active and preserves the previous Active on failure.
- `.escadapkg` import validates paths, duplicates, checksums/lengths, assets and aggregate resource limits.
- Runtime View, Engineering Modify, TAG writes and administration remain separately authorized.
- Script execution uses bounded queues, cancellation/timeout boundaries and sanitized diagnostics.
- Core Engineering mutations use workspace serialization and compare-and-swap version checks.
- Local-identity logical mutations are serialized across the complete read/validate/write sequence, including PostgreSQL multi-process cooperation.
- Login limiter state has an expiry lifecycle without evicting active/unexpired lockout windows.
- Unsafe `/api` mutations require append-only durable audit admission before endpoint execution.
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
| W12-AUTH-002 | Medium | Login limiting | Bound/expire unique remote-key entries without weakening lockout. | **FIXED / REGRESSION / VALIDATED** |
| W12-API-001 | Medium | Requests/diagnostics | Normalize request validation/provider failures and sanitize deterministic diagnostics. | **FIXED / REGRESSION / VALIDATED** |
| W12-AUD-001 | High | Audit durability | Select and implement an explicit product-safe audit-outage contract; silent protected-action audit loss is unacceptable. | **FIXED / REGRESSION / VALIDATED** |

## 3. Remediation details

### W12-RT-001

Realtime delivery isolates clients with bounded per-client outbound work and stalled/overflowing client eviction so one client cannot block the TAG event consumer. Session revocation still sends WebSocket `1008 Policy Violation` before receive cancellation/disposal. An early regression to browser 1006 was diagnosed as premature lifetime cancellation and corrected without weakening the assertion.

### W12-PER-001

Persistence Save derives one canonical Engineering snapshot and holds the workspace mutation lease through durable persistence and AcceptSave, preventing mixed or falsely-clean state.

### W12-ING-001

JSON/CSV preview/apply ingress uses explicit byte limits, Content-Length fast rejection, streaming enforcement, strict UTF-8 and sanitized deterministic errors.

### W12-PKG-001

Package export enforces the Engineering-size, payload-count, manifest-size and aggregate uncompressed limits enforced by import, preventing self-generated packages that EliteSCADA itself would reject.

### W12-PER-002

Persistence Apply acquires the canonical Working mutation lease and requires caller-observed `x-elitescada-workspace-version`. Stale caller state returns conflict rather than replacing newer Working edits. The first exact-head CI exposed one E2E caller missing the new header; the caller was updated rather than relaxing the contract.

### W12-AUTH-001

Local-identity logical mutations hold a store-level mutation lease across read/validate/write. InMemory uses `SemaphoreSlim`; PostgreSQL uses a dedicated-session advisory transaction lock, serializing cooperating EliteSCADA processes that share a database. Administration create/update/password-reset and bootstrap paths use the same mutation boundary where applicable, preserving the last-enabled-administrator invariant. Regressions cover lost-update serialization, concurrent invariant preservation and PostgreSQL cross-session locking.

### W12-AUTH-002

`LocalLoginAttemptLimiter` reclaims expired remote-key windows opportunistically without evicting active/unexpired windows. Same-key cleanup/rollover is serialized so concurrent requests cannot split one lockout window into independent counters. Regressions prove stale reclamation and active-limit preservation.

### W12-API-001

Engineering Persistence validates revision-list `limit` and positive revision identifiers at the HTTP boundary instead of allowing provider-specific argument exceptions to surface as inconsistent 500s. Historical Query now distinguishes typed public validation/cursor/authorization failures from provider/internal failures; provider details are sanitized and internal `ArgumentException` is not automatically misclassified as caller input.

Focused regressions cover persistence invalid limits/revisions, typed historical failure mapping, provider-failure classification and sanitized diagnostics. Validation also exposed stale test expectations and runner-sensitive timing/watchdog assumptions in Gateway and realtime tests. Those tests were made deterministic without weakening product assertions. Final API-001 checkpoint `77804167afb14e084086386522891705e07b7873` passed EliteSCADA CI #1091, L3 #87, Preview #140 and Wave 11 Runtime #38.

### W12-AUD-001

The buffered audit sink already rejected queue overflow explicitly, but `ApiAuditService.RecordAsync` caught the rejection and allowed protected actions to proceed, making silent protected-action audit loss possible.

The selected product-safe outage contract is **durable pre-mutation admission plus non-retry-inducing post-action audit**:

1. every unsafe `/api` request (`POST`, `PUT`, `PATCH`, `DELETE`) writes an `api.mutation.admission` event directly to the append-only `IAuditStore` before endpoint execution, bypassing the asynchronous buffer;
2. the admission records server-derived subject identity, HTTP method/path and trace correlation, but no request body/query secret material;
3. if the direct durable append fails, middleware returns sanitized **503 Service Unavailable** and does not invoke the endpoint;
4. detailed success/failure audit remains buffered after endpoint effects;
5. a detailed post-action audit failure is logged but does not turn a process mutation into a false endpoint failure, because a client retry could duplicate a physical/runtime command;
6. the prior durable admission event remains explicit evidence of an attempted/allowed mutation whose detailed outcome may require diagnosis.

Regressions prove that direct admission is persisted before endpoint execution even when the buffered sink rejects, and that store failure returns 503 without calling the endpoint or leaking lower-level exception text.

Commit `0905ce4313122dc266444a047abeb92c8a122572` introduced the contract. EliteSCADA CI #1092 failed only at test compilation because the new test omitted the `Scada.Api.Runtime` namespace; product code compiled successfully. Commit `29141feab168fa6e33d98b0f36cdd6e79f3811d8` corrected only that test namespace and then passed all exact-head gates listed above.

## 4. Integration acceptance

Remediation is complete, but issue #201 stays open until all integration acceptance steps are true:

1. documentation, PR #202 and issue #201 are synchronized to `29141fe...` evidence;
2. live `main` and exact PR head are revalidated immediately before merge;
3. PR #202 is merged using exact-head protection;
4. the resulting `main` merge SHA passes the universal EliteSCADA CI;
5. continuity documents are updated to the accepted `main` baseline and issue #201 is closed completed.

## 5. Exclusions / gates

- Wave 13 signing, Linux `.deb`, new Drivers/protocols, owner validation and physical L4 remain outside this branch;
- DNP3 commercial inclusion remains gated on an appropriate Step Function commercial license or an approved/revalidated dependency replacement;
- specialized workflows are impact-based and do not substitute for universal CI;
- failures are diagnosed before rerun.
