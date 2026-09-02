# Wave 12 — Hardening Audit

**Status:** COMPLETE / ACCEPTED / CLOSED  
**Audit date:** 2026-09-01 BRT  
**Issue:** #201 — Wave 12 — Hardening  
**Implementation integration:** PR #203  
**Post-merge CI stabilization:** PR #204  
**Wave 12 start/base:** `a2d865c017b8b8ad804f9270e5224ac1fa620ed0`

This is the final Wave 12 finding/remediation ledger. All identified findings were fixed, regression-covered, integrated and accepted on `main`.

Final accepted Wave 12 product-code baseline:

`63bced02426fcb84b26028913f6c68feb3457d80`

Final post-merge evidence:

- EliteSCADA CI #1096 / `33576603185`: **SUCCESS**, including backend build/tests/runtime smoke, Web build and Chromium E2E;
- L3 Seven-Driver Lab #92 / `33576603158`: **SUCCESS**.

Pre-merge Wave 12 implementation checkpoint `29141feab168fa6e33d98b0f36cdd6e79f3811d8` passed EliteSCADA CI #1093, L3 #89, Preview Licensing #142 and Wave 11 Active HMI Runtime #40.

## 1. Accepted controls

- HMI Runtime resolves persisted Active Engineering and fails closed on project/revision/package inconsistency.
- Activation validates the candidate before publishing Active and preserves the previous Active on failure.
- `.escadapkg` import validates paths, duplicates, checksums/lengths, assets and aggregate resource limits; export obeys symmetric limits.
- Runtime View, Engineering Modify, TAG writes and administration remain separately authorized.
- Script execution uses bounded queues, cancellation/timeout boundaries and sanitized diagnostics.
- Core Engineering mutations use workspace serialization and compare-and-swap version checks.
- Local-identity logical mutations are serialized across complete read/validate/write, including PostgreSQL multi-process cooperation.
- Login limiter state has an expiry lifecycle without evicting active/unexpired lockout windows.
- Unsafe `/api` mutations require append-only durable audit admission before endpoint execution.
- No test, security or lifecycle boundary may be weakened merely to make CI green.

## 2. Final finding ledger

| ID | Severity | Surface | Disposition | Final state |
|---|---|---|---|---|
| W12-RT-001 | High | Realtime | Isolate slow/stalled realtime clients with bounded outbound work and deterministic eviction. | **FIXED / REGRESSION / VALIDATED** |
| W12-PER-001 | High | Persistence Save | Derive persisted payloads from one canonical snapshot and serialize snapshot/persist/AcceptSave. | **FIXED / REGRESSION / VALIDATED** |
| W12-ING-001 | High | Engineering ingress | Bound JSON/CSV request bodies with fast/streaming rejection, strict decoding and sanitized errors. | **FIXED / REGRESSION / VALIDATED** |
| W12-PKG-001 | High | `.escadapkg` | Enforce export resource limits symmetric with importer limits. | **FIXED / REGRESSION / VALIDATED** |
| W12-PER-002 | High | Persistence Apply | Serialize Apply with Working mutation lease and caller-observed version/CAS. | **FIXED / REGRESSION / VALIDATED** |
| W12-AUTH-001 | High | Local identities | Prevent concurrent lost updates and preserve last-enabled-administrator invariant. | **FIXED / REGRESSION / VALIDATED** |
| W12-AUTH-002 | Medium | Login limiting | Bound/expire unique remote-key entries without weakening lockout. | **FIXED / REGRESSION / VALIDATED** |
| W12-API-001 | Medium | Requests/diagnostics | Normalize validation/provider failures and sanitize deterministic diagnostics. | **FIXED / REGRESSION / VALIDATED** |
| W12-AUD-001 | High | Audit durability | Durable pre-mutation admission with fail-closed audit-store outage behavior. | **FIXED / REGRESSION / VALIDATED** |

## 3. Remediation summary

