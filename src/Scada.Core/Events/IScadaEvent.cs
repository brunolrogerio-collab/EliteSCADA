namespace Scada.Core.Events;

public interface IScadaEvent
{
    DateTimeOffset OccurredAt { get; }
}
