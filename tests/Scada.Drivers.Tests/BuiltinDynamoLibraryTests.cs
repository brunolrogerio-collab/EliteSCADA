using Scada.Api.Runtime;
using Scada.Engineering.Validation;

namespace Scada.Drivers.Tests;

public sealed class BuiltinDynamoLibraryTests
{
    [Fact]
    public void Library_ProvidesRepresentativeInsertableDefinitionsAcrossIndustrialFamilies()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(72, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Id).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count());

        var categories = definitions
            .GroupBy(definition => definition.Properties!["category"], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(6, categories["pump"]);
        Assert.Equal(6, categories["motor"]);
        Assert.Equal(15, categories["valve"]);
        Assert.Equal(6, categories["tank"]);
        Assert.Equal(9, categories["compressor"]);
        Assert.Equal(9, categories["process"]);
        Assert.Equal(15, categories["substation"]);
        Assert.Equal(3, categories["electrical"]);
        Assert.Equal(3, categories["instrument"]);
        Assert.All(definitions, definition =>
        {
            Assert.NotEmpty(definition.Elements!);
            Assert.Equal("true", definition.Metadata!["builtinLibrary"]);
            Assert.Equal("original-elitescada-vector", definition.Metadata!["assetOrigin"]);
            Assert.Equal(BuiltinDynamoLibrary.Version, definition.Properties!["libraryVersion"]);
            Assert.True(double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture) > 0);
            Assert.True(double.Parse(definition.Properties!["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture) > 0);
            Assert.Contains(definition.Properties!["visualStyle"], new[] { "detailed-2d", "dimensional-front", "high-performance" });
            Assert.Equal(definition.Properties!["visualStyle"], definition.Metadata!["visualStyle"]);
            Assert.Equal("front-orthographic", definition.Metadata!["view"]);
            Assert.Equal("pid-inspired-native-vector-v1", definition.Properties["visualReferenceProfile"]);
            Assert.Equal("pid-inspired-native-vector-v1", definition.Metadata!["visualReferenceProfile"]);
            Assert.Equal("original-editable-geometry; third-party SVGs are not embedded", definition.Metadata!["visualReferencePolicy"]);
        });

        var styleCounts = definitions
            .GroupBy(definition => definition.Properties!["visualStyle"], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Assert.Equal(24, styleCounts["detailed-2d"]);
        Assert.Equal(24, styleCounts["dimensional-front"]);
        Assert.Equal(24, styleCounts["high-performance"]);

        var dimensional = definitions.Where(definition => definition.Properties!["visualStyle"] == "dimensional-front").ToArray();
        Assert.All(dimensional, definition =>
            Assert.Contains(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("fillStyle", out var fillStyle) &&
                string.Equals(fillStyle.GetString(), "gradient", StringComparison.Ordinal)));

        var highPerformance = definitions.Where(definition => definition.Properties!["visualStyle"] == "high-performance").ToArray();
        Assert.All(highPerformance, definition =>
            Assert.DoesNotContain(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("fillStyle", out var fillStyle) &&
                string.Equals(fillStyle.GetString(), "gradient", StringComparison.Ordinal)));
    }

    [Fact]
    public void Workspace_SeedsTheBuiltInLibraryAndKeepsEquipmentBindingsParameterized()
    {
        using var workspace = new EngineeringWorkspace();

        var definitions = workspace.Assets.SnapshotDynamos();
        Assert.Equal(BuiltinDynamoLibrary.Create().Count, definitions.Count);
        Assert.DoesNotContain(definitions, definition =>
            definition.Metadata?.GetValueOrDefault("assetOrigin") == "elipse-e3-import");
        var targets = definitions
            .SelectMany(definition => definition.Elements ?? [])
            .SelectMany(element => element.Bindings ?? [])
            .Select(binding => binding.Target)
            .ToArray();

        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.StartsWith("{equipmentPath}.", target));
    }

    [Fact]
    public void CentrifugalBlowerStyles_KeepTheSameInterfaceAndRenderAnInspectableRotorAssembly()
    {
        var variants = BuiltinDynamoLibrary.Create()
            .Where(definition => definition.Metadata!["familyKey"] == "process.blower.centrifugal")
            .ToArray();

        Assert.Equal(3, variants.Length);
        foreach (var variant in variants)
        {
            var elementKeys = variant.Elements!.Select(element => element.Key).ToHashSet(StringComparer.Ordinal);
            Assert.Contains("inlet-flange", elementKeys);
            Assert.Contains("outlet-flange", elementKeys);
            Assert.Contains(elementKeys, key => key is "casing" or "volute-case");
            Assert.Contains("impeller-recess", elementKeys);
            Assert.Contains("hub-cap", elementKeys);
            Assert.Contains("outlet-flow-arrow", elementKeys);
            Assert.Equal(6, elementKeys.Count(key => key.StartsWith("impeller-blade-", StringComparison.Ordinal)));
            Assert.Contains(variant.Parameters!, parameter => parameter.Key == "equipmentPath");
            Assert.Contains(variant.Elements!, element => element.Bindings?.Any(binding => binding.Target == "{equipmentPath}.Running") == true);
            Assert.Contains(variant.Elements!, element => element.Bindings?.Any(binding => binding.Target == "{equipmentPath}.Fault") == true);
        }
    }

    [Fact]
    public void AllOtherBuiltinDynamoFamilies_ReceiveVersionedFamilySpecificVisualDetails()
    {
        var definitions = BuiltinDynamoLibrary.Create()
            .Where(definition => definition.Properties!["visualStyle"] != "high-performance")
            .Where(definition => definition.Metadata!["familyKey"] is
                "dynamo.pump.standard" or "process.pump.submersible" or "process.motor.standard" or
                "process.motor.vfd" or "process.valve.onoff" or "process.valve.control" or
                "process.tank.vertical" or "process.tank.horizontal" or "process.instrument.indicator")
            .ToArray();

        Assert.Equal(18, definitions.Length);
        Assert.All(definitions, definition =>
        {
            Assert.Equal(BuiltinDynamoLibrary.Version, definition.Properties!["libraryVersion"]);
            Assert.Contains(definition.Elements!, element => element.Key.StartsWith("detail-", StringComparison.Ordinal));
            Assert.Equal(definition.Elements!.Count, definition.Elements.Select(element => element.Key).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(definition.Elements.Count, definition.Elements.Select(element => element.Id).Distinct().Count());
        });

        var keys = definitions.SelectMany(definition => definition.Elements!).Select(element => element.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("detail-casing-bolt-1", keys);
        Assert.Contains("detail-upper-cooling-slot-1", keys);
        Assert.Contains("detail-cooling-rib-left-1", keys);
        Assert.Contains("detail-drive-status-1", keys);
        Assert.Contains("detail-flange-bolt-31-49", keys);
        Assert.Contains("detail-shell-weld-50", keys);
        Assert.Contains("detail-shell-seam-65", keys);
        Assert.Contains("detail-scale-tick--160", keys);
    }

    [Fact]
    public void StateAwareDynamos_ExposeTagAndEditableColorsWithHighPerformanceDefaults()
    {
        var definitions = BuiltinDynamoLibrary.Create();
        var highPerformanceMotor = definitions.Single(definition =>
            definition.Key == "process.motor.standard.high-performance");
        var stateColorElement = Assert.Single(highPerformanceMotor.Elements!, element =>
            element.Metadata?.ContainsKey("dynamoStateColorProfile") == true);
        var stateMap = Assert.Single(stateColorElement.PropertyMaps!);

        Assert.Contains(highPerformanceMotor.Parameters!, parameter => parameter.Key == "state" && parameter.Kind == Scada.Engineering.Contracts.DynamoParameterKind.TagReference);
        Assert.Equal("#8FBF98", stateMap.Rules.ElementAt(0).Value.GetString());
        Assert.Equal("#D98282", stateMap.Rules.ElementAt(1).Value.GetString());
        Assert.Equal("#D8B95F", stateMap.Rules.ElementAt(2).Value.GetString());
        Assert.Contains(highPerformanceMotor.Parameters!, parameter => parameter.Key == "stoppedColor" && parameter.DefaultValue?.GetString() == "#8FBF98");
        Assert.Contains(highPerformanceMotor.Parameters!, parameter => parameter.Key == "runningColor" && parameter.DefaultValue?.GetString() == "#D98282");
        Assert.Contains(highPerformanceMotor.Parameters!, parameter => parameter.Key == "faultColor" && parameter.DefaultValue?.GetString() == "#D8B95F");

        var families = definitions.Select(definition => definition.Metadata!["familyKey"]).Distinct().ToArray();
        Assert.Contains("electrical.transformer.power", families);
        Assert.Contains("electrical.breaker", families);
        Assert.Contains("electrical.disconnector", families);
        Assert.Contains("electrical.earthing-switch", families);
        Assert.Contains("process.exchanger.shell-tube", families);

        var mapped = definitions.Where(definition => definition.Metadata!["stateTagProfile"] != "none").ToArray();
        Assert.NotEmpty(mapped);
        Assert.All(mapped, definition =>
        {
            Assert.Contains(definition.Parameters!, parameter => parameter.Key == "state" && parameter.Kind == Scada.Engineering.Contracts.DynamoParameterKind.TagReference);
            Assert.Contains(definition.Parameters!, parameter => parameter.Key == "stoppedColor");
            Assert.Contains(definition.Parameters!, parameter => parameter.Key == "runningColor");
            Assert.Contains(definition.Parameters!, parameter => parameter.Key == "faultColor");
            Assert.Contains(definition.Elements!, element => element.PropertyMaps?.Any(map => map.PropertyKey == "fillColor") == true);
        });
    }

    [Fact]
    public void EveryBuiltinDynamo_UsesItsVersionedArtworkFinishWithoutFlatteningSemanticColors()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(72, definitions.Count);
        Assert.All(definitions, definition =>
        {
            var style = definition.Properties!["visualStyle"];
            var expectedFinish = style switch
            {
                "detailed-2d" => "industrial-steel-2d-v5",
                "dimensional-front" => "soft-machined-steel-v5",
                "high-performance" => "high-performance-neutral-v5",
                _ => throw new Xunit.Sdk.XunitException($"Unexpected visual style '{style}'.")
            };

            Assert.Equal(BuiltinDynamoLibrary.Version, definition.Properties["libraryVersion"]);
            Assert.Equal(expectedFinish, definition.Properties["visualFinish"]);
            Assert.Equal(expectedFinish, definition.Metadata!["visualFinish"]);
            Assert.Equal("C-DYNAMO-ARTWORK-02", definition.Properties["artworkContract"]);
            Assert.Equal("C-DYNAMO-ARTWORK-02", definition.Metadata["artworkContract"]);

            foreach (var element in definition.Elements!)
            {
                if (element.Properties is null) continue;
                if (element.Properties.TryGetValue("strokeWidth", out var strokeWidth) &&
                    strokeWidth.ValueKind == System.Text.Json.JsonValueKind.Number &&
                    strokeWidth.GetDouble() > 0)
                {
                    Assert.InRange(strokeWidth.GetDouble(), 0.75, 2.5);
                    Assert.Equal(0, strokeWidth.GetDouble() * 2 % 1, precision: 6);
                }
            }
        });

        var detailedBlower = definitions.Single(definition =>
            definition.Key == "process.blower.centrifugal");
        Assert.Contains(detailedBlower.Elements!, element =>
            element.Key == "outlet-flow-arrow" &&
            element.Properties!["fillColor"].GetString() == "#13799D");

        var highPerformanceMotor = definitions.Single(definition =>
            definition.Key == "process.motor.standard.high-performance");
        Assert.Contains(highPerformanceMotor.Elements!, element =>
            element.Metadata?.ContainsKey("dynamoStateColorProfile") == true);

        var dimensionalDynamos = definitions.Where(definition =>
            definition.Properties!["visualStyle"] == "dimensional-front").ToArray();
        Assert.Equal(24, dimensionalDynamos.Length);
        Assert.All(dimensionalDynamos, definition =>
            Assert.Contains(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("shadowEnabled", out var shadowEnabled) &&
                shadowEnabled.ValueKind == System.Text.Json.JsonValueKind.True));

        Assert.All(definitions.Where(definition =>
            definition.Properties!["visualStyle"] != "dimensional-front"), definition =>
            Assert.DoesNotContain(definition.Elements!, element =>
                element.Properties is not null &&
                element.Properties.TryGetValue("shadowEnabled", out var shadowEnabled) &&
                shadowEnabled.ValueKind == System.Text.Json.JsonValueKind.True));
    }

    [Fact]
    public void EveryBuiltinDynamo_PassesParameterizedStateSourceValidation()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(72, definitions.Count);
        foreach (var definition in definitions)
        {
            var issues = VisualCompositionEngineeringValidation.ValidateDynamo(definition);
            Assert.DoesNotContain(issues, issue =>
                issue.Code is "VISUAL_VALUE_SOURCE_REFERENCE_REQUIRED" or "DYNAMO_STATE_COLOR_PARAMETER_NOT_FOUND");
        }
    }

    [Fact]
    public void EveryBuiltinFamily_HasThreeStylesAndFamilySpecificFinishDetails()
    {
        var definitions = BuiltinDynamoLibrary.Create();
        var families = definitions.GroupBy(definition => definition.Metadata!["familyKey"]).ToArray();

        Assert.Equal(24, families.Length);
        Assert.All(families, family =>
        {
            Assert.Equal(3, family.Count());
            Assert.Equal(3, family.Select(definition => definition.Properties!["visualStyle"]).Distinct().Count());
            Assert.All(family, definition =>
            {
                var hasFamilyFinish = definition.Elements!.Any(element =>
                    element.Key.StartsWith("detail-", StringComparison.Ordinal));
                if (family.Key == "process.blower.centrifugal")
                    hasFamilyFinish |= definition.Elements!.Any(element => element.Key.StartsWith("impeller-blade-", StringComparison.Ordinal));
                if (definition.Properties!["visualStyle"] == "high-performance")
                    Assert.False(definition.Elements!.Any(element => element.Key.StartsWith("detail-", StringComparison.Ordinal)),
                        $"High-performance family '{family.Key}' should stay visually sparse.");
                else
                    Assert.True(hasFamilyFinish, $"Dynamo family '{family.Key}' is missing family-specific artwork details.");
            });
        });

        foreach (var transformer in definitions.Where(definition => definition.Metadata!["familyKey"] == "electrical.transformer.power"))
        {
            var elements = transformer.Elements!.ToDictionary(element => element.Key, StringComparer.Ordinal);
            var tank = elements["tank"].Properties!;
            Assert.Equal(75, tank["x"].GetDouble() + tank["width"].GetDouble() / 2d, precision: 6);

            var leftRadiators = elements
                .Where(pair => pair.Key.StartsWith("radiator-", StringComparison.Ordinal) &&
                    !pair.Key.StartsWith("radiator-r-", StringComparison.Ordinal))
                .OrderBy(pair => pair.Value.Properties!["x"].GetDouble())
                .Select(pair => pair.Value.Properties!["x"].GetDouble())
                .ToArray();
            var rightRadiators = elements
                .Where(pair => pair.Key.StartsWith("radiator-r-", StringComparison.Ordinal))
                .OrderBy(pair => pair.Value.Properties!["x"].GetDouble())
                .Select(pair => pair.Value.Properties!["x"].GetDouble())
                .ToArray();

            Assert.Equal(leftRadiators.Length, rightRadiators.Length);
            Assert.InRange(leftRadiators.Length, 3, 5);
            var radiatorWidth = elements["radiator-1"].Properties!["width"].GetDouble();
            Assert.InRange(Math.Abs((150 - leftRadiators[0] - radiatorWidth) - rightRadiators[^1]), 0, 0.35);
        }
    }

    [Fact]
    public void BuiltinArtwork_StaysInsideItsCanvasAndValveAssembliesShareOneVerticalAxis()
    {
        var definitions = BuiltinDynamoLibrary.Create();
        foreach (var definition in definitions)
        {
            var canvasWidth = double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture);
            var canvasHeight = double.Parse(definition.Properties["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture);
            foreach (var element in definition.Elements!)
            {
                if (element.Properties is null ||
                    !element.Properties.TryGetValue("x", out var x) || !x.TryGetDouble(out var left) ||
                    !element.Properties.TryGetValue("y", out var y) || !y.TryGetDouble(out var top) ||
                    !element.Properties.TryGetValue("width", out var width) || !width.TryGetDouble(out var elementWidth) ||
                    !element.Properties.TryGetValue("height", out var height) || !height.TryGetDouble(out var elementHeight))
                    continue;

                Assert.True(left >= -1 && top >= -1 && left + elementWidth <= canvasWidth + 1 && top + elementHeight <= canvasHeight + 1,
                    $"Element '{element.Key}' in '{definition.Key}' exceeds its {canvasWidth}×{canvasHeight} canvas.");
            }
        }

        var valves = definitions.Where(definition => definition.Metadata!["familyKey"] is
            "process.valve.onoff" or "process.valve.control" or "process.valve.butterfly" or "process.valve.ball" or "process.valve.gate");
        foreach (var valve in valves)
        {
            var elements = valve.Elements!.ToDictionary(element => element.Key, StringComparer.Ordinal);
            var axisElements = valve.Metadata!["familyKey"] switch
            {
                "process.valve.gate" => new[] { "stem", "handwheel", "handwheel-hub" },
                "process.valve.ball" => new[] { "stem", "ball", "bore" },
                "process.valve.butterfly" => new[] { "shaft", "disc", "disc-edge" },
                _ => new[] { "stem", "actuator" }
            };
            var centers = axisElements.Select(key =>
            {
                Assert.True(elements.TryGetValue(key, out var element), $"Missing '{key}' in '{valve.Key}'.");
                return element!.Properties!["x"].GetDouble() + element.Properties["width"].GetDouble() / 2d;
            }).ToArray();

            Assert.InRange(centers.Max() - centers.Min(), 0, 1.5);
        }
    }

    [Fact]
    public void BuiltinArtwork_RotatedPartsAndOutlinesStayInsideTheirLogicalCanvas()
    {
        var problems = new List<string>();
        foreach (var definition in BuiltinDynamoLibrary.Create())
        {
            var canvasWidth = double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture);
            var canvasHeight = double.Parse(definition.Properties["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture);
            foreach (var element in definition.Elements!)
            {
                var properties = element.Properties;
                if (properties is null || !properties.TryGetValue("x", out var x) || !x.TryGetDouble(out var left) ||
                    !properties.TryGetValue("y", out var y) || !y.TryGetDouble(out var top) ||
                    !properties.TryGetValue("width", out var width) || !width.TryGetDouble(out var elementWidth) ||
                    !properties.TryGetValue("height", out var height) || !height.TryGetDouble(out var elementHeight)) continue;
                var rotation = properties.TryGetValue("rotation", out var rotationValue) && rotationValue.TryGetDouble(out var degrees) ? degrees : 0;
                var radians = rotation * Math.PI / 180;
                var boundsWidth = Math.Abs(elementWidth * Math.Cos(radians)) + Math.Abs(elementHeight * Math.Sin(radians));
                var boundsHeight = Math.Abs(elementWidth * Math.Sin(radians)) + Math.Abs(elementHeight * Math.Cos(radians));
                var centerX = left + elementWidth / 2;
                var centerY = top + elementHeight / 2;
                var rotatedLeft = centerX - boundsWidth / 2;
                var rotatedTop = centerY - boundsHeight / 2;
                if (rotatedLeft < -2 || rotatedTop < -2 || rotatedLeft + boundsWidth > canvasWidth + 2 || rotatedTop + boundsHeight > canvasHeight + 2)
                    problems.Add($"{definition.Key}/{element.Key}: rotated bounds {rotatedLeft:0.##},{rotatedTop:0.##} {boundsWidth:0.##}×{boundsHeight:0.##} outside {canvasWidth}×{canvasHeight}");
            }
        }

        Assert.Empty(problems);
    }
}
