using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;

namespace Scada.Drivers.Tests;

/// <summary>
/// Process-level Stage 1 HA evidence. The API instances are independent child processes;
/// only the reference authority file is shared, as in the supported deployment topology.
/// The separate-database mode creates two database names on the same disposable PostgreSQL
/// server; it does not simulate loss of an independent PostgreSQL server. All files/databases
/// created by this test are disposable.
/// </summary>
[Collection(Wave15PostgreSqlHaEvidenceCollection.Name)]
public sealed class AuthorityPolicyTwoApiPostgreSqlEvidenceTests
{
    private const string AdminUsername = "w15-stage1-admin";
    private const string AdminPassword = "w15-stage1-disposable-admin-password";
    private const string ProjectKey = "w15-stage1-process-evidence";
    private const string JwtKey = "w15-stage1-integration-jwt-signing-key-only";
    private const string PeerSecret = "w15-stage1-integration-peer-secret-32-bytes";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealApiProcesses_RevokeCancelRejoinAndRequireExplicitReactivation(bool sharedDatabase)
    {
        var seed = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(seed)) return;

        var root = FindRepositoryRoot();
        var apiDll = Path.Combine(AppContext.BaseDirectory, "Scada.Api.dll");
        if (!File.Exists(apiDll))
            apiDll = Path.Combine(root, "src", "Scada.Api", "bin", "Release", "net10.0", "Scada.Api.dll");
        Assert.True(File.Exists(apiDll), $"Scada.Api child process binary was not found: {apiDll}");

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var databaseA = "w15_int_a_" + suffix;
        var databaseB = sharedDatabase ? databaseA : "w15_int_b_" + suffix;
        var role = "w15_int_" + suffix;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var adminConnection = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = "postgres",
            Pooling = false
        };
        var appConnectionA = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = databaseA,
            Username = role,
            Password = password,
            Pooling = true,
            ApplicationName = "w15-stage1-node-a-" + suffix,
            MaxPoolSize = 32
        };
        var appConnectionB = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = databaseB,
            Username = role,
            Password = password,
            Pooling = true,
            ApplicationName = "w15-stage1-node-b-" + suffix,
            MaxPoolSize = 32
        };
        var directory = Path.Combine(Path.GetTempPath(), "elitescada-w15-two-api-" + suffix);
        Directory.CreateDirectory(directory);

        ApiProcess? nodeA = null;
        ApiProcess? nodeB = null;
        var databasesCreated = new List<string>();
        var roleCreated = false;
        try
        {
            await CreateRoleAsync(adminConnection.ConnectionString, role, password);
            roleCreated = true;
            await CreateDatabaseAsync(adminConnection.ConnectionString, databaseA, role);
            databasesCreated.Add(databaseA);
            if (!sharedDatabase)
            {
                await CreateDatabaseAsync(adminConnection.ConnectionString, databaseB, role);
                databasesCreated.Add(databaseB);
            }
            await InstallTimescaleExtensionAsync(adminConnection.ConnectionString, databaseA);
            if (!sharedDatabase)
                await InstallTimescaleExtensionAsync(adminConnection.ConnectionString, databaseB);

            var license = CreateLicense(directory);
            var portA = ReserveLoopbackPort();
            var portB = ReserveLoopbackPort();
            Assert.NotEqual(portA, portB);
            var urlA = $"http://127.0.0.1:{portA}";
            var urlB = $"http://127.0.0.1:{portB}";
            var referencePath = Path.Combine(directory, "ha-reference.json");

            nodeA = ApiProcess.Start(
                "node-a", urlA, urlB, ProjectKey, referencePath, directory,
                appConnectionA.ConnectionString, license, apiDll, root, suffix);
            await nodeA.WaitForHealthAsync();
            await nodeA.BootstrapAsync();

            nodeB = ApiProcess.Start(
                "node-b", urlB, urlA, ProjectKey, referencePath, directory,
                appConnectionB.ConnectionString, license, apiDll, root, suffix);
            await nodeB.WaitForHealthAsync();
            if (sharedDatabase)
                await nodeB.LoginAsync();
            else
                await nodeB.BootstrapAsync();

            await PrepareProjectAsync(nodeA, activate: true);
            if (!sharedDatabase)
            {
                await PrepareProjectAsync(nodeB, activate: false);
                // A fresh neutral installation starts with process effects fenced.
                // Once the standby's first Application is attached and published,
                // restart it so startup recovers that binding before HA passive
                // Runtime projection begins. Do not locally activate the standby.
                await nodeB.DisposeAsync();
                nodeB = null;
                nodeB = ApiProcess.Start(
                    "node-b", urlB, urlA, ProjectKey, referencePath, directory,
                    appConnectionB.ConnectionString, license, apiDll, root, suffix);
                await nodeB.WaitForHealthAsync();
                await nodeB.LoginAsync();
            }
            await WaitForInitialPolicyCommitAsync(nodeA, nodeB, referencePath);
            var grantedPolicy = await GrantIntelligenceManageAsync(nodeA, nodeB);
            await ConfigureAndActivateIntelligenceAsync(nodeA);
            if (!sharedDatabase)
                await MirrorProjectToStandbyAsync(nodeA, nodeB);
            await SeedSlowHistorianAsync(appConnectionA.ConnectionString);

            await nodeB.StopAsync();
            var pendingRequest = nodeA.PostRawAsync(
                "/api/engineering/asset-process-intelligence/analytics/query",
                AnalyticsRequest());
            try
            {
                await WaitForHistorianReadAsync(
                    adminConnection.ConnectionString,
                    databaseA,
                    TimeSpan.FromSeconds(15),
                    "w15-stage1-page-2");
            }
            catch (TimeoutException exception)
            {
                if (pendingRequest.IsCompleted)
                {
                    var response = await pendingRequest;
                    throw new InvalidOperationException(
                        $"Historian query did not remain in flight. API returned {(int)response.StatusCode}: {response.RawBody}",
                        exception);
                }
                throw;
            }

            var committedPolicy = await nodeA.GetAsync("/api/auth/authority-policy");
            var revokedRoles = RemoveTrendUse(committedPolicy["roles"]!.DeepClone().AsArray());
            var revoke = await nodeA.SendAsync(
                HttpMethod.Put,
                "/api/auth/authority-policy",
                new JsonObject
                {
                    ["schema"] = "elitescada.authority-policy",
                    ["schemaVersion"] = 1,
                    ["expectedVersion"] = committedPolicy["version"]!.GetValue<long>(),
                    ["roles"] = revokedRoles,
                    ["scopes"] = committedPolicy["scopes"]!.DeepClone()
                },
                expectedStatus: HttpStatusCode.ServiceUnavailable);
            Assert.Equal("AUTHORITY_POLICY_HA_REPLICATION_PENDING", revoke["error"]!.GetValue<string>());

            var cancelled = await pendingRequest.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
            Assert.Equal("INTELLIGENCE_EXECUTION_PERMIT_CANCELLED", cancelled.Body?["code"]?.GetValue<string>());
            Assert.Null(cancelled.Body?["period"]);
            Assert.Equal(0, await ReadPageThreeObservationCountAsync(appConnectionA.ConnectionString));

            await RemoveSlowHistorianPolicyAsync(appConnectionA.ConnectionString);
            await nodeB.DisposeAsync();
            nodeB = null;
            nodeB = ApiProcess.Start(
                "node-b", urlB, urlA, ProjectKey, referencePath, directory,
                appConnectionB.ConnectionString, license, apiDll, root, suffix);
            await nodeB.WaitForHealthAsync();
            if (sharedDatabase)
                await nodeB.LoginAsync();
            else
                await nodeB.LoginAsync();

            var revokedVersion = committedPolicy["version"]!.GetValue<long>() + 1;
            await WaitForRejoinAndPolicyCommitAsync(nodeA, nodeB, referencePath, revokedVersion);
            var restore = await nodeA.SendAsync(
                HttpMethod.Put,
                "/api/auth/authority-policy",
                new JsonObject
                {
                    ["schema"] = "elitescada.authority-policy",
                    ["schemaVersion"] = 1,
                    ["expectedVersion"] = revokedVersion,
                    ["roles"] = grantedPolicy["roles"]!.DeepClone(),
                    ["scopes"] = grantedPolicy["scopes"]!.DeepClone()
                });
            var restoredVersion = revokedVersion + 1;
            Assert.Equal(restoredVersion, restore["version"]!.GetValue<long>());
            await WaitForPolicyVersionAsync(nodeB, restoredVersion);

            var scopeId = Guid.Parse("b3100000-0000-0000-0000-000000000201");
            var beforeExplicitActivation = await nodeA.GetAsync("/api/engineering/asset-process-intelligence/scopes");
            Assert.True(beforeExplicitActivation["authorityFence"]?["allowed"]?.GetValue<bool>());
            Assert.Equal(
                restoredVersion,
                beforeExplicitActivation["authorityFence"]?["policyVersion"]?.GetValue<long>());
            Assert.True(
                beforeExplicitActivation["configuration"]?["scopes"]?[0]?["active"]?.GetValue<bool>(),
                beforeExplicitActivation.ToJsonString());
            Assert.False(
                beforeExplicitActivation["configuration"]?["scopes"]?[0]?["executionArmed"]?.GetValue<bool>() ?? false,
                beforeExplicitActivation.ToJsonString());

            var deniedBeforeExplicitActivation = await nodeA.SendAsync(
                HttpMethod.Post,
                "/api/engineering/asset-process-intelligence/analytics/query",
                AnalyticsRequest(),
                expectedStatus: HttpStatusCode.Conflict);
            Assert.Equal(
                "INTELLIGENCE_EXECUTION_PERMIT_REQUIRED",
                deniedBeforeExplicitActivation["code"]!.GetValue<string>());
            Assert.Null(deniedBeforeExplicitActivation["period"]);

            var reactivated = await nodeA.SendAsync(
                HttpMethod.Post,
                $"/api/engineering/asset-process-intelligence/scopes/equipment/{scopeId:D}/activate",
                new JsonObject());
            Assert.True(reactivated["active"]!.GetValue<bool>());

            var recoveredQuery = await nodeA.SendAsync(
                HttpMethod.Post,
                "/api/engineering/asset-process-intelligence/analytics/query",
                AnalyticsRequest());
            Assert.Equal(1, recoveredQuery["contractVersion"]!.GetValue<int>());
            Assert.True(recoveredQuery["period"]?["summary"]?["rawObservationCount"]?.GetValue<int>() > 0);
            Assert.Equal(restoredVersion, (await nodeA.GetAsync("/api/auth/authority-policy"))["version"]!.GetValue<long>());
            Assert.Equal(restoredVersion, (await nodeB.GetAsync("/api/auth/authority-policy"))["version"]!.GetValue<long>());
        }
        finally
        {
            if (nodeB is not null) await nodeB.DisposeAsync();
            if (nodeA is not null) await nodeA.DisposeAsync();
            foreach (var database in databasesCreated.AsEnumerable().Reverse())
                await DropDatabaseAsync(adminConnection.ConnectionString, database);
            if (roleCreated)
                await DropRoleAsync(adminConnection.ConnectionString, role);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public Task RealApiProcesses_HaD2SharedDatabasePromotesAndFailsBack()
        => RunHaD2AuthorityLifecycleAsync(sharedDatabase: true);

    [Fact]
    public Task RealApiProcesses_HaD2SeparateDatabasesPromotesAndFailsBack()
        => RunHaD2AuthorityLifecycleAsync(sharedDatabase: false);

    private async Task RunHaD2AuthorityLifecycleAsync(bool sharedDatabase)
    {
        var seed = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(seed))
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("ELITESCADA_REQUIRE_HA_D2_POSTGRES"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "HA-D2 acceptance requires ELITESCADA_TEST_POSTGRES; the evidence test must not silently pass without PostgreSQL.");
            }

            return;
        }

        var root = FindRepositoryRoot();
        var apiDll = Path.Combine(AppContext.BaseDirectory, "Scada.Api.dll");
        if (!File.Exists(apiDll))
            apiDll = Path.Combine(root, "src", "Scada.Api", "bin", "Release", "net10.0", "Scada.Api.dll");
        Assert.True(File.Exists(apiDll), $"Scada.Api child process binary was not found: {apiDll}");

        // These authority-lifecycle variants use either one shared disposable database
        // name or two separate disposable names on the same PostgreSQL server. They do
        // not simulate independent PostgreSQL server failure or prove database durability.
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var databaseA = "w15_ha_d2_a_" + suffix;
        var databaseB = sharedDatabase ? databaseA : "w15_ha_d2_b_" + suffix;
        var role = "w15_ha_d2_" + suffix;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var adminConnection = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = "postgres",
            Pooling = false
        };
        var appConnectionA = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = databaseA,
            Username = role,
            Password = password,
            Pooling = true,
            ApplicationName = "w15-ha-d2-node-a-" + suffix,
            MaxPoolSize = 32
        };
        var appConnectionB = new NpgsqlConnectionStringBuilder(seed)
        {
            Database = databaseB,
            Username = role,
            Password = password,
            Pooling = true,
            ApplicationName = "w15-ha-d2-node-b-" + suffix,
            MaxPoolSize = 32
        };
        var directory = Path.Combine(Path.GetTempPath(), "elitescada-w15-ha-d2-" + suffix);
        Directory.CreateDirectory(directory);

        ApiProcess? nodeA = null;
        ApiProcess? nodeB = null;
        var databasesCreated = new List<string>();
        var roleCreated = false;
        try
        {
            await CreateRoleAsync(adminConnection.ConnectionString, role, password);
            roleCreated = true;
            foreach (var database in new[] { databaseA, databaseB }.Distinct(StringComparer.Ordinal))
            {
                await CreateDatabaseAsync(adminConnection.ConnectionString, database, role);
                databasesCreated.Add(database);
                await InstallTimescaleExtensionAsync(adminConnection.ConnectionString, database);
            }

            var license = CreateLicense(directory);
            var portA = ReserveLoopbackPort();
            var portB = ReserveLoopbackPort();
            Assert.NotEqual(portA, portB);
            var urlA = $"http://127.0.0.1:{portA}";
            var urlB = $"http://127.0.0.1:{portB}";
            var referencePath = Path.Combine(directory, "ha-reference.json");

            nodeA = ApiProcess.Start(
                "node-a", urlA, urlB, ProjectKey, referencePath, directory,
                appConnectionA.ConnectionString, license, apiDll, root, suffix);
            await nodeA.WaitForHealthAsync();
            await nodeA.BootstrapAsync();

            nodeB = ApiProcess.Start(
                "node-b", urlB, urlA, ProjectKey, referencePath, directory,
                appConnectionB.ConnectionString, license, apiDll, root, suffix);
            await nodeB.WaitForHealthAsync();
            if (sharedDatabase)
                await nodeB.LoginAsync();
            else
                await nodeB.BootstrapAsync();

            await PrepareProjectAsync(nodeA, activate: true);
            if (sharedDatabase)
                await WaitForInitialPolicyCommitAsync(nodeA, nodeB, referencePath);
            if (!sharedDatabase)
                await PrepareProjectAsync(nodeB, activate: false);
            // Node B was started before the first published revision existed. In the
            // separate-database case, prepare/publish its local copy without activating
            // it. Restart after provisioning so D2 starts from a loaded standby rather
            // than an empty-runtime case. Shared-database policy commit is established
            // before restart; isolated-database policy commit follows standby bootstrap.
            await nodeB.DisposeAsync();
            nodeB = null;
            nodeB = ApiProcess.Start(
                "node-b", urlB, urlA, ProjectKey, referencePath, directory,
                appConnectionB.ConnectionString, license, apiDll, root, suffix);
            await nodeB.WaitForHealthAsync();
            await nodeB.LoginAsync();
            if (!sharedDatabase)
            {
                await WaitForInitialPolicyCommitAsync(nodeA, nodeB, referencePath);
                await MirrorProjectToStandbyAsync(nodeA, nodeB);
            }

            var initialA = await WaitForHaTopologyAsync(
                nodeA,
                topology => IsEffectiveActive(topology, "node-a") && IsReadyStandby(topology, "node-b"),
                TimeSpan.FromSeconds(75),
                "the licensed standby never became Ready before the active process stopped");
            var initialB = await WaitForHaTopologyAsync(
                nodeB,
                topology => IsEffectiveActive(topology, "node-a") && IsReadyStandby(topology, "node-b"),
                TimeSpan.FromSeconds(20),
                "the standby process did not observe the initial single authority");
            AssertOneActive(initialA, "node-a");
            AssertOneActive(initialB, "node-a");
            var initialEpoch = initialA["authorityEpoch"]!.GetValue<long>();

            // OS-process loss: the standby must use the fresh pre-failure witness and
            // reference fence to claim a strictly newer authority epoch.
            await nodeA.DisposeAsync();
            nodeA = null;
            var promoted = await WaitForHaTopologyAsync(
                nodeB,
                topology => IsEffectiveActive(topology, "node-b") &&
                    topology["authorityEpoch"]!.GetValue<long>() > initialEpoch,
                TimeSpan.FromSeconds(90),
                "a Ready standby did not automatically claim authority after active-process loss");
            AssertOneActive(promoted, "node-b");
            var promotedEpoch = promoted["authorityEpoch"]!.GetValue<long>();

            // Rejoin the old node with the same NodeId. It must reconcile the newer
            // persisted epoch as Standby; restart alone must not restore its authority.
            nodeA = ApiProcess.Start(
                "node-a", urlA, urlB, ProjectKey, referencePath, directory,
                appConnectionA.ConnectionString, license, apiDll, root, suffix);
            await nodeA.WaitForHealthAsync();
            await nodeA.LoginAsync();
            var rejoinedA = await WaitForHaTopologyAsync(
                nodeA,
                topology => IsEffectiveActive(topology, "node-b") &&
                    IsReadyStandby(topology, "node-a") &&
                    !IsActive(topology, "node-a"),
                TimeSpan.FromSeconds(60),
                "the restarted old active did not rejoin as a fresh Ready, fenced Standby");
            var rejoinedB = await WaitForHaTopologyAsync(
                nodeB,
                topology => IsEffectiveActive(topology, "node-b") &&
                    IsReadyStandby(topology, "node-a"),
                TimeSpan.FromSeconds(20),
                "the active process did not observe the restarted peer as a fresh Ready Standby");
            AssertOneActive(rejoinedA, "node-b");
            AssertOneActive(rejoinedB, "node-b");
            Assert.Equal(promotedEpoch, rejoinedA["authorityEpoch"]!.GetValue<long>());

            // This request traverses the authenticated API endpoint with the temporary
            // V3 HaRuntime license installed in both real processes; no security/license
            // bypass is used for the controlled failback.
            var failback = await nodeB.PostRawAsync(
                "/api/runtime/ha/actions/failback",
                new JsonObject { ["targetNodeId"] = "node-a" });
            Assert.True(
                failback.StatusCode == HttpStatusCode.Accepted,
                $"Expected authenticated licensed failback to be accepted, got {(int)failback.StatusCode}: {failback.Body?.ToJsonString() ?? failback.RawBody}");
            Assert.Equal("explicit-failback", failback.Body?["kind"]?.GetValue<string>());
            Assert.Equal("completed", failback.Body?["state"]?.GetValue<string>());

            var failedBackA = await WaitForHaTopologyAsync(
                nodeA,
                topology => IsEffectiveActive(topology, "node-a") &&
                    topology["authorityEpoch"]!.GetValue<long>() > promotedEpoch,
                TimeSpan.FromSeconds(60),
                "the authenticated, licensed failback did not return authority to the preferred node");
            var failedBackB = await WaitForHaTopologyAsync(
                nodeB,
                topology => IsEffectiveActive(topology, "node-a"),
                TimeSpan.FromSeconds(20),
                "the former active did not observe the completed failback");
            AssertOneActive(failedBackA, "node-a");
            AssertOneActive(failedBackB, "node-a");

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                evidence = "HA-D2 two-process authority lifecycle",
                initialActive = "node-a",
                automaticFailoverActive = "node-b",
                explicitFailbackActive = "node-a",
                initialEpoch,
                promotedEpoch,
                failedBackEpoch = failedBackA["authorityEpoch"]!.GetValue<long>(),
                databaseTopology = sharedDatabase
                    ? "both API processes used one disposable shared database name"
                    : "each API process used its own disposable database name on the same PostgreSQL server",
                restartRecovery = "same-NodeId rejoined Ready Standby at the newer persisted epoch",
                runtimeSessionLeaseEvidence = new[]
                {
                    "RuntimeHighAvailabilityTests.RuntimeSessionAuthorityTakeover_RebindsSameLogicalSeatWithoutSecondAdmission",
                    "RuntimeHighAvailabilityPeerBoundaryTests.SessionContinuityImport_PreservesOneLogicalLeaseAndRejectsStaleOrResurrectedState"
                }
            }, Json));
        }
        finally
        {
            if (nodeB is not null) await nodeB.DisposeAsync();
            if (nodeA is not null) await nodeA.DisposeAsync();
            foreach (var database in databasesCreated.AsEnumerable().Reverse())
                await DropDatabaseAsync(adminConnection.ConnectionString, database);
            if (roleCreated)
                await DropRoleAsync(adminConnection.ConnectionString, role);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<JsonObject> WaitForHaTopologyAsync(
        ApiProcess node,
        Func<JsonObject, bool> predicate,
        TimeSpan timeout,
        string failure)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        JsonObject? last = null;
        Exception? lastException = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                last = (await node.GetAsync("/api/runtime/ha/topology")).AsObject();
                if (predicate(last)) return last;
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or JsonException)
            {
                lastException = exception;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"{failure}. Last topology: {last?.ToJsonString() ?? "<unavailable>"}. " +
            $"Last error: {lastException?.Message ?? "<none>"}");
    }

    private static bool IsEffectiveActive(JsonObject topology, string nodeId) =>
        string.Equals(topology["effectiveActiveNodeId"]?.GetValue<string>(), nodeId, StringComparison.OrdinalIgnoreCase);

    private static JsonObject HaNode(JsonObject topology, string nodeId) =>
        topology["nodes"]!.AsArray()
            .Select(entry => entry!.AsObject())
            .Single(entry => string.Equals(entry["nodeId"]?.GetValue<string>(), nodeId, StringComparison.OrdinalIgnoreCase));

    private static bool IsReady(JsonObject topology, string nodeId) =>
        HaNode(topology, nodeId)["ready"]?.GetValue<bool>() == true;

    private static bool IsFresh(JsonObject topology, string nodeId) =>
        HaNode(topology, nodeId)["fresh"]?.GetValue<bool>() == true;

    private static bool IsActive(JsonObject topology, string nodeId) =>
        IsInState(HaNode(topology, nodeId), Scada.Api.Runtime.RuntimeHaState.Active);

    private static bool IsReadyStandby(JsonObject topology, string nodeId)
    {
        var node = HaNode(topology, nodeId);
        return IsReady(topology, nodeId) && IsFresh(topology, nodeId) &&
            IsInState(node, Scada.Api.Runtime.RuntimeHaState.ReadyStandby);
    }

    private static bool IsInState(JsonObject node, Scada.Api.Runtime.RuntimeHaState expected)
    {
        var state = node["state"];
        if (state is null) return false;
        if (state.GetValueKind() == JsonValueKind.String)
        {
            return Enum.TryParse<Scada.Api.Runtime.RuntimeHaState>(state.GetValue<string>(), true, out var parsed) &&
                parsed == expected;
        }

        return state.GetValueKind() == JsonValueKind.Number &&
            state.GetValue<int>() == (int)expected;
    }

    private static void AssertOneActive(JsonObject topology, string expectedNodeId)
    {
        Assert.True(IsEffectiveActive(topology, expectedNodeId), topology.ToJsonString());
        var activeNodes = topology["nodes"]!.AsArray()
            .Count(entry => entry is JsonObject node &&
                IsInState(node, Scada.Api.Runtime.RuntimeHaState.Active));
        Assert.Equal(1, activeNodes);
    }

    private static LicenseFiles CreateLicense(string directory)
    {
        using var key = RSA.Create(2048);
        var keyId = "w15-process-test-" + Guid.NewGuid().ToString("N");
        var publicKeyPath = Path.Combine(directory, "license-public.pem");
        File.WriteAllText(publicKeyPath, key.ExportSubjectPublicKeyInfoPem(), Encoding.ASCII);
        var machine = new DefaultMachineIdentityProvider().GetMachineFingerprint();
        var license = EliteScadaLicenseCodec.CreateSignedLicenseV3(
            new EliteScadaLicenseV3Payload(
                EliteScadaLicenseCodec.LicenseV3SchemaVersion,
                Guid.NewGuid().ToString("D"),
                machine,
                LicenseTier.Tags5000,
                DateTimeOffset.UtcNow,
                null,
                keyId,
                ViewOnlySeats: 2,
                InteractiveSeats: 2,
                HaRuntime: true,
                AssetProcessIntelligence: true),
            key);
        var licensePath = Path.Combine(directory, "test-license.license");
        File.WriteAllText(licensePath, license, Encoding.UTF8);
        return new LicenseFiles(licensePath, publicKeyPath, keyId);
    }

    private static async Task PrepareProjectAsync(ApiProcess node, bool activate)
    {
        var first = await node.SendAsync(
            HttpMethod.Post,
            "/api/engineering/persistence/projects/first",
            new JsonObject { ["projectKey"] = ProjectKey, ["projectName"] = "W15 Stage 1 process evidence" },
            expectedStatus: HttpStatusCode.Created);

        var package = await node.GetAsync("/api/engineering/export/json");
        const string simulationSourceKey = "w15-stage1-simulation";
        var simulationSource = new JsonObject
        {
            ["id"] = "b3100000-0000-0000-0000-000000000200",
            ["key"] = simulationSourceKey,
            ["name"] = "Stage 1 evidence simulation",
            ["driver"] = "builtin.simulation",
            ["enabled"] = true,
            ["settings"] = new JsonObject { ["scanIntervalMilliseconds"] = "100" }
        };
        if (package["dataSources"] is JsonArray dataSources)
            dataSources.Add(simulationSource);
        else
            package["dataSources"] = new JsonArray(simulationSource);
        var tags = package["tags"]!.AsArray();
        tags.Add(new JsonObject
        {
            ["id"] = "b3100000-0000-0000-0000-000000000202",
            ["name"] = "Pressure",
            ["path"] = "Plant.Pump.Pressure",
            ["dataType"] = "double",
            ["source"] = simulationSourceKey,
            ["dataSourceId"] = "b3100000-0000-0000-0000-000000000200",
            ["engineeringUnit"] = "bar",
            ["readOnly"] = true
        });
        package["equipment"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "b3100000-0000-0000-0000-000000000201",
            ["path"] = "Plant.Pump",
            ["name"] = "Evidence Pump"
        });
        await node.SendAsync(
            HttpMethod.Post,
            "/api/engineering/import/json/apply",
            package);

        var saved = await node.SendAsync(
            HttpMethod.Post,
            $"/api/engineering/persistence/{ProjectKey}/save",
            new JsonObject { ["projectName"] = "W15 Stage 1 process evidence", ["savedBy"] = "process-evidence" });
        var revision = saved["revision"]!.GetValue<long>();
        await node.SendAsync(
            HttpMethod.Post,
            $"/api/engineering/persistence/{ProjectKey}/revisions/{revision}/publish",
            new JsonObject { ["publishedBy"] = "process-evidence" });

        if (activate)
        {
            var result = await node.SendAsync(
                HttpMethod.Post,
                $"/api/engineering/persistence/{ProjectKey}/published/activate",
                new JsonObject { ["activatedBy"] = "process-evidence" });
            Assert.True(result["activated"]!.GetValue<bool>(), result.ToJsonString());

            var runtimeTag = await node.GetAsync("/api/tags/by-path/Plant.Pump.Pressure");
            Assert.Equal(
                Guid.Parse("b3100000-0000-0000-0000-000000000202"),
                runtimeTag["tag"]!["id"]!.GetValue<Guid>());
        }
        else
        {
            Assert.True(first["revision"]?["revision"]!.GetValue<long>() > 0);
        }
    }

    private static async Task ConfigureAndActivateIntelligenceAsync(ApiProcess node)
    {
        var tagId = Guid.Parse("b3100000-0000-0000-0000-000000000202");
        var equipmentId = Guid.Parse("b3100000-0000-0000-0000-000000000201");
        var profileId = Guid.Parse("b3100000-0000-0000-0000-000000000203");
        var assignmentId = Guid.Parse("b3100000-0000-0000-0000-000000000204");
        var signalId = Guid.Parse("b3100000-0000-0000-0000-000000000205");
        var scopeId = equipmentId;

        await node.SendAsync(
            HttpMethod.Put,
            "/api/engineering/asset-process-intelligence/scopes",
            new JsonObject
            {
                ["scopes"] = new JsonArray(new JsonObject { ["kind"] = 0, ["scopeId"] = scopeId.ToString("D") })
            });
        await node.SendAsync(
            HttpMethod.Put,
            "/api/engineering/asset-process-intelligence/configuration",
            new JsonObject
            {
                ["profiles"] = new JsonArray(new JsonObject
                {
                    ["id"] = profileId.ToString("D"),
                    ["key"] = "w15.evidence.pressure",
                    ["name"] = "Pressure evidence profile",
                    ["signals"] = new JsonArray(new JsonObject
                    {
                        ["id"] = signalId.ToString("D"),
                        ["key"] = "pressure",
                        ["signalRole"] = "pressure",
                        ["tagId"] = tagId.ToString("D"),
                        ["required"] = true
                    })
                }),
                ["assignments"] = new JsonArray(new JsonObject
                {
                    ["id"] = assignmentId.ToString("D"),
                    ["equipmentId"] = equipmentId.ToString("D"),
                    ["profileId"] = profileId.ToString("D")
                }),
                ["processStages"] = new JsonArray()
            });

        // Configure endpoints mark the Engineering workspace dirty. The activation
        // contract requires the current runtime to be the published active revision,
        // so persist, publish, and activate this derived revision before activating
        // the intelligence scope itself.
        var saved = await node.SendAsync(
            HttpMethod.Post,
            $"/api/engineering/persistence/{ProjectKey}/save",
            new JsonObject { ["projectName"] = "W15 Stage 1 process evidence", ["savedBy"] = "process-evidence" });
        var revision = saved["revision"]!.GetValue<long>();
        await node.SendAsync(
            HttpMethod.Post,
            $"/api/engineering/persistence/{ProjectKey}/revisions/{revision}/publish",
            new JsonObject { ["publishedBy"] = "process-evidence" });
        var runtimeActivation = await node.SendAsync(
            HttpMethod.Post,
            $"/api/engineering/persistence/{ProjectKey}/published/activate",
            new JsonObject { ["activatedBy"] = "process-evidence" });
        Assert.True(runtimeActivation["activated"]!.GetValue<bool>(), runtimeActivation.ToJsonString());

        var activated = await node.SendAsync(
            HttpMethod.Post,
            $"/api/engineering/asset-process-intelligence/scopes/equipment/{scopeId:D}/activate",
            new JsonObject());
        Assert.True(activated["active"]!.GetValue<bool>(), activated.ToJsonString());
        var executionStatus = await node.GetAsync("/api/engineering/asset-process-intelligence/scopes");
        Assert.True(
            executionStatus["configuration"]?["scopes"]?[0]?["executionArmed"]?.GetValue<bool>(),
            executionStatus.ToJsonString());
    }

    private static async Task MirrorProjectToStandbyAsync(ApiProcess active, ApiProcess standby)
    {
        var application = await active.GetAsync("/api/engineering/export/json");
        var imported = await standby.SendAsync(
            HttpMethod.Post,
            "/api/engineering/import/json/apply",
            application);
        Assert.Empty(imported["issues"]?.AsArray() ?? []);

        var saved = await standby.SendAsync(
            HttpMethod.Post,
            $"/api/engineering/persistence/{ProjectKey}/save",
            new JsonObject { ["projectName"] = "W15 Stage 1 process evidence", ["savedBy"] = "process-evidence-standby-mirror" });
        var revision = saved["revision"]!.GetValue<long>();
        await standby.SendAsync(
            HttpMethod.Post,
            $"/api/engineering/persistence/{ProjectKey}/revisions/{revision}/publish",
            new JsonObject { ["publishedBy"] = "process-evidence-standby-mirror" });
    }

    private static async Task<JsonObject> GrantIntelligenceManageAsync(ApiProcess a, ApiProcess b)
    {
        var policy = (await a.GetAsync("/api/auth/authority-policy")).AsObject();
        var equipmentId = Guid.Parse("b3100000-0000-0000-0000-000000000201");
        var roles = policy["roles"]!.DeepClone().AsArray();
        var developer = roles.Select(role => role!.AsObject())
            .Single(role => string.Equals(role["key"]?.GetValue<string>(), "developer", StringComparison.OrdinalIgnoreCase));
        developer["grants"]!.AsArray().Add(new JsonObject
        {
            ["capability"] = "assetProcessIntelligenceManage",
            ["scope"] = new JsonObject
            {
                ["scopeNodeId"] = equipmentId.ToString("D"),
                ["includeDescendants"] = false
            }
        });

        var scopes = policy["scopes"]!.DeepClone().AsArray();
        scopes.Add(new JsonObject
        {
            ["id"] = equipmentId.ToString("D"),
            ["key"] = "equipment.plant.pump",
            ["name"] = "Plant.Pump",
            // This API's HttpJsonOptions serialize enums numerically; only the HA
            // replication wire applies the lower-camel string enum converter.
            ["kind"] = 2,
            ["resourceId"] = equipmentId.ToString("D")
        });

        var updated = await a.SendAsync(
            HttpMethod.Put,
            "/api/auth/authority-policy",
            new JsonObject
            {
                ["schema"] = "elitescada.authority-policy",
                ["schemaVersion"] = 1,
                ["expectedVersion"] = policy["version"]!.GetValue<long>(),
                ["roles"] = roles,
                ["scopes"] = scopes
            });
        Assert.Equal(policy["version"]!.GetValue<long>() + 1, updated["version"]!.GetValue<long>());
        await WaitForPolicyVersionAsync(b, updated["version"]!.GetValue<long>());
        return updated.AsObject();
    }

    private static async Task WaitForInitialPolicyCommitAsync(ApiProcess a, ApiProcess b, string referencePath)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        var lastState = "No policy status collected.";
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var documentA = await a.GetAsync("/api/auth/authority-policy");
                var documentB = await b.GetAsync("/api/auth/authority-policy");
                var reference = JsonNode.Parse(await File.ReadAllTextAsync(referencePath));
                var policy = reference?["authorityPolicy"];
                lastState = $"A={documentA.ToJsonString()}; B={documentB.ToJsonString()}; reference={policy?.ToJsonString() ?? "<missing>"}";
                if (documentA["version"]?.GetValue<long>() == documentB["version"]?.GetValue<long>() &&
                    policy?["committedVersion"]?.GetValue<long>() == documentA["version"]!.GetValue<long>() &&
                    policy?["pendingVersion"] is null)
                    return;
            }
            catch (Exception exception) when (exception is IOException or HttpRequestException or JsonException)
            {
                lastState = exception.Message;
            }
            await Task.Delay(150);
        }

        throw new TimeoutException(
            $"Initial policy was not committed and mirrored through the two API processes. Last observed state: {lastState}. " +
            $"A log: {a.LastOutput()}{Environment.NewLine}B log: {b.LastOutput()}");
    }

    private static async Task WaitForRejoinAndPolicyCommitAsync(ApiProcess a, ApiProcess b, string referencePath, long revokedVersion)
    {
        await WaitUntilAsync(async () =>
        {
            try
            {
                var documentA = await a.GetAsync("/api/auth/authority-policy");
                var documentB = await b.GetAsync("/api/auth/authority-policy");
                var reference = JsonNode.Parse(await File.ReadAllTextAsync(referencePath));
                var policy = reference?["authorityPolicy"];
                return documentA["version"]?.GetValue<long>() == revokedVersion &&
                    documentB["version"]?.GetValue<long>() == revokedVersion &&
                    policy?["committedVersion"]?.GetValue<long>() == revokedVersion &&
                    policy?["pendingVersion"] is null;
            }
            catch (Exception exception) when (exception is IOException or HttpRequestException or JsonException)
            {
                return false;
            }
        }, TimeSpan.FromSeconds(40), "Restarted standby did not rejoin and acknowledge the pending revoked policy.");
    }

    private static async Task WaitForPolicyVersionAsync(ApiProcess node, long version) =>
        await WaitUntilAsync(async () =>
        {
            try { return (await node.GetAsync("/api/auth/authority-policy"))["version"]?.GetValue<long>() == version; }
            catch (HttpRequestException) { return false; }
        }, TimeSpan.FromSeconds(20), $"Node {node.NodeId} did not reach policy version {version}.");

    private static JsonArray RemoveTrendUse(JsonArray roles)
    {
        var developer = roles.Select(role => role!.AsObject())
            .Single(role => string.Equals(role["key"]?.GetValue<string>(), "developer", StringComparison.OrdinalIgnoreCase));
        var grants = developer["grants"]!.AsArray();
        for (var index = grants.Count - 1; index >= 0; index--)
        {
            if (string.Equals(grants[index]?["capability"]?.GetValue<string>(), "trendUse", StringComparison.OrdinalIgnoreCase))
                grants.RemoveAt(index);
        }
        Assert.DoesNotContain(grants, grant => string.Equals(
            grant?["capability"]?.GetValue<string>(), "trendUse", StringComparison.OrdinalIgnoreCase));
        return roles;
    }

    private static JsonObject AnalyticsRequest() => new()
    {
        ["scopeKind"] = 0,
        ["scopeId"] = "b3100000-0000-0000-0000-000000000201",
        ["tagId"] = "b3100000-0000-0000-0000-000000000202",
        ["window"] = new JsonObject
        {
            ["fromUtc"] = DateTimeOffset.UtcNow.AddHours(-1).ToString("O"),
            ["toUtc"] = DateTimeOffset.UtcNow.ToString("O")
        },
        ["bucketSeconds"] = 900
    };

    private static async Task SeedSlowHistorianAsync(string appConnectionString)
    {
        await using var connection = new NpgsqlConnection(appConnectionString);
        await connection.OpenAsync();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        await using var transaction = await connection.BeginTransactionAsync();
        await using var insert = new NpgsqlCommand(
            "INSERT INTO elitescada.tag_history(tag_id, ts, quality, source, value, data_type) VALUES (@tag, @ts, @quality, 'w15-stage1-process-evidence', @value, @data_type)",
            connection,
            transaction);
        insert.Parameters.Add("tag", NpgsqlDbType.Uuid).Value = Guid.Parse("b3100000-0000-0000-0000-000000000202");
        insert.Parameters.Add("ts", NpgsqlDbType.TimestampTz);
        insert.Parameters.Add("quality", NpgsqlDbType.Integer).Value = (int)TagQuality.Good;
        insert.Parameters.Add("value", NpgsqlDbType.Jsonb).Value = "12.5";
        insert.Parameters.Add("data_type", NpgsqlDbType.Smallint).Value = (short)TagDataType.Double;
        for (var index = 0; index < 402; index++)
        {
            insert.Parameters["ts"].Value = start.AddMilliseconds(index * 100);
            await insert.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();

        // Finish the provider's lazy sample_id bootstrap before installing the
        // delayed RLS policy. Otherwise its infrastructure UPDATE is the first
        // active tag_history statement and can be mistaken for the analytics page.
        await using (var providerBootstrap = new NpgsqlCommand(
            """
            CREATE SEQUENCE IF NOT EXISTS elitescada.tag_history_sample_id_seq;
            ALTER TABLE elitescada.tag_history ADD COLUMN IF NOT EXISTS sample_id bigint NULL;
            ALTER TABLE elitescada.tag_history ALTER COLUMN sample_id SET DEFAULT nextval('elitescada.tag_history_sample_id_seq');
            UPDATE elitescada.tag_history SET sample_id = nextval('elitescada.tag_history_sample_id_seq') WHERE sample_id IS NULL;
            ALTER TABLE elitescada.tag_history ALTER COLUMN sample_id SET NOT NULL;
            ALTER SEQUENCE elitescada.tag_history_sample_id_seq OWNED BY elitescada.tag_history.sample_id;
            CREATE INDEX IF NOT EXISTS ix_tag_history_time_sample_id_desc ON elitescada.tag_history (ts DESC, sample_id DESC);
            """,
            connection))
            await providerBootstrap.ExecuteNonQueryAsync();

        // Seed before FORCE ROW LEVEL SECURITY. Delay page 2 so the test can revoke
        // during real pagination, and record any row from page 3 as a forbidden read.
        await using (var markers = new NpgsqlCommand(
            "CREATE TABLE elitescada.w15_evidence_page_bounds (page_two_from timestamptz NOT NULL, page_three_from timestamptz NOT NULL); " +
            "INSERT INTO elitescada.w15_evidence_page_bounds VALUES (@page_two, @page_three); " +
            "CREATE TABLE elitescada.w15_evidence_page_three_reads (read_count integer NOT NULL); " +
            "INSERT INTO elitescada.w15_evidence_page_three_reads VALUES (0)",
            connection))
        {
            markers.Parameters.AddWithValue("page_two", NpgsqlDbType.TimestampTz, start.AddMilliseconds(20_000));
            markers.Parameters.AddWithValue("page_three", NpgsqlDbType.TimestampTz, start.AddMilliseconds(40_050));
            await markers.ExecuteNonQueryAsync();
        }
        await using (var function = new NpgsqlCommand(
            """
            CREATE OR REPLACE FUNCTION elitescada.w15_evidence_delay(sample_timestamp timestamptz)
            RETURNS boolean LANGUAGE plpgsql VOLATILE SECURITY DEFINER
            SET search_path = elitescada, pg_temp
            AS $$
            DECLARE page_two_from timestamptz; page_three_from timestamptz;
            BEGIN
                SELECT bounds.page_two_from, bounds.page_three_from
                  INTO page_two_from, page_three_from
                  FROM w15_evidence_page_bounds AS bounds LIMIT 1;
                IF sample_timestamp > page_three_from THEN
                    UPDATE w15_evidence_page_three_reads SET read_count = read_count + 1;
                    PERFORM pg_sleep(0.04);
                ELSIF sample_timestamp >= page_two_from THEN
                    PERFORM set_config('application_name', 'w15-stage1-page-2', false);
                    PERFORM pg_sleep(0.04);
                END IF;
                RETURN TRUE;
            END
            $$
            """,
            connection))
            await function.ExecuteNonQueryAsync();
        await using (var enable = new NpgsqlCommand("ALTER TABLE elitescada.tag_history ENABLE ROW LEVEL SECURITY", connection))
            await enable.ExecuteNonQueryAsync();
        await using (var force = new NpgsqlCommand("ALTER TABLE elitescada.tag_history FORCE ROW LEVEL SECURITY", connection))
            await force.ExecuteNonQueryAsync();
        await using (var updatePolicy = new NpgsqlCommand(
            "CREATE POLICY w15_evidence_initialize ON elitescada.tag_history FOR UPDATE USING (true) WITH CHECK (true)",
            connection))
            await updatePolicy.ExecuteNonQueryAsync();
        await using (var policy = new NpgsqlCommand(
            "CREATE POLICY w15_evidence_delay ON elitescada.tag_history FOR SELECT USING (elitescada.w15_evidence_delay(ts))",
            connection))
            await policy.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadPageThreeObservationCountAsync(string appConnectionString)
    {
        await using var connection = new NpgsqlConnection(appConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT read_count FROM elitescada.w15_evidence_page_three_reads",
            connection);
        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private static async Task RemoveSlowHistorianPolicyAsync(string appConnectionString)
    {
        await using var connection = new NpgsqlConnection(appConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "DROP POLICY IF EXISTS w15_evidence_delay ON elitescada.tag_history; " +
            "DROP POLICY IF EXISTS w15_evidence_initialize ON elitescada.tag_history; " +
            "DROP FUNCTION IF EXISTS elitescada.w15_evidence_delay(timestamptz); " +
            "DROP TABLE IF EXISTS elitescada.w15_evidence_page_three_reads; " +
            "DROP TABLE IF EXISTS elitescada.w15_evidence_page_bounds",
            connection);
        await command.ExecuteNonQueryAsync();
        await using var clear = new NpgsqlCommand("ALTER TABLE elitescada.tag_history NO FORCE ROW LEVEL SECURITY; ALTER TABLE elitescada.tag_history DISABLE ROW LEVEL SECURITY", connection);
        await clear.ExecuteNonQueryAsync();
    }

    private static async Task WaitForHistorianReadAsync(
        string adminConnectionString,
        string database,
        TimeSpan timeout,
        string? applicationName = null)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = database,
                ApplicationName = "w15-stage1-observer",
                Pooling = false
            }.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = @database AND application_name <> 'w15-stage1-observer' AND state = 'active' AND query ILIKE '%FROM elitescada.tag_history%' AND (@application_name IS NULL OR application_name = @application_name))",
                connection);
            command.Parameters.AddWithValue("database", database);
            command.Parameters.AddWithValue("application_name", (object?)applicationName ?? DBNull.Value);
            if ((bool)(await command.ExecuteScalarAsync() ?? false)) return;
            await Task.Delay(50);
        }
        await using var diagnosticConnection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = database,
            Pooling = false
        }.ConnectionString);
        await diagnosticConnection.OpenAsync();
        await using var diagnostic = new NpgsqlCommand(
            "SELECT COALESCE(string_agg(application_name || ' [' || state || '] ' || left(query, 220), E'\\n'), '<none>') FROM pg_stat_activity WHERE datname = @database AND query ILIKE '%tag_history%'",
            diagnosticConnection);
        diagnostic.Parameters.AddWithValue("database", database);
        var activity = (string?)await diagnostic.ExecuteScalarAsync();
        throw new TimeoutException($"The real API process never entered the delayed Historian SELECT for application '{applicationName}'. Active matching sessions: {activity}");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, TimeSpan timeout, string message)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await predicate()) return;
            await Task.Delay(150);
        }
        throw new TimeoutException(message);
    }

    private static async Task CreateRoleAsync(string connectionString, string role, string password)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE ROLE {role} LOGIN PASSWORD '{password}'", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreateDatabaseAsync(string connectionString, string database, string owner)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {database} OWNER {owner}", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InstallTimescaleExtensionAsync(string connectionString, string database)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database,
            Pooling = false
        }.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS timescaledb", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string database)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid()",
            connection))
        {
            terminate.Parameters.AddWithValue("database", database);
            await terminate.ExecuteNonQueryAsync();
        }
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database}", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task DropRoleAsync(string connectionString, string role)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP ROLE IF EXISTS {role}", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static int ReserveLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "Scada.Api", "Scada.Api.csproj")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not find the EliteSCADA repository root from the test process.");
    }

    private sealed record LicenseFiles(string LicensePath, string PublicKeyPath, string KeyId);

    private sealed record ApiResponse(HttpStatusCode StatusCode, JsonNode? Body, string RawBody);

    private sealed class ApiProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly HttpClient _client;
        private readonly ConcurrentQueue<string> _output = new();
        private readonly string _logPath;

        private ApiProcess(string nodeId, Process process, HttpClient client, string logPath)
        {
            NodeId = nodeId;
            _process = process;
            _client = client;
            _logPath = logPath;
            _process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) _output.Enqueue(eventArgs.Data); };
            _process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) _output.Enqueue(eventArgs.Data); };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public string NodeId { get; }

        public string LastOutput() => string.Join(Environment.NewLine, _output.TakeLast(30));

        public static ApiProcess Start(
            string nodeId,
            string url,
            string peerUrl,
            string projectKey,
            string referencePath,
            string directory,
            string connectionString,
            LicenseFiles license,
            string apiDll,
            string repositoryRoot,
            string suffix)
        {
            var port = new Uri(url).Port;
            var statePath = Path.Combine(directory, nodeId + "-peer-state.json");
            var nodeDirectory = Path.Combine(directory, nodeId);
            Directory.CreateDirectory(nodeDirectory);
            var endpointA = nodeId == "node-a" ? url : peerUrl;
            var endpointB = nodeId == "node-b" ? url : peerUrl;
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(apiDll);
            Set("ASPNETCORE_URLS", url);
            Set("ASPNETCORE_ENVIRONMENT", "Production");
            Set("Authentication__Enabled", "true");
            Set("Authentication__Local__Enabled", "true");
            Set("Authentication__Local__SecureCookie", "false");
            Set("Authentication__Jwt__Issuer", "w15-stage1-process-test");
            Set("Authentication__Jwt__Audience", "w15-stage1-process-test");
            Set("Authentication__Jwt__SigningKey", JwtKey);
            Set("ConnectionStrings__EliteScada", connectionString);
            Set("ConnectionStrings__Historian", connectionString);
            Set("DatabaseTopology__RequireDurable", "true");
            Set("DatabaseTopology__StateFile", Path.Combine(nodeDirectory, "database-topology.json"));
            Set("DatabaseTopology__Secrets__StoreFile", Path.Combine(nodeDirectory, "database-secrets.json"));
            Set("DatabaseTopology__Secrets__MasterKeyBase64", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            Set("Historian__Provider", "timescaledb");
            Set("HistoricalQuery__Enabled", "true");
            Set("HistoricalQuery__CursorKeyBase64", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            Set("Licensing__LicenseFile", license.LicensePath);
            Set("Licensing__VerificationKeys__" + license.KeyId, license.PublicKeyPath);
            Set("HighAvailability__Enabled", "true");
            Set("HighAvailability__ClusterId", "w15-stage1-process-" + suffix);
            Set("HighAvailability__NodeId", nodeId);
            Set("HighAvailability__InitialActiveNodeId", "node-a");
            Set("HighAvailability__TopologyVersion", "1");
            Set("HighAvailability__FreshnessSeconds", "20");
            Set("HighAvailability__Nodes__0__NodeId", "node-a");
            Set("HighAvailability__Nodes__0__LocalEndpoint", endpointA);
            Set("HighAvailability__Nodes__0__RemoteEndpoint", endpointA);
            Set("HighAvailability__Nodes__1__NodeId", "node-b");
            Set("HighAvailability__Nodes__1__LocalEndpoint", endpointB);
            Set("HighAvailability__Nodes__1__RemoteEndpoint", endpointB);
            Set("HighAvailability__PeerTransport__Enabled", "true");
            Set("HighAvailability__PeerTransport__PeerEndpoint", peerUrl + "/api/runtime/ha/peer/replicate");
            Set("HighAvailability__PeerTransport__SharedSecret", PeerSecret);
            Set("HighAvailability__PeerTransport__StatePath", statePath);
            Set("HighAvailability__PeerTransport__PollMilliseconds", "500");
            Set("HighAvailability__PeerTransport__RequestTimeoutSeconds", "2");
            Set("HighAvailability__PeerTransport__MaximumRetrySeconds", "2");
            Set("HighAvailability__PeerTransport__AuthenticationFreshnessSeconds", "30");
            Set("HighAvailability__Protection__Enabled", "true");
            Set("HighAvailability__Protection__AutomaticFailoverEnabled", "true");
            Set("HighAvailability__Protection__ReferencePath", referencePath);
            Set("HighAvailability__Protection__LeaseSeconds", "20");
            Set("HighAvailability__Protection__PollMilliseconds", "250");
            Set("HighAvailability__Protection__ReadyWitnessMaximumAgeSeconds", "60");
            Set("HighAvailability__Protection__OperationHistoryPath", Path.Combine(nodeDirectory, "operations.json"));
            Set("HighAvailability__Administration__ConfigurationPath", Path.Combine(nodeDirectory, "host-configuration.json"));
            Set("Logging__LogLevel__Default", "Warning");

            var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Scada.Api process " + nodeId);
            var client = new HttpClient(new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true
            })
            {
                BaseAddress = new Uri(url),
                Timeout = TimeSpan.FromSeconds(60)
            };
            return new ApiProcess(nodeId, process, client, Path.Combine(directory, nodeId + ".log"));

            void Set(string name, string value) => start.Environment[name] = value;
        }

        public async Task WaitForHealthAsync()
        {
            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(60);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (_process.HasExited)
                    throw new InvalidOperationException($"Scada.Api {NodeId} exited with {_process.ExitCode}. {_logPath}: {LastOutput()}");
                try
                {
                    using var response = await _client.GetAsync("/health");
                    if (response.StatusCode == HttpStatusCode.OK) return;
                }
                catch (HttpRequestException) { }
                await Task.Delay(200);
            }
            throw new TimeoutException($"Scada.Api {NodeId} health did not become ready. {_logPath}: {LastOutput()}");
        }

        public async Task BootstrapAsync() =>
            _ = await SendAsync(HttpMethod.Post, "/api/auth/bootstrap", new JsonObject
            {
                ["username"] = AdminUsername,
                ["displayName"] = "W15 Stage 1 evidence administrator",
                ["password"] = AdminPassword
            });

        public async Task LoginAsync() =>
            _ = await SendAsync(HttpMethod.Post, "/api/auth/login", new JsonObject
            {
                ["username"] = AdminUsername,
                ["password"] = AdminPassword
            });

        public async Task<JsonNode> GetAsync(string path) =>
            await SendAsync(HttpMethod.Get, path, null);

        public async Task<JsonNode> SendAsync(
            HttpMethod method,
            string path,
            JsonNode? body,
            HttpStatusCode expectedStatus = HttpStatusCode.OK)
        {
            var response = await SendRawAsync(method, path, body);
            if (response.StatusCode != expectedStatus)
                throw new InvalidOperationException(
                    $"Scada.Api {NodeId} {method} {path}: expected {(int)expectedStatus}, got {(int)response.StatusCode}; body={(string.IsNullOrWhiteSpace(response.RawBody) ? "<empty>" : response.RawBody)}. {_logPath}: {LastOutput()}");
            return response.Body ?? new JsonObject();
        }

        public async Task<ApiResponse> PostRawAsync(string path, JsonNode body) =>
            await SendRawAsync(HttpMethod.Post, path, body);

        private async Task<ApiResponse> SendRawAsync(HttpMethod method, string path, JsonNode? body)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
                request.Content = new StringContent(body.ToJsonString(Json), Encoding.UTF8, "application/json");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var text = await response.Content.ReadAsStringAsync();
            JsonNode? payload = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try { payload = JsonNode.Parse(text); }
                catch (JsonException) { payload = JsonValue.Create(text); }
            }
            return new ApiResponse(response.StatusCode, payload, text);
        }

        public async Task StopAsync()
        {
            _client.Dispose();
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _process.Dispose();
        }
    }
}
