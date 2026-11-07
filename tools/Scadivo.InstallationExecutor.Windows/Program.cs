using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Scadivo.InstallationExecutor.Windows;

// This project runs as a separate Windows host service, never as part of Web/API.
// All scripts, service identities and paths originate from a host ACL-protected
// registration. HTTP clients can supply only a versioned module/port plan ID.
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Windows host executor only");
            if (args.Length < 2 || args[0] != "--registration")
                throw new InvalidOperationException("Usage: --registration FILE [--status | --recover --confirm-recovery]");
            var registration = Path.GetFullPath(args[1]);
            if (args.Length > 2)
            {
                var route = args.Length == 3 && args[2] == "--status" ? "/v1/status" :
                    args.Length == 4 && args[2] == "--recover" && args[3] == "--confirm-recovery" ? "/v1/recover" :
                    throw new InvalidOperationException("Only protected status or confirmed recovery is permitted");
                Console.WriteLine(await WindowsExecutor.AdministratorRequestAsync(registration, route));
                return 0;
            }
            using var executor = new WindowsExecutor(registration);
            await executor.ServeAsync(CancellationToken.None);
            return 0;
        }
        catch (Exception error)
        {
            // Never print configuration values, paths, service names or secrets.
            Console.Error.WriteLine("SCADIVO Windows installation host executor unavailable: " + error.GetType().Name);
            return 1;
        }
    }
}

internal sealed record Registration(
    string Schema, string Profile, string InstanceId, string PipeName, string ApiSid,
    string StateDirectory, string NativeProfilePath, string TokenFile,
    string AdapterDirectory, string? PythonExecutable,
    string? HttpsListenAddress = null, int? HttpsListenPort = null,
    string? HttpsCertificatePfx = null, string? HttpsCertificatePasswordFile = null);

internal sealed record PortPlan(
    string Id, string Module, Dictionary<string, int> Ports, Dictionary<string, int> Before,
    string Revision, long ExpiresAt);
internal sealed record Operation(
    string Id, string Module, string State, Dictionary<string, int> Before,
    Dictionary<string, int> Desired, long StartedAt, long? FinishedAt, string Detail);

internal sealed class WindowsExecutor : IDisposable
{
    public const string Schema = "scadivo-network-host-executor-v1";
    public const string PlanSchema = "scadivo-network-port-plan-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, string[]> Fields = new()
    {
        ["access"] = ["webPort", "apiPort"],
        ["database"] = ["databasePort"],
        ["media"] = ["mediaControlPort", "mediaPlaybackPort"]
    };
    private static readonly HashSet<string> AllowedAudit =
        ["preflight", "apply-started", "applied", "recovery-required", "recovery-started", "recovered"];
    private readonly Registration _registration;
    private readonly byte[] _token;
    private readonly PipeSecurity _pipeAcl;
    private readonly string _nativeConfiguration;
    private readonly string _serviceName;
    private readonly bool _dockerDesktop;
    private readonly string? _dockerCompose;
    private readonly string? _dockerOverride;
    private readonly string _statePath;
    private readonly string _planPath;
    private readonly string _auditPath;
    private readonly FileStream _processLock;
    private readonly object _guard = new();

