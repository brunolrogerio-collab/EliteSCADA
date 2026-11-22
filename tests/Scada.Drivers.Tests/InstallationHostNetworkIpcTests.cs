using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Engineering;
using Xunit;

namespace Scada.Drivers.Tests;

// Exercises the real API transport, rather than a host-script or browser mock.
// Host services remain disposable, isolated and unprivileged in this test.
public sealed class InstallationHostNetworkIpcTests
{
    [Fact]
    public async Task HostApiIpcRoundTripsThroughTheLocalRegisteredTransport()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows()) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        const string token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var settings = new Dictionary<string, string?>
        {
            ["InstallationNetwork:HostExecutorToken"] = token
        };
        if (OperatingSystem.IsLinux())
        {
            var socketPath = Path.Combine(Path.GetTempPath(), "scadivo-udstest-" + Guid.NewGuid().ToString("N") + ".sock");
            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                listener.Bind(new UnixDomainSocketEndPoint(socketPath));
                listener.Listen(1);
                settings["InstallationNetwork:HostExecutorSocket"] = socketPath;
                var server = Task.Run(async () =>
                {
                    using var accepted = await listener.AcceptAsync(timeout.Token);
                    await using var stream = new NetworkStream(accepted, ownsSocket: false);
                    await ReplyAsync(stream, token, timeout.Token);
                }, timeout.Token);
                await VerifyAsync(settings, server, timeout.Token);
            }
            finally { if (File.Exists(socketPath)) File.Delete(socketPath); }
        }
        else
        {
            var pipeName = "scadivo-ipctest-" + Guid.NewGuid().ToString("N");
            using var listener = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            settings["InstallationNetwork:HostExecutorPipe"] = pipeName;
            var server = Task.Run(async () =>
            {
                await listener.WaitForConnectionAsync(timeout.Token);
                await ReplyAsync(listener, token, timeout.Token);
            }, timeout.Token);
            await VerifyAsync(settings, server, timeout.Token);
        }
    }

    private static async Task ReplyAsync(Stream stream, string token, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var buffer = new byte[1];
        while (bytes.Count < 8192)
        {
            if (await stream.ReadAsync(buffer.AsMemory(), cancellationToken) == 0)
                throw new IOException("IPC peer closed before request headers");
            bytes.Add(buffer[0]);
            if (bytes.Count >= 4 && bytes[^4] == 13 && bytes[^3] == 10 &&
                bytes[^2] == 13 && bytes[^1] == 10) break;
        }
        var headers = Encoding.ASCII.GetString(bytes.ToArray());
        Assert.StartsWith("GET /v1/status HTTP/1.1", headers);
        Assert.Contains("X-Scadivo-Installation-Token: " + token, headers);
        const string json = "{\"schema\":\"scadivo-network-host-executor-v1\",\"available\":true,\"instanceId\":\"isolated-api-transport\"}";
        var response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
            Encoding.UTF8.GetByteCount(json) + "\r\nConnection: close\r\n\r\n" + json);
        await stream.WriteAsync(response.AsMemory(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task VerifyAsync(
        Dictionary<string, string?> settings, Task server, CancellationToken cancellationToken)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var response = await HostExecutorClient.SendAsync(
            config, "GET", "/v1/status", null, cancellationToken);
        await server;
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        await using var output = new MemoryStream();
        context.Response.Body = output;
        await response.ExecuteAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Contains("isolated-api-transport", Encoding.UTF8.GetString(output.ToArray()));
    }


    [Fact]
    public async Task DockerDesktopWindowsTlsBridgeUsesPinnedCertificateAndInstallationToken()
    {
        if (!OperatingSystem.IsLinux()) return;
        var token = new string('9', 64);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=scadivo-ci-docker-host", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var settings = new Dictionary<string, string?>
        {
            ["InstallationNetwork:HostExecutorHttpsHost"] = "127.0.0.1",
            ["InstallationNetwork:HostExecutorHttpsPort"] = port.ToString(),
            ["InstallationNetwork:HostExecutorServerCertificateSha256"] =
                Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256)),
            ["InstallationNetwork:HostExecutorToken"] = token
        };
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var tls = new SslStream(peer.GetStream(), leaveInnerStreamOpen: false);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, timeout.Token);
            await ReplyAsync(tls, token, timeout.Token);
        }, timeout.Token);
        await VerifyAsync(settings, server, timeout.Token);

        // A different hash is never silently accepted as a trusted Windows host.
        settings["InstallationNetwork:HostExecutorServerCertificateSha256"] = new string('0', 64);
        var badServer = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var tls = new SslStream(peer.GetStream(), leaveInnerStreamOpen: false);
            try
            {
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, timeout.Token);
                var buffer = new byte[1];
                await tls.ReadExactlyAsync(buffer, timeout.Token);
            }
            catch (IOException) { /* Invalid client pin terminates the TLS session. */ }
            catch (AuthenticationException) { }
        }, timeout.Token);
        var rejected = await HostExecutorClient.SendAsync(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            "GET", "/v1/status", null, timeout.Token);
        Assert.Equal(503, await ResultStatusAsync(rejected));
        await badServer;

        // Missing pin, arbitrary host, or conflicting local socket fail closed.
        foreach (var invalid in new[]
        {
            new Dictionary<string, string?> { ["InstallationNetwork:HostExecutorServerCertificateSha256"] = null },
            new Dictionary<string, string?> { ["InstallationNetwork:HostExecutorHttpsHost"] = "untrusted.example" },
            new Dictionary<string, string?> { ["InstallationNetwork:HostExecutorSocket"] = "/tmp/untrusted.sock" }
        })
        {
            var values = new Dictionary<string, string?>(settings);
            foreach (var pair in invalid) values[pair.Key] = pair.Value;
            Assert.Equal(503, await ResultStatusAsync(await HostExecutorClient.SendAsync(
                new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
                "GET", "/v1/status", null, CancellationToken.None)));
        }
    }

    private static async Task<int> ResultStatusAsync(IResult response)
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        await response.ExecuteAsync(context);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task UnregisteredHostExecutorIsNotTreatedAsApplied()
    {
        var response = await HostExecutorClient.SendAsync(
            new ConfigurationBuilder().Build(), "GET", "/v1/status", null,
            CancellationToken.None);
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        await response.ExecuteAsync(context);
        Assert.Equal(503, context.Response.StatusCode);
    }
}
