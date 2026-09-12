using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scada.Api.Timing;

namespace Scada.Drivers.Tests;

public sealed class TimingPolicyV1Tests
{
    [Fact]
    public void Defaults_AreValidAndProjectTheVersionedPublicContract()
    {
        var options = new TimingPolicyV1Options();
        var validation = new TimingPolicyV1Validator().Validate(null, options);

        Assert.True(validation.Succeeded);
        var contract = TimingPolicyV1Contract.Create(options);
        Assert.Equal("elitescada.timing-policy/v1", contract.Schema);
        Assert.Equal(1, contract.SchemaVersion);
        Assert.Equal(10_000, contract.Effective.ConnectBudgetMilliseconds);
        Assert.Equal(30_000, contract.Effective.ReadBudgetMilliseconds);
        Assert.Equal(45_000, contract.Effective.BootstrapBudgetMilliseconds);
        Assert.Equal(120_000, contract.Effective.LongOperationBudgetMilliseconds);
        Assert.Equal([500, 1_000, 2_000, 5_000, 10_000, 20_000, 30_000], contract.Effective.ReconnectDelayMilliseconds);
        Assert.Equal(2, contract.Effective.OrdinaryReadMaxRetries);
        Assert.Contains("commandWriteResponse", contract.AdaptiveCategories);
        Assert.Contains("policyTimeout", contract.FailureCategories);
        Assert.Contains("unknownMutationOutcome", contract.FailureCategories);
        Assert.Contains("securitySession", contract.ExcludedOwnershipDomains);
        Assert.Contains("haAuthority", contract.ExcludedOwnershipDomains);
        Assert.Contains("driverProtocol", contract.ExcludedOwnershipDomains);
        Assert.Contains("internalExecution", contract.ExcludedOwnershipDomains);
        Assert.Equal(TimingCorrelationMiddleware.CorrelationResponseHeader, contract.Correlation.ResponseHeader);
    }

    [Theory]
    [InlineData(2_999)]
    [InlineData(30_001)]
    public void ConnectBudget_OutsideDocumentedBounds_FailsClosed(int value)
    {
        var options = new TimingPolicyV1Options { ConnectBudgetMilliseconds = value };

        var validation = new TimingPolicyV1Validator().Validate(null, options);

        Assert.False(validation.Succeeded);
        Assert.Contains(nameof(TimingPolicyV1Options.ConnectBudgetMilliseconds), validation.FailureMessage);
    }

    [Fact]
    public void EveryScalarBudgetAndRetryBound_RejectsValuesOutsideItsRange()
    {
        var cases = new (string Name, Action<TimingPolicyV1Options> Below, Action<TimingPolicyV1Options> Above)[]
        {
            (nameof(TimingPolicyV1Options.ReadBudgetMilliseconds), x => x.ReadBudgetMilliseconds = 4_999, x => x.ReadBudgetMilliseconds = 120_001),
            (nameof(TimingPolicyV1Options.BootstrapBudgetMilliseconds), x => x.BootstrapBudgetMilliseconds = 9_999, x => x.BootstrapBudgetMilliseconds = 180_001),
            (nameof(TimingPolicyV1Options.LongOperationBudgetMilliseconds), x => x.LongOperationBudgetMilliseconds = 29_999, x => x.LongOperationBudgetMilliseconds = 600_001),
            (nameof(TimingPolicyV1Options.RealtimeConnectBudgetMilliseconds), x => x.RealtimeConnectBudgetMilliseconds = 4_999, x => x.RealtimeConnectBudgetMilliseconds = 60_001),
            (nameof(TimingPolicyV1Options.RealtimeObservationMilliseconds), x => x.RealtimeObservationMilliseconds = 4_999, x => x.RealtimeObservationMilliseconds = 120_001),
            (nameof(TimingPolicyV1Options.RealtimeStaleAfterMilliseconds), x => x.RealtimeStaleAfterMilliseconds = 9_999, x => x.RealtimeStaleAfterMilliseconds = 120_001),
            (nameof(TimingPolicyV1Options.OrdinaryReadMaxRetries), x => x.OrdinaryReadMaxRetries = -1, x => x.OrdinaryReadMaxRetries = 4),
            (nameof(TimingPolicyV1Options.CommandWriteResponseBudgetMilliseconds), x => x.CommandWriteResponseBudgetMilliseconds = 4_999, x => x.CommandWriteResponseBudgetMilliseconds = 120_001),
            (nameof(TimingPolicyV1Options.SlowThresholdMilliseconds), x => x.SlowThresholdMilliseconds = 499, x => x.SlowThresholdMilliseconds = 10_001),
            (nameof(TimingPolicyV1Options.StaleMinimumMilliseconds), x => x.StaleMinimumMilliseconds = 4_999, x => x.StaleMinimumMilliseconds = 120_001)
        };

        foreach (var testCase in cases)
        {
            var below = new TimingPolicyV1Options();
            testCase.Below(below);
            Assert.Contains(testCase.Name, new TimingPolicyV1Validator().Validate(null, below).FailureMessage);

            var above = new TimingPolicyV1Options();
            testCase.Above(above);
            Assert.Contains(testCase.Name, new TimingPolicyV1Validator().Validate(null, above).FailureMessage);
        }
    }

