using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Product.Licensing;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Security;
using Scada.Engineering.Contracts;
using Scada.Security.Authentication;

namespace Scada.Api.Licensing;

/// <summary>One-shot recovery. Configuration alone never arms execution.</summary>
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
    public async Task<int> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var recovered = 0;
        try
        {
            // Peer receipt replication is not implemented yet: retain explicit activation in HA.
            if (highAvailability.Enabled) return 0;
            if (!licensing.CurrentVerification.HasAssetProcessIntelligence ||
                !highAvailability.CanOwnIndustrialEffects()) return 0;
            var descriptor = workspace.Describe();
            var live = runtime.Describe();
            if (string.IsNullOrWhiteSpace(descriptor.ProjectKey) || !descriptor.BaseRevision.HasValue ||
                !string.Equals(descriptor.ProjectKey, live.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
                descriptor.BaseRevision != live.Revision) return 0;
            var fence = await authority.CheckAsync(cancellationToken);
            var lifecycleState = await lifecycle.GetAsync(cancellationToken);
            if (!fence.Allowed || fence.PolicyDigest is null || !lifecycleState.AllowsAuthorization) return 0;
            await using var mutation = await workspace.AcquireMutationAsync(cancellationToken: cancellationToken);
            var currentDescriptor = workspace.Describe();
            var currentRuntime = runtime.Describe();
            if (!string.Equals(descriptor.ProjectKey, currentDescriptor.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
                descriptor.BaseRevision != currentDescriptor.BaseRevision ||
                !string.Equals(descriptor.ProjectKey, currentRuntime.ProjectKey, StringComparison.OrdinalIgnoreCase) ||
                descriptor.BaseRevision != currentRuntime.Revision) return 0;
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
            return recovered;
        }
        catch
        {
            gate.InvalidateAll();
            throw;
        }
        finally
        {
            gate.CompleteStartupRecovery();
        }
    }
}
