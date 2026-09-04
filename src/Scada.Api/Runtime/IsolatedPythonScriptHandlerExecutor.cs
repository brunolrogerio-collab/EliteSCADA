using System.Diagnostics;
using System.Text.Json;
using Scada.Core.Sources;
using Scada.Core.Tags;
using Scada.Engineering.VisualScripting;

namespace Scada.Api.Runtime;

/// <summary>
/// Runs the deterministic Server Script Python subset in an isolated process.
/// The child receives only declared TAG values and normalized event metadata.
/// Reads and requested writes are revision-bound by ServerScriptRuntimeManager and
/// replayed through the official runtime coordinator rather than any Driver internals.
/// </summary>
public sealed class IsolatedPythonScriptHandlerExecutor(
    ServerScriptRuntimeManager host,
    string projectKey,
    long revision,
    string pythonExecutable,
    string runnerPath) : IPythonScriptHandlerExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask ExecuteAsync(
        PythonScriptDefinition script,
        ScriptEventEnvelope scriptEvent,
        ScriptExecutionLease lease)
    {
        if (script.Scope != PythonScriptScope.Server)
            throw new ScriptExecutionDiagnosticException("Only Server scripts may execute in the server runtime host.");

        var allowedTags = ResolveAllowedTags(script);
        var snapshots = await host.ReadDependenciesAsync(
                projectKey,
                revision,
                allowedTags,
                lease.CancellationToken)
            .ConfigureAwait(false);

        var values = snapshots.ToDictionary(
            pair => pair.Key.ToString("D"),
            pair => pair.Value.Value,
            StringComparer.Ordinal);
        var serverMemoryTags = allowedTags
            .Where(pair => pair.Value.Equals("ServerMemoryTag", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key.ToString("D"))
            .ToArray();

        var request = new PythonExecutionRequest(
            script.Source,
            scriptEvent.Identity.HandlerName,
            new PythonEventPayload(
                scriptEvent.Identity.EventKind.ToString(),
                scriptEvent.Identity.TargetReference,
                scriptEvent.Sequence,
                scriptEvent.EnqueuedAt),
            values,
            serverMemoryTags);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-I");
        process.StartInfo.ArgumentList.Add("-S");
        process.StartInfo.ArgumentList.Add(runnerPath);

        try
        {
            if (!process.Start())
                throw new ScriptExecutionDiagnosticException("Python runtime could not be started.");
        }
        catch (Exception ex) when (ex is not ScriptExecutionDiagnosticException)
        {
            throw new ScriptExecutionDiagnosticException($"Python runtime is unavailable ({ex.GetType().Name}).");
        }

        await process.StandardInput.WriteAsync(
            JsonSerializer.Serialize(request, JsonOptions).AsMemory(),
            lease.CancellationToken);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(lease.CancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(lease.CancellationToken);
        try
        {
            await process.WaitForExitAsync(lease.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new ScriptExecutionDiagnosticException(Sanitize(stderr));

        PythonExecutionResponse response;
        try
        {
            response = JsonSerializer.Deserialize<PythonExecutionResponse>(stdout, JsonOptions)
                ?? throw new InvalidDataException();
        }
        catch
        {
            throw new ScriptExecutionDiagnosticException("Python handler returned an invalid result payload.");
        }

        if (!response.Succeeded)
            throw new ScriptExecutionDiagnosticException(response.Error ?? "Python handler failed.");

        foreach (var write in response.Writes ?? Array.Empty<PythonWriteRequest>())
        {
            lease.CancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(write.TagId, out var tagId) ||
                !allowedTags.TryGetValue(tagId, out var dependencyKind) ||
                !snapshots.TryGetValue(tagId, out var snapshot))
            {
                throw new ScriptExecutionDiagnosticException(
                    "Python handler attempted to write an undeclared TAG dependency.");
            }

            if (write.ServerMemoryOnly &&
                !dependencyKind.Equals("ServerMemoryTag", StringComparison.OrdinalIgnoreCase))
            {
                throw new ScriptExecutionDiagnosticException(
                    "write_server_memory requires an explicit ServerMemoryTag dependency.");
            }

            var convertedValue = ConvertValue(write.Value, snapshot.DataType);
            object? runtimeValue = convertedValue;
            if (write.Quality is not null)
            {
                if (!write.ServerMemoryOnly ||
                    !dependencyKind.Equals("ServerMemoryTag", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ScriptExecutionDiagnosticException(
                        "Qualified samples require an explicit ServerMemoryTag dependency.");
                }

                if (!Enum.TryParse<TagQuality>(write.Quality, ignoreCase: false, out var quality) ||
                    !Enum.IsDefined(typeof(TagQuality), quality))
                {
                    throw new ScriptExecutionDiagnosticException(
                        $"Quality '{write.Quality}' is not a canonical TagQuality value.");
                }

                runtimeValue = new QualifiedSourceSample(convertedValue, quality);
            }

            await host.WriteTagAsync(
                    projectKey,
                    revision,
                    tagId,
                    runtimeValue,
                    write.ServerMemoryOnly,
                    lease.CancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static Dictionary<Guid, string> ResolveAllowedTags(PythonScriptDefinition script)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var dependency in script.Dependencies)
        {
            if (!dependency.Kind.Equals("Tag", StringComparison.OrdinalIgnoreCase) &&
                !dependency.Kind.Equals("ServerMemoryTag", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Guid.TryParse(dependency.StableReference, out var tagId) || tagId == Guid.Empty)
            {
                throw new ScriptExecutionDiagnosticException(
                    "Server Script contains an invalid stable TAG dependency.");
            }

            result[tagId] = dependency.Kind;
        }

        return result;
    }

    private static object? ConvertValue(JsonElement value, TagDataType dataType) => dataType switch
    {
        TagDataType.Boolean => value.GetBoolean(),
        TagDataType.Int16 => value.GetInt16(),
        TagDataType.Int32 => value.GetInt32(),
        TagDataType.Int64 => value.GetInt64(),
        TagDataType.Float => value.GetSingle(),
        TagDataType.Double => value.GetDouble(),
        TagDataType.String => value.ValueKind == JsonValueKind.Null ? null : value.GetString(),
        TagDataType.DateTime => value.GetDateTimeOffset(),
        TagDataType.Enum => value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetInt32(),
        _ => throw new InvalidOperationException($"Unsupported TAG data type '{dataType}'.")
    };

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort after cancellation. The coordinator records cancellation/timeout.
        }
    }

    private static string Sanitize(string error)
    {
        var line = error.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(line))
            return "Python handler failed.";
        return line.Length <= 240 ? line : line[..240];
    }

    private sealed record PythonExecutionRequest(
        string Source,
        string Handler,
        PythonEventPayload Event,
        IReadOnlyDictionary<string, object?> Values,
        IReadOnlyCollection<string> ServerMemoryTagIds);

    private sealed record PythonEventPayload(
        string Kind,
        string? TargetReference,
        long Sequence,
        DateTimeOffset EnqueuedAt);

    private sealed record PythonWriteRequest(
        string TagId,
        JsonElement Value,
        bool ServerMemoryOnly,
        string? Quality = null);

    private sealed record PythonExecutionResponse(
        bool Succeeded,
        string? Error,
        IReadOnlyCollection<PythonWriteRequest>? Writes);
}
