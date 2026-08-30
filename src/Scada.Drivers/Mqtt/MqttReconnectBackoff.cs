using System.Security.Cryptography;

namespace Scada.Drivers.Mqtt;

/// <summary>
/// Per-runtime reconnect backoff policy. Exponential base delay is preserved while
/// a bounded positive jitter spreads retries from independently started clients.
/// Jitter never schedules earlier than the configured base delay and never exceeds
/// the configured maximum delay.
/// </summary>
internal sealed class MqttReconnectBackoff
{
    internal const int MaximumJitterPercent = 25;

    private readonly TimeSpan _minimum;
    private readonly TimeSpan _maximum;
    private ulong _state;

    public MqttReconnectBackoff(TimeSpan minimum, TimeSpan maximum, ulong? seed = null)
    {
        if (minimum <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        if (maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(maximum));

        _minimum = minimum;
        _maximum = maximum;
        _state = seed ?? CreateRandomSeed();
        if (_state == 0)
            _state = 0x9E3779B97F4A7C15UL;
    }

    public TimeSpan ApplyJitter(TimeSpan baseDelay)
    {
        var normalized = ClampBaseDelay(baseDelay);
        if (normalized >= _maximum)
            return _maximum;

        var maximumJitterTicks = Math.Min(
            _maximum.Ticks - normalized.Ticks,
            Math.Max(1L, normalized.Ticks * MaximumJitterPercent / 100));

        var jitterTicks = maximumJitterTicks == long.MaxValue
            ? (long)(NextUInt64() & 0x7FFF_FFFF_FFFF_FFFFUL)
            : (long)(NextUInt64() % ((ulong)maximumJitterTicks + 1UL));

        return TimeSpan.FromTicks(normalized.Ticks + jitterTicks);
    }

    public TimeSpan NextBaseDelay(TimeSpan current)
    {
        var normalized = ClampBaseDelay(current);
        var doubledTicks = normalized.Ticks > long.MaxValue / 2
            ? long.MaxValue
            : normalized.Ticks * 2;
        return TimeSpan.FromTicks(Math.Min(doubledTicks, _maximum.Ticks));
    }

    private TimeSpan ClampBaseDelay(TimeSpan value)
    {
        if (value <= _minimum) return _minimum;
        if (value >= _maximum) return _maximum;
        return value;
    }

    private ulong NextUInt64()
    {
        // xorshift64* gives a compact deterministic stream for scheduling jitter.
        // This is deliberately not used for security-sensitive material.
        var state = _state;
        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        _state = state;
        return state * 0x2545F4914F6CDD1DUL;
    }

    private static ulong CreateRandomSeed()
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(buffer);
        return BitConverter.ToUInt64(buffer);
    }
}
