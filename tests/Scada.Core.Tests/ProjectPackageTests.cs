using System.IO.Compression;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Security;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class ProjectPackageTests
{
    [Fact]
    public void ExportAndInspect_RoundTripsManifestAndEngineering()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        tags.Register(TagDefinition.Create(
            "Setpoint",
            "Plant.P01.Setpoint",
            TagDataType.Double,
            "plant.simulation",
            "bar",
            readOnly: false,
            accessPolicy: new TagAccessPolicy(
                new[] { "Operator" },
                new[] { "Supervisor" },
                new[] { "Engineering" })));
        var exchange = new EngineeringExchangeService(tags, alarms);
        var service = new ProjectPackageService(exchange);

        var packageBytes = service.Export("plant-a", "Plant A");
        var inspection = service.Inspect(packageBytes);

        Assert.Equal(ProjectPackageService.CurrentFormat, inspection.Manifest.Format);
        Assert.Equal(ProjectPackageService.LegacySecurityPolicyFormatVersion, inspection.Manifest.FormatVersion);
        Assert.Equal("EliteSCADA", inspection.Manifest.Product);
        Assert.Equal("plant-a", inspection.Manifest.ProjectKey);
        Assert.Equal("Plant A", inspection.Manifest.ProjectName);
        Assert.Equal(EngineeringExchangeService.CurrentSchema, inspection.Manifest.EngineeringSchema);
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, inspection.Manifest.EngineeringSchemaVersion);
        var file = Assert.Single(inspection.Manifest.Files);
        Assert.Equal(ProjectPackageService.EngineeringPath, file.Path);
        Assert.Equal(64, file.Sha256.Length);
        var tag = Assert.Single(inspection.Engineering.Tags);
        Assert.Equal("Plant.P01.Setpoint", tag.Path);
        Assert.Equal(new[] { "Supervisor" }, tag.AccessPolicy!.WriteRoles);
    }

    [Fact]
    public void ExportAndInspect_RoundTripsLogicalTagBitBindingReference()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var status = TagDefinition.Create("Status", "Plant.P01.Status", TagDataType.Int16);
        tags.Register(status);
        var assets = new InMemoryEngineeringAssetRegistry();
        assets.UpsertEquipment(new EquipmentEngineeringDto(
            null,
            "Plant.P01",
            "Pump P01",
            Bindings:
            [
                new EngineeringBindingDto(
                    "running",
                    EngineeringBindingKind.Tag,
                    "Plant.P01.Status.03",
                    TagReference: new TagValueReference(
                        status.Id,
                        new TagValueSelector(TagValueSelectorKind.Bit, 3)))
            ]));
        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            assets);
        var service = new ProjectPackageService(exchange);

        var inspection = service.Inspect(service.Export("plant-bit", "Plant Bit"));
        var binding = Assert.Single(Assert.Single(inspection.Engineering.Equipment!).Bindings!);

        Assert.Equal("Plant.P01.Status.03", binding.Target);
        Assert.NotNull(binding.TagReference);
        Assert.Equal(status.Id, binding.TagReference!.TagId);
        Assert.Equal(TagValueSelectorKind.Bit, binding.TagReference.Selector!.Kind);
        Assert.Equal(3, binding.TagReference.Selector.Index);
    }

    [Fact]
    public void ExportAndInspect_RoundTripsOperationalCommands()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var commands = new InMemoryCommandEngineeringRegistry();
        var runTag = TagDefinition.Create("Run", "Plant.P01.Run", TagDataType.Boolean, readOnly: false);
        tags.Register(runTag);
        commands.Upsert(new CommandEngineeringDto(
            Guid.NewGuid(),
            "plant.p01.start",
            "Start P01",
            CommandKind.WriteTagValue,
            "true",
            runTag.Id,
            runTag.Path,
            Area: "Plant",
            EquipmentPath: "Plant.P01"));
        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            commands);
        var service = new ProjectPackageService(exchange);

        var inspection = service.Inspect(service.Export("plant-a", "Plant A"));

        var command = Assert.Single(inspection.Engineering.Commands!);
        Assert.Equal("plant.p01.start", command.Key);
        Assert.Equal(runTag.Id, command.TargetTagId);
        Assert.Equal("Plant.P01", command.EquipmentPath);
    }

    [Fact]
    public void Package_CanPreviewAndRestoreIntoAnotherRuntime()
    {
        var sourceTags = new InMemoryTagRegistry();
        var sourceBus = new InMemoryScadaEventBus();
        using var sourceAlarms = new InMemoryAlarmEngine(sourceBus);
        sourceTags.Register(TagDefinition.Create("Pressure", "Plant.P01.Pressure", TagDataType.Double, engineeringUnit: "bar"));
        var sourcePackageService = new ProjectPackageService(new EngineeringExchangeService(sourceTags, sourceAlarms));
        var packageBytes = sourcePackageService.Export("plant-a", "Plant A");

        var targetTags = new InMemoryTagRegistry();
        var targetBus = new InMemoryScadaEventBus();
        using var targetAlarms = new InMemoryAlarmEngine(targetBus);
        var targetPackageService = new ProjectPackageService(new EngineeringExchangeService(targetTags, targetAlarms));

        var preview = targetPackageService.Preview(packageBytes, ImportMode.CreateAndUpdate);
        var result = targetPackageService.Apply(packageBytes, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Equal(1, preview.CreateCount);
        Assert.Equal(1, result.Created);
        Assert.True(targetTags.TryGetByPath("Plant.P01.Pressure", out var restored));
        Assert.Equal("bar", restored!.EngineeringUnit);
    }

    [Fact]
    public void Inspect_RejectsEngineeringPayloadWhenChecksumWasTampered()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        tags.Register(TagDefinition.Create("Pressure", "Plant.P01.Pressure", TagDataType.Double));
        var service = new ProjectPackageService(new EngineeringExchangeService(tags, alarms));
        var valid = service.Export("plant-a", "Plant A");
        var tampered = TamperEngineeringPayload(valid);

        var exception = Assert.Throws<InvalidDataException>(() => service.Inspect(tampered));

        Assert.Contains("checksum", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_RejectsUnexpectedArchiveEntries()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new ProjectPackageService(new EngineeringExchangeService(tags, alarms));
        var valid = service.Export("plant-a", "Plant A");
        var withExtra = AddUnexpectedEntry(valid);

        Assert.Throws<InvalidDataException>(() => service.Inspect(withExtra));
    }

    [Fact]
    public void ExportLimits_AcceptExactImportBoundaries()
    {
        ProjectPackageService.ValidateExportLimits(
            ProjectPackageService.MaximumEngineeringBytes,
            ProjectPackageService.MaximumPayloadFiles,
            ProjectPackageService.MaximumPackageBytes - ProjectPackageService.MaximumManifestBytes,
            ProjectPackageService.MaximumManifestBytes);
    }

    [Fact]
    public void ExportLimits_RejectEngineeringPayloadImporterWouldReject()
    {
        Assert.Throws<InvalidDataException>(() =>
            ProjectPackageService.ValidateExportLimits(
                ProjectPackageService.MaximumEngineeringBytes + 1L,
                1,
                ProjectPackageService.MaximumEngineeringBytes + 1L,
                1));
    }

    [Fact]
    public void ExportLimits_RejectPayloadCountImporterWouldReject()
    {
        Assert.Throws<InvalidDataException>(() =>
            ProjectPackageService.ValidateExportLimits(
                1,
                ProjectPackageService.MaximumPayloadFiles + 1,
                1,
                1));
    }

    [Fact]
    public void ExportLimits_RejectUncompressedContentImporterWouldReject()
    {
        Assert.Throws<InvalidDataException>(() =>
            ProjectPackageService.ValidateExportLimits(
                1,
                1,
                ProjectPackageService.MaximumPackageBytes,
                1));
    }

    [Fact]
    public async Task AuthorityBoundPackageV3_RequiresExactAuthorityReferenceForPreviewAndApply()
    {
        var role = new SecurityRoleEngineeringDto(
            Guid.Parse("93000000-0000-0000-0000-000000000001"),
            "authority-reader",
            "Authority Reader");
        var authority = new InMemoryAuthorityPolicyStore([role]);
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new AuthorityPolicyRegistryView(authority),
            new InMemoryCommandEngineeringRegistry());
        var service = new ProjectPackageService(exchange);

        var package = service.Export("plant-a", "Plant A");
        var inspection = service.Inspect(package);
        var validPreview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.Equal(ProjectPackageService.CurrentFormatVersion, inspection.Manifest.FormatVersion);
        Assert.NotNull(inspection.Engineering.AuthorityPolicyReference);
        Assert.Empty(inspection.Engineering.SecurityRoles!);
        Assert.Empty(inspection.Engineering.SecurityScopes!);
        Assert.True(validPreview.CanApply);

        var changed = await authority.TryReplaceAsync(0, [role], []);
        Assert.True(changed.Applied);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var apply = service.Apply(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "SECURITY_AUTHORITY_POLICY_REFERENCE_MISMATCH");
        Assert.Contains(
            apply.Issues,
            issue => issue.Code == "SECURITY_AUTHORITY_POLICY_REFERENCE_MISMATCH");
    }

    [Fact]
    public void LegacyV2Package_IsBlockedByCanonicalAuthorityWithoutReference()
    {
        var sourceTags = new InMemoryTagRegistry();
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var legacyPackage = new ProjectPackageService(
            new EngineeringExchangeService(sourceTags, sourceAlarms))
            .Export("plant-a", "Plant A");

        var role = new SecurityRoleEngineeringDto(
            Guid.Parse("93000000-0000-0000-0000-000000000011"),
            "authority-reader",
            "Authority Reader");
        var authority = new InMemoryAuthorityPolicyStore([role]);
        var targetTags = new InMemoryTagRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var target = new ProjectPackageService(new EngineeringExchangeService(
            targetTags,
            targetAlarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new AuthorityPolicyRegistryView(authority),
            new InMemoryCommandEngineeringRegistry()));

        var inspection = target.Inspect(legacyPackage);
        var preview = target.Preview(legacyPackage, ImportMode.CreateAndUpdate);
        var apply = target.Apply(legacyPackage, ImportMode.CreateAndUpdate);

        Assert.Equal(ProjectPackageService.LegacySecurityPolicyFormatVersion, inspection.Manifest.FormatVersion);
        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "SECURITY_AUTHORITY_POLICY_REFERENCE_REQUIRED");
        Assert.Contains(
            apply.Issues,
            issue => issue.Code == "SECURITY_AUTHORITY_POLICY_REFERENCE_REQUIRED");
    }

    private static byte[] TamperEngineeringPayload(byte[] packageBytes)
    {
        using var input = new MemoryStream(packageBytes);
        using var source = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var targetEntry = target.CreateEntry(entry.FullName);
                using var sourceStream = entry.Open();
                using var payload = new MemoryStream();
                sourceStream.CopyTo(payload);
                var bytes = payload.ToArray();
                if (entry.FullName == ProjectPackageService.EngineeringPath && bytes.Length > 0)
                    bytes[0] ^= 0x01;

                using var targetStream = targetEntry.Open();
                targetStream.Write(bytes, 0, bytes.Length);
            }
        }
        return output.ToArray();
    }

    private static byte[] AddUnexpectedEntry(byte[] packageBytes)
    {
        using var input = new MemoryStream(packageBytes);
        using var source = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var targetEntry = target.CreateEntry(entry.FullName);
                using var sourceStream = entry.Open();
                using var targetStream = targetEntry.Open();
                sourceStream.CopyTo(targetStream);
            }
            using var extra = target.CreateEntry("unexpected.txt").Open();
            extra.WriteByte(1);
        }
        return output.ToArray();
    }
}
