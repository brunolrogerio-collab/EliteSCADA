using Scada.Core.Tags;

namespace Scada.Historian.Abstractions;

public interface IHistorian : IAsyncDisposable
{
    long WrittenSamples { get; }
    long PendingSamples { get; }
    long AcceptedSamples { get; }
    long SkippedSamples { get; }
    long CoalescedSamples { get; }
    IReadOnlyList<TagValue> Query(Guid tagId, DateTimeOffset from, DateTimeOffset to, int limit = 5000);
}
