using System.Globalization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Validation;

namespace Scada.Api.Engineering;

public sealed record ModbusTagAddressBuildRequest(
    string Area,
    int Reference,
    string ReferenceBase = "zeroBased",
    int? UnitId = null,
    string? ValueType = null,
    string? WordOrder = null,
    double? Scale = null,
    double? Offset = null,
    int? BitIndex = null);

public static class EngineeringDriverCatalogApi
{
    public static void AddEngineeringDriverCatalog(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<ICommunicationDriverProtectedMaterialResolver>(_ =>
            EnvironmentCommunicationDriverProtectedMaterialResolver.CreateDeterministicScopedEnvironment());
        builder.Services.AddSingleton(sp => CommunicationDriverRuntimeComposition.BuildForCurrentSchema(
            hostProtectedMaterialResolver: sp.GetRequiredService<ICommunicationDriverProtectedMaterialResolver>()));
        builder.Services.AddSingleton<EngineeringDataSourceTypeCatalog>(sp =>
            EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(
                sp.GetRequiredService<CommunicationDriverRuntimeComponentRegistry>()));
        builder.Services.AddSingleton<IDataSourceConfigurationValidator>(sp =>
            sp.GetRequiredService<EngineeringDataSourceTypeCatalog>());
        builder.Services.AddSingleton<IEngineeringDriverToolProviderFactory, OpcUaEngineeringDriverToolProviderFactory>();
        builder.Services.AddSingleton<EngineeringDriverToolProviderFactoryRegistry>();
    }

