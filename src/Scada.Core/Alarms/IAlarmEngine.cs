namespace Scada.Core.Alarms;

public interface IAlarmEngine : IDisposable
{
    AlarmDefinition Register(AlarmDefinition definition);
    IReadOnlyCollection<AlarmDefinition> Definitions();
    IReadOnlyCollection<AlarmInstance> Snapshot(bool activeOnly = false);
    ValueTask<bool> AcknowledgeAsync(Guid definitionId, string user, CancellationToken cancellationToken = default);
}
