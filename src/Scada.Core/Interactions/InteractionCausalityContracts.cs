namespace Scada.Core.Interactions;

/// <summary>
/// Trusted Runtime origin classification for an interaction lineage. This is
/// bounded diagnostic context, not caller-supplied authorization identity.
/// </summary>
public enum InteractionOrigin
{
    Hmi,
    ClientScript,
    ServerScript,
    Automation,
    Driver,
    System
}

/// <summary>
/// Small protocol-neutral lineage envelope for Event/Command/Automation
/// diagnostics. Correlation groups related activity but never proves that one
/// physical process outcome was caused by another interaction.
/// </summary>
public sealed record InteractionCausalityContext(
    Guid CorrelationId,
    Guid? CausationId,
    InteractionOrigin Origin,
    int HopCount = 0,
    int MaximumHops = InteractionCausalityContract.DefaultMaximumHops);

public static class InteractionCausalityContract
{
    public const int Version = 1;
    public const int DefaultMaximumHops = 16;
    public const int AbsoluteMaximumHops = 64;

    public static InteractionCausalityContext Validate(InteractionCausalityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CorrelationId == Guid.Empty)
            throw new ArgumentException("Interaction CorrelationId is required.", nameof(context));
        if (context.CausationId == Guid.Empty)
            throw new ArgumentException("Interaction CausationId cannot be empty when present.", nameof(context));
        if (!Enum.IsDefined(typeof(InteractionOrigin), context.Origin))
            throw new ArgumentOutOfRangeException(nameof(context), context.Origin, "Unsupported interaction origin.");
        if (context.MaximumHops is < 1 or > AbsoluteMaximumHops)
            throw new ArgumentOutOfRangeException(
                nameof(context),
                $"Interaction MaximumHops must be in the range 1..{AbsoluteMaximumHops}.");
        if (context.HopCount < 0)
            throw new ArgumentOutOfRangeException(nameof(context), "Interaction HopCount cannot be negative.");
        if (context.HopCount > context.MaximumHops)
            throw new ArgumentOutOfRangeException(
                nameof(context),
                "Interaction HopCount cannot exceed MaximumHops.");

        return context;
    }
}
