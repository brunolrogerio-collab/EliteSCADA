using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Security;
using Scada.Engineering.Contracts;
using Scada.Security.Authentication;

namespace Scada.Api.Licensing;

/// <summary>Recovers only an exact durable activation after host and HA readiness.</summary>
public sealed class AssetProcessIntelligenceStartupRecovery(
    AssetProcessIntelligenceActivationStore store,
    AssetProcessIntelligenceExecutionGate gate,
    IAuthorityLifecycleStore lifecycle,
    AuthorityPolicyHaCoordinator authority,
    EngineeringWorkspace workspace,
    ScadaRuntimeFacade runtime,
    IProductLicenseService licensing,
    RuntimeHighAvailabilityService highAvailability)
{
    /// <summary>
    /// Returns null while startup prerequisites are still converging. A non-null
    /// result means the startup decision is complete, including the case where
    /// no exact receipt exists and no scope was recovered.
    /// </summary>
    public async Task<int?> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var recovered = 0;
        try
        {
            if (!licensing.CurrentVerification.HasAssetProcessIntelligence)
            {
                gate.CompleteStartupRecovery();
                return 0;
            }

            // HA readiness becomes observable only after the hosted protection
            // and Authority synchronization services have started. Do not spend
            // the one startup-recovery opportunity while that fence is pending.
            if (!highAvailability.CanOwnIndustrialEffects())
            {
                gate.InvalidateForAuthorityReadinessFailure();
                return null;
            }
            var descriptor = workspace.Describe();
            var live = runtime.Describe();
            if (string.IsNullOrWhiteSpace(descriptor.ProjectKey) || !descriptor.BaseRevision.HasValue ||
                !string.Equals(descriptor.ProjectKey, live.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
                descriptor.BaseRevision != live.Revision)
            {
                gate.CompleteStartupRecovery();
                return 0;
            }
            var fence = await authority.CheckAsync(cancellationToken);
            if (!fence.Allowed)
            {
                gate.InvalidateForAuthorityReadinessFailure();
                return null;
            }
            var lifecycleState = await lifecycle.GetAsync(cancellationToken);
            if (fence.PolicyDigest is null || !lifecycleState.AllowsAuthorization)
            {
                gate.CompleteStartupRecovery();
                return 0;
            }
            await using var mutation = await workspace.AcquireMutationAsync(cancellationToken: cancellationToken);
            var currentDescriptor = workspace.Describe();
            var currentRuntime = runtime.Describe();
            if (!string.Equals(descriptor.ProjectKey, currentDescriptor.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
                descriptor.BaseRevision != currentDescriptor.BaseRevision ||
                !string.Equals(descriptor.ProjectKey, currentRuntime.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
                descriptor.BaseRevision != currentRuntime.Revision)
            {
                gate.CompleteStartupRecovery();
                return 0;
            }
            foreach (var scope in workspace.AssetProcessIntelligence.Snapshot().Scopes ?? [])
            {
                if (!scope.Active) continue;
                var evidence = await store.ReadAsync(descriptor.ProjectKey, scope.Kind, scope.ScopeId, cancellationToken);
                if (evidence is null) continue;
                var accepted = await store.TryRecoverAsync(evidence, receipt => gate.TryRecoverAtStartup(
                    receipt, lifecycleState, descriptor.ProjectKey, descriptor.BaseRevision.Value,
                    scope.Kind, scope.ScopeId, fence.PolicyVersion, fence.PolicyDigest, fence.HaEpoch,
                    evidence.Generation), cancellationToken);
                if (accepted) recovered++;
            }
            var finalFence = await authority.CheckAsync(cancellationToken);
            var finalLifecycle = await lifecycle.GetAsync(cancellationToken);
            if (!finalFence.Allowed || finalFence.PolicyVersion != fence.PolicyVersion ||
                finalFence.PolicyDigest != fence.PolicyDigest || finalFence.HaEpoch != fence.HaEpoch ||
                finalLifecycle != lifecycleState || !licensing.CurrentVerification.HasAssetProcessIntelligence ||
                !highAvailability.CanOwnIndustrialEffects())
            {
                gate.InvalidateAll();
                return 0;
            }
            gate.CompleteStartupRecovery();
            return recovered;
        }
        catch
        {
            // If anything failed after at least one local permit was rebuilt,
            // immediately cancel it and durably revoke the owned receipts. A
            // database/startup error before any permit can be retried while all
            // Intelligence execution remains closed.
            if (gate.ActivePermitCount > 0)
                gate.InvalidateAll();
            throw;
        }
    }
}
