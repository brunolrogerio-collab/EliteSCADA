namespace Scada.Security.Authorization;

/// <summary>
/// Stable public vocabulary for Authority capability exchange. Public IDs are deliberately
/// mapped explicitly so adding enum members cannot change an existing wire identity.
/// </summary>
public static class AuthorityPolicyContract
{
    public const string Schema = "elitescada.authority-policy";
    public const int SchemaVersion = 1;

    private static readonly IReadOnlyDictionary<SecurityCapability, string> IdByCapability =
        new Dictionary<SecurityCapability, string>
        {
            [SecurityCapability.View] = "View",
            [SecurityCapability.TagRead] = "TagRead",
            [SecurityCapability.CommandExecute] = "CommandExecute",
            [SecurityCapability.ProcessValueWrite] = "ProcessValueWrite",
            [SecurityCapability.AlarmAcknowledge] = "AlarmAcknowledge",
            [SecurityCapability.AlarmShelve] = "AlarmShelve",
            [SecurityCapability.TrendUse] = "TrendUse",
            [SecurityCapability.TrendSave] = "TrendSave",
            [SecurityCapability.EngineeringModify] = "EngineeringModify",
            [SecurityCapability.UserRoleAdmin] = "UserRoleAdmin",
            [SecurityCapability.SystemAdmin] = "SystemAdmin",
            [SecurityCapability.EngineeringView] = "EngineeringView",
            [SecurityCapability.HighAvailabilityObserve] = "HighAvailabilityObserve",
            [SecurityCapability.HighAvailabilityTransfer] = "HighAvailabilityTransfer",
            [SecurityCapability.HighAvailabilityAdmin] = "HighAvailabilityAdmin"
        };

    private static readonly IReadOnlyDictionary<string, SecurityCapability> CapabilityById =
        IdByCapability.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<SecurityCapability, string> EngineeringWireIdByCapability =
        new Dictionary<SecurityCapability, string>
        {
            [SecurityCapability.View] = "view",
            [SecurityCapability.TagRead] = "tagRead",
            [SecurityCapability.CommandExecute] = "commandExecute",
            [SecurityCapability.ProcessValueWrite] = "processValueWrite",
            [SecurityCapability.AlarmAcknowledge] = "alarmAcknowledge",
            [SecurityCapability.AlarmShelve] = "alarmShelve",
            [SecurityCapability.TrendUse] = "trendUse",
            [SecurityCapability.TrendSave] = "trendSave",
            [SecurityCapability.EngineeringModify] = "engineeringModify",
            [SecurityCapability.UserRoleAdmin] = "userRoleAdmin",
            [SecurityCapability.SystemAdmin] = "systemAdmin",
            [SecurityCapability.EngineeringView] = "engineeringView",
            [SecurityCapability.HighAvailabilityObserve] = "highAvailabilityObserve",
            [SecurityCapability.HighAvailabilityTransfer] = "highAvailabilityTransfer",
            [SecurityCapability.HighAvailabilityAdmin] = "highAvailabilityAdmin"
        };

    public static IReadOnlyCollection<string> CapabilityIds { get; } =
        Array.AsReadOnly(Enum.GetValues<SecurityCapability>()
            .Select(GetCapabilityId)
            .ToArray());

    public static string GetCapabilityId(SecurityCapability capability) =>
        IdByCapability.TryGetValue(capability, out var id)
            ? id
            : throw new ArgumentOutOfRangeException(nameof(capability), capability, "Unknown Authority capability.");

    public static string GetEngineeringWireId(SecurityCapability capability) =>
        EngineeringWireIdByCapability.TryGetValue(capability, out var id)
            ? id
            : throw new ArgumentOutOfRangeException(nameof(capability), capability, "Unknown Authority capability.");

    public static bool TryParseCapabilityId(string? id, out SecurityCapability capability)
    {
        capability = default;
        return !string.IsNullOrWhiteSpace(id) &&
               CapabilityById.TryGetValue(id.Trim(), out capability);
    }
}
