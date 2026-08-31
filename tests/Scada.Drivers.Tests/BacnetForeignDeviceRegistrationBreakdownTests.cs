using System.Reflection;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetForeignDeviceRegistrationBreakdownTests
{
    [Fact]
    public async Task Breakdown_SeparatesInitialRenewalAndRetryAttemptsAndFailures()
    {
        await using var session = new SystemIoBacnetSession(new BacnetSessionOptions(
            BbmdAddress: "192.168.20.1",
            ForeignDeviceTtlSeconds: 120));

        RecordAttempt(session, BacnetForeignDeviceRegistrationAttemptKind.Initial);
        RecordAttempt(session, BacnetForeignDeviceRegistrationAttemptKind.Renewal);
        RecordFailure(session, BacnetForeignDeviceRegistrationAttemptKind.Renewal);
        RecordAttempt(session, BacnetForeignDeviceRegistrationAttemptKind.Retry);
        RecordFailure(session, BacnetForeignDeviceRegistrationAttemptKind.Retry);

        var diagnostics = session.GetForeignDeviceRegistrationBreakdownDiagnostics();

        Assert.Equal(BacnetForeignDeviceRegistrationAttemptKind.Retry, diagnostics.LastAttemptKind);
        Assert.Equal(1, diagnostics.InitialAttempts);
        Assert.Equal(0, diagnostics.InitialFailures);
        Assert.Equal(1, diagnostics.RenewalAttempts);
        Assert.Equal(1, diagnostics.RenewalFailures);
        Assert.Equal(1, diagnostics.RetryAttempts);
        Assert.Equal(1, diagnostics.RetryFailures);
        Assert.Equal(3, diagnostics.TotalAttempts);
        Assert.Equal(2, diagnostics.TotalFailures);
    }

    [Fact]
    public async Task Breakdown_DefaultsToZeroBeforeAnyRegistrationAttempt()
    {
        await using var session = new SystemIoBacnetSession(new BacnetSessionOptions(
            BbmdAddress: "192.168.20.1",
            ForeignDeviceTtlSeconds: 120));

        var diagnostics = session.GetForeignDeviceRegistrationBreakdownDiagnostics();

        Assert.Equal(BacnetForeignDeviceRegistrationAttemptKind.Initial, diagnostics.LastAttemptKind);
        Assert.Equal(0, diagnostics.TotalAttempts);
        Assert.Equal(0, diagnostics.TotalFailures);
    }

    private static void RecordAttempt(
        SystemIoBacnetSession session,
        BacnetForeignDeviceRegistrationAttemptKind kind)
    {
        InvokePrivate(
            session,
            "RecordForeignDeviceRegistrationAttempt",
            kind,
            DateTimeOffset.UtcNow);
    }

    private static void RecordFailure(
        SystemIoBacnetSession session,
        BacnetForeignDeviceRegistrationAttemptKind kind)
    {
        InvokePrivate(
            session,
            "RecordForeignDeviceRegistrationFailure",
            kind,
            new InvalidOperationException("synthetic FDR failure"));
    }

    private static void InvokePrivate(SystemIoBacnetSession session, string methodName, params object[] arguments)
    {
        var method = typeof(SystemIoBacnetSession).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(session, arguments);
    }
}
