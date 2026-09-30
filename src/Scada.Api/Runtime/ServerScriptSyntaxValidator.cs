using System.Diagnostics;
using System.Text.Json;

namespace Scada.Api.Runtime;

public sealed record ServerScriptSyntaxValidationRequest(string Source);

public sealed record ServerScriptSyntaxDiagnostic(
    string Severity,
    string Code,
    string Message,
    int Line,
    int Column,
    int? EndLine = null,
    int? EndColumn = null);

public sealed record ServerScriptSyntaxValidationResponse(
    IReadOnlyCollection<ServerScriptSyntaxDiagnostic> Diagnostics);

public sealed class ServerScriptSyntaxValidatorUnavailableException(string message) : Exception(message)
{
}

public static class ServerScriptSyntaxValidator
{
    private const int MaxSourceLength = 1_000_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ServerScriptSyntaxValidationResponse> ValidateAsync(
        string source,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        source ??= string.Empty;
        if (source.Length > MaxSourceLength)
            throw new ArgumentOutOfRangeException(nameof(source), $"Python source exceeds {MaxSourceLength} characters.");

        var pythonExecutable = configuration["ServerScripts:PythonExecutable"];
        if (string.IsNullOrWhiteSpace(pythonExecutable))
            pythonExecutable = OperatingSystem.IsWindows() ? "python" : "python3";

        var runnerPath = Path.Combine(AppContext.BaseDirectory, "ServerScriptRunner.py");
        if (!File.Exists(runnerPath))
            throw new ServerScriptSyntaxValidatorUnavailableException("Server Python validator is unavailable.");

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
                throw new ServerScriptSyntaxValidatorUnavailableException("Server Python validator could not be started.");
        }
        catch (Exception ex) when (ex is not ServerScriptSyntaxValidatorUnavailableException)
        {
            throw new ServerScriptSyntaxValidatorUnavailableException(
                $"Server Python validator is unavailable ({ex.GetType().Name}).");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            var payload = JsonSerializer.Serialize(new { validateOnly = true, source }, JsonOptions);
            await process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token);
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
                throw new ServerScriptSyntaxValidatorUnavailableException(
                    string.IsNullOrWhiteSpace(stderr) ? "Server Python validator failed." : "Server Python validator failed safely.");

            var result = JsonSerializer.Deserialize<ServerScriptSyntaxValidationResponse>(stdout, JsonOptions);
            return result ?? throw new ServerScriptSyntaxValidatorUnavailableException(
                "Server Python validator returned an invalid result.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new ServerScriptSyntaxValidatorUnavailableException("Server Python validation timed out.");
        }
        catch (JsonException)
        {
            throw new ServerScriptSyntaxValidatorUnavailableException(
                "Server Python validator returned an invalid result.");
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested) TryKill(process);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort after timeout/cancellation.
        }
    }
}
