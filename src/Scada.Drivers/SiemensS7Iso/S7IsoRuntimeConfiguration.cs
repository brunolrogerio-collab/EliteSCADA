using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

/// <summary>
/// Protocol-owned runtime configuration bridge. It deliberately reuses the same
/// validation/parsing authority as the Engineering adapter so runtime planning
/// cannot drift into a second interpretation of Siemens Data Source settings.
/// </summary>
public static class S7IsoRuntimeConfiguration
{
    public static bool TryCreateOptions(
        IReadOnlyDictionary<string, string> settings,
        out S7IsoConnectionOptions? options,
        out IReadOnlyCollection<DriverEngineeringIssue> issues) =>
        S7IsoEngineeringAdapter.TryCreateOptions(settings, out options, out issues);
}
