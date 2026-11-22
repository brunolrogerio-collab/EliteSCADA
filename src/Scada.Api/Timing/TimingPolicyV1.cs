using Microsoft.Extensions.Options;

namespace Scada.Api.Timing;

public sealed class TimingPolicyV1Options
{
    public int ConnectBudgetMilliseconds { get; set; } = 10_000;
    public int ReadBudgetMilliseconds { get; set; } = 30_000;
    public int BootstrapBudgetMilliseconds { get; set; } = 45_000;
    public int LongOperationBudgetMilliseconds { get; set; } = 120_000;
    public int RealtimeConnectBudgetMilliseconds { get; set; } = 15_000;
    public int[]? ReconnectDelayMilliseconds { get; set; }
    public double ReconnectJitterRatio { get; set; } = 0.20;
    public int RealtimeObservationMilliseconds { get; set; } = 15_000;
    public int RealtimeStaleAfterMilliseconds { get; set; } = 45_000;
    public int OrdinaryReadMaxRetries { get; set; } = 2;
    public int CommandWriteResponseBudgetMilliseconds { get; set; } = 30_000;
    public int SlowThresholdMilliseconds { get; set; } = 2_000;
    public int StaleMinimumMilliseconds { get; set; } = 10_000;
}

public sealed record TimingValueBounds(int Default, int Minimum, int Maximum);

public sealed record TimingPolicyV1Limits(
    TimingValueBounds ConnectBudgetMilliseconds,
    TimingValueBounds ReadBudgetMilliseconds,
    TimingValueBounds BootstrapBudgetMilliseconds,
    TimingValueBounds LongOperationBudgetMilliseconds,
    TimingValueBounds RealtimeConnectBudgetMilliseconds,
    TimingValueBounds ReconnectDelayMilliseconds,
    double ReconnectJitterRatioDefault,
    double ReconnectJitterRatioMinimum,
    double ReconnectJitterRatioMaximum,
    TimingValueBounds RealtimeObservationMilliseconds,
    TimingValueBounds RealtimeStaleAfterMilliseconds,
    TimingValueBounds OrdinaryReadMaxRetries,
    TimingValueBounds CommandWriteResponseBudgetMilliseconds,
    TimingValueBounds SlowThresholdMilliseconds,
    TimingValueBounds StaleMinimumMilliseconds);

public sealed record TimingPolicyV1Values(
    int ConnectBudgetMilliseconds,
    int ReadBudgetMilliseconds,
    int BootstrapBudgetMilliseconds,
    int LongOperationBudgetMilliseconds,
    int RealtimeConnectBudgetMilliseconds,
    IReadOnlyList<int> ReconnectDelayMilliseconds,
    double ReconnectJitterRatio,
    int RealtimeObservationMilliseconds,
    int RealtimeStaleAfterMilliseconds,
    int OrdinaryReadMaxRetries,
    int CommandWriteResponseBudgetMilliseconds,
    int SlowThresholdMilliseconds,
    int StaleMinimumMilliseconds);

public sealed record TimingCorrelationContract(
    string ResponseHeader,
    string RequestIdHeader,
    string OperationHeader,
    string CategoryHeader,
    string AttemptHeader);

public sealed record TimingPolicyV1Contract(
    string Schema,
    int SchemaVersion,
    IReadOnlyList<string> AdaptiveCategories,
    IReadOnlyList<string> FailureCategories,
    TimingPolicyV1Values Effective,
    TimingPolicyV1Limits Limits,
    IReadOnlyList<string> ExcludedOwnershipDomains,
    TimingCorrelationContract Correlation)
{
    public const string ContractSchema = "elitescada.timing-policy/v1";
    public const int ContractSchemaVersion = 1;

    public static TimingPolicyV1Contract Create(TimingPolicyV1Options options) => new(
        ContractSchema,
        ContractSchemaVersion,
        TimingPolicyV1Taxonomy.AdaptiveCategories,
        TimingPolicyV1Taxonomy.FailureCategories,
        new TimingPolicyV1Values(
            options.ConnectBudgetMilliseconds,
            options.ReadBudgetMilliseconds,
            options.BootstrapBudgetMilliseconds,
            options.LongOperationBudgetMilliseconds,
            options.RealtimeConnectBudgetMilliseconds,
            Array.AsReadOnly((options.ReconnectDelayMilliseconds ?? TimingPolicyV1Validator.DefaultReconnectDelays).ToArray()),
            options.ReconnectJitterRatio,
            options.RealtimeObservationMilliseconds,
            options.RealtimeStaleAfterMilliseconds,
            options.OrdinaryReadMaxRetries,
            options.CommandWriteResponseBudgetMilliseconds,
            options.SlowThresholdMilliseconds,
            options.StaleMinimumMilliseconds),
        TimingPolicyV1Validator.Limits,
        TimingPolicyV1Taxonomy.ExcludedOwnershipDomains,
        new TimingCorrelationContract(
            TimingCorrelationMiddleware.CorrelationResponseHeader,
            TimingCorrelationMiddleware.RequestIdHeader,
            TimingCorrelationMiddleware.OperationHeader,
            TimingCorrelationMiddleware.CategoryHeader,
            TimingCorrelationMiddleware.AttemptHeader));
}

