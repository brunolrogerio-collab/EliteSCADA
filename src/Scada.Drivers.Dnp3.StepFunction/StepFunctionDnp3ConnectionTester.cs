using System.Globalization;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Dnp3.StepFunction;

/// <summary>
/// Protocol-backed Engineering connection test for the optional Step Function module.
/// It never mutates canonical Engineering and only succeeds after the DNP3 association
/// completes startup integrity and reaches the Online state.
/// </summary>
public sealed class StepFunctionDnp3ConnectionTester : ICommunicationDriverConnectionTester
{
    private static readonly CommunicationDriverTypeDescriptor StepFunctionDescriptor =
        Dnp3DriverDescriptorProvider.SharedDescriptor with
        {
            EngineeringCapabilities =
                Dnp3DriverDescriptorProvider.SharedDescriptor.EngineeringCapabilities |
                DriverEngineeringCapabilities.ConnectionTest
        };

    private readonly IDnp3MasterSessionFactory _sessionFactory;

    public StepFunctionDnp3ConnectionTester()
        : this(new StepFunctionDnp3MasterSessionFactory())
    {
    }

    public StepFunctionDnp3ConnectionTester(IDnp3MasterSessionFactory sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(sessionFactory);
        _sessionFactory = sessionFactory;
    }

    public CommunicationDriverTypeDescriptor Descriptor => StepFunctionDescriptor;

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!context.DriverType.Equals(Dnp3DriverDescriptorProvider.DriverType, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                endpoint: null,
                issues:
                [
                    Error(
                        "DNP3_CONNECTION_TEST_DRIVER_TYPE",
                        $"Connection test requires driver type '{Dnp3DriverDescriptorProvider.DriverType}'.",
                        fieldKey: null)
                ]);
        }

        var parsed = Dnp3DataSourceSettingsParser.Parse(context.Settings);
        var issues = parsed.Issues.ToList();
        if (!parsed.Succeeded || parsed.Value is null)
            return Failure(endpoint: null, issues: issues);

        var connection = parsed.Value.Connection;
        var association = parsed.Value.Association;
        var endpoint = connection.SanitizedEndpoint;
        await using var session = _sessionFactory.Create(connection);
        var online = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var testTimeout = CalculateTestTimeout(connection, association);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(testTimeout);

        try
        {
            await session.StartAsync(
                association,
                static (_, _) => ValueTask.CompletedTask,
                (state, _) =>
                {
                    if (state == Dnp3SessionState.Online)
                        online.TrySetResult(true);
                    else if (state == Dnp3SessionState.Faulted)
                        online.TrySetException(new InvalidOperationException("DNP3 association faulted during connection test."));
                    return ValueTask.CompletedTask;
                },
                timeoutCts.Token);

            if (session.State == Dnp3SessionState.Online)
                online.TrySetResult(true);

            await online.Task.WaitAsync(timeoutCts.Token);
            var diagnostics = session.GetDiagnostics();
            var properties = BuildObservedProperties(diagnostics);
            return new DriverConnectionTestResult(
                Succeeded: true,
                SanitizedEndpoint: endpoint,
                ObservedIdentity: null,
                ObservedProperties: properties,
                Issues: issues.Count == 0 ? null : issues);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var diagnostics = session.GetDiagnostics();
            issues.Add(Error(
                "DNP3_CONNECTION_TEST_TIMEOUT",
                $"DNP3 association did not reach Online within the bounded connection-test window ({testTimeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s).",
                fieldKey: null));
            return Failure(endpoint, issues, BuildObservedProperties(diagnostics));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var diagnostics = session.GetDiagnostics();
            issues.Add(Error(
                "DNP3_CONNECTION_TEST_FAILED",
                "DNP3 association failed before the connection test reached Online.",
                fieldKey: null));
            return Failure(endpoint, issues, BuildObservedProperties(diagnostics));
        }
        finally
        {
            try
            {
                await session.StopAsync(CancellationToken.None);
            }
            catch
            {
                // Engineering test cleanup is best-effort; the original result remains authoritative.
            }
        }
    }

    private static TimeSpan CalculateTestTimeout(
        Dnp3TcpConnectionOptions connection,
        Dnp3AssociationOptions association)
    {
        var seconds = connection.ConnectTimeout.TotalSeconds + association.ResponseTimeout.TotalSeconds + 1d;
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 1d, 30d));
    }

    private static IReadOnlyDictionary<string, string> BuildObservedProperties(Dnp3SessionDiagnosticSnapshot diagnostics) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["associationState"] = diagnostics.State.ToString(),
            ["connections"] = diagnostics.Connections.ToString(CultureInfo.InvariantCulture),
            ["startupIntegrityScans"] = diagnostics.StartupIntegrityScans.ToString(CultureInfo.InvariantCulture),
            ["successfulOperations"] = diagnostics.SuccessfulOperations.ToString(CultureInfo.InvariantCulture),
            ["failedOperations"] = diagnostics.FailedOperations.ToString(CultureInfo.InvariantCulture)
        };

    private static DriverConnectionTestResult Failure(
        string? endpoint,
        IReadOnlyCollection<DriverEngineeringIssue> issues,
        IReadOnlyDictionary<string, string>? observedProperties = null) =>
        new(
            Succeeded: false,
            SanitizedEndpoint: endpoint,
            ObservedIdentity: null,
            ObservedProperties: observedProperties,
            Issues: issues);

    private static DriverEngineeringIssue Error(string code, string message, string? fieldKey) =>
        new(code, DriverEngineeringIssueSeverity.Error, message, fieldKey);
}
