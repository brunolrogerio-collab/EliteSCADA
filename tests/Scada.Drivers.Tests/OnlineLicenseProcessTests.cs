using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Scada.Api.Licensing;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Persistence.PostgreSql;

namespace Scada.Drivers.Tests;

public sealed partial class OnlineProductLicenseLifecycleTests
{
    private const string ProcessRootVariable = "SCADIVO_LICENSE_TEST_PROCESS_ROOT";

    [Theory]
    [InlineData(1, false)]
    [InlineData(201, false)]
    [InlineData(1, true)]
    [InlineData(201, true)]
    public async Task OnlineExpiry_SeparateProcessReevaluatesSettledAuthority(int count, bool separateFiles)
    {
        var connection = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new PostgreSqlRuntimeSessionLeaseStore(connection);
        await store.InitializeAsync();
        var lifecycle = CreateLifecycle(source, store, new RecordingRuntime(), f.Clock);
        Assert.True((await lifecycle.InstallOnlineAsync(Candidate())).Succeeded);
        await File.WriteAllTextAsync(Path.Combine(f.Root, "public.pem"), keys.Online.ExportSubjectPublicKeyInfoPem());
        var workerLicense = separateFiles ? Path.Combine(f.Root, "worker.license") : f.Path;
        if (separateFiles) File.Copy(f.Path, workerLicense);
        await File.WriteAllTextAsync(Path.Combine(f.Root, "input.json"), JsonSerializer.Serialize(new ProcessInput(workerLicense, Binding.MachineFingerprint, count, separateFiles)));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(OnlineProductLicenseLifecycleTests).Assembly.Location);
        start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~OnlineLicenseProcessWorker");
        start.Environment[ProcessRootVariable] = f.Root;
        using var worker = Process.Start(start)!;
        var stdout = worker.StandardOutput.ReadToEndAsync();
        var stderr = worker.StandardError.ReadToEndAsync();
        try
        {
            await WaitForProcessFileAsync(f.Root, "ready", worker);
            f.Clock.Advance(TimeSpan.FromHours(72));
            Assert.True((await lifecycle.ExpireOnlineAsync(source.ReadOnlineInstallation()!.Revision)).Succeeded);
            await File.WriteAllTextAsync(Path.Combine(f.Root, "continue"), "go");
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
            Assert.True(worker.ExitCode == 0, (await stdout) + (await stderr));
            var result = JsonSerializer.Deserialize<ProcessResult>(await File.ReadAllTextAsync(Path.Combine(f.Root, "result.json")))!;
            Assert.NotEqual(Environment.ProcessId, result.ProcessId);
            Assert.Equal(count <= 200, result.Allowed);
            Assert.Equal(count <= 200 ? count : 0, result.Tags);
        }
        finally
        {
            if (!worker.HasExited) { worker.Kill(entireProcessTree: true); await worker.WaitForExitAsync(); }
            await stdout; await stderr;
        }
    }

    [Fact]
    public async Task OnlineLicenseProcessWorker()
    {
        var root = Environment.GetEnvironmentVariable(ProcessRootVariable);
        if (string.IsNullOrWhiteSpace(root)) return;
        var input = JsonSerializer.Deserialize<ProcessInput>(await File.ReadAllTextAsync(Path.Combine(root, "input.json")))!;
        using var publicKey = RSA.Create();
        publicKey.ImportFromPem(await File.ReadAllTextAsync(Path.Combine(root, "public.pem")));
        var clock = new TestClock();
        using var source = new FileProductLicenseService(new ProcessMachine(input.Fingerprint), input.LicensePath,
            new Dictionary<string, RSA>(), clock)
        {
            OnlineVerifier = (code, binding, now) => OnlineLeaseVerifier.VerifyWithKey(code, binding, now, publicKey)
        };
        await using var store = new PostgreSqlRuntimeSessionLeaseStore(Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES")!);
        await store.InitializeAsync();
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, clock, store);
        var original = Package(input.Count, true);
        Assert.True((await runtime.ActivateAsync("process-test", 1,
            original with { Tags = original.Tags.Select(t => t with { ReadOnly = false }).ToArray() })).Activated);
        var tag = runtime.Tags().First();
        await runtime.WriteAsync(tag.Id, 7d);
        await File.WriteAllTextAsync(Path.Combine(root, "ready"), Environment.ProcessId.ToString());
        await WaitForProcessFileAsync(root, "continue");
        clock.Advance(TimeSpan.FromHours(72));
        if (input.SeparateFiles)
        {
            Assert.Equal(LicenseState.Invalid, source.CurrentVerification.State);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await runtime.WriteAsync(tag.Id, 8d));
            Assert.True(runtime.TryGetCurrent(tag.Id, out var preserved));
            Assert.Equal(7d, preserved!.Value);
            var lifecycle = CreateLifecycle(source, store, runtime, clock);
            Assert.Equal("expired-to-demo", (await Guard(source, lifecycle, runtime, clock).RunOnceAsync()).Code);
        }
        var allowed = true;
        try { await runtime.WriteAsync(tag.Id, 9d); }
        catch (InvalidOperationException) { allowed = false; }
        if (allowed)
        {
            Assert.True(runtime.TryGetCurrent(tag.Id, out var value));
            Assert.Equal(9d, value!.Value);
        }
        await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new ProcessResult(Environment.ProcessId, allowed, runtime.Tags().Count)));
    }

    private static async Task WaitForProcessFileAsync(string root, string name, Process? worker = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        while (!File.Exists(Path.Combine(root, name)))
        {
            if (worker?.HasExited == true) throw new InvalidOperationException("License test worker exited before its barrier.");
            await Task.Delay(25, timeout.Token);
        }
    }
    private sealed record ProcessInput(string LicensePath, string Fingerprint, int Count, bool SeparateFiles);
    private sealed record ProcessResult(int ProcessId, bool Allowed, int Tags);
    private sealed class ProcessMachine(string fingerprint) : IMachineIdentityProvider
    {
        public string GetMachineFingerprint() => fingerprint;
    }
}
