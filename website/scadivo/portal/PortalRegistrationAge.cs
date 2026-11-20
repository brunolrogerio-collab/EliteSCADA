using System.Globalization;
using System.Text.Json;

namespace Scadivo.Portal;

internal static class PortalRegistrationAge
{
    private const int MinimumAge = 16;

    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("Portal:RegistrationAgeCheckEnabled");

    public static string? ErrorFor(JsonElement declaredDate, DateTimeOffset now)
    {
        if (declaredDate.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return "registration-age-required";
        if (declaredDate.ValueKind != JsonValueKind.String ||
            !DateOnly.TryParseExact(declaredDate.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            return "registration-age-invalid";
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "America/Sao_Paulo").DateTime);
        if (birthDate > today) return "registration-age-invalid";
        return birthDate > today.AddYears(-MinimumAge) ? "registration-minimum-age" : null;
    }
}
