# ADR-029: Recover Intelligence execution after a normal restart

Status: owner-authorized correction in #871; implementation in progress.
Base: 6a51581b30dbfa3d6565acb76fa75a1d452c2d69.

## Problem
Configured Active is durable Engineering state, while AssetProcessIntelligenceExecutionGate stores only local cancellation tokens. Restart therefore loses the execution permit. The owner superseded the unconditional restart prohibition from #607/#608. Revocation and uncertain Authority state must still close execution.

## Decision
Persist a recoverable activation receipt in Authority-owned operational storage, outside Engineering and .escadapkg. A receipt identifies Authority lifecycle epoch, project, scope, revision, policy version and digest, HA topology/fence, and a monotonic receipt generation. The existing durable activation audit event records the actor, project, revision and scope; do not duplicate user identity inside the recovery receipt. A cancellation token remains local and is never serialized.

Activation writes the receipt under the canonical Authority transaction/advisory-lock discipline and durable-write admission, with the policy/fence rechecked at commit. Only a successful durable write may arm the local gate. Pause first cancels local work, then durably closes the receipt; the response cannot report success until closure is durable. Concurrent recovery must compare the receipt generation and cannot overwrite a later pause.

Recovery is attempted after workspace/runtime restoration and only when canonical Authority/license/HA readiness is observable. A temporary startup readiness miss must not consume the one recovery opportunity or erase a durable receipt; the host retries its readiness check while execution remains closed. Once a node had an armed permit, loss of verifiable Authority/HA remains a durable invalidation and reconnect does not rearm. Recovery is not a fallback on every missing permit. Recover only an exact receipt match; never replace its policy version with the latest version. Recheck the receipt generation and canonical fence before exposing the local token. Existing query permission, license, effective-active HA ownership, per-page cancellation and final result checks remain required.

Policy advances, revocation/restore, Authority switch/detach, revision mismatch, ambiguous records and HA fence mismatch do not recover. A runtime loss of verifiable Authority closes recovery eligibility durably; reconnect does not rearm. Failed durable invalidation prevents readiness and requires explicit activation. Existing receipts must not be inferred from configured Active or legacy audit entries: installations upgraded without a receipt require one explicit activation.

For shared databases, all nodes read the same receipt and serialize closure/recovery; query pages and the final result recheck the durable generation. With isolated databases, each API can recover only from the receipt in its own configured Authority database. A same-node normal restart may recover while the exact HA fence is unchanged. Promotion or failback to another node changes the Authority/HA fence and is not treated as a normal restart: the old activation must not be copied or rewritten into the new epoch, and the new active node remains blocked until an explicit activation. Any future peer receipt replication must use the existing authenticated Authority HA fence and acknowledgement discipline.

## Required implementation touchpoints
- AssetProcessIntelligenceApi: explicit activation and durable pause transitions, recovery status.
- AssetProcessIntelligenceExecutionGate: local tokens and exact persisted receipt generation binding.
- AuthorityPolicyHaCoordinator: durable invalidation and startup recovery boundaries.
- AssetProcessIntelligenceAnalyticsService: preserve all existing use/page/final checks.
- PostgreSqlAuthorityPolicyStore and shared schema initialization: reuse lock/write-admission discipline; do not change policy ownership.
- AssetProcessIntelligenceWorkspace and Help/manual: explain automatic normal recovery and exceptional blocks.

## Acceptance evidence
Use real API processes and disposable PostgreSQL databases. Cover restart without another click; durable pause; revoke/restore while offline; Authority switch; revision changes; partition/reconnect; HA shared/isolated topology; concurrent pause/recovery; crash before/after receipt commit; invalidation before result return. Demonstrate that a recovered token cannot survive a later receipt generation or canonical fence change. Run focused local tests before CI and deploy the accepted merge to the preserved-data EEE preview.