    [Fact]
    public void InvalidReconnectAndFreshnessRelationships_FailClosed()
    {
        var options = new TimingPolicyV1Options
        {
            ReconnectDelayMilliseconds = [1_000, 500],
            ReconnectJitterRatio = 0.75,
            RealtimeObservationMilliseconds = 20_000,
            RealtimeStaleAfterMilliseconds = 30_000
        };

        var validation = new TimingPolicyV1Validator().Validate(null, options);

        Assert.False(validation.Succeeded);
        Assert.Contains("strictly increasing", validation.FailureMessage, StringComparison.Ordinal);
        Assert.Contains(nameof(TimingPolicyV1Options.ReconnectJitterRatio), validation.FailureMessage);
        Assert.Contains(nameof(TimingPolicyV1Options.RealtimeStaleAfterMilliseconds), validation.FailureMessage);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.01)]
    [InlineData(0.51)]
    public void InvalidReconnectJitter_FailsClosed(double value)
    {
        var options = new TimingPolicyV1Options { ReconnectJitterRatio = value };

        var validation = new TimingPolicyV1Validator().Validate(null, options);

        Assert.False(validation.Succeeded);
        Assert.Contains(nameof(TimingPolicyV1Options.ReconnectJitterRatio), validation.FailureMessage);
    }

    [Fact]
    public void ReconnectSchedule_IsBoundedInLengthAndPerDelay()
    {
        var tooMany = new TimingPolicyV1Options
        {
            ReconnectDelayMilliseconds = Enumerable.Range(1, 17).Select(index => index * 250).ToArray()
        };
        var outOfRange = new TimingPolicyV1Options
        {
            ReconnectDelayMilliseconds = [249, 500]
        };

        var tooManyResult = new TimingPolicyV1Validator().Validate(null, tooMany);
        var outOfRangeResult = new TimingPolicyV1Validator().Validate(null, outOfRange);

        Assert.Contains("more than 16", tooManyResult.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("[0]", outOfRangeResult.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidConfigurationOverrides_AreBoundAndValidated()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TimingPolicyV1:ReadBudgetMilliseconds"] = "60000",
            ["TimingPolicyV1:OrdinaryReadMaxRetries"] = "1",
            ["TimingPolicyV1:ReconnectDelayMilliseconds:0"] = "750",
            ["TimingPolicyV1:ReconnectDelayMilliseconds:1"] = "1500"
        });
        builder.AddTimingPolicyV1();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TimingPolicyV1Options>>().Value;

        Assert.Equal(60_000, options.ReadBudgetMilliseconds);
        Assert.Equal(1, options.OrdinaryReadMaxRetries);
        Assert.Equal([750, 1_500], options.ReconnectDelayMilliseconds!);
    }

    [Theory]
    [InlineData("SecuritySessionBudgetMilliseconds")]
    [InlineData("HaAuthorityHeartbeatMilliseconds")]
    [InlineData("DriverProtocolReconnectMilliseconds")]
    [InlineData("InternalExecutionScriptBudgetMilliseconds")]
    [InlineData("GlobalTimeoutMultiplier")]
    public void ExcludedOrUnknownConfigurationKey_IsRejectedBeforeStartup(string key)
    {
        var configuration = new ConfigurationManager
        {
            [$"TimingPolicyV1:{key}"] = "999999"
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TimingPolicyV1Configuration.RejectUnknownKeys(
                configuration.GetSection(TimingPolicyV1Configuration.SectionName)));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
        Assert.Contains("not configurable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdaptiveConfigurationSurface_ContainsOnlyTheExplicitV1Keys()
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(TimingPolicyV1Options.ConnectBudgetMilliseconds),
            nameof(TimingPolicyV1Options.ReadBudgetMilliseconds),
            nameof(TimingPolicyV1Options.BootstrapBudgetMilliseconds),
            nameof(TimingPolicyV1Options.LongOperationBudgetMilliseconds),
            nameof(TimingPolicyV1Options.RealtimeConnectBudgetMilliseconds),
            nameof(TimingPolicyV1Options.ReconnectDelayMilliseconds),
            nameof(TimingPolicyV1Options.ReconnectJitterRatio),
            nameof(TimingPolicyV1Options.RealtimeObservationMilliseconds),
            nameof(TimingPolicyV1Options.RealtimeStaleAfterMilliseconds),
            nameof(TimingPolicyV1Options.OrdinaryReadMaxRetries),
            nameof(TimingPolicyV1Options.CommandWriteResponseBudgetMilliseconds),
            nameof(TimingPolicyV1Options.SlowThresholdMilliseconds),
            nameof(TimingPolicyV1Options.StaleMinimumMilliseconds)
        };

        Assert.True(expected.SetEquals(TimingPolicyV1Configuration.KnownKeys));
        Assert.DoesNotContain(TimingPolicyV1Configuration.KnownKeys, key =>
            key.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Authority", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Driver", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Execution", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Multiplier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CorrelationMiddleware_ReturnsServerTraceAndAcceptsOnlySafeMetadata()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "server-trace-123";
        context.Request.Headers[TimingCorrelationMiddleware.RequestIdHeader] = "client-request-456";
        context.Request.Headers[TimingCorrelationMiddleware.OperationHeader] = "runtime.application.load";
        context.Request.Headers[TimingCorrelationMiddleware.CategoryHeader] = "requestRead";
        context.Request.Headers[TimingCorrelationMiddleware.AttemptHeader] = "2";
        var nextCalled = false;
        var middleware = new TimingCorrelationMiddleware(
            request =>
            {
                nextCalled = true;
                request.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            NullLogger<TimingCorrelationMiddleware>.Instance);

        var metadata = TimingCorrelationMiddleware.ReadMetadata(context);
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal("client-request-456", metadata.RequestId);
        Assert.Equal("runtime.application.load", metadata.Operation);
        Assert.Equal("requestRead", metadata.Category);
        Assert.Equal(2, metadata.Attempt);
        Assert.Equal("server-trace-123", context.Response.Headers[TimingCorrelationMiddleware.CorrelationResponseHeader]);
        Assert.NotEqual(
            metadata.RequestId,
            context.Response.Headers[TimingCorrelationMiddleware.CorrelationResponseHeader].ToString());
    }

    [Fact]
    public void UnsafeOrUnknownClientMetadata_IsIgnoredRatherThanLoggedAsAuthority()
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "server-trace-safe";
        context.Request.Headers[TimingCorrelationMiddleware.RequestIdHeader] = "request?secret=value";
        context.Request.Headers[TimingCorrelationMiddleware.OperationHeader] = "operation with spaces";
        context.Request.Headers[TimingCorrelationMiddleware.CategoryHeader] = "securitySession";
        context.Request.Headers[TimingCorrelationMiddleware.AttemptHeader] = "99";

        var metadata = TimingCorrelationMiddleware.ReadMetadata(context);

        Assert.Equal(context.TraceIdentifier, metadata.RequestId);
        Assert.Null(metadata.Operation);
        Assert.Null(metadata.Category);
        Assert.Equal(1, metadata.Attempt);
    }
}
