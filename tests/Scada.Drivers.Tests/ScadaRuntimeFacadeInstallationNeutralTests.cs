using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ScadaRuntimeFacadeInstallationNeutralTests
{
    [Fact]
    public async Task NeutralFenceHidesOperationalMetadata()
    {
        await using var runtime = new OperationalRuntimeFixture();
        var facade = new ScadaRuntimeFacade(
            runtime,
            installationFence: new FixedInstallationFence());

        Assert.Equal("neutral", facade.Describe().Mode);
        Assert.Empty(facade.Tags());
        Assert.Empty(facade.Drivers());
        Assert.Empty(facade.OperationalEventDefinitions());
        Assert.Empty(facade.ClientMemorySources());
        Assert.False(facade.TryGetOperationalEvent(OperationalRuntimeFixture.EventId, out _));
        Assert.False(facade.IsServerMemoryTag(Guid.NewGuid()));
    }

    [Fact]
    public async Task MissingActiveProjectIsNeutralAndCannotExposeOrOperateAnUnownedRuntime()
    {
        var eventBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            eventBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(1));
        var facade = new ScadaRuntimeFacade(runtime);

        var descriptor = facade.Describe();
        Assert.Equal("neutral", descriptor.Mode);
        Assert.Null(descriptor.ProjectKey);
        Assert.Null(descriptor.Revision);
        Assert.Empty(facade.Tags());
        Assert.Empty(facade.CurrentValues());
        Assert.Empty(facade.AlarmDefinitions());
        Assert.Empty(facade.Alarms());
        Assert.Empty(facade.Commands());
        Assert.Empty(facade.Drivers());
        Assert.False(facade.TryGetTag(Guid.NewGuid(), out _));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => facade.WriteAsync(Guid.NewGuid(), 1d).AsTask());
        Assert.Contains("No Active Engineering Runtime", error.Message);
    }

    private sealed class FixedInstallationFence : IInstallationRuntimeFence
    {
        public bool ProcessEffectsFenced => true;
        public Task FenceProcessEffectsForInstallationDetachAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<InstallationRuntimeFenceResult> StopFencedRuntimeForInstallationDetachAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new InstallationRuntimeFenceResult(null, null, RuntimeStopped: false));
    }

    private sealed class OperationalRuntimeFixture : IEngineeringRuntimeCoordinator, IOperationalEventRuntime
    {
        public static readonly Guid EventId = Guid.Parse("7c000000-0000-0000-0000-000000000001");
        private static readonly OperationalEventDefinition Event = new(
            EventId,
            "plant.previous.event",
            "Previous event",
            "process",
            "test",
            "previous-project");

        public RuntimeDescriptor Describe() => new("previous-project", 7, DateTimeOffset.UtcNow, [], [], 0, 0);
        public IReadOnlyCollection<TagDefinition> Tags() => [];
        public IReadOnlyCollection<TagValue> CurrentValues() => [];
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => [];
        public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => [];
        public IReadOnlyCollection<CommandDefinition> Commands() => [];
        public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => [];
        public bool TryGetTag(Guid tagId, out TagDefinition? tag) { tag = null; return false; }
        public bool TryGetTagByPath(string path, out TagDefinition? tag) { tag = null; return false; }
        public bool TryGetCurrent(Guid tagId, out TagValue? value) { value = null; return false; }
        public bool TryGetCommand(Guid commandId, out CommandDefinition? command) { command = null; return false; }
        public bool IsServerMemoryTag(Guid tagId) => true;
        public ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IReadOnlyCollection<OperationalEventDefinition> OperationalEventDefinitions() => [Event];
        public bool TryGetOperationalEvent(Guid definitionId, out OperationalEventDefinition? definition)
        {
            definition = definitionId == EventId ? Event : null;
            return definition is not null;
        }
        public ValueTask<OperationalEventOccurred> EmitOperationalEventAsync(Guid definitionId, OperationalEventEmissionContext? context = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(OperationalEventContract.CreateOccurrence(Event, context));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
