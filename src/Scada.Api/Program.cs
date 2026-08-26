using System.Text;
using Scada.Api.Historian;
using Scada.Api.HostedServices;
using Scada.Api.Persistence;
using Scada.Api.ProjectPackages;
using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Simulation;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Historian.Abstractions;
using Scada.Security.Audit;
using Scada.Security.Authorization;

var builder = WebApplication.CreateBuilder(args);
var authenticationEnabled = builder.AddEliteScadaJwtAuthentication();

builder.Services.AddSingleton<IScadaEventBus, InMemoryScadaEventBus>();
builder.Services.AddSingleton<TagRealtimeHub>();
builder.AddConfiguredHistorian();

builder.Services.AddSingleton<EngineeringWorkspace>();
builder.Services.AddSingleton<ITagRegistry>(sp => sp.GetRequiredService<EngineeringWorkspace>().Tags);
builder.Services.AddSingleton<IAlarmEngine>(sp => sp.GetRequiredService<EngineeringWorkspace>().Alarms);
builder.Services.AddSingleton<IDataSourceEngineeringRegistry>(sp => sp.GetRequiredService<EngineeringWorkspace>().DataSources);
builder.Services.AddSingleton<IEngineeringAssetRegistry>(sp => sp.GetRequiredService<EngineeringWorkspace>().Assets);
builder.Services.AddSingleton<IEngineeringViewRegistry>(sp => sp.GetRequiredService<EngineeringWorkspace>().Views);
builder.Services.AddSingleton<ISecurityPolicyEngineeringRegistry>(sp => sp.GetRequiredService<EngineeringWorkspace>().SecurityPolicies);
builder.Services.AddSingleton<ICommandEngineeringRegistry>(sp => sp.GetRequiredService<EngineeringWorkspace>().Commands);
builder.Services.AddSingleton<DemoRuntimeServices>();

builder.Services.AddSingleton<IEngineeringDriverCompiler, EngineeringDriverCompiler>();
builder.Services.AddSingleton<IEngineeringRuntimeCoordinator>(sp =>
    new EngineeringRuntimeCoordinator(
        sp.GetRequiredService<IScadaEventBus>(),
        sp.GetRequiredService<IEngineeringDriverCompiler>(),
        TimeSpan.FromSeconds(Math.Max(
            1,
            builder.Configuration.GetValue<double?>("EngineeringRuntime:ActivationTimeoutSeconds") ?? 10))));

builder.Services.AddSingleton<IEngineeringExchangeService, EngineeringExchangeService>();
builder.Services.AddSingleton<IProjectPackageService, ProjectPackageService>();
builder.Services.AddSingleton<ApiAuthorizationService>();
builder.AddOptionalEngineeringPersistence();
builder.AddConfiguredAudit();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton(sp =>
{
    var demoRuntime = sp.GetRequiredService<DemoRuntimeServices>();
    return new SimulationDriver(
        demoRuntime.Cache,
        demoRuntime.Registry,
        DemoProcessModel.CreateSimulationPoints(),
        TimeSpan.FromMilliseconds(500));
});
builder.Services.AddSingleton<ScadaRuntimeFacade>();
builder.Services.AddHostedService<SimulationDriverHostedService>();

var app = builder.Build();

// Resolve the historian before the hosted driver starts so it subscribes to the event bus.
_ = app.Services.GetRequiredService<IHistorian>();
await app.InitializeEngineeringPersistenceAsync();
await app.InitializeAuditAsync();

app.UseCors();
if (authenticationEnabled) app.UseAuthentication();
app.UseWebSockets();
app.MapOpenApi();
app.MapProjectPackageEndpoints();
app.MapEngineeringPersistenceEndpoints();
app.MapAuditEndpoints();
app.MapAlarmShelvingEndpoints();
app.MapCommandEndpoints();

// Public health intentionally exposes no plant, driver, project or historian detail.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "scada-api"
}));

app.MapGet("/api/diagnostics/runtime", (ScadaRuntimeFacade runtime, IHistorian historian) =>
{
    var descriptor = runtime.Describe();
    return Results.Ok(new
    {
        driver = descriptor.Drivers.FirstOrDefault(),
        runtime = descriptor,
        historian = new
        {
            provider = HistorianConfiguration.DescribeProvider(historian),
            historian.WrittenSamples,
            historian.PendingSamples
        },
        activeAlarms = descriptor.ActiveAlarmCount
    });
}).RequireRuntimeEngineeringRead();

