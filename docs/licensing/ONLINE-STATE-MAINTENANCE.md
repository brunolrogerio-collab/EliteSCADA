# Online state maintenance (#301)

Bounded slice `work/w15-online-license-maintenance-20261027`, based on verified integration `8de815d724a02cc5f0fa9ad1b867156d3fd9bf41`. [Design/activation](https://github.com/brunolrogerio-collab/EliteSCADA/issues/301#issuecomment-6449628468). PR #798 integrated at `67a290ff208b7aa4a9029fd71f428e93c4b9d895`. Exact-head T1 40556425486 and exact-merge T2 40557263409 passed on first attempts. T2 backend2900/1skip, Runtime smoke, Web and Chromium13/13. [Evidence](https://github.com/brunolrogerio-collab/EliteSCADA/issues/301#issuecomment-6449810628). The journal/client still have no DI registration, public API, worker, Runtime entitlement or UI integration. Issuance/signing remains disabled.

## Scoped ciphertext retention

`FileOnlineLicenseStateStore.CollectObsoleteSnapshotsAsync` requires administration and holds the existing operation and journal file leases. It loads the committed state first; missing/unreadable state cannot authorize cleanup. The optional `IProtectedMaterialRetentionAuthority` maintenance contract is implemented by the canonical host authority. It authenticates the retained reference/key/scope before removing any obsolete ciphertext and returns only a count, with no catalogue or plaintext read-back.

The owning consumer must hold exclusive cross-process write ownership of that scope throughout collection. Configure one journal per machine and protected-material scope; independent journals must not share that scope. The collector removes at most 128 obsolete snapshots per call, including same-scope orphans from failed pointer publication. Another machine/owner/resource/purpose is preserved. Each removal uses the existing decrypt/scope check and protected-material deletion audit. Unknown/malformed files are preserved; matching but unauthentic ciphertext fails cleanup. An interrupted pass can be repeated without changing the selected pointer.

This is conservative maintenance, not secure erasure or a repair tool. No automatic schedule is wired yet; the future authorized worker must call/retry retention. A first-attempt orphan without a committed readable journal stays until a valid state is established: it cannot be swept without an authenticated anchor. Wrong-key/corrupt material may require separate protected operational recovery rather than blind deletion.

## Explicit local reset

`ResetAsync(expectedActivationId)` requires administration and compare-and-set against the current activation. It serializes with the complete HTTP operation and journal publication using existing leases. The encrypted tombstone drops the current refresh credential, pending request and lease while retaining the last-observed UTC watermark. Resetting that same activation is idempotent; a stale activation ID or late response cannot restore its old state. Subsequent activation still refuses clock rollback and creates a new ID only after this explicit reset.

Reset does not revoke a Portal activation or free a remote slot; those are separate Portal operations. Previous encrypted versions disappear through retention. Before exposing reset in the product, lifecycle integration must fence/re-evaluate online Runtime entitlements and add the semantic operation audit. This slice has protected-material mutation/deletion audits, not a complete user-facing reset audit or remote revocation transaction.

Snapshot schema 2 adds reset and reauthentication state. Schema 1 snapshots remain readable and migrate on the next successful mutation. The opaque pointer schema stays 1. Older binaries reject the new state instead of ignoring its semantics.

## New login after renewal rejection

An HTTP 401/403 device response durably marks `ReauthenticationRequired` before returning that stable error. The previous intent/credential remains available for recovery, but `PrepareRenewalAsync` refuses further automatic sends. Failure to persist rejection fails the operation; it never reports successful renewal. A later attempt may see the same rejection again.

Explicit `ReauthenticateAsync` requires a fresh in-memory password and the same confirmed license/organization/application. It preserves ActivationId and uses the existing activation endpoint. Portal #778 accepts a new login only when that activation requires reauthentication; active/revoked/limit conflicts remain authoritative. A rejected response never silently generates another activation to bypass those rules.

The new request ID and credential are committed before HTTP. Username/selection stay the same when replaying that intent. Password is never stored. The previous signed lease and credential remain until verified completion replaces them; the reauthentication flag grants no Runtime authority. Response loss/reopen resends the exact login intent. Successful completion clears the flag. This component does not change Authority identities or offline licensing.

## Evidence and remaining work

Local Release build: zero warnings/errors. Focused Drivers 122/122 and Security 42/42 passed, including 36 device-protocol cases and 7 retention cases. Real encrypted-file tests cover batching, foreign-scope survival, missing/wrong-key/wrong-scope anchors, authorization, tampered obsolete ciphertext, orphan cleanup, reset CAS/failure/clock fence, contention with HTTP, durable rejection and login replay after response loss. HTTP and signature evidence are fictitious; the real verifier remains separately tested in Core. Reopening objects is not a forced process-kill/power-loss test or real Portal/TLS validation.

Next: review/CI/integrate this slice; canonical lifecycle/epoch and expiry-to-Demo without timer; authorized worker with continuous/persisted time and retention scheduling; status/usage API/UI; integrated Windows/Linux/Docker/Portal validation and semantic auditing. Portal metadata confirmation gap remains under #741. Help/manual follows exposed behavior; the existing future-review marker remains until the feature works. Preserve owner Preview/DEMO and Portal lane. Deliver product and independently verified EEE RHO package to Codex only after licensing is complete.
