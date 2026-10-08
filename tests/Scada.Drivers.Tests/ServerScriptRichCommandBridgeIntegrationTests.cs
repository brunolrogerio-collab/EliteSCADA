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
        var triggerId = Guid.NewGuid();
        var commandDataSourceId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var package = Package(
            scriptId,
            commandId,
            stateId,
            triggerId,
            commandDataSourceId,
            declaredCommandId: commandId);
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
        await runtime.WriteAsync(triggerId, 1);
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

    [Fact]
    public Task ActiveServerScript_UndeclaredCommandIsRejectedBeforeDispatch() =>
        AssertCommandRejectedBeforeDispatchAsync(
            declareCommand: false,
            grantExactCommand: true);

    [Fact]
    public Task ActiveServerScript_CommandWithoutExactCommandExecuteGrantIsRejectedBeforeDispatch() =>
        AssertCommandRejectedBeforeDispatchAsync(
            declareCommand: true,
            grantExactCommand: false);

    private static async Task AssertCommandRejectedBeforeDispatchAsync(
        bool declareCommand,
        bool grantExactCommand)
    {
        const string projectKey = "server-script-command-denial-test";
        const long revision = 7;
        var scriptId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var triggerId = Guid.NewGuid();
        var commandDataSourceId = Guid.NewGuid();
        var package = Package(
            scriptId,
            commandId,
            stateId,
            triggerId,
            commandDataSourceId,
            declaredCommandId: declareCommand ? commandId : null);
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
        var commandScopeId = Guid.NewGuid();
        var grantScopeId = grantExactCommand ? commandScopeId : Guid.NewGuid();
        var grantedCommandId = grantExactCommand ? commandId : Guid.NewGuid();
        var scopes = new List<SecurityScopeEngineeringDto>
        {
            new(
                commandScopeId,
                "script-command-target",
                "Script Command Target",
                SecurityScopeNodeKind.Command,
                ResourceId: commandId)
        };
        if (!grantExactCommand)
        {
            scopes.Add(new SecurityScopeEngineeringDto(
                grantScopeId,
                "different-command-target",
                "Different Command Target",
                SecurityScopeNodeKind.Command,
                ResourceId: grantedCommandId));
        }

        var policyStore = new InMemoryAuthorityPolicyStore(
            [new SecurityRoleEngineeringDto(
                Guid.NewGuid(),
                roleKey,
                "Server Script Command Denial Test",
                Grants:
                [new CapabilityGrantEngineeringDto(
                    SecurityCapability.CommandExecute,
                    new AuthorizationScopeEngineeringDto(ScopeNodeId: grantScopeId))])],
            scopes);
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
        await runtime.WriteAsync(triggerId, 1);
        await WaitUntilAsync(
            () =>
            {
                var diagnostics = manager.Snapshot().Scripts.Single().Diagnostics;
                return declareCommand ? diagnostics.CompletedCount >= 1 : diagnostics.FaultedCount >= 1;
            },
            TimeSpan.FromSeconds(5),
            () => manager.Snapshot().Scripts.Single().Diagnostics.LastSanitizedError);

        Assert.Equal(0, commandRuntime.InvocationCount);
        if (!declareCommand)
        {
            Assert.Contains(
                "Rich Command is not an explicitly declared dependency.",
                manager.Snapshot().Scripts.Single().Diagnostics.LastSanitizedError);
        }
        else
        {
            Assert.Contains(auditStore.Snapshot(), item =>
                item.Action == AuditActions.CommandExecute &&
                item.Source == "server-script" &&
                item.SubjectId == ServerScriptCommandIdentity.CreatePrincipal(projectKey, revision, scriptId).SubjectId &&
                item.ProjectKey == projectKey &&
                item.Revision == revision &&
                item.TargetId == commandId.ToString("D") &&
                item.Outcome == AuditOutcome.Denied);
        }

        Assert.True(runtime.TryGetCurrent(stateId, out var unchanged));
        Assert.Equal(0, Convert.ToInt32(unchanged!.Value));

        await manager.DisposeAsync();
    }

    private static EngineeringPackage Package(
        Guid scriptId,
        Guid commandId,
        Guid stateId,
        Guid triggerId,
        Guid commandDataSourceId,
        Guid? declaredCommandId = null)
    {
        var dependencies = new List<ScriptEngineeringDependency>
        {
            new(
                ScriptEngineeringDependencyKind.ServerMemoryTag,
                stateId.ToString("D")),
            new(
                ScriptEngineeringDependencyKind.ServerMemoryTag,
                triggerId.ToString("D"))
        };
        if (declaredCommandId.HasValue)
        {
            dependencies.Add(new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.RichCommand,
                declaredCommandId.Value.ToString("D")));
        }

        var script = new ScriptEngineeringDefinition(
            scriptId,
            "Scripts.InvokeRichCommand",
            "Invoke Rich Command",
            ScriptEngineeringScope.Server,
            $$"""
def on_change(event):
    result = invoke_rich_command("{{commandId:D}}", {"enabled": True})
    if result["outcome"] == "Completed":
        write_server_memory("{{stateId:D}}", 1)
""",
            entryPoints:
            [
                new ScriptEngineeringEntryPoint(
                    ScriptEngineeringEventKind.TagChanged,
                    "on_change",
                    TagReference: new TagValueReference(triggerId))
            ],
            dependencies: dependencies);

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [
                new TagEngineeringDto(
                    stateId,
                    "CommandResultObserved",
                    "Simulation.CommandResultObserved",
                    TagDataType.Int32,
                    Source: "memory.server",
                    ReadOnly: false,
                    InitialValue: new MemoryInitialValueDto(
                        TagDataType.Int32,
                        JsonSerializer.SerializeToElement(0))),
                new TagEngineeringDto(
                    triggerId,
                    "CommandRequestTrigger",
                    "Simulation.CommandRequestTrigger",
                    TagDataType.Int32,
                    Source: "memory.server",
                    ReadOnly: false,
                    InitialValue: new MemoryInitialValueDto(
                        TagDataType.Int32,
                        JsonSerializer.SerializeToElement(0)))
            ],
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
        public int InvocationCount { get; private set; }

        public ValueTask<RichCommandResult> InvokeAsync(
            RichCommandInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocation = invocation;
            InvocationCount++;
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
