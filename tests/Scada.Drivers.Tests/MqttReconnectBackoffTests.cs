using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttReconnectBackoffTests
{
    [Fact]
    public void JitterNeverRunsEarlierThanBaseOrPastMaximum()
    {
        var minimum = TimeSpan.FromSeconds(1);
        var maximum = TimeSpan.FromSeconds(30);
        var backoff = new MqttReconnectBackoff(minimum, maximum, seed: 0x1234UL);

        var bases = new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(16),
            TimeSpan.FromSeconds(30)
        };

        foreach (var baseDelay in bases)
        {
            for (var sample = 0; sample < 32; sample++)
            {
                var actual = backoff.ApplyJitter(baseDelay);
                var expectedCeilingTicks = Math.Min(
                    maximum.Ticks,
                    baseDelay.Ticks + baseDelay.Ticks * MqttReconnectBackoff.MaximumJitterPercent / 100);

                Assert.True(actual >= baseDelay);
                Assert.True(actual <= TimeSpan.FromTicks(expectedCeilingTicks));
                Assert.True(actual <= maximum);
            }
        }
    }

    [Fact]
    public void BaseDelayDoublesAndCapsAtConfiguredMaximum()
    {
        var backoff = new MqttReconnectBackoff(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10),
            seed: 1UL);

        var current = TimeSpan.FromMilliseconds(10);
        current = backoff.NextBaseDelay(current);
        Assert.Equal(TimeSpan.FromSeconds(2), current);

        current = backoff.NextBaseDelay(current);
        Assert.Equal(TimeSpan.FromSeconds(4), current);

        current = backoff.NextBaseDelay(current);
        Assert.Equal(TimeSpan.FromSeconds(8), current);

        current = backoff.NextBaseDelay(current);
        Assert.Equal(TimeSpan.FromSeconds(10), current);

        current = backoff.NextBaseDelay(current);
        Assert.Equal(TimeSpan.FromSeconds(10), current);
    }

    [Fact]
    public void FixedSeedProducesDeterministicSequence()
    {
        var first = new MqttReconnectBackoff(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            seed: 0xA5A5A5A5UL);
        var second = new MqttReconnectBackoff(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            seed: 0xA5A5A5A5UL);

        var baseDelay = TimeSpan.FromSeconds(4);
        var firstSequence = Enumerable.Range(0, 16)
            .Select(_ => first.ApplyJitter(baseDelay))
            .ToArray();
        var secondSequence = Enumerable.Range(0, 16)
            .Select(_ => second.ApplyJitter(baseDelay))
            .ToArray();

        Assert.Equal(firstSequence, secondSequence);
    }

    [Fact]
    public void DifferentSeedsDoNotStayPhaseLocked()
    {
        var first = new MqttReconnectBackoff(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            seed: 0x11111111UL);
        var second = new MqttReconnectBackoff(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            seed: 0x22222222UL);

        var baseDelay = TimeSpan.FromSeconds(8);
        var firstSequence = Enumerable.Range(0, 16)
            .Select(_ => first.ApplyJitter(baseDelay))
            .ToArray();
        var secondSequence = Enumerable.Range(0, 16)
            .Select(_ => second.ApplyJitter(baseDelay))
            .ToArray();

        Assert.False(firstSequence.SequenceEqual(secondSequence));
    }

    [Fact]
    public void ArithmeticRemainsBoundedNearTimeSpanMaximum()
    {
        var minimum = TimeSpan.FromTicks(long.MaxValue / 4);
        var maximum = TimeSpan.MaxValue;
        var backoff = new MqttReconnectBackoff(minimum, maximum, seed: 0x7777UL);

        for (var sample = 0; sample < 32; sample++)
        {
            var delay = backoff.ApplyJitter(minimum);
            Assert.True(delay >= minimum);
            Assert.True(delay <= maximum);
        }

        Assert.Equal(TimeSpan.FromTicks(long.MaxValue / 2), backoff.NextBaseDelay(minimum));
        Assert.Equal(TimeSpan.MaxValue, backoff.NextBaseDelay(TimeSpan.FromTicks(long.MaxValue / 2 + 1)));
    }

    [Fact]
    public void InvalidBoundsFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MqttReconnectBackoff(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            seed: 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MqttReconnectBackoff(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            seed: 1UL));
    }
}
