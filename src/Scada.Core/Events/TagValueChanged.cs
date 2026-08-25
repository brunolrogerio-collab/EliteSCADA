using Scada.Core.Tags;

namespace Scada.Core.Events;

public sealed record TagValueChanged(
    TagDefinition Tag,
    TagValue? Previous,
    TagValue Current,
    DateTimeOffset OccurredAt) : IScadaEvent;
