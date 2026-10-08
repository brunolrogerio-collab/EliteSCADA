using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Interactions;
using Scada.Core.InternalMemory;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Interactions;
using Scada.Engineering.Security;
using Scada.Engineering.Scripts;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class ServerScriptRichCommandBridgeIntegrationTests
{
    [Fact]
    public async Task ActiveServerScript_InvokesOnlyDeclaredCommandAndReceivesHostAuditedResult()
    {
        const string projectKey = "server-script-command-test";
        const long revision = 7;
        var scriptId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var commandDataSourceId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var package = Package(scriptId, commandId, stateId, commandDataSourceId);
        var interactions = new ActiveDriverInteractionRuntimeCatalog();
        var prepared = interactions.Prepare(package);
        var eventBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            eventBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2),
            new InMemoryServerMemoryRetentionStore());
        var configuration = new ConfigurationManager();
        var manager = ServerScriptRuntimeManager.GetShared(runtime, eventBus, configuration, interactions);
        var roleKey = ServerScriptCommandIdentity.RoleKey(projectKey, scriptId);
        var policyStore = new InMemoryAuthorityPolicyStore(
            [new SecurityRoleEngineeringDto(
                Guid.NewGuid(),
                roleKey,
                "Server Script Command Test",
                Grants:
                [new CapabilityGrantEngineeringDto(
                    SecurityCapability.CommandExecute,
                    new AuthorizationScopeEngineeringDto(ScopeNodeId: scopeId))])],
            [new SecurityScopeEngineeringDto(
                scopeId,
                "script-command-test",
                "Script Command Test",
                SecurityScopeNodeKind.Command,
                ResourceId: commandId)]);
        var authorization = new ApiAuthorizationService(
            new NullServiceProvider(),
            policyStore,
            configuration);
        var auditStore = new InMemoryAuditSink();
        var audit = new ApiAuditService(auditStore, auditStore, NullLogger<ApiAuditService>.Instance);
        var commandRuntime = new CapturingRichCommandRuntime();
        ServerScriptRichCommandBridge.Bind(
            manager,
            interactions,
            commandRuntime,
            authorization,
            audit);

        var activated = await manager.ActivateRuntimeAsync(
            projectKey,
            revision,
            package,
            (_, _) =>
            {
                interactions.Commit(prepared);
                return Task.CompletedTask;
            });

        Assert.True(activated.Activated, JsonSerializer.Serialize(activated));
        await WaitUntilAsync(
            () => runtime.TryGetCurrent(stateId, out var state) && Convert.ToInt32(state!.Value) == 1,
            TimeSpan.FromSeconds(5),
            () => manager.Snapshot().Scripts.Single().Diagnostics.LastSanitizedError);

        var invocation = Assert.IsType<RichCommandInvocation>(commandRuntime.Invocation);
        Assert.Equal(commandId, invocation.CommandId);
        var parameter = Assert.Single(invocation.Parameters);
        Assert.Equal("enabled", parameter.Key);
        Assert.Equal(InteractionScalarValue.Boolean(true), parameter.Value);
        Assert.Equal(InteractionOrigin.ServerScript, invocation.Causality?.Origin);
        Assert.Equal(invocation.InvocationId, invocation.Causality?.CorrelationId);

        var principal = ServerScriptCommandIdentity.CreatePrincipal(projectKey, revision, scriptId);
        var events = auditStore.Snapshot();
        Assert.Contains(events, item =>
            item.Action == AuditActions.ProtectedMutationAdmission &&
            item.Source == "server-script-admission" &&
            item.SubjectId == principal.SubjectId &&
            item.ProjectKey == projectKey &&
            item.Revision == revision &&
            item.TargetId == commandId.ToString("D"));
        Assert.Contains(events, item =>
            item.Action == AuditActions.CommandExecute &&
            item.Source == "server-script" &&
            item.SubjectId == principal.SubjectId &&
            item.ProjectKey == projectKey &&
            item.Revision == revision &&
            item.TargetId == commandId.ToString("D") &&
            item.Outcome == AuditOutcome.Succeeded);

        await manager.DisposeAsync();
    }

    private static EngineeringPackage Package(
        Guid scriptId,
        Guid commandId,
        Guid stateId,
        Guid commandDataSourceId)
    {
        var script = new ScriptEngineeringDefinition(
            scriptId,
            "Scripts.InvokeRichCommand",
            "Invoke Rich Command",
            ScriptEngineeringScope.Server,
            $$"""
def initialize(event):
    result = invoke_rich_command("{{commandId:D}}", {"enabled": True})
    if result["outcome"] == "Completed":
        write_server_memory("{{stateId:D}}", 1)
""",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "initialize")
            ],
            dependencies:
            [
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.ServerMemoryTag,
                    stateId.ToString("D")),
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.RichCommand,
                    commandId.ToString("D"))
            ]);

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                stateId,
                "CommandResultObserved",
                "Simulation.CommandResultObserved",
                TagDataType.Int32,
                Source: "memory.server",
                ReadOnly: false,
                InitialValue: new MemoryInitialValueDto(
                    TagDataType.Int32,
                    JsonSerializer.SerializeToElement(0)))],
            Array.Empty<AlarmEngineeringDto>(),
            DataSources:
            [
                new DataSourceEngineeringDto(
                    null,
                    "memory.server",
                    "Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey),
                new DataSourceEngineeringDto(
                    commandDataSourceId,
                    "test.commands",
                    "Test Commands",
                    EngineeringDriverCompiler.SimulationDriverKey)
            ],
            Scripts: [script],
            RichCommandDefinitions:
            [
                new RichCommandDefinitionEngineeringDto(
                    commandId,
                    "test.enable",
                    [new RichCommandParameterDefinition(
                        "enabled",
                        new InteractionScalarSchema(InteractionScalarKind.Boolean),
                        Required: true)])
            ],
            DriverCommandBindings:
            [
                new DriverCommandBindingEngineeringDto(
                    commandId,
                    commandDataSourceId,
                    "test-device-1",
                    "test.enable")
            ]);
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        Func<string?>? failureDetails = null)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(20);
        Assert.True(condition(), failureDetails?.Invoke());
    }

    private sealed class CapturingRichCommandRuntime : IRichCommandRuntime
    {
        public RichCommandInvocation? Invocation { get; private set; }

        public ValueTask<RichCommandResult> InvokeAsync(
            RichCommandInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocation = invocation;
            return ValueTask.FromResult(new RichCommandResult(
                invocation.InvocationId,
                invocation.CommandId,
                RichCommandOutcome.Completed,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
