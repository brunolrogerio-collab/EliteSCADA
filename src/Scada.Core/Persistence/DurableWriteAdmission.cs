namespace Scada.Core.Persistence;

public interface IDurableWriteAdmission
{
    ValueTask<IAsyncDisposable> AcquireAsync(
        string writer,
        CancellationToken cancellationToken = default);
}

public sealed class DurableWriteQuiescedException : InvalidOperationException
{
    public DurableWriteQuiescedException(string writer, Guid operationId)
        : base($"Durable writer '{writer}' is quiesced by database maintenance operation '{operationId:D}'.")
    {
        Writer = writer;
        OperationId = operationId;
    }

    public string Writer { get; }
    public Guid OperationId { get; }
}