public static class TimingPolicyV1Taxonomy
{
    public static readonly IReadOnlyList<string> AdaptiveCategories = Array.AsReadOnly([
        "transportConnect",
        "requestRead",
        "bootstrap",
        "longOperation",
        "realtimeConnect",
        "reconnect",
        "realtimeHeartbeat",
        "commandWriteResponse",
        "staleData"
    ]);

    public static readonly IReadOnlyList<string> FailureCategories = Array.AsReadOnly([
        "callerCancelled",
        "policyTimeout",
        "offline",
        "dns",
        "connect",
        "tls",
        "transport",
        "responseIntegrity",
        "httpStatus",
        "parseSchema",
        "staleData",
        "unknownMutationOutcome"
    ]);

    public static readonly IReadOnlyList<string> ExcludedOwnershipDomains = Array.AsReadOnly([
        "securitySession",
        "haAuthority",
        "driverProtocol",
        "internalExecution"
    ]);
}

public sealed class TimingPolicyV1Validator : IValidateOptions<TimingPolicyV1Options>
{
    public static readonly IReadOnlyList<int> DefaultReconnectDelays = Array.AsReadOnly([
        500,
        1_000,
        2_000,
        5_000,
        10_000,
        20_000,
        30_000
    ]);

    public static readonly TimingPolicyV1Limits Limits = new(
        new(10_000, 3_000, 30_000),
        new(30_000, 5_000, 120_000),
        new(45_000, 10_000, 180_000),
        new(120_000, 30_000, 600_000),
        new(15_000, 5_000, 60_000),
        new(500, 250, 30_000),
        0.20,
        0,
        0.50,
        new(15_000, 5_000, 120_000),
        new(45_000, 10_000, 120_000),
        new(2, 0, 3),
        new(30_000, 5_000, 120_000),
        new(2_000, 500, 10_000),
        new(10_000, 5_000, 120_000));

