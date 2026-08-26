namespace Scada.Security.Audit;

public enum AuditOutcome
{
    Succeeded,
    Denied,
    Failed
}

public static class AuditActions
{
    public const string TagWrite = "tag.write";
    public const string CommandExecute = "command.execute";
    public const string AlarmAcknowledge = "alarm.acknowledge";
    public const string AlarmShelve = "alarm.shelve";
    public const string EngineeringImportApply = "engineering.import.apply";
    public const string EngineeringDelete = "engineering.delete";
    public const string EngineeringBulkApply = "engineering.bulk.apply";
    public const string EngineeringPackageRestore = "engineering.package.restore";
    public const string EngineeringCheckout = "engineering.checkout";
    public const string EngineeringSave = "engineering.save";
    public const string EngineeringPublish = "engineering.publish";
    public const string EngineeringActivate = "engineering.activate";
    public const string AuditRead = "audit.read";
    public const string UserRoleManage = "user-role.manage";
}

public sealed record AuditEvent(
    Guid Id,
    DateTimeOffset TimestampUtc,
    string SubjectId,
    string? DisplayName,
    string Action,
    AuditOutcome Outcome,
    string TargetKind,
    string TargetId,
    IReadOnlyDictionary<string, string>? Details = null,
    string? CorrelationId = null)
{
    public static AuditEvent Create(
        string subjectId,
        string? displayName,
        string action,
        AuditOutcome outcome,
        string targetKind,
        string targetId,
        IReadOnlyDictionary<string, string>? details = null,
        string? correlationId = null) =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            subjectId,
            displayName,
            action,
            outcome,
            targetKind,
            targetId,
            details,
            correlationId);
}