### W12-RT-001
Realtime delivery isolates clients with bounded per-client outbound work and deterministic stalled/overflow eviction. Session revocation preserves WebSocket `1008 Policy Violation`; an early 1006 regression was diagnosed as premature lifetime cancellation and corrected without weakening the assertion.

### W12-PER-001
Persistence Save derives one canonical Engineering snapshot and holds the workspace mutation lease through durable persistence and AcceptSave, preventing mixed or falsely-clean state.

### W12-ING-001
JSON/CSV preview/apply ingress uses explicit byte limits, Content-Length fast rejection, streaming enforcement, strict UTF-8 and sanitized deterministic errors.

### W12-PKG-001
Package export enforces Engineering-size, payload-count, manifest-size and aggregate uncompressed limits enforced by import, preventing self-generated packages the product would reject.

### W12-PER-002
Persistence Apply acquires the canonical Working mutation lease and requires caller-observed `x-elitescada-workspace-version`. Stale caller state returns conflict rather than replacing newer Working edits.

### W12-AUTH-001
Local-identity logical mutations hold a store-level mutation lease across read/validate/write. InMemory uses `SemaphoreSlim`; PostgreSQL uses a dedicated-session advisory transaction lock, preserving the last-enabled-administrator invariant across cooperating EliteSCADA processes.

### W12-AUTH-002
`LocalLoginAttemptLimiter` reclaims expired remote-key windows without evicting active/unexpired windows. Same-key cleanup/rollover is serialized so concurrent requests cannot split one lockout window into independent counters.

### W12-API-001
Engineering Persistence validates request bounds/positive revisions at the HTTP boundary. Historical Query distinguishes typed public validation/cursor/authorization failures from provider/internal failures and sanitizes provider diagnostics.

### W12-AUD-001
Unsafe `/api` requests (`POST`, `PUT`, `PATCH`, `DELETE`) require an `api.mutation.admission` event to be durably appended before endpoint execution. If that append fails, middleware returns sanitized 503 and does not invoke the endpoint. Detailed post-action audit remains non-failing because a physical/runtime mutation may already have occurred and an artificial endpoint failure could induce an unsafe retry. The prior durable admission remains explicit evidence of the attempted mutation.

## 4. Integration acceptance history

Wave 12 implementation entered `main` through PR #203 at merge SHA `be710e630da63639af9a0fc63458f9bd92068746`.

The first post-merge EliteSCADA CI #1094 failed two existing Modbus loopback healthy-path writes at their identical configured 500 ms timeout. The cause was diagnosed before rerun: timeout begins after the transport serialization gate, no server delay was injected, and the tests did not define a 500 ms latency product requirement. Explicit timeout/reconnect/degraded tests remained green.

PR #204 changed only those two healthy-path test request timeouts from 500 ms to 2 s plus the matching diagnostics expectation. No production code or explicit failure assertion was changed.

Exact #204 head `8d9950f56cf4cac8d835f448df8f77dc6a780928` passed:

- EliteSCADA CI #1095 / `33576006577`: **SUCCESS**;
- L3 Seven-Driver Lab #91 / `33576006594`: **SUCCESS**.

PR #204 squash-merged to `main` as `63bced02426fcb84b26028913f6c68feb3457d80`, which then passed #1096 and L3 #92. Integration acceptance is therefore complete.

## 5. Handoff to Wave 13

Wave 13 is separate release-engineering work. Issue #205 and `docs/WAVE-13-WINDOWS-RELEASE-PREPARATION.md` define its prepared, not-started boundary.

No Wave 13 implementation branch was created during Wave 12 closure. The next Coordinator must create it from then-live `main` only after the mandatory live-state audit.

## 6. Continuing exclusions / gates

- Wave 13 signing does not authorize new Drivers/protocols or unrelated product features;
- Linux `.deb` remains specification-only until explicit Development Lead authorization;
- owner validation remains Wave 14 and feedback/corrections Wave 15;
- physical L4 remains outside the current accepted claim;
- commercial DNP3 inclusion remains gated on an appropriate Step Function commercial license or approved/revalidated dependency replacement;
- specialized workflows remain impact-based and never substitute for universal CI.