    public WindowsExecutor(string registrationPath)
    {
        CheckProtected(registrationPath, directory: false);
        _registration = JsonSerializer.Deserialize<Registration>(File.ReadAllText(registrationPath), JsonOptions)
            ?? throw new InvalidOperationException("Missing host registration");
        _dockerDesktop = _registration.Profile == "stable-docker-windows";
        if (_registration.Schema != Schema || (_registration.Profile != "native-windows" && !_dockerDesktop) ||
            !Regex.IsMatch(_registration.InstanceId ?? "", @"\A[a-z0-9][a-z0-9-]{0,47}\z") ||
            !Regex.IsMatch(_registration.PipeName ?? "", @"\A[a-zA-Z0-9_-]{1,128}\z"))
            throw new InvalidOperationException("Invalid Windows installation registration");
        if (!Regex.IsMatch(_registration.ApiSid ?? "", @"\AS-1-[0-9]+(-[0-9]+)+\z"))
            throw new InvalidOperationException("Invalid registered API service SID");
        var apiSid = new SecurityIdentifier(_registration.ApiSid ?? throw new InvalidOperationException("Missing dedicated API SID"));
        var broad = new HashSet<string> { "S-1-1-0", "S-1-5-11", "S-1-5-32-545", "S-1-5-32-544",
                                          "S-1-5-7", "S-1-5-2", "S-1-5-4" };
        if (broad.Contains(apiSid.Value))
            throw new InvalidOperationException("API pipe SID must identify a dedicated service identity");
        CheckProtected(_registration.StateDirectory, directory: true);
        CheckProtected(_registration.TokenFile, directory: false);
        CheckProtected(_registration.NativeProfilePath, directory: false);
        CheckAdminWritableDirectory(_registration.AdapterDirectory);
        _token = Encoding.UTF8.GetBytes(File.ReadAllText(_registration.TokenFile).Trim());
        if (_token.Length is < 32 or > 256)
            throw new InvalidOperationException("Missing protected executor token");
        using var profile = JsonDocument.Parse(File.ReadAllText(_registration.NativeProfilePath));
        var root = profile.RootElement;
        if (_dockerDesktop)
        {
            if (root.GetProperty("schema").GetString() != "scadivo-docker-host-network-v1")
                throw new InvalidOperationException("Incorrect registered Docker Desktop host profile");
            var registeredRoot = Path.GetFullPath(root.GetProperty("installationDirectory").GetString() ?? "");
            CheckAdminWritableDirectory(registeredRoot);
            _dockerCompose = Path.Combine(registeredRoot, "docker-compose.stable.yml");
            _dockerOverride = root.TryGetProperty("composeOverrideSha256", out _) ?
                Path.Combine(registeredRoot, "docker", "stable", "compose-host-executor.windows.override.yml") : null;
            _nativeConfiguration = Path.Combine(registeredRoot, ".env");
            _serviceName = string.Empty;
            CheckProtected(_nativeConfiguration, directory: false);
            VerifyDockerProfile();
            if (string.IsNullOrWhiteSpace(_registration.HttpsListenAddress) ||
                !IPAddress.TryParse(_registration.HttpsListenAddress, out var address) ||
                address.AddressFamily != AddressFamily.InterNetwork || address.Equals(IPAddress.Any) ||
                _registration.HttpsListenPort is null or < 1024 or > 65535 ||
                string.IsNullOrWhiteSpace(_registration.HttpsCertificatePfx) ||
                string.IsNullOrWhiteSpace(_registration.HttpsCertificatePasswordFile))
                throw new InvalidOperationException("Registered Windows Docker HTTPS listener is not constrained");
            CheckProtected(_registration.HttpsCertificatePfx, directory: false);
            CheckProtected(_registration.HttpsCertificatePasswordFile, directory: false);
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                _registration.HttpsCertificatePfx, File.ReadAllText(_registration.HttpsCertificatePasswordFile).Trim());
            if (!certificate.HasPrivateKey || DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() ||
                DateTime.UtcNow > certificate.NotAfter.ToUniversalTime())
                throw new InvalidOperationException("Registered Windows Docker TLS certificate unavailable");
        }
        else
        {
            if (root.GetProperty("schema").GetString() != "scadivo-native-network-v1" ||
                root.GetProperty("platform").GetString() != "windows" ||
                root.GetProperty("endpointName").GetString() != "Web" ||
                root.GetProperty("apiEndpointName").GetString() != "Api")
                throw new InvalidOperationException("Native Windows profile must own distinct Web/API listeners");
            _serviceName = root.GetProperty("serviceName").GetString() ?? "";
            if (!Regex.IsMatch(_serviceName, @"\A[A-Za-z0-9_.-]{1,128}\z"))
                throw new InvalidOperationException("Invalid registered Windows service");
            _nativeConfiguration = Path.GetFullPath(root.GetProperty("configurationFile").GetString() ?? "");
            CheckAdminWritableFile(_nativeConfiguration);
            _dockerCompose = null;
            _dockerOverride = null;
        }
        // Do not infer service control or file paths from any HTTP payload.
        _statePath = Path.Combine(_registration.StateDirectory, "network-operation.json");
        _planPath = Path.Combine(_registration.StateDirectory, "network-plan.json");
        _auditPath = Path.Combine(_registration.StateDirectory, "network-audit.jsonl");
        _processLock = new FileStream(Path.Combine(_registration.StateDirectory, "network-executor.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _pipeAcl = new PipeSecurity();
        _pipeAcl.SetAccessRuleProtection(true, false);
        var current = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Executor has no Windows identity");
        foreach (var sid in new[] { current, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                     new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
            _pipeAcl.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        _pipeAcl.AddAccessRule(new PipeAccessRule(apiSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        // Validate the registered native configuration and mark interrupted
        // state durably before accepting another request.
        _ = Current();
        lock (_guard)
        {
            var operation = Load<Operation>(_statePath);
            if (operation?.State == "applying")
            {
                operation = operation with { State = "recovery_required",
                    Detail = "Executor interrupted; recover the registered host service" };
                Save(_statePath, operation);
                Audit("recovery-required", operation.Module, operation.Id);
            }
        }
    }

    private static void CheckProtected(string path, bool directory)
    {
        if (!Path.IsPathFullyQualified(path) ||
            (directory ? !Directory.Exists(path) : !File.Exists(path)) ||
            File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Registered protected host resource is unavailable");
        FileSystemSecurity acl = directory ? new DirectoryInfo(path).GetAccessControl() :
            new FileInfo(path).GetAccessControl();
        if (!acl.AreAccessRulesProtected)
            throw new InvalidOperationException("Registration/state ACL inherits untrusted permissions");
        var trusted = new HashSet<string> {
            WindowsIdentity.GetCurrent().User!.Value, "S-1-5-18", "S-1-5-32-544"
        };
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            var sid = ((SecurityIdentifier)rule.IdentityReference).Value;
            if (rule.AccessControlType == AccessControlType.Allow && !trusted.Contains(sid))
                throw new InvalidOperationException("Registered host ACL grants an unexpected identity");
        }
    }

    private static void CheckAdminWritableDirectory(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path) ||
            File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Adapter directory unavailable");
        var acl = new DirectoryInfo(path).GetAccessControl();
        RejectUntrustedWriters(acl);
    }

    private static void CheckAdminWritableFile(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) ||
            File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Native effective configuration unavailable");
        RejectUntrustedWriters(new FileInfo(path).GetAccessControl());
    }

    private static void RejectUntrustedWriters(FileSystemSecurity acl)
    {
        var trusted = new HashSet<string> {
            WindowsIdentity.GetCurrent().User!.Value, "S-1-5-18", "S-1-5-32-544"
        };
        const FileSystemRights writes = FileSystemRights.WriteData | FileSystemRights.AppendData |
            FileSystemRights.Delete | FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow &&
                !trusted.Contains(((SecurityIdentifier)rule.IdentityReference).Value) &&
                (rule.FileSystemRights & writes) != 0)
                throw new InvalidOperationException("Untrusted principal may alter registered host resources");
    }


    private void VerifyDockerProfile()
    {
        if (!_dockerDesktop || _dockerCompose is null) return;
        using var profile = JsonDocument.Parse(File.ReadAllText(_registration.NativeProfilePath));
        var root = profile.RootElement;
        var fields = root.EnumerateObject().Select(p => p.Name).ToHashSet();
        var required = new HashSet<string>
        {
            "schema", "instanceId", "installationDirectory", "composeSha256", "dockerExecutable"
        };
        if (!fields.SetEquals(required) && !fields.SetEquals(required.Append("composeOverrideSha256")))
            throw new InvalidOperationException("Registered Docker Desktop profile authority changed");
        if (root.GetProperty("schema").GetString() != "scadivo-docker-host-network-v1" ||
            root.GetProperty("instanceId").GetString() != _registration.InstanceId)
            throw new InvalidOperationException("Registered Docker Desktop identity changed");
        CheckAdminWritableFile(_dockerCompose);
        CheckProtected(_nativeConfiguration, directory: false);
        var dockerExecutable = root.GetProperty("dockerExecutable").GetString() ?? "";
        CheckAdminWritableFile(dockerExecutable);
        if (!dockerExecutable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Registered Windows Docker executable invalid");
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_dockerCompose))).ToLowerInvariant();
        if (!string.Equals(actual, root.GetProperty("composeSha256").GetString(), StringComparison.Ordinal))
            throw new InvalidOperationException("Registered Windows Docker Compose content changed");
        if (_dockerOverride is not null)
        {
            CheckAdminWritableFile(_dockerOverride);
            var overrideHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_dockerOverride))).ToLowerInvariant();
            if (!string.Equals(overrideHash,
                root.GetProperty("composeOverrideSha256").GetString(), StringComparison.Ordinal))
                throw new InvalidOperationException("Registered Windows Docker IPC overlay changed");
        }
    }

    private Dictionary<string, int> DockerCurrent()
    {
        VerifyDockerProfile();
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(_nativeConfiguration))
        {
            var match = Regex.Match(line, @"^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$");
            if (!match.Success) continue;
            var key = match.Groups[1].Value;
            if (!env.TryAdd(key, match.Groups[2].Value.Trim().Trim('"', '\'')))
                throw new InvalidOperationException("Duplicate registered Docker environment key");
        }
        if (env.GetValueOrDefault("SCADIVO_INSTALLATION_ID", "elitescada-stable") != _registration.InstanceId)
            throw new InvalidOperationException("Registered Docker installation identity diverged");
        foreach (var key in new[] { "SCADIVO_WEB_BIND", "SCADIVO_API_BIND" })
            if (env.TryGetValue(key, out var address) && address is not ("127.0.0.1" or "0.0.0.0"))
                throw new InvalidOperationException("Unsupported Docker published bind address");
        var result = new Dictionary<string, int>();
        foreach (var (field, key, fallback) in new[]
        {
            ("webPort", "ELITESCADA_WEB_PORT", "18080"),
            ("apiPort", "ELITESCADA_API_PORT", "15120"),
            ("databasePort", "ELITESCADA_DB_PORT", "15432"),
            ("mediaControlPort", "SCADIVO_MEDIA_CONTROL_PORT", "9997"),
            ("mediaPlaybackPort", "SCADIVO_MEDIA_PLAYBACK_PORT", "8888")
        })
        {
            var raw = env.GetValueOrDefault(key, fallback);
            if (!Regex.IsMatch(raw, @"\A[0-9]{1,5}\z") ||
                !int.TryParse(raw, out var port) || port is < 1 or > 65535)
                throw new InvalidOperationException("Registered Docker port is invalid");
            result[field] = port;
        }
        if (new[] { result["webPort"], result["apiPort"], result["databasePort"] }.Distinct().Count() != 3 ||
            result["mediaControlPort"] == result["mediaPlaybackPort"])
            throw new InvalidOperationException("Registered Docker host port collision");
        return result;
    }

    private string AdapterScript(string module)
    {
        var name = _dockerDesktop ? "apply-docker-host-network-profile.ps1" : module switch
        {
            "access" => "apply-native-network-profile.ps1",
            "database" => "apply-native-database-port.ps1",
            "media" => "apply-native-media-ports.ps1",
            _ => throw new InvalidOperationException("Unknown registered service")
        };
        var script = Path.Combine(_registration.AdapterDirectory, name);
        CheckAdminWritableFile(script);
        return script;
    }

    private Dictionary<string, int> Current()
    {
        if (_dockerDesktop) return DockerCurrent();
        using var app = JsonDocument.Parse(File.ReadAllText(_nativeConfiguration));
        using var profile = JsonDocument.Parse(File.ReadAllText(_registration.NativeProfilePath));
        var endpoints = app.RootElement.GetProperty("Kestrel").GetProperty("Endpoints");
        var result = new Dictionary<string, int>();
        foreach (var (name, target) in new[] { ("Web", "webPort"), ("Api", "apiPort") })
        {
            var uri = new Uri(endpoints.GetProperty(name).GetProperty("Url").GetString() ?? "", UriKind.Absolute);
            if (uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
                throw new InvalidOperationException("Registered listener has invalid URL");
            result[target] = uri.Port;
        }
        if (profile.RootElement.TryGetProperty("database", out var database) && database.ValueKind == JsonValueKind.Object &&
            app.RootElement.TryGetProperty("DatabaseTopology", out var topology) &&
            topology.TryGetProperty("LocalManagedPort", out var databasePort) &&
            databasePort.TryGetInt32(out int db) && db is >= 1 and <= 65535)
            result["databasePort"] = db;
        if (profile.RootElement.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Object &&
            app.RootElement.TryGetProperty("MediaSources", out var mediaSources) &&
            Uri.TryCreate(mediaSources.GetProperty("GatewayControlUrl").GetString(), UriKind.Absolute, out var control) &&
            Uri.TryCreate(mediaSources.GetProperty("GatewayPlaybackUrl").GetString(), UriKind.Absolute, out var playback) &&
            (control.Host is "127.0.0.1" or "localhost" or "::1") &&
            (playback.Host is "127.0.0.1" or "localhost" or "::1"))
        {
            result["mediaControlPort"] = control.Port;
            result["mediaPlaybackPort"] = playback.Port;
        }
        return result;
    }

    private string Revision()
    {
        using var mac = new HMACSHA256(_token);
        using var profile = JsonDocument.Parse(File.ReadAllText(_registration.NativeProfilePath));
        var files = new List<string> { _registration.NativeProfilePath, _nativeConfiguration };
        if (_dockerDesktop)
        {
            VerifyDockerProfile();
            files.Add(_dockerCompose!);
            if (_dockerOverride is not null) files.Add(_dockerOverride);
        }
        foreach (var field in _dockerDesktop ? Array.Empty<string>() : new[] { "database", "media" })
            if (profile.RootElement.TryGetProperty(field, out var entry) &&
                entry.ValueKind == JsonValueKind.Object &&
                entry.TryGetProperty("configurationFile", out var file))
            {
                var path = file.GetString() ?? "";
                CheckAdminWritableFile(path);
                files.Add(path);
            }
        using var buffer = new MemoryStream();
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            buffer.Write(BitConverter.GetBytes(bytes.Length));
            buffer.Write(bytes);
        }
        return Convert.ToHexString(mac.ComputeHash(buffer.ToArray())).ToLowerInvariant();
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private T? Load<T>(string path)
    {
        if (!File.Exists(path)) return default;
        CheckAdminWritableFile(path);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }

    private void Save<T>(string path, T record)
    {
        var temp = Path.Combine(_registration.StateDirectory, "host-" + Guid.NewGuid().ToString("N") + ".tmp");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                file.Write(bytes);
                file.Flush(true);
            }
            // State directory has a private, non-inheriting ACL.
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void Audit(string action, string module, string id = "")
    {
        if (!AllowedAudit.Contains(action) || !Fields.ContainsKey(module) ||
            (id.Length > 0 && !Regex.IsMatch(id, @"\A[0-9a-f]{32}\z")))
            throw new InvalidOperationException("Invalid durable audit event");
        File.AppendAllText(_auditPath, JsonSerializer.Serialize(new
        {
            at = Now(), instanceId = _registration.InstanceId,
            @event = action, module, operationId = id
        }) + "\n");
    }

    private static Dictionary<string, int> ValidatePortPlan(JsonElement request, out string module)
    {
        if (request.ValueKind != JsonValueKind.Object || request.EnumerateObject().Count() != 3 ||
            request.GetProperty("schema").GetString() != PlanSchema)
            throw new InvalidOperationException("Unsupported versioned request");
        module = request.GetProperty("module").GetString() ?? "";
        if (!Fields.TryGetValue(module, out var names))
            throw new InvalidOperationException("Unsupported registered module");
        var values = request.GetProperty("ports");
        if (values.ValueKind != JsonValueKind.Object ||
            values.EnumerateObject().Count() != names.Length)
            throw new InvalidOperationException("Invalid typed service ports");
        var ports = new Dictionary<string, int>();
        foreach (var key in names)
        {
            if (!values.TryGetProperty(key, out var value) ||
                !value.TryGetInt32(out var number) || number is < 1 or > 65535)
                throw new InvalidOperationException("Invalid service port");
            ports[key] = number;
        }
        if (ports.Values.Distinct().Count() != ports.Count)
            throw new InvalidOperationException("Duplicate service port");
        return ports;
    }

    private static string Confirm(JsonElement request, bool recover)
    {
        if (request.ValueKind != JsonValueKind.Object ||
            request.EnumerateObject().Count() != (recover ? 2 : 3) ||
            request.GetProperty("schema").GetString() != PlanSchema ||
            request.GetProperty("confirm").ValueKind != JsonValueKind.True)
            throw new InvalidOperationException("Explicit confirmation required");
        if (recover) return "";
        var id = request.GetProperty("planId").GetString() ?? "";
        if (!Regex.IsMatch(id, @"\A[0-9a-f]{32}\z"))
            throw new InvalidOperationException("Invalid planned operation ID");
        return id;
    }

    private async Task RunAdapterAsync(string module, Dictionary<string, int> ports, string action)
    {
        var script = AdapterScript(module);
        var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(powershell)) throw new InvalidOperationException("Windows PowerShell unavailable");
        var start = new ProcessStartInfo(powershell)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                                  "-File", script, "-ProfilePath", _registration.NativeProfilePath })
            start.ArgumentList.Add(arg);
        if (_dockerDesktop)
        {
            if (action is not ("preflight" or "apply" or "recover" or "verify"))
                throw new InvalidOperationException("Unsupported registered Docker host action");
            if (action is "preflight" or "apply")
            {
                var target = Current();
                foreach (var pair in ports) target[pair.Key] = pair.Value;
                foreach (var (key, flag) in new[]
                {
                    ("webPort", "-WebPort"), ("apiPort", "-ApiPort"), ("databasePort", "-DatabasePort"),
                    ("mediaControlPort", "-MediaControlPort"), ("mediaPlaybackPort", "-MediaPlaybackPort")
                })
                {
                    start.ArgumentList.Add(flag);
                    start.ArgumentList.Add(target[key].ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            if (action == "apply") start.ArgumentList.Add("-Apply");
            if (action == "recover") start.ArgumentList.Add("-Recover");
            if (action == "verify") start.ArgumentList.Add("-Verify");
        }
        else
        {
        if (module != "access")
        {
            if (string.IsNullOrEmpty(_registration.PythonExecutable))
                throw new InvalidOperationException("Registered Python is required for managed database/media");
            CheckAdminWritableFile(_registration.PythonExecutable);
            start.ArgumentList.Add("-PythonExecutable");
            start.ArgumentList.Add(_registration.PythonExecutable);
        }
        if (action != "recover")
        {
            (string key, string flag)[] portFlags = module switch
            {
                "access" => new[] { ("webPort", "-WebPort"), ("apiPort", "-ApiPort") },
                "database" => [("databasePort", "-DatabasePort")],
                "media" => [("mediaControlPort", "-ControlPort"), ("mediaPlaybackPort", "-HlsPort")],
                _ => throw new InvalidOperationException("Invalid module")
            };
            foreach (var (key, flag) in portFlags)
            {
                start.ArgumentList.Add(flag);
                start.ArgumentList.Add(ports[key].ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        if (action == "apply") start.ArgumentList.Add("-Apply");
        if (action == "recover") start.ArgumentList.Add("-Recover");
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Registered adapter could not start");
        // Discard output from protected scripts; it may contain paths or credentials.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Registered Windows adapter exceeded its health deadline");
        }
        _ = await stdout;
        _ = await stderr;
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Registered Windows adapter rejected operation or health verification");
    }

    private object Snapshot()
    {
        try
        {
            var current = Current();
            return new
            {
                schema = Schema, available = true, profile = _registration.Profile,
                instanceId = _registration.InstanceId, configuredPorts = current,
                modules = Fields.Where(pair => pair.Value.All(current.ContainsKey)).Select(pair => pair.Key).ToArray(),
                operation = Load<Operation>(_statePath),
                observed = "host-configuration", hostMappingVerified = false
            };
        }
        catch (Exception)
        {
            return new { schema = Schema, available = false, reason = "Registered Windows host configuration cannot be verified" };
        }
    }

    private object Preflight(JsonElement request)
    {
        var ports = ValidatePortPlan(request, out var module);
        lock (_guard)
        {
            var previous = Load<Operation>(_statePath);
            if (previous?.State is "applying" or "recovery_required")
                throw new InvalidOperationException("Previous host operation requires completion or recovery");
            var current = Current();
            if (ports.Keys.Any(key => !current.ContainsKey(key)))
                throw new InvalidOperationException("Service is not registered as locally managed");
            var combined = new Dictionary<string, int>(current);
            foreach (var (key, value) in ports) combined[key] = value;
            if (combined.Values.Distinct().Count() != combined.Count)
                throw new InvalidOperationException("Requested ports conflict with another local service");
            // Adapter performs SCM, listener, pending-journal and permission checks
            // before any service stop; never imply that a UI-only check suffices.
            RunAdapterAsync(module, ports, "preflight").GetAwaiter().GetResult();
            var before = ports.Keys.ToDictionary(key => key, key => current[key]);
            var plan = new PortPlan(Guid.NewGuid().ToString("N"), module, ports, before, Revision(), Now() + 600);
            Save(_planPath, plan);
            Audit("preflight", module);
            return new { planId = plan.Id, module, ports, before, expiresAt = plan.ExpiresAt,
                         requiresRestart = !ports.OrderBy(x => x.Key).SequenceEqual(before.OrderBy(x => x.Key)),
                         verified = "host-preflight-only" };
        }
    }

    private object Apply(JsonElement request)
    {
        var planId = Confirm(request, recover: false);
        lock (_guard)
        {
            var record = Load<Operation>(_statePath);
            if (record?.State is "applying" or "recovery_required")
                throw new InvalidOperationException("Host operation is running or requires recovery");
            var plan = Load<PortPlan>(_planPath);
            if (plan is null || !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(plan.Id), Encoding.ASCII.GetBytes(planId)) ||
                plan.ExpiresAt <= Now() || plan.Revision != Revision())
                throw new InvalidOperationException("Expired or changed plan; repeat host preflight");
            var current = Current();
            if (plan.Before.Any(pair => !current.TryGetValue(pair.Key, out int value) || value != pair.Value))
                throw new InvalidOperationException("Registered service changed since preflight");
            var operation = new Operation(Guid.NewGuid().ToString("N"), plan.Module, "applying",
                plan.Before, plan.Ports, Now(), null, "Host operation accepted; restart and checks pending");
            Save(_statePath, operation);
            Audit("apply-started", operation.Module, operation.Id);
            File.Delete(_planPath);
            _ = Task.Run(() => WorkerAsync(operation, recover: false));
            return new { operationId = operation.Id, state = operation.State };
        }
    }

    private object Recover(JsonElement request)
    {
        _ = Confirm(request, recover: true);
        lock (_guard)
        {
            var record = Load<Operation>(_statePath);
            if (record?.State != "recovery_required")
                throw new InvalidOperationException("No registered recovery is pending");
            record = record with { State = "applying", Detail = "Restoring registered prior host state" };
            Save(_statePath, record);
            Audit("recovery-started", record.Module, record.Id);
            _ = Task.Run(() => WorkerAsync(record, recover: true));
            return new { operationId = record.Id, state = "applying" };
        }
    }

    private async Task WorkerAsync(Operation operation, bool recover)
    {
        try
        {
            if (recover)
            {
                if (_dockerDesktop)
                {
                    // The previous successful apply may have an "applied" journal,
                    // while a later attempt failed before writing a new journal.
                    // Verify the exact previous live ports first; do not roll back
                    // an unrelated earlier successful operation.
                    try { await RunAdapterAsync(operation.Module, operation.Before, "verify"); }
                    catch { await RunAdapterAsync(operation.Module, operation.Before, "recover"); }
                }
                else
                {
                    try { await RunAdapterAsync(operation.Module, operation.Before, "recover"); }
                    catch
                    {
                        var observed = Current();
                        if (operation.Before.Any(pair => !observed.TryGetValue(pair.Key, out var p) || p != pair.Value))
                            throw;
                        await RunAdapterAsync(operation.Module, operation.Before, "preflight");
                    }
                }
            }
            else
                await RunAdapterAsync(operation.Module, operation.Desired, "apply");
            var current = Current();
            var target = recover ? operation.Before : operation.Desired;
            if (target.Any(pair => !current.TryGetValue(pair.Key, out var p) || p != pair.Value))
                throw new InvalidOperationException("Host effective configuration differs from requested state");
            operation = operation with { State = recover ? "recovered" : "applied",
                FinishedAt = Now(), Detail = "Registered Windows host service completed and effective ports were verified" };
        }
        catch (Exception)
        {
            operation = operation with { State = "recovery_required", FinishedAt = Now(),
                Detail = "Registered Windows operation not verified; protected administrator recovery required" };
        }
        lock (_guard)
        {
            Save(_statePath, operation);
            Audit(operation.State switch
            {
                "applied" => "applied", "recovered" => "recovered", _ => "recovery-required"
            }, operation.Module, operation.Id);
        }
    }

    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        if (_dockerDesktop)
        {
            await Task.WhenAll(ServePipeAsync(cancellationToken), ServeHttpsAsync(cancellationToken));
        }
        else await ServePipeAsync(cancellationToken);
    }

    private async Task ServeHttpsAsync(CancellationToken cancellationToken)
    {
        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            _registration.HttpsCertificatePfx!,
            File.ReadAllText(_registration.HttpsCertificatePasswordFile!).Trim());
        var address = IPAddress.Parse(_registration.HttpsListenAddress!);
        var listener = new TcpListener(address, _registration.HttpsListenPort!.Value);
        listener.Start(16);
        using var slots = new SemaphoreSlim(16, 16);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await slots.WaitAsync(cancellationToken);
                TcpClient peer;
                try { peer = await listener.AcceptTcpClientAsync(cancellationToken); }
                catch { slots.Release(); throw; }
                _ = Task.Run(async () =>
                {
                    using (peer)
                    {
                        try
                        {
                            await using var tls = new SslStream(peer.GetStream(), leaveInnerStreamOpen: false);
                            using var handshakeLimit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                            {
                                ServerCertificate = certificate,
                                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                            }, handshakeLimit.Token);
                            await ServeClientAsync(tls);
                        }
                        catch (Exception) { /* Never expose protected host request or credentials. */ }
                        finally { slots.Release(); }
                    }
                }, cancellationToken);
            }
        }
        finally { listener.Stop(); }
    }

    private async Task ServePipeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = NamedPipeServerStreamAcl.Create(_registration.PipeName, PipeDirection.InOut, 16,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 8192, 8192, _pipeAcl);
            try { await pipe.WaitForConnectionAsync(cancellationToken); }
            catch { await pipe.DisposeAsync(); throw; }
            _ = Task.Run(async () =>
            {
                await using (pipe)
                {
                    try { await ServeClientAsync(pipe); }
                    catch (Exception) { /* Never log protected request data. */ }
                }
            }, cancellationToken);
        }
    }

    private async Task ServeClientAsync(Stream pipe)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(130));
        var head = new List<byte>();
        while (head.Count < 8192)
        {
            var byteBuffer = new byte[1];
            var size = await pipe.ReadAsync(byteBuffer, timeout.Token);
            if (size == 0) return;
            head.Add(byteBuffer[0]);
            if (head.Count >= 4 && head[^4] == 13 && head[^3] == 10 &&
                head[^2] == 13 && head[^1] == 10) break;
        }
        if (head.Count >= 8192)
        {
            await Reply(pipe, 400, new { error = "Invalid IPC request size" });
            return;
        }
        var lines = Encoding.ASCII.GetString(head.ToArray()).Split("\r\n", StringSplitOptions.None);
        var line = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in lines.Skip(1).Where(s => s.Contains(':')))
        {
            var at = header.IndexOf(':');
            if (!headers.TryAdd(header[..at].Trim(), header[(at + 1)..].Trim()))
                throw new InvalidOperationException("Duplicate IPC header");
        }
        if (line.Length != 3 || !headers.TryGetValue("X-Scadivo-Installation-Token", out var provided) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), _token))
        {
            await Reply(pipe, 403, new { error = "Executor authorization denied" });
            return;
        }
        try
        {
            object response;
            int status;
            if (line[0] == "GET" && line[1] == "/v1/status")
            {
                lock (_guard) { response = Snapshot(); }
                status = 200;
            }
            else if (line[0] == "POST" && line[1] is "/v1/preflight" or "/v1/apply" or "/v1/recover")
            {
                if (!headers.TryGetValue("Content-Length", out var length) ||
                    !int.TryParse(length, out int count) || count is < 2 or > 4096)
                    throw new InvalidOperationException("Unsupported IPC content length");
                var buffer = new byte[count];
                await pipe.ReadExactlyAsync(buffer, timeout.Token);
                using var doc = JsonDocument.Parse(buffer);
                response = line[1] switch
                {
                    "/v1/preflight" => Preflight(doc.RootElement),
                    "/v1/apply" => Apply(doc.RootElement),
                    "/v1/recover" => Recover(doc.RootElement),
                    _ => throw new InvalidOperationException("Unrecognized IPC action")
                };
                status = line[1] == "/v1/preflight" ? 200 : 202;
            }
            else throw new InvalidOperationException("Unsupported IPC method or route");
            await Reply(pipe, status, response);
        }
        catch (Exception)
        {
            await Reply(pipe, 409, new { error = "Host operation denied or unavailable; inspect protected administrator diagnostics" });
        }
    }

    private static async Task Reply(Stream pipe, int code, object data)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " " + (code == 200 ? "OK" : code == 202 ? "Accepted" :
                 code == 403 ? "Forbidden" : "Conflict") + "\r\nContent-Type: application/json\r\nCache-Control: no-store\r\nContent-Length: " +
                 body.Length + "\r\nConnection: close\r\n\r\n");
        await pipe.WriteAsync(header);
        await pipe.WriteAsync(body);
        await pipe.FlushAsync();
    }

    public static async Task<string> AdministratorRequestAsync(string registrationPath, string route)
    {
        CheckProtected(registrationPath, directory: false);
        var config = JsonSerializer.Deserialize<Registration>(File.ReadAllText(registrationPath), JsonOptions)
            ?? throw new InvalidOperationException("Missing Windows executor");
        CheckProtected(config.TokenFile, directory: false);
        using var pipe = new NamedPipeClientStream(".", config.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000);
        var body = route == "/v1/recover" ? JsonSerializer.Serialize(new { schema = PlanSchema, confirm = true }) : null;
        var request = Encoding.ASCII.GetBytes((body is null ? "GET" : "POST") + " " + route + " HTTP/1.1\r\n" +
             "Host: localhost\r\nX-Scadivo-Installation-Token: " + File.ReadAllText(config.TokenFile).Trim() +
             "\r\nContent-Type: application/json\r\nContent-Length: " + (body?.Length ?? 0) + "\r\nConnection: close\r\n\r\n" + body);
        await pipe.WriteAsync(request);
        await pipe.FlushAsync();
        using var reader = new StreamReader(pipe, Encoding.UTF8);
        var response = await reader.ReadToEndAsync();
        if (!response.StartsWith("HTTP/1.1 20", StringComparison.Ordinal))
            throw new InvalidOperationException("Registered Windows executor denied administrator operation");
        return response[(response.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
    }

    public void Dispose() => _processLock.Dispose();
}