app.MapGet("/api/auth/me", (HttpContext context, ApiAuthorizationService security) =>
{
    var principal = security.GetPrincipal(context);
    if (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId))
        return Results.Unauthorized();

    return Results.Ok(new
    {
        principal.SubjectId,
        principal.DisplayName,
        principal.Roles
    });
});

app.MapGet("/api/tags", async (
    HttpContext context,
    ScadaRuntimeFacade runtime,
    ApiAuthorizationService security,
    CancellationToken ct) =>
{
    var access = await security.GetReadableRuntimeTagsAsync(context, runtime, ct);
    var failure = access.FailureResult();
    if (failure is not null) return failure;

    var current = runtime.CurrentValues().ToDictionary(x => x.TagId);
    var tags = access.Tags.Select(tag => new
    {
        tag.Id,
        tag.Name,
        tag.Path,
        dataType = tag.DataType.ToString(),
        tag.EngineeringUnit,
        tag.Description,
        tag.ReadOnly,
        current = current.TryGetValue(tag.Id, out var value) ? value : null
    });
    return Results.Ok(tags);
});

app.MapGet("/api/tags/current", async (
    HttpContext context,
    ScadaRuntimeFacade runtime,
    ApiAuthorizationService security,
    CancellationToken ct) =>
{
    var access = await security.GetReadableRuntimeTagsAsync(context, runtime, ct);
    var failure = access.FailureResult();
    if (failure is not null) return failure;

    var readableIds = access.Tags.Select(x => x.Id).ToHashSet();
    return Results.Ok(runtime.CurrentValues().Where(value => readableIds.Contains(value.TagId)));
});

app.MapGet("/api/tags/by-path/{*path}", async (
    string path,
    HttpContext context,
    ScadaRuntimeFacade runtime,
    ApiAuthorizationService security,
    CancellationToken ct) =>
{
    if (!runtime.TryGetTagByPath(path, out var tag) || tag is null) return Results.NotFound();

    var authorization = await security.CheckRuntimeTagReadAsync(context, runtime, tag, ct);
    var failure = authorization?.FailureResult();
    if (failure is not null) return failure;

    runtime.TryGetCurrent(tag.Id, out var current);
    return Results.Ok(new { tag, current });
});

