using System.Text.Json;

namespace Scada.Api.Runtime;

public interface IRuntimeHaProtectionOperationHistoryStore
{
    IReadOnlyCollection<RuntimeHaProtectionOperation> Load();
    void Save(IReadOnlyCollection<RuntimeHaProtectionOperation> operations);
}

/// <summary>
/// Stores the small, node-local HA operation history outside process memory. Each HA node
/// uses its own app-data path; this is diagnostic history, not a shared authority store.
/// </summary>
public sealed class FileRuntimeHaProtectionOperationHistoryStore(
    string path,
    Func<DateTimeOffset>? utcNow = null) : IRuntimeHaProtectionOperationHistoryStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path = Path.GetFullPath(path);
    private readonly Func<DateTimeOffset> _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    private readonly object _gate = new();

    public IReadOnlyCollection<RuntimeHaProtectionOperation> Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return Array.Empty<RuntimeHaProtectionOperation>();

            var json = File.ReadAllText(_path);
            var operations = JsonSerializer.Deserialize<RuntimeHaProtectionOperation[]>(json, Json)
                ?? Array.Empty<RuntimeHaProtectionOperation>();
            var recoveredAt = _utcNow();
            var recovered = operations
                .OrderByDescending(operation => operation.StartedAtUtc)
                .Take(32)
                .Select(operation => operation.State.Equals("running", StringComparison.OrdinalIgnoreCase)
                    ? operation with
                    {
                        State = "interrupted",
                        CompletedAtUtc = recoveredAt,
                        ReasonCode = "operation-status-no-longer-available"
                    }
                    : operation)
                .ToArray();

            if (recovered.Any(operation => operation.State == "interrupted" &&
                                           operations.Any(original => original.OperationId == operation.OperationId && original.State == "running")))
                Save(recovered);

            return recovered;
        }
    }

    public void Save(IReadOnlyCollection<RuntimeHaProtectionOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    operations.OrderByDescending(operation => operation.StartedAtUtc).Take(32),
                    Json);
                using (var stream = new FileStream(
                           tempPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           bufferSize: 4096,
                           FileOptions.WriteThrough))
                {
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(tempPath, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
