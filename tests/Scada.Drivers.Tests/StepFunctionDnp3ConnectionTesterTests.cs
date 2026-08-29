using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;

namespace Scada.Drivers.Tests;

public sealed class StepFunctionDnp3ConnectionTesterTests
{
    [Fact]
    public void Descriptor_AdvertisesConnectionTestOnlyForInstalledStepFunctionModule()
    {
        var core = Dnp3DriverDescriptorProvider.SharedDescriptor;
        var stepFunction = new StepFunctionDnp3ConnectionTester(new FakeFactory()).Descriptor;

        Assert.Equal(DriverEngineeringCapabilities.None, core.EngineeringCapabilities);
        Assert.True(stepFunction.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.ConnectionTest));
        Assert.Equal(core.DriverType, stepFunction.DriverType);
        Assert.Equal(core.ConfigurationSchema, stepFunction.ConfigurationSchema);
    }

    [Fact]
    public async Task ValidSettings_RequiresOnlineAssociationAndReturnsSanitizedEvidence()
    {
        var factory = new FakeFactory(FakeSessionBehavior.Online);
        var tester = new StepFunctionDnp3ConnectionTester(factory);

        var result = await tester.TestConnectionAsync(CreateContext());

        Assert.True(result.Succeeded);
        Assert.Equal("127.0.0.1:20000", result.SanitizedEndpoint);
        Assert.Null(result.ObservedIdentity);
        Assert.NotNull(result.ObservedProperties);
        Assert.Equal("Online", result.ObservedProperties["associationState"]);
        Assert.Equal("1", result.ObservedProperties["startupIntegrityScans"]);
        Assert.Equal(1, factory.CreateCount);
        Assert.NotNull(factory.LastConnection);
        Assert.Equal((ushort)1, factory.LastConnection.MasterAddress);
        Assert.Equal((ushort)1024, factory.LastConnection.OutstationAddress);
        Assert.True(factory.LastSession?.StopCalled);
    }

    [Fact]
    public async Task InvalidSettings_FailsBeforeCreatingVendorSession()
    {
        var factory = new FakeFactory();
        var tester = new StepFunctionDnp3ConnectionTester(factory);
        var context = CreateContext(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["transport"] = "tcp",
            ["host"] = "https://user:secret@example.invalid/path",
            ["masterAddress"] = "1",
            ["outstationAddress"] = "1"
        });

        var result = await tester.TestConnectionAsync(context);

        Assert.False(result.Succeeded);
        Assert.Equal(0, factory.CreateCount);
        Assert.NotNull(result.Issues);
        Assert.Contains(result.Issues, issue => issue.Severity == DriverEngineeringIssueSeverity.Error);
        Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FaultedAssociation_ReturnsFailureAndStillStopsSession()
    {
        var factory = new FakeFactory(FakeSessionBehavior.Faulted);
        var tester = new StepFunctionDnp3ConnectionTester(factory);

        var result = await tester.TestConnectionAsync(CreateContext());

        Assert.False(result.Succeeded);
        Assert.Equal("127.0.0.1:20000", result.SanitizedEndpoint);
        Assert.NotNull(result.Issues);
        Assert.Contains(result.Issues, issue => issue.Code == "DNP3_CONNECTION_TEST_FAILED");
        Assert.True(factory.LastSession?.StopCalled);
    }

    private static DriverEngineeringDataSourceContext CreateContext(
        IReadOnlyDictionary<string, string>? settings = null) =>
        new(
            DataSourceKey: "dnp3-test",
            DataSourceName: "DNP3 Test",
            DriverType: Dnp3DriverDescriptorProvider.DriverType,
            Settings: settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["transport"] = "tcp",
                ["host"] = "127.0.0.1",
                ["masterAddress"] = "1",
                ["outstationAddress"] = "1024"
            },
            SecretReferences: new Dictionary<string, string>());

    private enum FakeSessionBehavior
    {
        Online,
        Faulted
    }

    private sealed class FakeFactory(FakeSessionBehavior behavior = FakeSessionBehavior.Online)
        : IDnp3MasterSessionFactory
    {
        public int CreateCount { get; private set; }
        public Dnp3TcpConnectionOptions? LastConnection { get; private set; }
        public FakeSession? LastSession { get; private set; }

        public IDnp3MasterSession Create(Dnp3TcpConnectionOptions connectionOptions)
        {
            CreateCount++;
            LastConnection = connectionOptions;
            LastSession = new FakeSession(connectionOptions.SanitizedEndpoint, behavior);
            return LastSession;
        }
    }

    private sealed class FakeSession(string endpoint, FakeSessionBehavior behavior) : IDnp3MasterSession
    {
        public Dnp3SessionState State { get; private set; } = Dnp3SessionState.Stopped;
        public bool StopCalled { get; private set; }

        public async ValueTask StartAsync(
            Dnp3AssociationOptions options,
            Func<Dnp3Measurement, CancellationToken, ValueTask> measurementHandler,
            Func<Dnp3SessionState, CancellationToken, ValueTask> stateHandler,
            CancellationToken cancellationToken = default)
        {
            options.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            State = Dnp3SessionState.StartupIntegrity;
            await stateHandler(State, cancellationToken);
            State = behavior == FakeSessionBehavior.Online ? Dnp3SessionState.Online : Dnp3SessionState.Faulted;
            await stateHandler(State, cancellationToken);
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCalled = true;
            State = Dnp3SessionState.Stopped;
            return ValueTask.CompletedTask;
        }

        public ValueTask<Dnp3CommandResult> ExecuteBinaryAsync(
            ushort index,
            Dnp3BinaryOperation operation,
            Dnp3BinaryCommandProfile profile,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<Dnp3CommandResult> ExecuteAnalogAsync(
            ushort index,
            object value,
            Dnp3AnalogCommandProfile profile,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Dnp3SessionDiagnosticSnapshot GetDiagnostics() => new(
            Endpoint: endpoint,
            State,
            StateChangedAt: DateTimeOffset.UtcNow,
            LastSuccessfulCommunicationAt: State == Dnp3SessionState.Online ? DateTimeOffset.UtcNow : null,
            LastFailedCommunicationAt: State == Dnp3SessionState.Faulted ? DateTimeOffset.UtcNow : null,
            SuccessfulOperations: State == Dnp3SessionState.Online ? 1 : 0,
            FailedOperations: State == Dnp3SessionState.Faulted ? 1 : 0,
            Connections: 1,
            StartupIntegrityScans: State == Dnp3SessionState.Online ? 1 : 0);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
