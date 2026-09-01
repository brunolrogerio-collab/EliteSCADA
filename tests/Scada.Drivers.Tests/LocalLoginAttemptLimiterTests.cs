using Scada.Api.Security;

namespace Scada.Drivers.Tests;

public sealed class LocalLoginAttemptLimiterTests
{
    [Fact]
    public void TryAcquire_ReclaimsExpiredKeysWithoutWeakeningActiveLimit()
    {
        var limiter = new LocalLoginAttemptLimiter(
            permitLimit: 2,
            window: TimeSpan.FromMinutes(1),
            cleanupInterval: TimeSpan.FromSeconds(10));
        var startedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.True(limiter.TryAcquire("stale-client", startedAt));

        var activeAt = startedAt.AddSeconds(30);
        Assert.True(limiter.TryAcquire("active-client", activeAt));
        Assert.True(limiter.TryAcquire("active-client", activeAt));
        Assert.False(limiter.TryAcquire("active-client", activeAt));
        Assert.Equal(2, limiter.TrackedKeyCount);

        var cleanupAt = startedAt.AddSeconds(61);
        Assert.True(limiter.TryAcquire("cleanup-trigger", cleanupAt));

        Assert.Equal(2, limiter.TrackedKeyCount);
        Assert.False(limiter.TryAcquire("active-client", cleanupAt));
    }

    [Fact]
    public async Task TryAcquire_ExpiredKeyStartsOneSharedWindowUnderConcurrency()
    {
        const int permitLimit = 5;
        var limiter = new LocalLoginAttemptLimiter(
            permitLimit,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(10));
        var startedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        for (var attempt = 0; attempt < permitLimit; attempt++)
            Assert.True(limiter.TryAcquire("shared-client", startedAt));
        Assert.False(limiter.TryAcquire("shared-client", startedAt));

        var nextWindow = startedAt.AddMinutes(1);
        var attempts = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => limiter.TryAcquire("shared-client", nextWindow)))
            .ToArray();
        var results = await Task.WhenAll(attempts);

        Assert.Equal(permitLimit, results.Count(result => result));
        Assert.Equal(1, limiter.TrackedKeyCount);
        Assert.False(limiter.TryAcquire("shared-client", nextWindow));
    }
}