app.MapPost("/api/tags/{id:guid}/write", async (
    Guid id,
    TagWriteRequest request,
    HttpContext context,
    ScadaRuntimeFacade runtime,
    ApiAuthorizationService security,
    ApiAuditService audit,
    CancellationToken ct) =>
{
    if (!runtime.TryGetTag(id, out var tag) || tag is null) return Results.NotFound();
    if (tag.ReadOnly) return Results.BadRequest(new { error = "Tag is read-only." });

    var authorization = await security.CheckRuntimeTagAsync(
        context,
        runtime,
        tag,
        TagAccessOperation.Write,
        ct);
    var failure = authorization.FailureResult();
    if (failure is not null)
    {
        await audit.RecordAuthorizationDeniedAsync(
            context,
            authorization,
            AuditActions.TagWrite,
            "tag",
            tag.Path,
            new Dictionary<string, string> { ["tagId"] = tag.Id.ToString() });
        return failure;
    }

    try
    {
        await runtime.WriteAsync(id, request.Value, ct);
        await audit.RecordAsync(
            context,
            authorization.Principal,
            AuditActions.TagWrite,
            AuditOutcome.Succeeded,
            "tag",
            tag.Path,
            new Dictionary<string, string> { ["tagId"] = tag.Id.ToString() });
        return Results.Accepted();
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
    {
        await audit.RecordAsync(
            context,
            authorization.Principal,
            AuditActions.TagWrite,
            AuditOutcome.Failed,
            "tag",
            tag.Path,
            new Dictionary<string, string>
            {
                ["tagId"] = tag.Id.ToString(),
                ["errorType"] = ex.GetType().Name
            });
        throw;
    }
});

app.MapGet("/api/history/{tagId:guid}", async (
    Guid tagId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? limit,
    HttpContext context,
    ScadaRuntimeFacade runtime,
    ApiAuthorizationService security,
    IHistorian historian,
    CancellationToken ct) =>
{
    if (!runtime.TryGetTag(tagId, out var tag) || tag is null) return Results.NotFound();

    var authorization = await security.CheckRuntimeTagReadAsync(context, runtime, tag, ct);
    var failure = authorization?.FailureResult();
    if (failure is not null) return failure;

    var end = to ?? DateTimeOffset.UtcNow;
    var start = from ?? end.AddMinutes(-15);
    return Results.Ok(historian.Query(tagId, start, end, limit ?? 5000));
});

app.MapGet("/api/alarms", async (
    bool? activeOnly,
    HttpContext context,
    ScadaRuntimeFacade runtime,
    ApiAuthorizationService security,
    CancellationToken ct) =>
{
    var tagAccess = await security.GetReadableRuntimeTagsAsync(context, runtime, ct);
    var failure = tagAccess.FailureResult();
    if (failure is not null) return failure;

    var readableTagIds = tagAccess.Tags.Select(tag => tag.Id).ToHashSet();
    var visible = new List<AlarmInstance>();
    foreach (var alarm in runtime.Alarms(activeOnly ?? false))
    {
        if (!readableTagIds.Contains(alarm.TagId)) continue;
        if (await security.CanViewRuntimeResourceAsync(
                tagAccess.Principal,
                runtime,
                new AuthorizationResource(Area: alarm.Area),
                ct))
            visible.Add(alarm);
    }

    return Results.Ok(visible);
});

app.MapGet("/api/alarms/definitions", async (
    HttpContext context,
    ScadaRuntimeFacade runtime,
    ApiAuthorizationService security,
    CancellationToken ct) =>
{
    var tagAccess = await security.GetReadableRuntimeTagsAsync(context, runtime, ct);
    var failure = tagAccess.FailureResult();
    if (failure is not null) return failure;

    var readableTagIds = tagAccess.Tags.Select(tag => tag.Id).ToHashSet();
    var visible = new List<AlarmDefinition>();
    foreach (var alarm in runtime.AlarmDefinitions())
    {
        if (!readableTagIds.Contains(alarm.TagId)) continue;
        if (await security.CanViewRuntimeResourceAsync(
                tagAccess.Principal,
                runtime,
                new AuthorizationResource(Area: alarm.Area),
                ct))
            visible.Add(alarm);
    }

    return Results.Ok(visible);
});

app.MapPost("/api/alarms/{id:guid}/ack", async (
    Guid id,
    AlarmAckRequest request,
    HttpContext context,
    ScadaRuntimeFacade runtime,
    ApiAuthorizationService security,
    ApiAuditService audit,
    CancellationToken ct) =>
{
    var definition = runtime.AlarmDefinitions().FirstOrDefault(alarm => alarm.Id == id);
    if (definition is null) return Results.NotFound();

    var authorization = await security.CheckRuntimeAsync(
        context,
        runtime,
        SecurityCapability.AlarmAcknowledge,
        new AuthorizationResource(Area: definition.Area),
        ct);
    var failure = authorization.FailureResult();
    if (failure is not null)
    {
        await audit.RecordAuthorizationDeniedAsync(
            context,
            authorization,
            AuditActions.AlarmAcknowledge,
            "alarm",
            id.ToString(),
            new Dictionary<string, string> { ["alarmName"] = definition.Name });
        return failure;
    }

    var acknowledgedBy = authorization.Principal.DisplayName ?? authorization.Principal.SubjectId;
    _ = request; // Legacy body field is intentionally ignored; identity comes from the authenticated token.

    try
    {
        var acknowledged = await runtime.AcknowledgeAlarmAsync(id, acknowledgedBy, ct);
        await audit.RecordAsync(
            context,
            authorization.Principal,
            AuditActions.AlarmAcknowledge,
            acknowledged ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            "alarm",
            id.ToString(),
            new Dictionary<string, string> { ["alarmName"] = definition.Name });
        return acknowledged ? Results.Ok() : Results.NotFound();
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
    {
        await audit.RecordAsync(
            context,
            authorization.Principal,
            AuditActions.AlarmAcknowledge,
            AuditOutcome.Failed,
            "alarm",
            id.ToString(),
            new Dictionary<string, string>
            {
                ["alarmName"] = definition.Name,
                ["errorType"] = ex.GetType().Name
            });
        throw;
    }
});

app.MapGet("/api/drivers", (ScadaRuntimeFacade runtime) => Results.Ok(runtime.Drivers()))
    .RequireRuntimeEngineeringRead();

app.MapGet("/api/engineering/workspace", (EngineeringWorkspace workspace) => Results.Ok(workspace.Describe()))
    .RequireWorkspaceEngineeringRead();
app.MapGet("/api/engineering/data-sources", (IDataSourceEngineeringRegistry registry) => Results.Ok(registry.Snapshot()))
    .RequireWorkspaceEngineeringRead();
app.MapGet("/api/engineering/templates", (IEngineeringAssetRegistry registry) => Results.Ok(registry.SnapshotTemplates()))
    .RequireWorkspaceEngineeringRead();
app.MapGet("/api/engineering/equipment", (IEngineeringAssetRegistry registry) => Results.Ok(registry.SnapshotEquipment()))
    .RequireWorkspaceEngineeringRead();
app.MapGet("/api/engineering/dynamos", (IEngineeringAssetRegistry registry) => Results.Ok(registry.SnapshotDynamos()))
    .RequireWorkspaceEngineeringRead();
app.MapGet("/api/engineering/screens", (IEngineeringViewRegistry registry) => Results.Ok(registry.SnapshotScreens()))
    .RequireWorkspaceEngineeringRead();
app.MapGet("/api/engineering/popups", (IEngineeringViewRegistry registry) => Results.Ok(registry.SnapshotPopups()))
    .RequireWorkspaceEngineeringRead();
app.MapGet("/api/engineering/security-roles", (ISecurityPolicyEngineeringRegistry registry) => Results.Ok(registry.SnapshotRoles()))
    .RequireWorkspaceEngineeringRead();
app.MapGet("/api/engineering/commands", (ICommandEngineeringRegistry registry) => Results.Ok(registry.Snapshot()))
    .RequireWorkspaceEngineeringRead();

app.MapGet("/api/engineering/export/json", (IEngineeringExchangeService exchange) =>
    Results.File(Encoding.UTF8.GetBytes(exchange.ExportJson()), "application/json", "scada-engineering.json"))
    .RequireWorkspaceEngineeringRead();

app.MapGet("/api/engineering/export/tags.csv", (IEngineeringExchangeService exchange) =>
    Results.File(Encoding.UTF8.GetBytes(exchange.ExportTagsCsv()), "text/csv; charset=utf-8", "scada-tags.csv"))
    .RequireWorkspaceEngineeringRead();

app.MapGet("/api/engineering/export/alarms.csv", (IEngineeringExchangeService exchange) =>
    Results.File(Encoding.UTF8.GetBytes(exchange.ExportAlarmsCsv()), "text/csv; charset=utf-8", "scada-alarms.csv"))
    .RequireWorkspaceEngineeringRead();

app.MapGet("/api/engineering/export/datasources.csv", (IEngineeringExchangeService exchange) =>
    Results.File(Encoding.UTF8.GetBytes(exchange.ExportDataSourcesCsv()), "text/csv; charset=utf-8", "scada-datasources.csv"))
    .RequireWorkspaceEngineeringRead();

app.MapPost("/api/engineering/import/json/preview", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseJson(await reader.ReadToEndAsync());
    return Results.Ok(exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate));
}).RequireWorkspaceEngineeringRead();

