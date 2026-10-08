using System.Text.Json;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Interactions;
using Scada.Engineering.Reports;
using Scada.Engineering.Scripts;

namespace Scada.Engineering.ImportExport;

internal static class EngineeringFragmentRemapper
{
    public static EngineeringPackage Apply(
        EngineeringPackage package,
        IReadOnlyDictionary<(ImportEntityKind Kind, Guid SourceId), Guid> remaps)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(remaps);

        Guid Map(ImportEntityKind kind, Guid id) =>
            remaps.TryGetValue((kind, id), out var mapped) ? mapped : id;

        Guid? MapNullable(ImportEntityKind kind, Guid? id) =>
            id is { } value && value != Guid.Empty ? Map(kind, value) : id;

        TagValueReference? MapTagReference(TagValueReference? reference) =>
            reference is null ? null : reference with { TagId = Map(ImportEntityKind.Tag, reference.TagId) };

        EngineeringBindingDto MapBinding(EngineeringBindingDto binding) =>
            binding with { TagReference = MapTagReference(binding.TagReference) };

        VisualExpressionEngineeringDto? MapExpression(VisualExpressionEngineeringDto? expression) =>
            expression is null
                ? null
                : expression with
                {
                    Dependencies = (expression.Dependencies ?? Array.Empty<VisualExpressionDependencyEngineeringDto>())
                        .Select(dependency => dependency with
                        {
                            TagReference = dependency.TagReference with
                            {
                                TagId = Map(ImportEntityKind.Tag, dependency.TagReference.TagId)
                            }
                        })
                        .ToArray()
                };

        VisualValueSourceEngineeringDto MapSource(VisualValueSourceEngineeringDto source) =>
            source with
            {
                TagReference = MapTagReference(source.TagReference),
                Expression = MapExpression(source.Expression)
            };

        JsonElement MapAssetReference(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty("assetId", out var assetProperty) ||
                assetProperty.ValueKind != JsonValueKind.String)
                return value;

            var text = assetProperty.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return value;

            var raw = text.StartsWith("asset:", StringComparison.Ordinal)
                ? text["asset:".Length..]
                : text;
            if (!Guid.TryParse(raw, out var sourceId) || sourceId == Guid.Empty)
                return value;

