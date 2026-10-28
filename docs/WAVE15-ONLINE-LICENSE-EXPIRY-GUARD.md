# Online expiry guard and transition audit (#301)

Implementation checkpoint based on `7d39e4dff583849e50fa30fdb02d6c6883f95c38`, containing verified lifecycle #800 and coordination #801. [Design before coding](https://github.com/brunolrogerio-collab/EliteSCADA/issues/301#issuecomment-6458396573). Branch `work/w15-online-license-expiry-guard-20261027`. Exact candidate/merge/T1/T2 disposition is recorded in the owning #301/#305 ledger and the product reference in #756. Local results below do not imply an integrated CI pass.

## Safety independent from HTTP

`OnlineLicenseExpiryHostedService` is registered in production DI. With an absent/ordinary offline selection it performs no mutation, checkpoint or Authority query. With an online selection it uses `FileProductLicenseService` and the existing `ProductLicenseLifecycleCoordinator`. It does not resolve the device client, journal, password or refresh credential, and cannot wait on an HTTP operation lease. Network renewal can therefore be added without owning the expiry timer.

The maximum recheck interval is two seconds, shortened to the remaining signed lifetime near the deadline. This is normal hosted scheduling, not a hard real-time latency guarantee. The source already denies commercial entitlement at its verified deadline; the guard applies the coordinated transition. Every minute it persists the observed online time floor through the existing CAS checkpoint, without changing capabilities or epoch. No second timer budget, credential store, SQL advisory lock or policy is introduced.

Authentic expiry uses pending → selected Demo artifact → epoch → local re-evaluation → old-lease fencing → audit → Complete. A real package of200 definitions may continue without a Demo timer;201 stops the whole Run. Invalid signatures/descriptors, premature Demo markers and restart clock rollback stay Invalid and invoke a canonical invalidation transition. They cannot manufacture Demo. Intelligence permits are cancelled when the canonical feature is absent; a valid renewal that retains Intelligence does not clear the user's activation.

A shared-authority failure cannot be repaired locally. When commercial evidence is already invalid, the guard still invokes the existing local entitlement re-evaluator to stop that Run. Once the shared store returns, canonical expiry can complete; a Run stopped for this failure is not silently restarted. This is conservative local denial, not a claim of new distributed failover behavior or a replacement for canonical lease validation.

## Recovery ownership

A pending transition may still be executing in another caller/process. The live guard never assumes ownership solely from `TransitionPending`. The lifecycle tracks only online transitions whose post-publication safety sequence threw in this instance. `ReconcileFailedOnlineAsync` recovers only a matching tracked transition, through the existing `ReconcilePendingAsync`. An unrelated active transition remains untouched.

After process restart, the already established startup reconciliation remains the owner before serving requests. Missing transition kind now joins the existing incomplete-metadata rejection. Invalidation recovery retains its evidence identity so one audit outage does not advance the epoch twice for unchanged invalid evidence. SQL/file CAS remains authoritative for concurrent selection changes.

## Semantic audit and failure

The canonical `IAuditStore` is required for online lifecycle operations. Production DI provides it; legacy/offline unit constructors can remain without it. Recovery that runs before ordinary foundation initialization calls that same store's idempotent initializer rather than moving the frozen startup sequence or creating another audit path.

After local reduction and old-lease fencing, `product-license.online-transition` records the stable transition/correlation ID, operation, evidence source, target/current and available previous verification state, activation when readable, epochs and recovery flag. The host actor has no Authority roles and obtains no interactive permissions. No envelope, refresh credential, login or private key enters the record. A missing historical value is explicitly unavailable rather than inferred.

Complete waits for audit persistence. Failure leaves the existing pending marker closed. A response lost after the audit write can produce another audit event on recovery with a different event ID and the same stable operation correlation: at-least-once recording, not exactly-once delivery. No duplicate-ID exception can strand recovery. The guard reports bounded error codes/types only and suppresses unchanged warning repeats.

## Evidence and remaining work

Local Windows SDK10.0.403: focused Drivers147/147, zero skips; Release builds clean and diff-check passed. Cases execute the actual hosted loop with controlled time, real signed leases/canonical files and real Runtime/compiler for200/201TAGs. They cover clock checkpoints/reopen, old-session fencing, malformed/early Demo evidence, audit-before/after-write failures, startup and owned recovery, another caller's pending transition, local denial on shared-store failure, cancellation on Intelligence loss and preservation on unchanged licensed renewal. A real device client request blocked in a fake HTTP handler holds the encrypted journal operation lease while independent expiry completes.

These are component/simulated-failure checks, not Portal/TLS, forced power-loss/process-kill or online HA deployment evidence. Exact CI outcomes and any residual HA occurrence remain in the live ledger. Frozen HA/failover/replication code, existing tests, workflow timeouts/retries/filters and owner Preview/Portal/DEMO are unchanged by this scope.

Next: authorized journal→lifecycle device orchestrator, durable bounded maintenance delegation,48-hour renewal/retry and encrypted-snapshot retention. Then confirmed Portal device metadata/selection, status/usage/API/UI and Windows/Linux/Docker/Portal deployment validation. Online licensing remains incomplete; issuance stays disabled. Installed Help/manual online review markers remain pending, and Codex/EEE RHO handoff follows the completed cycle with the separate application artifact/schema/hash verified.

Review closure: unavailable abort is tracked as an owned interrupted online operation, preventing a pending marker from waiting for a restart indefinitely. Canonical verification, mode detection and selected-artifact reads use the same whitespace normalization. The live guard invalidates a damaged header after observing an online selection; it still leaves ordinary offline installations alone. These cases are included in the147 local checks.
