using System.Globalization;
using Scada.Api.Security;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Modbus;
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
        builder.Services.AddSingleton(_ => CommunicationDriverRuntimeComposition.BuildForCurrentSchema());
        builder.Services.AddSingleton<EngineeringDataSourceTypeCatalog>(sp =>
            EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(
                sp.GetRequiredService<CommunicationDriverRuntimeComponentRegistry>()));
        builder.Services.AddSingleton<IDataSourceConfigurationValidator>(sp =>
            sp.GetRequiredService<EngineeringDataSourceTypeCatalog>());
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
    }

    private static string NormalizeEnumToken(string value) =>
        value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
}
