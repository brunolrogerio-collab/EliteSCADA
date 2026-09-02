using System.Buffers;
using System.Text;
using Scada.Engineering.ProjectPackages;

namespace Scada.Api.Runtime;

public sealed class EngineeringImportBodyTooLargeException(int limitBytes)
    : IOException("Engineering import payload exceeds its safety limit.")
{
    public int LimitBytes { get; } = limitBytes;
}

public sealed class EngineeringImportBodyEncodingException : IOException
{
    public EngineeringImportBodyEncodingException(Exception innerException)
        : base("Engineering import payload is not valid UTF-8.", innerException)
    {
    }
}

public static class EngineeringImportBodyReader
{
    public const int MaximumJsonBytes = ProjectPackageService.MaximumEngineeringBytes;
    public const int MaximumCsvBytes = 16 * 1024 * 1024;

    private const int BufferSize = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static Task<string> ReadJsonAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default) =>
        ReadAsync(request, MaximumJsonBytes, cancellationToken);

    public static Task<string> ReadCsvAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default) =>
        ReadAsync(request, MaximumCsvBytes, cancellationToken);

    private static async Task<string> ReadAsync(
        HttpRequest request,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength is long contentLength &&
            (contentLength < 0 || contentLength > maximumBytes))
            throw new EngineeringImportBodyTooLargeException(maximumBytes);

        var initialCapacity = request.ContentLength.HasValue
            ? checked((int)request.ContentLength.Value)
            : 0;
        using var payload = new MemoryStream(initialCapacity);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            while (true)
            {
                var read = await request.Body.ReadAsync(
                    buffer.AsMemory(0, BufferSize),
                    cancellationToken);
                if (read == 0) break;
                if (payload.Length + read > maximumBytes)
                    throw new EngineeringImportBodyTooLargeException(maximumBytes);
                payload.Write(buffer, 0, read);
            }

            try
            {
                return StrictUtf8.GetString(payload.GetBuffer(), 0, checked((int)payload.Length));
            }
            catch (DecoderFallbackException ex)
            {
                throw new EngineeringImportBodyEncodingException(ex);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
