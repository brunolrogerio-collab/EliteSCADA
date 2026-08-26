namespace Scada.Engineering.VisualScripting;

public enum VisualTweenEasing
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut
}

public enum VisualTweenConflictBehavior
{
    ReplaceExisting,
    RejectIfActive
}

public enum VisualTweenCompletionReason
{
    Completed,
    Cancelled,
    Replaced,
    Faulted
}

public sealed record VisualTweenRequest(
    string PropertyKey,
    VisualPropertyValue TargetValue,
    TimeSpan Duration,
    VisualTweenEasing Easing = VisualTweenEasing.Linear,
    int RepeatCount = 0,
    bool PingPong = false,
    VisualTweenConflictBehavior ConflictBehavior = VisualTweenConflictBehavior.ReplaceExisting)
{
    public void ValidateFor(VisualObjectPropertySchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(TargetValue);

        if (string.IsNullOrWhiteSpace(PropertyKey))
            throw new ArgumentException("Tween property key is required.", nameof(PropertyKey));

        if (Duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Duration), Duration, "Tween duration must be greater than zero.");

        if (RepeatCount < 0)
            throw new ArgumentOutOfRangeException(nameof(RepeatCount), RepeatCount, "Tween repeat count cannot be negative.");

        var property = schema.GetRequired(PropertyKey);
        if (!property.Animatable)
            throw new InvalidOperationException($"Property '{PropertyKey}' is not animatable.");

        property.ValidateValue(TargetValue);
    }
}

public readonly record struct VisualTweenHandle(Guid Value)
{
    public static VisualTweenHandle New() => new(Guid.NewGuid());
}

public sealed record VisualTweenCompletion(
    VisualTweenHandle Handle,
    string RuntimeInstanceId,
    string ObjectId,
    string PropertyKey,
    VisualTweenCompletionReason Reason,
    DateTimeOffset CompletedAt,
    string? SanitizedError = null);

/// <summary>
/// Renderer-facing scheduler contract. Implementations are expected to use native renderer animation/tween
/// primitives rather than driving every frame through Python.
/// </summary>
public interface IVisualTweenScheduler
{
    ValueTask<VisualTweenHandle> StartAsync(
        string runtimeInstanceId,
        string objectId,
        VisualObjectPropertySchema schema,
        VisualTweenRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<bool> CancelAsync(
        VisualTweenHandle handle,
        CancellationToken cancellationToken = default);

    event Action<VisualTweenCompletion>? Completed;
}
