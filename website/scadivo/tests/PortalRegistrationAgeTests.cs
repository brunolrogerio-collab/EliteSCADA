using System.Globalization;
using System.Text.Json;
using Xunit;

namespace Scadivo.Portal.Tests;

public sealed class PortalRegistrationAgeTests
{
    [Theory]
    [InlineData("2010-11-20", "2026-11-20T02:59:59Z", "registration-minimum-age")]
    [InlineData("2010-11-20", "2026-11-20T03:00:00Z", null)]
    [InlineData("2010-11-21", "2026-11-20T15:00:00Z", "registration-minimum-age")]
    [InlineData("2010-11-19", "2026-11-20T15:00:00Z", null)]
    [InlineData("2026-11-21", "2026-11-20T15:00:00Z", "registration-age-invalid")]
    [InlineData("2008-02-30", "2026-11-20T15:00:00Z", "registration-age-invalid")]
    [InlineData(null, "2026-11-20T15:00:00Z", "registration-age-required")]
    [InlineData("2008-02-29", "2024-02-28T15:00:00Z", "registration-minimum-age")]
    [InlineData("2008-02-29", "2024-02-29T15:00:00Z", null)]
    public void EnforcesSixteenCompletedYearsUsingTheSaoPauloCalendar(
        string? birthDate, string instant, string? expectedError)
    {
        var now = DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture);

        Assert.Equal(expectedError, PortalRegistrationAge.ErrorFor(JsonSerializer.SerializeToElement(birthDate), now));
    }

    [Fact]
    public void RejectsAnAgeFieldWithTheWrongJsonType()
    {
        Assert.Equal("registration-age-invalid", PortalRegistrationAge.ErrorFor(
            JsonSerializer.SerializeToElement(20081120), DateTimeOffset.UnixEpoch));
    }
}