            var mapped = Map(ImportEntityKind.VisualAsset, sourceId);
            return mapped == sourceId
                ? value
                : JsonSerializer.SerializeToElement(new { assetId = $"asset:{mapped:D}" });
        }

        VisualElementEngineeringDto MapElement(VisualElementEngineeringDto element)
        {
            Dictionary<string, JsonElement>? properties = null;
            if (element.Properties is not null)
            {
                properties = new Dictionary<string, JsonElement>(element.Properties, StringComparer.Ordinal);
                if (properties.TryGetValue("assetRef", out var assetRef))
                    properties["assetRef"] = MapAssetReference(assetRef);
            }

            return element with
            {
                DynamoDefinitionId = MapNullable(ImportEntityKind.Dynamo, element.DynamoDefinitionId),
                EquipmentId = MapNullable(ImportEntityKind.Equipment, element.EquipmentId),
                Bindings = element.Bindings is null
                    ? null
                    : element.Bindings
                        .Select(MapBinding)
                        .ToArray(),
                Properties = properties,
                PropertyExpressions = element.PropertyExpressions is null
                    ? null
                    : element.PropertyExpressions
                        .Select(expression => expression with { Expression = MapExpression(expression.Expression)! })
                        .ToArray(),
                BooleanConditions = element.BooleanConditions is null
                    ? null
                    : element.BooleanConditions
                        .Select(condition => condition with { Source = MapSource(condition.Source) })
                        .ToArray(),
                AnalogFill = element.AnalogFill is null
                    ? null
                    : element.AnalogFill with { Source = MapSource(element.AnalogFill.Source) },
                PropertyMaps = element.PropertyMaps is null
                    ? null
                    : element.PropertyMaps
                        .Select(map => map with { Source = MapSource(map.Source) })
                        .ToArray(),
                DynamoParameters = element.DynamoParameters is null
                    ? null
                    : element.DynamoParameters
                        .Select(parameter => parameter with { TagReference = MapTagReference(parameter.TagReference) })
                        .ToArray(),
                Actions = element.Actions is null
                    ? null
                    : element.Actions
                        .Select(action => action with
                        {
                            CommandId = MapNullable(ImportEntityKind.Command, action.CommandId),
                            TargetKey = (action.Kind is VisualNavigationActionKind.SetTagValue or VisualNavigationActionKind.ToggleTagBoolean) &&
                                Guid.TryParse(action.TargetKey, out var tagId)
                                    ? Map(ImportEntityKind.Tag, tagId).ToString("D")
                                    : action.TargetKey
                        })
                        .ToArray(),
                Children = element.Children is null
                    ? null
                    : element.Children
                        .Select(MapElement)
                        .ToArray()
            };
        }

        ScriptEngineeringDefinition MapScript(ScriptEngineeringDefinition script)
        {
            var mappedId = Map(ImportEntityKind.Script, script.Id);
            var entryPoints = script.EntryPoints.Select(entry => entry with
            {
                TagReference = MapTagReference(entry.TagReference)
            }).ToArray();
            var dependencies = script.Dependencies.Select(dependency =>
            {
                var stable = dependency.StableReference;
                var binding = dependency.TagBinding;
                switch (dependency.Kind)
                {
                    case ScriptEngineeringDependencyKind.Script:
                        stable = MapStableGuid(stable, ImportEntityKind.Script, Map);
                        break;
                    case ScriptEngineeringDependencyKind.Tag:
                    case ScriptEngineeringDependencyKind.ClientMemoryTag:
                    case ScriptEngineeringDependencyKind.ServerMemoryTag:
                        stable = MapStableGuid(stable, ImportEntityKind.Tag, Map);
                        if (binding is not null)
                            binding = binding with
                            {
                                Expected = binding.Expected with
                                {
                                    TagId = Map(ImportEntityKind.Tag, binding.Expected.TagId)
                                }
                            };
                        break;
                    case ScriptEngineeringDependencyKind.VisualDefinition:
                        stable = MapVisualDefinitionGuid(stable, remaps);
                        break;
                    case ScriptEngineeringDependencyKind.VisualObject:
                    {
                        var separator = stable.IndexOf('/');
                        if (separator > 0)
                        {
                            var head = stable[..separator];
                            stable = $"{MapVisualDefinitionGuid(head, remaps)}{stable[separator..]}";
                        }
                        break;
                    }
                    case ScriptEngineeringDependencyKind.Resource:
                        stable = MapStableGuid(stable, ImportEntityKind.VisualAsset, Map);
                        break;
                    case ScriptEngineeringDependencyKind.RichCommand:
                        stable = MapStableGuid(stable, ImportEntityKind.RichCommandDefinition, Map);
                        break;
                }

                return dependency with { StableReference = stable, TagBinding = binding };
            }).ToArray();

            return new ScriptEngineeringDefinition(
                mappedId,
                script.Path,
                script.Name,
                script.Scope,
                script.Source,
                script.Enabled,
                script.Language,
                script.LanguageVersion,
                entryPoints,
                dependencies,
                script.Description,
                script.Metadata);
        }

        var scripts = (package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>())
            .Select(MapScript)
            .ToArray();

        var visualReferences = (package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>())
            .Select(reference => reference with
            {
                VisualDefinitionId = MapVisualDefinition(reference.VisualDefinitionId, remaps),
                ScriptId = Map(ImportEntityKind.Script, reference.ScriptId),
                TagReference = MapTagReference(reference.TagReference)
            })
            .ToArray();

        return package with
        {
            Tags = package.Tags.Select(tag => tag with
            {
                Id = MapNullable(ImportEntityKind.Tag, tag.Id),
                DataSourceId = MapNullable(ImportEntityKind.DataSource, tag.DataSourceId),
                HistorianCaptureProfileId = MapNullable(ImportEntityKind.HistorianCaptureProfile, tag.HistorianCaptureProfileId)
            }).ToArray(),
            Alarms = package.Alarms.Select(alarm => alarm with
            {
                Id = MapNullable(ImportEntityKind.Alarm, alarm.Id),
                TagId = MapNullable(ImportEntityKind.Tag, alarm.TagId)
            }).ToArray(),
            DataSources = (package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
                .Select(source => source with { Id = MapNullable(ImportEntityKind.DataSource, source.Id) })
                .ToArray(),
            Templates = (package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
                .Select(template => template with
                {
                    Id = MapNullable(ImportEntityKind.Template, template.Id),
                    Bindings = template.Bindings is null
                        ? null
                        : template.Bindings.Select(MapBinding).ToArray(),
                    Elements = template.Elements is null
                        ? null
                        : template.Elements.Select(MapElement).ToArray()
                })
                .ToArray(),
            Equipment = (package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
                .Select(equipment => equipment with
                {
                    Id = MapNullable(ImportEntityKind.Equipment, equipment.Id),
                    TemplateId = MapNullable(ImportEntityKind.Template, equipment.TemplateId),
                    Bindings = equipment.Bindings is null
                        ? null
                        : equipment.Bindings.Select(MapBinding).ToArray()
                })
                .ToArray(),
            Dynamos = (package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
                .Select(dynamo => dynamo with
                {
                    Id = MapNullable(ImportEntityKind.Dynamo, dynamo.Id),
                    TemplateId = MapNullable(ImportEntityKind.Template, dynamo.TemplateId),
                    Bindings = dynamo.Bindings is null
                        ? null
                        : dynamo.Bindings.Select(MapBinding).ToArray(),
                    Elements = dynamo.Elements is null
                        ? null
                        : dynamo.Elements.Select(MapElement).ToArray()
                })
                .ToArray(),
            Screens = (package.Screens ?? Array.Empty<ScreenEngineeringDto>())
                .Select(screen => screen with
                {
                    Id = MapNullable(ImportEntityKind.Screen, screen.Id),
                    Elements = screen.Elements is null
                        ? null
                        : screen.Elements.Select(MapElement).ToArray()
                })
                .ToArray(),
            Popups = (package.Popups ?? Array.Empty<PopupEngineeringDto>())
                .Select(popup => popup with
                {
                    Id = MapNullable(ImportEntityKind.Popup, popup.Id),
                    TemplateId = MapNullable(ImportEntityKind.Template, popup.TemplateId),
                    Elements = popup.Elements is null
                        ? null
                        : popup.Elements.Select(MapElement).ToArray()
                })
                .ToArray(),
            Commands = (package.Commands ?? Array.Empty<CommandEngineeringDto>())
                .Select(command => command with
                {
                    Id = MapNullable(ImportEntityKind.Command, command.Id),
                    TargetTagId = MapNullable(ImportEntityKind.Tag, command.TargetTagId)
                })
                .ToArray(),
            Gateways = (package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>())
                .Select(gateway => gateway with
                {
                    Id = MapNullable(ImportEntityKind.Gateway, gateway.Id),
                    SourceTagId = MapNullable(ImportEntityKind.Tag, gateway.SourceTagId),
                    DestinationTagId = MapNullable(ImportEntityKind.Tag, gateway.DestinationTagId)
                })
                .ToArray(),
            Scripts = scripts,
            ScriptVisualEventReferences = visualReferences,
            VisualAssets = (package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
                .Select(asset => asset with { Id = MapNullable(ImportEntityKind.VisualAsset, asset.Id) })
                .ToArray(),
            Reports = (package.Reports ?? Array.Empty<ReportEngineeringDto>())
                .Select(report => report with
                {
                    Id = MapNullable(ImportEntityKind.Report, report.Id),
                    Sections = report.Sections is null
                        ? null
                        : report.Sections
                            .Select(section => section with
                            {
                                Controls = section.Controls is null
                                    ? null
                                    : section.Controls
                                        .Select(control => control with
                                        {
                                            AssetId = MapNullable(ImportEntityKind.VisualAsset, control.AssetId)
                                        })
                                        .ToArray()
                            })
                            .ToArray()
                })
                .ToArray(),
            OperationalEvents = (package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>())
                .Select(item => item with
                {
                    Id = MapNullable(ImportEntityKind.OperationalEvent, item.Id),
                    TagId = MapNullable(ImportEntityKind.Tag, item.TagId)
                })
                .ToArray(),
            HistorianCaptureProfiles = (package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>())
                .Select(profile => profile with { Id = MapNullable(ImportEntityKind.HistorianCaptureProfile, profile.Id) })
                .ToArray(),
            DataQueries = (package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>())
                .Select(query => query with { Id = MapNullable(ImportEntityKind.DataQuery, query.Id) })
                .ToArray(),
            AlarmViews = (package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>())
                .Select(view => view with { Id = MapNullable(ImportEntityKind.AlarmView, view.Id) })
                .ToArray(),
            RichCommandDefinitions = (package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>())
                .Select(command => command with { CommandId = Map(ImportEntityKind.RichCommandDefinition, command.CommandId) })
                .ToArray(),
            DriverCommandBindings = (package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>())
                .Select(binding => binding with
                {
                    CommandId = Map(ImportEntityKind.RichCommandDefinition, binding.CommandId),
                    DataSourceId = Map(ImportEntityKind.DataSource, binding.DataSourceId),
                    EquipmentId = MapNullable(ImportEntityKind.Equipment, binding.EquipmentId)
                })
                .ToArray()
        };
    }

    private static string MapStableGuid(
        string value,
        ImportEntityKind kind,
        Func<ImportEntityKind, Guid, Guid> map)
    {
        if (!Guid.TryParse(value, out var source) || source == Guid.Empty)
            return value;
        return map(kind, source).ToString("D");
    }

    private static string MapVisualDefinitionGuid(
        string value,
        IReadOnlyDictionary<(ImportEntityKind Kind, Guid SourceId), Guid> remaps)
    {
        if (!Guid.TryParse(value, out var source) || source == Guid.Empty)
            return value;
        return MapVisualDefinition(source, remaps).ToString("D");
    }

    private static Guid MapVisualDefinition(
        Guid source,
        IReadOnlyDictionary<(ImportEntityKind Kind, Guid SourceId), Guid> remaps)
    {
        foreach (var kind in new[] { ImportEntityKind.Screen, ImportEntityKind.Popup, ImportEntityKind.Dynamo })
        {
            if (remaps.TryGetValue((kind, source), out var mapped))
                return mapped;
        }
        return source;
    }
}
