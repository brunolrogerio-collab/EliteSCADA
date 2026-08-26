using Scada.Api.Security;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public sealed record EngineeringDependency(
    string EntityKind,
    string EntityId,
    string EntityKey,
    string Relation);

public static class EngineeringMutationEndpoints
{
    private const string WorkspaceVersionHeader = "x-elitescada-workspace-version";

    public static void MapEngineeringMutationEndpoints(this WebApplication app)
    {
        app.MapDelete("/api/engineering/tags/{id:guid}", (
            Guid id,
            HttpContext context,
            EngineeringWorkspace workspace,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
            DeleteAsync(
                id,
                context,
                workspace,
                security,
                audit,
                "tag",
                () => BuildTagDeletePlan(workspace, id)));

        app.MapDelete("/api/engineering/alarms/{id:guid}", (
            Guid id,
            HttpContext context,
            EngineeringWorkspace workspace,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
            DeleteAsync(
                id,
                context,
                workspace,
                security,
                audit,
                "alarm",
                () => BuildAlarmDeletePlan(workspace, id)));

        app.MapDelete("/api/engineering/data-sources/{id:guid}", (
            Guid id,
            HttpContext context,
            EngineeringWorkspace workspace,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
            DeleteAsync(
                id,
                context,
                workspace,
                security,
                audit,
                "data-source",
                () => BuildDataSourceDeletePlan(workspace, id)));

        app.MapEngineeringBulkEndpoints();
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext context,
        EngineeringWorkspace workspace,
        ApiAuthorizationService security,
        ApiAuditService audit,
        string targetKind,
        Func<DeletePlan?> buildPlan)
    {
        var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
        var failure = authorization.FailureResult();
        if (failure is not null)
        {
            await audit.RecordAuthorizationDeniedAsync(
                context,
                authorization,
                AuditActions.EngineeringDelete,
                targetKind,
                id.ToString());
            return failure;
        }

        if (!TryReadExpectedVersion(context.Request, out var expectedChangeVersion))
        {
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.EngineeringDelete,
                AuditOutcome.Failed,
                targetKind,
                id.ToString(),
                new Dictionary<string, string> { ["reason"] = "missing-or-invalid-workspace-version" });
            return Results.BadRequest(new
            {
                error = $"Header '{WorkspaceVersionHeader}' with a non-negative integer Workspace version is required."
            });
        }

        try
        {
            await using var mutation = await workspace.AcquireMutationAsync(
                expectedChangeVersion,
                context.RequestAborted);

            var plan = buildPlan();
            if (plan is null) return Results.NotFound();

            if (plan.Dependencies.Count > 0)
            {
                await audit.RecordAsync(
                    context,
                    authorization.Principal,
                    AuditActions.EngineeringDelete,
                    AuditOutcome.Failed,
                    targetKind,
                    plan.TargetId,
                    new Dictionary<string, string>
                    {
                        ["reason"] = "dependencies",
                        ["targetKey"] = plan.TargetKey,
                        ["dependencyCount"] = plan.Dependencies.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                return Results.Conflict(new
                {
                    error = $"{plan.TargetKind} '{plan.TargetKey}' cannot be deleted because Engineering dependencies still reference it.",
                    dependencies = plan.Dependencies
                });
            }

            if (!plan.Remove()) return Results.NotFound();

            var resultingChangeVersion = workspace.CaptureChangeVersion();
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.EngineeringDelete,
                AuditOutcome.Succeeded,
                targetKind,
                plan.TargetId,
                new Dictionary<string, string>
                {
                    ["targetKey"] = plan.TargetKey,
                    ["expectedChangeVersion"] = expectedChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["resultingChangeVersion"] = resultingChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });

            return Results.Ok(new
            {
                deleted = true,
                entityKind = plan.TargetKind,
                entityId = plan.TargetId,
                entityKey = plan.TargetKey,
                changeVersion = resultingChangeVersion
            });
        }
        catch (EngineeringWorkspaceVersionConflictException conflict)
        {
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.EngineeringDelete,
                AuditOutcome.Failed,
                targetKind,
                id.ToString(),
                new Dictionary<string, string>
                {
                    ["reason"] = "workspace-version-conflict",
                    ["expectedChangeVersion"] = conflict.ExpectedChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["currentChangeVersion"] = conflict.CurrentChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            return Results.Conflict(new
            {
                error = "Engineering Workspace changed before Delete. Reload before trying again.",
                expectedChangeVersion = conflict.ExpectedChangeVersion,
                currentChangeVersion = conflict.CurrentChangeVersion
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.EngineeringDelete,
                AuditOutcome.Failed,
                targetKind,
                id.ToString(),
                new Dictionary<string, string>
                {
                    ["reason"] = "unexpected-error",
                    ["errorType"] = ex.GetType().Name
                });
            throw;
        }
    }

    private static DeletePlan? BuildAlarmDeletePlan(EngineeringWorkspace workspace, Guid id)
    {
        var alarm = workspace.Alarms.Definitions().FirstOrDefault(candidate => candidate.Id == id);
        return alarm is null
            ? null
            : new DeletePlan(
                "alarm",
                alarm.Id.ToString(),
                alarm.Name,
                Array.Empty<EngineeringDependency>(),
                () => workspace.Alarms.Remove(alarm.Id));
    }

    private static DeletePlan? BuildDataSourceDeletePlan(EngineeringWorkspace workspace, Guid id)
    {
        var source = workspace.DataSources.Find(id);
        if (source is null) return null;

        var dependencies = workspace.Tags.Snapshot()
            .Where(tag => string.Equals(tag.Source, source.Key, StringComparison.OrdinalIgnoreCase))
            .Select(tag => new EngineeringDependency(
                "tag",
                tag.Id.ToString(),
                tag.Path,
                "source"))
            .ToArray();

        return new DeletePlan(
            "data-source",
            id.ToString(),
            source.Key,
            dependencies,
            () => workspace.DataSources.Remove(id));
    }

    private static DeletePlan? BuildTagDeletePlan(EngineeringWorkspace workspace, Guid id)
    {
        if (!workspace.Tags.TryGet(id, out var tag) || tag is null) return null;

        var dependencies = new List<EngineeringDependency>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var alarm in workspace.Alarms.Definitions().Where(alarm => alarm.TagId == tag.Id))
            AddDependency(dependencies, seen, "alarm", alarm.Id.ToString(), alarm.Name, "tag");

        foreach (var command in workspace.Commands.Snapshot())
        {
            if (command.TargetTagId == tag.Id ||
                string.Equals(command.TargetTagPath, tag.Path, StringComparison.OrdinalIgnoreCase))
            {
                AddDependency(
                    dependencies,
                    seen,
                    "command",
                    command.Id?.ToString() ?? command.Key,
                    command.Key,
                    "targetTag");
            }
        }

        var templates = workspace.Assets.SnapshotTemplates().ToDictionary(
            template => template.Key,
            StringComparer.OrdinalIgnoreCase);
        var dynamos = workspace.Assets.SnapshotDynamos().ToDictionary(
            dynamo => dynamo.Key,
            StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates.Values)
            AddBindingDependencies(dependencies, seen, tag.Path, "template", template.Id, template.Key, template.Bindings);

        foreach (var equipment in workspace.Assets.SnapshotEquipment())
        {
            AddBindingDependencies(
                dependencies,
                seen,
                tag.Path,
                "equipment",
                equipment.Id,
                equipment.Path,
                equipment.Bindings,
                equipment.Path);

            if (!string.IsNullOrWhiteSpace(equipment.TemplateKey) &&
                templates.TryGetValue(equipment.TemplateKey, out var template))
            {
                AddBindingDependencies(
                    dependencies,
                    seen,
                    tag.Path,
                    "equipment",
                    equipment.Id,
                    equipment.Path,
                    template.Bindings,
                    equipment.Path,
                    "templateBinding");
            }
        }

        foreach (var dynamo in dynamos.Values)
            AddBindingDependencies(dependencies, seen, tag.Path, "dynamo", dynamo.Id, dynamo.Key, dynamo.Bindings);

        foreach (var screen in workspace.Views.SnapshotScreens())
            AddElementDependencies(
                dependencies,
                seen,
                tag.Path,
                "screen",
                screen.Id,
                screen.Key,
                screen.Elements,
                templates,
                dynamos);

        foreach (var popup in workspace.Views.SnapshotPopups())
            AddElementDependencies(
                dependencies,
                seen,
                tag.Path,
                "popup",
                popup.Id,
                popup.Key,
                popup.Elements,
                templates,
                dynamos);

        foreach (var role in workspace.SecurityPolicies.SnapshotRoles())
        {
            if ((role.Grants ?? Array.Empty<CapabilityGrantEngineeringDto>())
                .Any(grant => string.Equals(grant.Scope?.TagPath, tag.Path, StringComparison.OrdinalIgnoreCase)))
            {
                AddDependency(
                    dependencies,
                    seen,
                    "security-role",
                    role.Id?.ToString() ?? role.Key,
                    role.Key,
                    "tagScope");
            }
        }

        return new DeletePlan(
            "tag",
            tag.Id.ToString(),
            tag.Path,
            dependencies,
            () => workspace.Tags.Remove(tag.Id));
    }

    private static void AddElementDependencies(
        List<EngineeringDependency> dependencies,
        HashSet<string> seen,
        string tagPath,
        string ownerKind,
        Guid? ownerId,
        string ownerKey,
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        IReadOnlyDictionary<string, EquipmentTemplateEngineeringDto> templates,
        IReadOnlyDictionary<string, DynamoEngineeringDto> dynamos)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            AddBindingDependencies(
                dependencies,
                seen,
                tagPath,
                ownerKind,
                ownerId,
                ownerKey,
                element.Bindings,
                element.EquipmentPath,
                $"element:{element.Key}");

            if (!string.IsNullOrWhiteSpace(element.DynamoKey) &&
                !string.IsNullOrWhiteSpace(element.EquipmentPath) &&
                dynamos.TryGetValue(element.DynamoKey, out var dynamo))
            {
                AddBindingDependencies(
                    dependencies,
                    seen,
                    tagPath,
                    ownerKind,
                    ownerId,
                    ownerKey,
                    dynamo.Bindings,
                    element.EquipmentPath,
                    $"dynamo:{dynamo.Key}");

                if (!string.IsNullOrWhiteSpace(dynamo.TemplateKey) &&
                    templates.TryGetValue(dynamo.TemplateKey, out var template))
                {
                    AddBindingDependencies(
                        dependencies,
                        seen,
                        tagPath,
                        ownerKind,
                        ownerId,
                        ownerKey,
                        template.Bindings,
                        element.EquipmentPath,
                        $"dynamoTemplate:{template.Key}");
                }
            }

            AddElementDependencies(
                dependencies,
                seen,
                tagPath,
                ownerKind,
                ownerId,
                ownerKey,
                element.Children,
                templates,
                dynamos);
        }
    }

    private static void AddBindingDependencies(
        List<EngineeringDependency> dependencies,
        HashSet<string> seen,
        string tagPath,
        string ownerKind,
        Guid? ownerId,
        string ownerKey,
        IReadOnlyCollection<EngineeringBindingDto>? bindings,
        string? equipmentPath = null,
        string relation = "binding")
    {
        foreach (var binding in bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            if (binding.Kind != EngineeringBindingKind.Tag) continue;
            var target = binding.Target;
            if (!string.IsNullOrWhiteSpace(equipmentPath))
                target = target.Replace("{equipmentPath}", equipmentPath, StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(target, tagPath, StringComparison.OrdinalIgnoreCase)) continue;

            AddDependency(
                dependencies,
                seen,
                ownerKind,
                ownerId?.ToString() ?? ownerKey,
                ownerKey,
                $"{relation}:{binding.Key}");
        }
    }

    private static void AddDependency(
        List<EngineeringDependency> dependencies,
        HashSet<string> seen,
        string kind,
        string id,
        string key,
        string relation)
    {
        var signature = $"{kind}|{id}|{relation}";
        if (!seen.Add(signature)) return;
        dependencies.Add(new EngineeringDependency(kind, id, key, relation));
    }

    private static bool TryReadExpectedVersion(HttpRequest request, out long expectedChangeVersion)
    {
        expectedChangeVersion = default;
        return request.Headers.TryGetValue(WorkspaceVersionHeader, out var header) &&
               header.Count == 1 &&
               long.TryParse(
                   header.ToString(),
                   System.Globalization.NumberStyles.None,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out expectedChangeVersion) &&
               expectedChangeVersion >= 0;
    }

    private sealed record DeletePlan(
        string TargetKind,
        string TargetId,
        string TargetKey,
        IReadOnlyCollection<EngineeringDependency> Dependencies,
        Func<bool> Remove);
}
