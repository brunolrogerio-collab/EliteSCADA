using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Security;
using Scada.Engineering.ImportExport;
using Scada.Security.Audit;

namespace Scada.Drivers.Tests;

public sealed class AuditMutationAdmissionTests
{
    [Fact]
    public async Task UnsafeApiRequest_PersistsAdmissionDirectlyBeforeEndpointEvenWhenBufferedSinkRejects()
    {
        using var workspace = new EngineeringWorkspace();
        var exchange = new EngineeringExchangeService(workspace.Tags, workspace.Alarms);
        var configuration = new ConfigurationManager
        {
            ["Authentication:Enabled"] = "false"
        };
        var security = new ApiAuthorizationService(
            new NullServiceProvider(),
            workspace,
            exchange,
            configuration);
        var store = new InMemoryAuditSink();
        var audit = new ApiAuditService(
            new RejectingBufferedSink(),
            store,
            NullLogger<ApiAuditService>.Instance);
        var nextCalled = false;
        var middleware = new ApiMutationAuditAdmissionMiddleware(context =>
        {
            var admission = Assert.Single(store.Snapshot());
            Assert.Equal(AuditActions.ProtectedMutationAdmission, admission.Action);
            Assert.Equal(AuditOutcome.Succeeded, admission.Outcome);
            Assert.Equal("api-route", admission.TargetKind);
            Assert.Equal("/api/tags/00000000-0000-0000-0000-000000000001/write", admission.TargetId);
            Assert.Equal("POST", admission.Details!["method"]);
            Assert.Equal(context.TraceIdentifier, admission.CorrelationId);
            nextCalled = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = "/api/tags/00000000-0000-0000-0000-000000000001/write";

        await middleware.InvokeAsync(http, audit, security);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task UnsafeApiRequest_StoreFailureReturns503WithoutCallingEndpoint()
    {
        using var workspace = new EngineeringWorkspace();
        var exchange = new EngineeringExchangeService(workspace.Tags, workspace.Alarms);
        var configuration = new ConfigurationManager
        {
            ["Authentication:Enabled"] = "false"
        };
        var security = new ApiAuthorizationService(
            new NullServiceProvider(),
            workspace,
            exchange,
            configuration);
        var store = new RejectingAuditStore();
        var audit = new ApiAuditService(
            new InMemoryAuditSink(),
            store,
            NullLogger<ApiAuditService>.Instance);
        var nextCalled = false;
        var middleware = new ApiMutationAuditAdmissionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var http = new DefaultHttpContext();
        http.Request.Method = HttpMethods.Delete;
        http.Request.Path = "/api/engineering/tags/00000000-0000-0000-0000-000000000001";
        http.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(http, audit, security);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Equal(1, store.WriteAttempts);
        http.Response.Body.Position = 0;
        var body = await new StreamReader(http.Response.Body).ReadToEndAsync();
        Assert.Contains("not executed", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("synthetic audit outage", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class RejectingBufferedSink : IAuditSink
    {
        public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new AuditBufferFullException(1));
    }

    private sealed class RejectingAuditStore : IAuditStore
    {
        private int _writeAttempts;

        public int WriteAttempts => Volatile.Read(ref _writeAttempts);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _writeAttempts);
            return ValueTask.FromException(new InvalidOperationException("synthetic audit outage"));
        }

        public Task<AuditPage> QueryPageAsync(AuditQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<AuditEvent>> QueryAsync(
            int limit = 100,
            string? subjectId = null,
            string? action = null,
            AuditOutcome? outcome = null,
            DateTimeOffset? fromUtc = null,
            DateTimeOffset? toUtc = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> ApplyRetentionBatchAsync(
            DateTimeOffset cutoffUtc,
            int batchSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public AuditStoreHealthSnapshot GetHealthSnapshot() =>
            new(0, WriteAttempts, null, null, null, 0);
    }
}
