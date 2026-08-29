using System.Buffers.Binary;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class VisualAssetIdentityConflictTests
{
    [Fact]
    public void Preview_RejectsExistingKeyBeingReassignedToDifferentStableAssetId()
    {
        var registry = new InMemoryVisualAssetEngineeringRegistry();
        var payload = VisualAssetPayload.Create("image/bmp", CreateBmp());
        var existing = CreateAsset(Guid.NewGuid(), payload);
        registry.PutPayload(payload);
        registry.UpsertAsset(existing);

        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            new InMemoryGatewayEngineeringRegistry(),
            new InMemoryScriptEngineeringRegistry(),
            registry);

        var incoming = CreateAsset(Guid.NewGuid(), payload);
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            VisualAssets: new[] { incoming });
        var context = new EngineeringImportContext(
            new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase)
            {
                [payload.Sha256] = payload
            });

        var preview = exchange.Preview(package, ImportMode.CreateAndUpdate, context);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(x => x.Issues),
            x => x.Code == "VISUAL_ASSET_ID_KEY_CONFLICT" && x.IsError);
        Assert.Equal(existing.Id, registry.FindAssetByKey("logo")!.Id);
    }

    private static VisualAssetEngineeringDto CreateAsset(Guid id, VisualAssetPayload payload) =>
        new(
            id,
            "logo",
            "Logo",
            "logo.bmp",
            "image/bmp",
            payload.ByteLength,
            payload.Sha256,
            1,
            1);

    private static byte[] CreateBmp()
    {
        const int fileSize = 58;
        var bytes = new byte[fileSize];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2, 4), fileSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10, 4), 54);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28, 2), 24);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(34, 4), 4);
        return bytes;
    }
}