    public static void MapEngineeringDriverCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/engineering/data-source-types", (EngineeringDataSourceTypeCatalog catalog) =>
            Results.Ok(catalog.Describe()))
            .RequireWorkspaceEngineeringRead();

        app.MapPost("/api/engineering/tag-address/modbus/build", (ModbusTagAddressBuildRequest request) =>
        {
            if (!ModbusTagAddressCodec.TryParseArea(request.Area, out var area))
                return Results.BadRequest(new { error = "Area must be coil, discrete, holding or input." });

            if (!Enum.TryParse<ModbusAddressReferenceBase>(request.ReferenceBase, true, out var referenceBase))
                return Results.BadRequest(new { error = "ReferenceBase must be zeroBased or oneBased." });

            if (!ModbusTagAddressCodec.TryBuild(area, request.Reference, referenceBase, out var address, out var addressError))
                return Results.BadRequest(new { error = addressError });

            if (request.UnitId is < 0 or > 255)
                return Results.BadRequest(new { error = "UnitId must be from 0 to 255." });

            ModbusValueType? valueType = null;
            if (!string.IsNullOrWhiteSpace(request.ValueType))
            {
                var normalized = NormalizeEnumToken(request.ValueType);
                if (!Enum.TryParse<ModbusValueType>(normalized, true, out var parsed))
                    return Results.BadRequest(new { error = $"Unsupported Modbus value type '{request.ValueType}'." });
                valueType = parsed;
            }

            ModbusWordOrder? wordOrder = null;
            if (!string.IsNullOrWhiteSpace(request.WordOrder))
            {
                var normalized = NormalizeEnumToken(request.WordOrder);
                if (!Enum.TryParse<ModbusWordOrder>(normalized, true, out var parsed))
                    return Results.BadRequest(new { error = "WordOrder must be HighWordFirst or LowWordFirst." });
                wordOrder = parsed;
            }

            if (request.Scale.HasValue && (!double.IsFinite(request.Scale.Value) || request.Scale.Value == 0d))
                return Results.BadRequest(new { error = "Scale must be a finite non-zero number." });
            if (request.Offset.HasValue && !double.IsFinite(request.Offset.Value))
                return Results.BadRequest(new { error = "Offset must be a finite number." });
            if (request.BitIndex is < 0 or > 15)
                return Results.BadRequest(new { error = "BitIndex must be from 0 to 15." });
            if (request.BitIndex.HasValue && area is not (ModbusDataArea.HoldingRegister or ModbusDataArea.InputRegister))
                return Results.BadRequest(new { error = "Bit selection is supported only for holding or input registers." });

            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (request.UnitId.HasValue) metadata["modbus.unitId"] = request.UnitId.Value.ToString(CultureInfo.InvariantCulture);
            if (valueType.HasValue) metadata["modbus.valueType"] = valueType.Value.ToString();
            if (wordOrder.HasValue) metadata["modbus.wordOrder"] = wordOrder.Value.ToString();
            if (request.Scale.HasValue) metadata["modbus.scale"] = request.Scale.Value.ToString("R", CultureInfo.InvariantCulture);
            if (request.Offset.HasValue) metadata["modbus.offset"] = request.Offset.Value.ToString("R", CultureInfo.InvariantCulture);

            var selector = request.BitIndex.HasValue
                ? new TagValueSelector(TagValueSelectorKind.Bit, request.BitIndex.Value)
                : null;
            var writableArea = area is ModbusDataArea.Coil or ModbusDataArea.HoldingRegister;

            return Results.Ok(new
            {
                address,
                metadata,
                addressSelector = selector,
                writableArea,
                canonicalReferenceBase = "zeroBased"
            });
        })
        .RequireWorkspaceEngineeringRead();

        app.MapPost("/api/engineering/data-sources/{id:guid}/driver-tools/connection-test", async (
            Guid id,
            EngineeringWorkspace workspace,
            IDataSourceEngineeringRegistry dataSources,
            EngineeringDriverToolProviderFactoryRegistry factories,
            CancellationToken cancellationToken) =>
        {
            var dataSource = dataSources.Find(id);
            if (dataSource is null)
                return Results.NotFound(new { error = $"Engineering Data Source '{id}' was not found." });
            if (!factories.TryGet(dataSource.Driver, out var factory) || factory is null)
                return UnsupportedTooling(dataSource.Driver);

            try
            {
                await using var lease = await factory.CreateAsync(
                    workspace.Describe().ProjectKey,
                    dataSource,
                    cancellationToken);
                var tester = lease.Registration.ConnectionTester;
                if (tester is null)
                    return UnsupportedCapability(dataSource.Driver, DriverEngineeringCapabilities.ConnectionTest);

                var result = await tester.TestConnectionAsync(ToDriverContext(dataSource), cancellationToken);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return DriverToolFailure(ex);
            }
        })
        .RequireWorkspaceEngineeringRead();

        app.MapPost("/api/engineering/data-sources/{id:guid}/driver-tools/discover", async (
            Guid id,
            DriverEngineeringDiscoveryApiRequest request,
            EngineeringWorkspace workspace,
            IDataSourceEngineeringRegistry dataSources,
            EngineeringDriverToolProviderFactoryRegistry factories,
            CancellationToken cancellationToken) =>
        {
            var dataSource = dataSources.Find(id);
            if (dataSource is null)
                return Results.NotFound(new { error = $"Engineering Data Source '{id}' was not found." });
            if (!factories.TryGet(dataSource.Driver, out var factory) || factory is null)
                return UnsupportedTooling(dataSource.Driver);

            try
            {
                await using var lease = await factory.CreateAsync(
                    workspace.Describe().ProjectKey,
                    dataSource,
                    cancellationToken);
                var discovery = lease.Registration.DiscoverySource;
                if (discovery is null)
                    return UnsupportedCapability(dataSource.Driver, DriverEngineeringCapabilities.Discover);

                var candidates = new List<DriverDiscoveryCandidate>();
                await foreach (var candidate in discovery.DiscoverAsync(
                                   new DriverDiscoveryRequest(
                                       ToDriverContext(dataSource),
                                       request.Parameters,
                                       request.MaximumResults),
                                   cancellationToken))
                {
                    candidates.Add(candidate);
                }

                return Results.Ok(candidates);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return DriverToolFailure(ex);
            }
        })
        .RequireWorkspaceEngineeringRead();

        app.MapPost("/api/engineering/data-sources/{id:guid}/driver-tools/browse", async (
            Guid id,
            DriverEngineeringBrowseApiRequest request,
            EngineeringWorkspace workspace,
            IDataSourceEngineeringRegistry dataSources,
            EngineeringDriverToolProviderFactoryRegistry factories,
            CancellationToken cancellationToken) =>
        {
            var dataSource = dataSources.Find(id);
            if (dataSource is null)
                return Results.NotFound(new { error = $"Engineering Data Source '{id}' was not found." });
            if (!factories.TryGet(dataSource.Driver, out var factory) || factory is null)
                return UnsupportedTooling(dataSource.Driver);

            try
            {
                await using var lease = await factory.CreateAsync(
                    workspace.Describe().ProjectKey,
                    dataSource,
                    cancellationToken);
                var browser = lease.Registration.Browser;
                if (browser is null)
                    return UnsupportedCapability(dataSource.Driver, DriverEngineeringCapabilities.Browse);

                var page = await browser.BrowseAsync(
                    new DriverBrowseRequest(
                        ToDriverContext(dataSource),
                        request.ParentNodeId,
                        request.ContinuationToken,
                        request.PageSize,
                        request.Parameters),
                    cancellationToken);
                return Results.Ok(page);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return DriverToolFailure(ex);
            }
        })
        .RequireWorkspaceEngineeringRead();
    }

    private static DriverEngineeringDataSourceContext ToDriverContext(DataSourceEngineeringDto dataSource) =>
        new(
            dataSource.Key,
            dataSource.Name,
            dataSource.Driver,
            dataSource.Settings is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(dataSource.Settings, StringComparer.OrdinalIgnoreCase),
            dataSource.SecretReferences is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(dataSource.SecretReferences, StringComparer.OrdinalIgnoreCase));

    private static IResult UnsupportedTooling(string driverType) =>
        Results.BadRequest(new
        {
            error = $"Driver '{driverType}' does not expose a registered Engineering tooling provider in this build."
        });

    private static IResult UnsupportedCapability(
        string driverType,
        DriverEngineeringCapabilities capability) =>
        Results.BadRequest(new
        {
            error = $"Driver '{driverType}' does not support Engineering capability '{capability}'."
        });

    private static IResult DriverToolFailure(Exception exception) =>
        Results.Problem(
            title: "Driver Engineering operation failed.",
            detail: exception.Message,
            statusCode: StatusCodes.Status502BadGateway);

    private static string NormalizeEnumToken(string value) =>
        value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
}
