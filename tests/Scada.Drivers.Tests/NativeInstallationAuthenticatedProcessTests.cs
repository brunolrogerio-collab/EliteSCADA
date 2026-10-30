using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Scada.Drivers.Tests;

public sealed class NativeInstallationAuthenticatedProcessTests
{
    [Fact]
    public async Task RealApi_RetainsAuthenticatedDatabaseReadinessAfterAccessPortsChange()
    {
        var seed = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(seed)) return;
        var root = Path.Combine(Path.GetTempPath(), "scadivo-authenticated-ports-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var database = "scadivo_ports_" + Guid.NewGuid().ToString("N");
        var admin = new NpgsqlConnectionStringBuilder(seed) { Database = "postgres", Pooling = false };
        var connection = new NpgsqlConnectionStringBuilder(seed) { Database = database, Pooling = false };
        var created = false;
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var jwt = Token(key);
        Process? process = null;
        try
        {
            await using (var db = new NpgsqlConnection(admin.ConnectionString))
            {
                await db.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", db);
                await create.ExecuteNonQueryAsync(); created = true;
            }
            var config = Path.Combine(root, "appsettings.json");
            var apiDll = Path.Combine(AppContext.BaseDirectory, "Scada.Api.dll");
            Assert.True(File.Exists(apiDll));
            var ports = new HashSet<int>();
            while (ports.Count < 4)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
                ports.Add(((IPEndPoint)listener.LocalEndpoint).Port); listener.Stop();
            }
            var selected = ports.ToArray();
            for (var stage = 0; stage < 2; stage++)
            {
                var web = selected[stage * 2]; var api = selected[stage * 2 + 1];
                await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new {
                    Kestrel = new { Endpoints = new { Web = new { Url = $"http://127.0.0.1:{web}" }, Api = new { Url = $"http://127.0.0.1:{api}" } } }
                }));
                var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add(apiDll); start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(root);
                void Set(string name, string value) => start.Environment[name] = value;
                Set("ASPNETCORE_ENVIRONMENT", "Production");
                Set("ConnectionStrings__EliteScada", connection.ConnectionString);
                Set("ConnectionStrings__Historian", connection.ConnectionString);
                Set("Authentication__Enabled", "true"); Set("Authentication__Local__Enabled", "true"); Set("Authentication__Local__SecureCookie", "false");
                Set("Authentication__Jwt__Issuer", "native-port-test"); Set("Authentication__Jwt__Audience", "native-port-test"); Set("Authentication__Jwt__SigningKey", key);
                Set("DatabaseTopology__RequireDurable", "true"); Set("DatabaseTopology__StateFile", Path.Combine(root, "topology.json"));
                Set("DatabaseTopology__Secrets__StoreFile", Path.Combine(root, "secrets.json"));
                Set("DatabaseTopology__Secrets__MasterKeyBase64", Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key))));
                Set("Historian__Provider", "timescaledb"); Set("Logging__LogLevel__Default", "Warning");
                process = Process.Start(start)!;
                process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri($"http://127.0.0.1:{api}"), Timeout = TimeSpan.FromSeconds(5) };
                var ready = false;
                for (var attempt = 0; attempt < 120; attempt++)
                {
                    Assert.False(process.HasExited, "Isolated API exited before readiness.");
                    try { ready = (await client.GetAsync("/health")).IsSuccessStatusCode; } catch (HttpRequestException) { }
                    if (ready) break;
                    await Task.Delay(500);
                }
                Assert.True(ready, "Isolated API did not become healthy.");
                if (stage == 0)
                {
                    var bootstrap = await client.PostAsJsonAsync("/api/auth/bootstrap", new { username = "native-port-admin", displayName = "Disposable native port test", password = "disposable-native-port-test-password" });
                    Assert.True(bootstrap.IsSuccessStatusCode, "Disposable Administrator bootstrap failed.");
                }
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/installation/database-readiness")).StatusCode);
                client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);
                var response = await client.GetAsync("/api/installation/database-readiness");
                Assert.True(response.IsSuccessStatusCode, "Authenticated readiness rejected: " + response.StatusCode);
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.True(payload.RootElement.GetProperty("ready").GetBoolean());
                Assert.False(payload.RootElement.GetProperty("recoveryRequired").GetBoolean());
                Assert.False(payload.RootElement.GetProperty("restartRequired").GetBoolean());
                process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); process.Dispose(); process = null;
            }
        }
        finally
        {
            if (process is not null) { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); process.Dispose(); }
            if (created)
            {
                await using var db = new NpgsqlConnection(admin.ConnectionString); await db.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", db); await drop.ExecuteNonQueryAsync();
            }
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Token(string key)
    {
        static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Encode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new { iss = "native-port-test", aud = "native-port-test", sub = "native-port-admin", name = "native-port-admin", role = new[] { "developer" }, nbf = now - 5, exp = now + 600 }));
        var unsigned = header + "." + payload;
        return unsigned + "." + Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(unsigned)));
    }
}
