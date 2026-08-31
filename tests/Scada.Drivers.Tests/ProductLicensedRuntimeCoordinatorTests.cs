using System.Text.Json;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Product;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class ProductLicensedRuntimeCoordinatorTests
{
    [Fact]
    public async Task Demo_BlocksRunAboveTwoHundredTagsBeforeRuntimeCompilation()
    {
        var license = DemoLicense(ProductLicensePolicy.DemoMaxContinuousRuntime);
        await using var runtime = CreateRuntime(new FixedLicenseService(license));
        var package = CreatePackage(ProductLicensePolicy.DemoMaxTags + 1);

        var result = await runtime.ActivateAsync("demo-over-limit", 1, package);

        Assert.False(result.Activated);
        var issue = Assert.Single(result.RuntimeIssues);
        Assert.Equal(ProductLicensePolicy.DemoTagLimitIssueCode, issue.Code);
        Assert.Contains("200 TAGs", issue.Message, StringComparison.Ordinal);
        Assert.Null(runtime.Describe().Revision);
    }

    [Fact]
    public async Task InvalidInstalledLicense_BlocksRunEvenBelowDemoLimit()
    {
        var invalid = new ProductLicenseSnapshot(
            ProductLicenseMode.Invalid,
            "ESREQ1.test",
            null,
            false,
            null,
            Message: ProductLicensePolicy.InvalidLicenseMessage("Hardware mismatch."));
        await using var runtime = CreateRuntime(new FixedLicenseService(invalid));

        var result = await runtime.ActivateAsync("invalid-license", 1, CreatePackage(1));

        Assert.False(result.Activated);
        Assert.Equal(ProductLicensePolicy.InvalidLicenseIssueCode, Assert.Single(result.RuntimeIssues).Code);
        Assert.Null(runtime.Describe().Revision);
    }

    [Fact]
    public async Task Demo_ExpiresContinuousRun_StopsRuntime_AndAllowsFreshRun()
    {
        var shortDemo = DemoLicense(TimeSpan.FromMilliseconds(80));
        await using var runtime = CreateRuntime(new FixedLicenseService(shortDemo));
        var package = CreateRunnablePackage();

        var first = await runtime.ActivateAsync("demo-restart", 1, package);
        Assert.True(first.Activated);
        Assert.Equal(1, runtime.Describe().Revision);

        await WaitForAsync(
            () => runtime.LicenseStatus().LastRuntimeIssueCode == ProductLicensePolicy.DemoRuntimeExpiredIssueCode,
            TimeSpan.FromSeconds(3));

        var expired = runtime.LicenseStatus();
        Assert.False(expired.RuntimeActive);
        Assert.Null(runtime.Describe().Revision);
        Assert.Contains("300 minutos", expired.LastRuntimeMessage, StringComparison.Ordinal);

        var second = await runtime.ActivateAsync("demo-restart", 2, package);

        Assert.True(second.Activated);
        Assert.Equal(2, runtime.Describe().Revision);
        Assert.True(runtime.LicenseStatus().RuntimeActive);
    }

    [Fact]
    public async Task LicensedRun_HasNoContinuousEvaluationTimer()
    {
        var licensed = new ProductLicenseSnapshot(
            ProductLicenseMode.Licensed,
            "ESREQ1.test",
            500,
            false,
            null,
            "license-500",
            "Customer");
        await using var runtime = CreateRuntime(new FixedLicenseService(licensed));

        var result = await runtime.ActivateAsync("licensed", 1, CreateRunnablePackage());

        Assert.True(result.Activated);
        var status = runtime.LicenseStatus();
        Assert.True(status.RuntimeActive);
        Assert.Null(status.RuntimeExpiresAtUtc);
        Assert.Null(status.LastRuntimeIssueCode);
    }

    [Fact]
    public async Task SuccessfulNewDemoRun_ReplacesPreviousContinuousWindow()
    {
        var shortDemo = DemoLicense(TimeSpan.FromMilliseconds(180));
        await using var runtime = CreateRuntime(new FixedLicenseService(shortDemo));
        var package = CreateRunnablePackage();

        Assert.True((await runtime.ActivateAsync("demo-window", 1, package)).Activated);
        await Task.Delay(100);
        Assert.True((await runtime.ActivateAsync("demo-window", 2, package)).Activated);

        await Task.Delay(110);
        Assert.Equal(2, runtime.Describe().Revision);
        Assert.True(runtime.LicenseStatus().RuntimeActive);

        await WaitForAsync(
            () => runtime.LicenseStatus().LastRuntimeIssueCode == ProductLicensePolicy.DemoRuntimeExpiredIssueCode,
            TimeSpan.FromSeconds(3));
        Assert.Null(runtime.Describe().Revision);
    }

    private static ProductLicensedRuntimeCoordinator CreateRuntime(IProductLicenseService license) =>
        new(
            () => new EngineeringRuntimeCoordinator(
                new InMemoryScadaEventBus(),
                new EngineeringDriverCompiler(),
                TimeSpan.FromSeconds(1)),
            license);

    private static ProductLicenseSnapshot DemoLicense(TimeSpan duration) =>
        new(
            ProductLicenseMode.Demo,
            "ESREQ1.test",
            ProductLicensePolicy.DemoMaxTags,
            false,
            duration,
            Message: "Demo");

    private static EngineeringPackage CreateRunnablePackage()
    {
        var tagId = Guid.NewGuid();
        var initialValue = new MemoryInitialValueDto(
            TagDataType.Int32,
            JsonSerializer.SerializeToElement(0));

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    tagId,
                    "Demo Counter",
                    "License.DemoCounter",
                    TagDataType.Int32,
                    Source: "memory.server",
                    ReadOnly: false,
                    InitialValue: initialValue)
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    null,
                    "memory.server",
                    "Demo Server Memory",
                    InternalMemoryRuntimePlanner.ServerMemoryDriverKey)
            });
    }

    private static EngineeringPackage CreatePackage(int tagCount)
    {
        var tags = Enumerable.Range(0, tagCount)
            .Select(index => new TagEngineeringDto(
                Guid.NewGuid(),
                $"Tag {index}",
                $"License.Tag{index:D4}",
                TagDataType.Double))
            .ToArray();

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            tags,
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Condition was not satisfied within the expected interval.");
            await Task.Delay(10);
        }
    }

    private sealed class FixedLicenseService(ProductLicenseSnapshot snapshot) : IProductLicenseService
    {
        public ProductLicenseSnapshot Current() => snapshot;

        public ProductRuntimePermit EvaluateRuntime(int projectTagCount)
        {
            if (snapshot.Mode == ProductLicenseMode.Invalid)
            {
                return new ProductRuntimePermit(
                    false,
                    snapshot,
                    projectTagCount,
                    ProductLicensePolicy.InvalidLicenseIssueCode,
                    snapshot.Message);
            }

            if (snapshot.Mode == ProductLicenseMode.Demo && projectTagCount > ProductLicensePolicy.DemoMaxTags)
            {
                return new ProductRuntimePermit(
                    false,
                    snapshot,
                    projectTagCount,
                    ProductLicensePolicy.DemoTagLimitIssueCode,
                    ProductLicensePolicy.DemoTagLimitMessage(projectTagCount));
            }

            if (snapshot.Mode == ProductLicenseMode.Licensed &&
                !snapshot.UnlimitedTags &&
                snapshot.MaxTags.HasValue &&
                projectTagCount > snapshot.MaxTags.Value)
            {
                return new ProductRuntimePermit(
                    false,
                    snapshot,
                    projectTagCount,
                    ProductLicensePolicy.LicenseTagLimitIssueCode,
                    ProductLicensePolicy.LicensedTagLimitMessage(projectTagCount, snapshot.MaxTags.Value));
            }

            return new ProductRuntimePermit(true, snapshot, projectTagCount);
        }
    }
}