app.MapPost("/api/engineering/import/json/apply", async (
    HttpRequest request,
    HttpContext context,
    ImportMode? mode,
    IEngineeringExchangeService exchange,
    ApiAuthorizationService security,
    ApiAuditService audit) =>
{
    return await ApplyEngineeringImportAsync(
        request,
        context,
        mode,
        exchange,
        security,
        audit,
        "json",
        async () =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            return exchange.ParseJson(await reader.ReadToEndAsync());
        });
});

app.MapPost("/api/engineering/import/tags.csv/preview", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseTagsCsv(await reader.ReadToEndAsync());
    return Results.Ok(exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate));
}).RequireWorkspaceEngineeringRead();

app.MapPost("/api/engineering/import/tags.csv/apply", async (
    HttpRequest request,
    HttpContext context,
    ImportMode? mode,
    IEngineeringExchangeService exchange,
    ApiAuthorizationService security,
    ApiAuditService audit) =>
{
    return await ApplyEngineeringImportAsync(
        request,
        context,
        mode,
        exchange,
        security,
        audit,
        "tags.csv",
        async () =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            return exchange.ParseTagsCsv(await reader.ReadToEndAsync());
        });
});

app.MapPost("/api/engineering/import/alarms.csv/preview", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseAlarmsCsv(await reader.ReadToEndAsync());
    return Results.Ok(exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate));
}).RequireWorkspaceEngineeringRead();