    public ValidateOptionsResult Validate(string? name, TimingPolicyV1Options options)
    {
        var failures = new List<string>();
        ValidateRange(options.ConnectBudgetMilliseconds, Limits.ConnectBudgetMilliseconds, nameof(options.ConnectBudgetMilliseconds), failures);
        ValidateRange(options.ReadBudgetMilliseconds, Limits.ReadBudgetMilliseconds, nameof(options.ReadBudgetMilliseconds), failures);
        ValidateRange(options.BootstrapBudgetMilliseconds, Limits.BootstrapBudgetMilliseconds, nameof(options.BootstrapBudgetMilliseconds), failures);
        ValidateRange(options.LongOperationBudgetMilliseconds, Limits.LongOperationBudgetMilliseconds, nameof(options.LongOperationBudgetMilliseconds), failures);
        ValidateRange(options.RealtimeConnectBudgetMilliseconds, Limits.RealtimeConnectBudgetMilliseconds, nameof(options.RealtimeConnectBudgetMilliseconds), failures);
        ValidateRange(options.RealtimeObservationMilliseconds, Limits.RealtimeObservationMilliseconds, nameof(options.RealtimeObservationMilliseconds), failures);
        ValidateRange(options.RealtimeStaleAfterMilliseconds, Limits.RealtimeStaleAfterMilliseconds, nameof(options.RealtimeStaleAfterMilliseconds), failures);
        ValidateRange(options.OrdinaryReadMaxRetries, Limits.OrdinaryReadMaxRetries, nameof(options.OrdinaryReadMaxRetries), failures);
        ValidateRange(options.CommandWriteResponseBudgetMilliseconds, Limits.CommandWriteResponseBudgetMilliseconds, nameof(options.CommandWriteResponseBudgetMilliseconds), failures);
        ValidateRange(options.SlowThresholdMilliseconds, Limits.SlowThresholdMilliseconds, nameof(options.SlowThresholdMilliseconds), failures);
        ValidateRange(options.StaleMinimumMilliseconds, Limits.StaleMinimumMilliseconds, nameof(options.StaleMinimumMilliseconds), failures);

        if (!double.IsFinite(options.ReconnectJitterRatio) || options.ReconnectJitterRatio is < 0 or > 0.50)
            failures.Add($"{nameof(options.ReconnectJitterRatio)} must be between 0 and 0.5.");

        var reconnectDelays = options.ReconnectDelayMilliseconds ?? DefaultReconnectDelays;
        if (reconnectDelays.Count == 0)
        {
            failures.Add($"{nameof(options.ReconnectDelayMilliseconds)} must contain at least one delay.");
        }
        else if (reconnectDelays.Count > 16)
        {
            failures.Add($"{nameof(options.ReconnectDelayMilliseconds)} must not contain more than 16 delays.");
        }
        else
        {
            for (var index = 0; index < reconnectDelays.Count; index++)
            {
                ValidateRange(
                    reconnectDelays[index],
                    Limits.ReconnectDelayMilliseconds,
                    $"{nameof(options.ReconnectDelayMilliseconds)}[{index}]",
                    failures);
                if (index > 0 && reconnectDelays[index] <= reconnectDelays[index - 1])
                    failures.Add($"{nameof(options.ReconnectDelayMilliseconds)} must be strictly increasing.");
            }
        }

        if (options.RealtimeStaleAfterMilliseconds < options.RealtimeObservationMilliseconds * 2L)
        {
            failures.Add(
                $"{nameof(options.RealtimeStaleAfterMilliseconds)} must be at least twice {nameof(options.RealtimeObservationMilliseconds)}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRange(
        int value,
        TimingValueBounds bounds,
        string name,
        ICollection<string> failures)
    {
        if (value < bounds.Minimum || value > bounds.Maximum)
            failures.Add($"{name} must be between {bounds.Minimum} and {bounds.Maximum}.");
    }
}

public static class TimingPolicyV1Configuration
{
    public const string SectionName = "TimingPolicyV1";

    internal static readonly IReadOnlySet<string> KnownKeys = new HashSet<string>(
        typeof(TimingPolicyV1Options).GetProperties().Select(property => property.Name),
        StringComparer.OrdinalIgnoreCase);

    public static void AddTimingPolicyV1(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(SectionName);
        RejectUnknownKeys(section);

        builder.Services
            .AddOptions<TimingPolicyV1Options>()
            .Bind(section, binder => binder.ErrorOnUnknownConfiguration = true)
            .ValidateOnStart();
        builder.Services.PostConfigure<TimingPolicyV1Options>(options =>
            options.ReconnectDelayMilliseconds ??= TimingPolicyV1Validator.DefaultReconnectDelays.ToArray());
        builder.Services.AddSingleton<IValidateOptions<TimingPolicyV1Options>, TimingPolicyV1Validator>();
    }

    internal static void RejectUnknownKeys(IConfigurationSection section)
    {
        var unknown = section.GetChildren()
            .Select(child => child.Key)
            .Where(key => !KnownKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length == 0) return;

        throw new InvalidOperationException(
            $"{SectionName} contains unsupported configuration key(s): {string.Join(", ", unknown)}. " +
            "Security sessions, HA authority, Driver protocol timing and internal execution budgets are not configurable through the adaptive WAN timing policy.");
    }
}

public static class TimingPolicyV1Endpoints
{
    public static IEndpointRouteBuilder MapTimingPolicyV1Endpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/system/timing-policy", (IOptions<TimingPolicyV1Options> options) =>
            Results.Ok(TimingPolicyV1Contract.Create(options.Value)));
        return endpoints;
    }
}
