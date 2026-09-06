using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Libraries;
using Scada.Security.Audit;

namespace Scada.Api.Libraries;

public sealed record ReusableLibraryIncorporationRequest(string Kind);

public static class ReusableLibraryIncorporationEndpoints
{
    public const string IncorporateRouteTemplate =
        "/api/engineering/libraries/{libraryId:guid}/resources/{resourceId:guid}/incorporate";
    private const string WorkspaceVersionHeader = "x-elitescada-workspace-version";

    public static IEndpointRouteBuilder MapReusableLibraryIncorporationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(IncorporateRouteTemplate, async (
            Guid libraryId,
            Guid resourceId,
            ReusableLibraryIncorporationRequest request,
            HttpContext context,
            EngineeringWorkspace workspace,
            IEngineeringExchangeService exchange,
            ApiAuthorizationService security,
            ApiAuditService audit) =>
        {
            var access = ReusableLibraryEndpoints.CheckAccess(context, security, exchange);
            if (access.Failure is not null)
            {
                if (access.Reason == "capability")
                {
                    await audit.RecordAuthorizationDeniedAsync(
                        context,
                        access.Authorization,
                        AuditActions.EngineeringLibraryIncorporate,
                        "reusable-resource",
                        resourceId.ToString("D"),
                        new Dictionary<string, string> { ["libraryId"] = libraryId.ToString("D") });
                }
                else
                {
                    await audit.RecordAsync(
                        context,
                        access.Authorization.Principal,
                        AuditActions.EngineeringLibraryIncorporate,
                        AuditOutcome.Denied,
                        "reusable-resource",
                        resourceId.ToString("D"),
                        new Dictionary<string, string>
                        {
                            ["libraryId"] = libraryId.ToString("D"),
                            ["reason"] = access.Reason ?? "denied"
                        });
                }
                return access.Failure;
            }

            if (!TryReadExpectedVersion(context.Request, out var expectedChangeVersion))
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryIncorporate,
                    AuditOutcome.Failed,
                    "reusable-resource",
                    resourceId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["libraryId"] = libraryId.ToString("D"),
                        ["reason"] = "missing-or-invalid-workspace-version"
                    });
                return Results.BadRequest(new
                {
                    error = $"Header '{WorkspaceVersionHeader}' with a non-negative integer Workspace version is required."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Kind))
                return Results.BadRequest(new { error = "Reusable resource kind is required." });

            var catalogEntry = ReusableLibraryCatalogStore.Shared.Find(
                ReusableLibraryEndpoints.CatalogScope(workspace),
                libraryId);
            if (catalogEntry is null) return Results.NotFound();