app.MapPost("/api/engineering/import/alarms.csv/apply", async (
    HttpRequest request,
    HttpContext context,
    ImportMode? mode,
    IEngineeringExchangeService exchange,
    ApiAuthorizationService security,
    ApiAuditService audit) =>
{
    return await ApplyEngineeringImportAsync(
        request,
        context,
        mode,
        exchange,
        security,
        audit,
        "alarms.csv",
        async () =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            return exchange.ParseAlarmsCsv(await reader.ReadToEndAsync());
        });
});

app.MapPost("/api/engineering/import/datasources.csv/preview", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseDataSourcesCsv(await reader.ReadToEndAsync());
    return Results.Ok(exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate));
}).RequireWorkspaceEngineeringRead();

app.MapPost("/api/engineering/import/datasources.csv/apply", async (
    HttpRequest request,
    HttpContext context,
    ImportMode? mode,
    IEngineeringExchangeService exchange,
    ApiAuthorizationService security,
    ApiAuditService audit) =>
{
    return await ApplyEngineeringImportAsync(
        request,
        context,
        mode,
        exchange,
        security,
        audit,
        "datasources.csv",
        async () =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            return exchange.ParseDataSourcesCsv(await reader.ReadToEndAsync());
        });
});

app.Map("/ws/tags", async (
    HttpContext context,
    TagRealtimeHub hub,
    ApiAuthorizationService security) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var principal = security.GetPrincipal(context);
    if (security.AuthenticationEnabled &&
        (!principal.IsAuthenticated || string.IsNullOrWhiteSpace(principal.SubjectId)))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    await hub.HandleAsync(
        socket,
        principal,
        security.AuthenticationEnabled,
        context.RequestAborted);
});

app.Run();

static async Task<IResult> ApplyEngineeringImportAsync(
    HttpRequest request,
    HttpContext context,
    ImportMode? mode,
    IEngineeringExchangeService exchange,
    ApiAuthorizationService security,
    ApiAuditService audit,
    string format,
    Func<Task<EngineeringPackage>> parseAsync)
{
    _ = request;
    var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
    var failure = authorization.FailureResult();
    if (failure is not null)
    {
        await audit.RecordAuthorizationDeniedAsync(
            context,
            authorization,
            AuditActions.EngineeringImportApply,
            "engineering-workspace",
            "current",
            new Dictionary<string, string> { ["format"] = format });
        return failure;
    }

    try
    {
        var importMode = mode ?? ImportMode.CreateAndUpdate;
        var package = await parseAsync();
        var preview = exchange.Preview(package, importMode);
        if (!preview.CanApply)
        {
            await audit.RecordAsync(
                context,
                authorization.Principal,
                AuditActions.EngineeringImportApply,
                AuditOutcome.Failed,
                "engineering-workspace",
                "current",
                new Dictionary<string, string>
                {
                    ["format"] = format,
                    ["reason"] = "preview-errors",
                    ["errorCount"] = preview.ErrorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            return Results.BadRequest(preview);
        }

        var result = exchange.Apply(package, importMode);
        var hasErrors = result.Issues.Any(x => x.IsError);
        await audit.RecordAsync(
            context,
            authorization.Principal,
            AuditActions.EngineeringImportApply,
            hasErrors ? AuditOutcome.Failed : AuditOutcome.Succeeded,
            "engineering-workspace",
            "current",
            new Dictionary<string, string>
            {
                ["format"] = format,
                ["mode"] = importMode.ToString(),
                ["created"] = result.Created.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["updated"] = result.Updated.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["skipped"] = result.Skipped.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
        return hasErrors ? Results.BadRequest(result) : Results.Ok(result);
    }
    catch (Exception ex)
    {
        await audit.RecordAsync(
            context,
            authorization.Principal,
            AuditActions.EngineeringImportApply,
            AuditOutcome.Failed,
            "engineering-workspace",
            "current",
            new Dictionary<string, string>
            {
                ["format"] = format,
                ["errorType"] = ex.GetType().Name
            });
        throw;
    }
}

public sealed record TagWriteRequest(object? Value);
public sealed record AlarmAckRequest(string? User = null);
