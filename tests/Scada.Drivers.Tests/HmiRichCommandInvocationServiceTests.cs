using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Interactions;
using Scada.Core.InternalMemory;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class HmiRichCommandInvocationServiceTests
{
    private static readonly Guid CommandId =
        Guid.Parse("8f1fd44e-c3ea-4a19-8e2f-776b3eecc19a");

    [Fact]
    public async Task InvokeAsync_UsesTypedActiveDefinitionAndAuditsHumanCommandExecution()
    {
        await using var fixture = CreateFixture();

        using var percentage = JsonDocument.Parse("\"37.5\"");
        var request = new HmiRichCommandInvocationRequest(
            CommandId,
            new Dictionary<string, JsonElement>
            {
                ["position"] = percentage.RootElement.Clone()
            });

        var result = await fixture.Service.InvokeAsync(
            fixture.Context,
            fixture.Runtime,
            CommandId,
            request);

        Assert.Equal("Completed", result.Outcome);
        var invocation = Assert.IsType<RichCommandInvocation>(fixture.CommandRuntime.LastInvocation);
        Assert.Equal(CommandId, invocation.CommandId);
        Assert.Equal(InteractionOrigin.Hmi, invocation.Causality?.Origin);
        Assert.Equal(invocation.InvocationId, invocation.Causality?.CorrelationId);
        Assert.Equal(
            InteractionScalarValue.Percentage(37.5m),
            Assert.Single(invocation.Parameters).Value);
        Assert.Equal("cover.move", fixture.Authorization.LastDefinition?.SemanticKey);
        Assert.Equal(true, fixture.Authorization.LastRequiredRuntimeSession);

        Assert.Contains(fixture.AuditStore.Snapshot(), audit =>
            audit.Action == AuditActions.CommandExecute &&
            audit.TargetKind == "rich-command" &&
            audit.TargetId == CommandId.ToString("D") &&
            audit.SubjectId == "human-operator" &&
            audit.Outcome == AuditOutcome.Succeeded);
    }

    [Fact]
    public async Task GetActiveDefinitionAsync_UsesCommandScopeWithoutMutationLease()
    {
        await using var fixture = CreateFixture();

        var lookup = await fixture.Service.GetActiveDefinitionAsync(
            fixture.Context,
            fixture.Runtime,
            CommandId);

        Assert.Equal(StatusCodes.Status200OK, lookup.StatusCode);
        Assert.Equal("cover.move", lookup.Definition?.SemanticKey);
        Assert.Equal("position", Assert.Single(lookup.Definition!.Parameters).Key);
        Assert.Equal(false, fixture.Authorization.LastRequiredRuntimeSession);
        Assert.Null(fixture.CommandRuntime.LastInvocation);
    }

    [Fact]
    public async Task InvokeAsync_RejectsRichCommandOutsideActiveDefinitionSet()
    {
        await using var fixture = CreateFixture(includeDefinition: false);

        var result = await fixture.Service.InvokeAsync(
            fixture.Context,
            fixture.Runtime,
            CommandId,
            new HmiRichCommandInvocationRequest(CommandId));

        Assert.Equal("Rejected", result.Outcome);
        Assert.Equal("definition.not_found", result.Code);
        Assert.Null(fixture.CommandRuntime.LastInvocation);
        Assert.Contains(fixture.AuditStore.Snapshot(), audit =>
            audit.Action == AuditActions.CommandExecute &&
            audit.TargetId == CommandId.ToString("D") &&
            audit.Outcome == AuditOutcome.Failed);
    }

    [Fact]
    public async Task InvokeAsync_RejectsUnknownActiveBindingWithoutRuntimeDispatch()
    {
        await using var fixture = CreateFixture(includeBinding: false);
        using var percentage = JsonDocument.Parse("\"37.5\"");

        var result = await fixture.Service.InvokeAsync(
            fixture.Context,
            fixture.Runtime,
            CommandId,
            new HmiRichCommandInvocationRequest(
                CommandId,
                new Dictionary<string, JsonElement>
                {
                    ["position"] = percentage.RootElement.Clone()
                }));

        Assert.Equal("Rejected", result.Outcome);
        Assert.Equal("binding.not_found", result.Code);
        Assert.Null(fixture.CommandRuntime.LastInvocation);
    }

    [Theory]
    [InlineData(RichCommandOutcome.Rejected)]
    [InlineData(RichCommandOutcome.Accepted)]
    [InlineData(RichCommandOutcome.Completed)]
    [InlineData(RichCommandOutcome.Failed)]
    [InlineData(RichCommandOutcome.TimedOut)]
    [InlineData(RichCommandOutcome.Unknown)]
    public async Task InvokeAsync_PreservesEveryCanonicalRuntimeOutcome(RichCommandOutcome outcome)
    {
        await using var fixture = CreateFixture(runtimeOutcome: outcome);
        using var percentage = JsonDocument.Parse("\"37.5\"");

        var result = await fixture.Service.InvokeAsync(
            fixture.Context,
            fixture.Runtime,
            CommandId,
            new HmiRichCommandInvocationRequest(
                CommandId,
                new Dictionary<string, JsonElement>
                {
                    ["position"] = percentage.RootElement.Clone()
                }));

        Assert.Equal(outcome.ToString(), result.Outcome);
        Assert.NotNull(fixture.CommandRuntime.LastInvocation);
    }

    [Fact]
    public async Task InvokeAsync_RejectsInvalidTypedParametersBeforeRuntimeDispatch()
    {
        await using var fixture = CreateFixture();

        using var tooLarge = JsonDocument.Parse("1e100");
        var request = new HmiRichCommandInvocationRequest(
            CommandId,
            new Dictionary<string, JsonElement>
            {
                ["position"] = tooLarge.RootElement.Clone()
            });

        var result = await fixture.Service.InvokeAsync(
            fixture.Context,
            fixture.Runtime,
            CommandId,
            request);

        Assert.Equal("Rejected", result.Outcome);
        Assert.Equal("invocation.invalid", result.Code);
        Assert.Null(fixture.CommandRuntime.LastInvocation);
        Assert.Contains(fixture.AuditStore.Snapshot(), audit =>
            audit.Action == AuditActions.CommandExecute &&
            audit.TargetId == CommandId.ToString("D") &&
            audit.Outcome == AuditOutcome.Failed);
    }

    [Fact]
    public async Task InvokeAsync_AuthorizationDenialIsAuditedWithoutDispatch()
    {
        await using var fixture = CreateFixture(authorizationAllowed: false);
        using var percentage = JsonDocument.Parse("\"37.5\"");
        var request = new HmiRichCommandInvocationRequest(
            CommandId,
            new Dictionary<string, JsonElement>
            {
                ["position"] = percentage.RootElement.Clone()
            });

        var result = await fixture.Service.InvokeAsync(
            fixture.Context,
            fixture.Runtime,
            CommandId,
            request);

        Assert.Equal("Rejected", result.Outcome);
        Assert.Equal("authorization.denied", result.Code);
        Assert.Null(fixture.CommandRuntime.LastInvocation);
        Assert.Contains(fixture.AuditStore.Snapshot(), audit =>
            audit.Action == AuditActions.CommandExecute &&
            audit.TargetId == CommandId.ToString("D") &&
            audit.SubjectId == "human-operator" &&
            audit.Outcome == AuditOutcome.Denied);
    }

    private static Fixture CreateFixture(
        bool authorizationAllowed = true,
        bool includeDefinition = true,
        bool includeBinding = true,
        RichCommandOutcome runtimeOutcome = RichCommandOutcome.Completed)
    {
        var eventBus = new InMemoryScadaEventBus();
        var coordinator = new EngineeringRuntimeCoordinator(
            eventBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2),
            new InMemoryServerMemoryRetentionStore());
        var runtime = new ScadaRuntimeFacade(coordinator);
        var definition = new RichCommandDefinition(
            CommandId,
            "cover.move",
            [
                new RichCommandParameterDefinition(
                    "position",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Percentage,
                        Minimum: 0,
                        Maximum: 100),
                    Required: true)
            ]);
        var definitions = new InMemoryRichCommandDefinitionResolver(
            includeDefinition ? new[] { definition } : Array.Empty<RichCommandDefinition>());
        var binding = new DriverCommandBinding(
            CommandId,
            Guid.Parse("974b9a62-8d59-4a17-baa1-17c23d47d8de"),
            "cover-01",
            "cover.move");
        var bindings = new InMemoryRichCommandBindingResolver(
            includeBinding ? new[] { binding } : Array.Empty<DriverCommandBinding>());
        var commandRuntime = new CapturingRuntime(runtimeOutcome);
        var principal = new SecurityPrincipal(
            "human-operator",
            "Human Operator",
            Array.Empty<string>());
        var authorization = new RecordingAuthorization(principal, authorizationAllowed);
        var auditStore = new InMemoryAuditSink();
        var audit = new ApiAuditService(
            auditStore,
            auditStore,
            NullLogger<ApiAuditService>.Instance);
        var service = new HmiRichCommandInvocationService(
            definitions,
            bindings,
            commandRuntime,
            authorization,
            audit);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, principal.SubjectId)],
                    "test-auth"))
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/runtime/rich-commands/" + CommandId.ToString("D") + "/execute";

        return new Fixture(
            coordinator,
            runtime,
            service,
            context,
            commandRuntime,
            authorization,
            auditStore);
    }

    private sealed class Fixture(
        EngineeringRuntimeCoordinator coordinator,
        ScadaRuntimeFacade runtime,
        HmiRichCommandInvocationService service,
        HttpContext context,
        CapturingRuntime commandRuntime,
        RecordingAuthorization authorization,
        InMemoryAuditSink auditStore) : IAsyncDisposable
    {
        public ScadaRuntimeFacade Runtime { get; } = runtime;
        public HmiRichCommandInvocationService Service { get; } = service;
        public HttpContext Context { get; } = context;
        public CapturingRuntime CommandRuntime { get; } = commandRuntime;
        public RecordingAuthorization Authorization { get; } = authorization;
        public InMemoryAuditSink AuditStore { get; } = auditStore;

        public ValueTask DisposeAsync() => coordinator.DisposeAsync();
    }

    private sealed class CapturingRuntime(RichCommandOutcome outcome) : IRichCommandRuntime
    {
        public RichCommandInvocation? LastInvocation { get; private set; }

        public ValueTask<RichCommandResult> InvokeAsync(
            RichCommandInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            LastInvocation = invocation;
            return ValueTask.FromResult(new RichCommandResult(
                invocation.InvocationId,
                invocation.CommandId,
                outcome,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class RecordingAuthorization(
        SecurityPrincipal principal,
        bool allowed) : IRichCommandHmiAuthorization
    {
        public RichCommandDefinition? LastDefinition { get; private set; }
        public bool? LastRequiredRuntimeSession { get; private set; }

        public SecurityPrincipal GetPrincipal(HttpContext context) => principal;

        public Task<ApiAuthorizationCheck> CheckAsync(
            HttpContext context,
            ScadaRuntimeFacade runtime,
            RichCommandDefinition definition,
            bool requireRuntimeSession,
            CancellationToken cancellationToken)
        {
            LastDefinition = definition;
            LastRequiredRuntimeSession = requireRuntimeSession;
            return Task.FromResult(new ApiAuthorizationCheck(
                principal,
                new AuthorizationDecision(
                    allowed,
                    SecurityCapability.CommandExecute,
                    allowed ? "allowed" : "denied",
                    allowed ? new[] { "operator" } : Array.Empty<string>())));
        }
    }
}