            var selection = new ReusableLibraryIncorporationSelection(request.Kind.Trim(), resourceId);
            ReusableLibraryIncorporationPlan? plan = null;
            try
            {
                var packages = new ReusableLibraryPackageService(workspace.Assets, workspace.VisualAssets);
                var incorporation = new ReusableLibraryIncorporationService(
                    packages,
                    workspace.Assets,
                    workspace.VisualAssets,
                    exchange);
                plan = incorporation.Plan(catalogEntry.Content, selection);

                if (!plan.RequiresMutation)
                {
                    var unchangedVersion = workspace.CaptureChangeVersion();
                    await audit.RecordAsync(
                        context,
                        access.Authorization.Principal,
                        AuditActions.EngineeringLibraryIncorporate,
                        AuditOutcome.Succeeded,
                        "reusable-resource",
                        resourceId.ToString("D"),
                        new Dictionary<string, string>
                        {
                            ["libraryId"] = libraryId.ToString("D"),
                            ["kind"] = selection.Kind,
                            ["closureCount"] = plan.DependencyClosure.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["deduplicated"] = plan.DeduplicatedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["resultingChangeVersion"] = unchangedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["workingChanged"] = bool.FalseString
                        });
                    return Results.Ok(new
                    {
                        libraryId,
                        resourceId,
                        kind = selection.Kind,
                        incorporated = false,
                        deduplicated = true,
                        closureCount = plan.DependencyClosure.Count,
                        changeVersion = unchangedVersion
                    });
                }

                await using var mutation = await workspace.AcquireMutationAsync(
                    expectedChangeVersion,
                    context.RequestAborted);

                var finalPreview = exchange.Preview(
                    plan.Engineering,
                    ImportMode.CreateOnly,
                    plan.ImportContext);
                if (!finalPreview.CanApply)
                {
                    await audit.RecordAsync(
                        context,
                        access.Authorization.Principal,
                        AuditActions.EngineeringLibraryIncorporate,
                        AuditOutcome.Failed,
                        "reusable-resource",
                        resourceId.ToString("D"),
                        new Dictionary<string, string>
                        {
                            ["libraryId"] = libraryId.ToString("D"),
                            ["kind"] = selection.Kind,
                            ["reason"] = "canonical-preview-errors",
                            ["errorCount"] = finalPreview.ErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        });
                    return Results.BadRequest(finalPreview);
                }

                var result = exchange.Apply(
                    plan.Engineering,
                    ImportMode.CreateOnly,
                    plan.ImportContext);
                if (result.Issues.Any(issue => issue.IsError))
                {
                    await audit.RecordAsync(
                        context,
                        access.Authorization.Principal,
                        AuditActions.EngineeringLibraryIncorporate,
                        AuditOutcome.Failed,
                        "reusable-resource",
                        resourceId.ToString("D"),
                        new Dictionary<string, string>
                        {
                            ["libraryId"] = libraryId.ToString("D"),
                            ["kind"] = selection.Kind,
                            ["reason"] = "canonical-apply-errors"
                        });
                    return Results.BadRequest(result);
                }

                if (result.Created <= 0)
                    throw new InvalidOperationException("Reusable resource incorporation produced no canonical create operation.");

                var resultingChangeVersion = workspace.CaptureChangeVersion();
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryIncorporate,
                    AuditOutcome.Succeeded,
                    "reusable-resource",
                    resourceId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["libraryId"] = libraryId.ToString("D"),
                        ["kind"] = selection.Kind,
                        ["closureCount"] = plan.DependencyClosure.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["created"] = result.Created.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["deduplicated"] = plan.DeduplicatedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["expectedChangeVersion"] = expectedChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["resultingChangeVersion"] = resultingChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return Results.Ok(new
                {
                    libraryId,
                    resourceId,
                    kind = selection.Kind,
                    incorporated = true,
                    created = result.Created,
                    deduplicated = plan.DeduplicatedCount,
                    closureCount = plan.DependencyClosure.Count,
                    changeVersion = resultingChangeVersion
                });
            }
            catch (EngineeringWorkspaceVersionConflictException conflict)
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryIncorporate,
                    AuditOutcome.Failed,
                    "reusable-resource",
                    resourceId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["libraryId"] = libraryId.ToString("D"),
                        ["kind"] = selection.Kind,
                        ["reason"] = "workspace-version-conflict",
                        ["expectedChangeVersion"] = conflict.ExpectedChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["currentChangeVersion"] = conflict.CurrentChangeVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                return Results.Conflict(new
                {
                    error = "Engineering Workspace changed before reusable resource incorporation. Reload and try again.",
                    expectedChangeVersion = conflict.ExpectedChangeVersion,
                    currentChangeVersion = conflict.CurrentChangeVersion
                });
            }
            catch (ReusableLibraryIncorporationConflictException conflict)
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryIncorporate,
                    AuditOutcome.Failed,
                    "reusable-resource",
                    resourceId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["libraryId"] = libraryId.ToString("D"),
                        ["kind"] = selection.Kind,
                        ["reason"] = "resource-collision",
                        ["collision"] = conflict.Reason
                    });
                return Results.Conflict(new { error = conflict.Message });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidDataException ex)
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryIncorporate,
                    AuditOutcome.Failed,
                    "reusable-resource",
                    resourceId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["libraryId"] = libraryId.ToString("D"),
                        ["kind"] = selection.Kind,
                        ["reason"] = "invalid-incorporation-plan",
                        ["errorType"] = ex.GetType().Name
                    });
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
            {
                await audit.RecordAsync(
                    context,
                    access.Authorization.Principal,
                    AuditActions.EngineeringLibraryIncorporate,
                    AuditOutcome.Failed,
                    "reusable-resource",
                    resourceId.ToString("D"),
                    new Dictionary<string, string>
                    {
                        ["libraryId"] = libraryId.ToString("D"),
                        ["kind"] = selection.Kind,
                        ["reason"] = "unexpected-error",
                        ["errorType"] = ex.GetType().Name
                    });
                throw;
            }
        });

        return endpoints;
    }

    internal static bool TryReadExpectedVersion(HttpRequest request, out long expectedChangeVersion)
    {
        expectedChangeVersion = 0;
        if (!request.Headers.TryGetValue(WorkspaceVersionHeader, out var header) ||
            header.Count != 1 ||
            !long.TryParse(
                header.ToString(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed < 0)
            return false;

        expectedChangeVersion = parsed;
        return true;
    }
}
