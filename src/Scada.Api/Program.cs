using System.Text;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Views;
using Scada.Api.Historian;
using Scada.Api.HostedServices;
using Scada.Api.Persistence;
using Scada.Api.ProjectPackages;
using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Simulation;
using Scada.Historian.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IScadaEventBus, InMemoryScadaEventBus>();
builder.Services.AddSingleton<TagRealtimeHub>();
builder.AddConfiguredHistorian();

builder.Services.AddSingleton<EngineeringWorkspace>();
builder.Services.AddSingleton<ITagRegistry>(sp => sp.GetRequiredService<EngineeringWorkspace>().Tags);
builder.Services.AddSingleton<IAlarmEngine>(sp => sp.GetRequiredService<EngineeringWorkspace>().Alarms);
builder.Services.AddSingleton<DemoRuntimeServices>();

builder.Services.AddSingleton<IEngineeringDriverCompiler, EngineeringDriverCompiler>();
builder.Services.AddSingleton<IEngineeringRuntimeCoordinator>(sp =>
    new EngineeringRuntimeCoordinator(
        sp.GetRequiredService<IScadaEventBus>(),
        sp.GetRequiredService<IEngineeringDriverCompiler>(),
        TimeSpan.FromSeconds(Math.Max(
            1,
            builder.Configuration.GetValue<double?>("EngineeringRuntime:ActivationTimeoutSeconds") ?? 10))));
builder.Services.AddSingleton<IDataSourceEngineeringRegistry>(_ =>
{
    var registry = new InMemoryDataSourceEngineeringRegistry();
    registry.Upsert(new DataSourceEngineeringDto(
        Id: null,
        Key: "builtin.simulation",
        Name: "Built-in Simulation",
        Driver: "builtin.simulation",
        Enabled: true,
        Settings: new Dictionary<string, string>
        {
            ["scanIntervalMilliseconds"] = "500"
        },
        Metadata: new Dictionary<string, string>
        {
            ["system"] = "true"
        }));
    return registry;
});
builder.Services.AddSingleton<IEngineeringAssetRegistry>(_ =>
{
    var registry = new InMemoryEngineeringAssetRegistry();

    var templateBindings = new[]
    {
        new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "{equipmentPath}.Running", "read"),
        new EngineeringBindingDto("fault", EngineeringBindingKind.Tag, "{equipmentPath}.Fault", "read"),
        new EngineeringBindingDto("current", EngineeringBindingKind.Tag, "{equipmentPath}.Current", "read"),
        new EngineeringBindingDto("frequency", EngineeringBindingKind.Tag, "{equipmentPath}.Frequency", "readWrite")
    };

    registry.UpsertTemplate(new EquipmentTemplateEngineeringDto(
        Id: null,
        Key: "pump.standard",
        Name: "Standard Pump",
        Bindings: templateBindings,
        Properties: new Dictionary<string, string>
        {
            ["category"] = "pump",
            ["defaultFrequencyHz"] = "60"
        },
        Context: new Dictionary<string, string>
        {
            ["domain"] = "pumping"
        }));

    registry.UpsertEquipment(new EquipmentEngineeringDto(
        Id: null,
        Path: "Demo.P01",
        Name: "Pump P01",
        TemplateKey: "pump.standard",
        Bindings: new[]
        {
            new EngineeringBindingDto("running", EngineeringBindingKind.Tag, "Demo.P01.Running", "read"),
            new EngineeringBindingDto("fault", EngineeringBindingKind.Tag, "Demo.P01.Fault", "read"),
            new EngineeringBindingDto("current", EngineeringBindingKind.Tag, "Demo.P01.Current", "read"),
            new EngineeringBindingDto("frequency", EngineeringBindingKind.Tag, "Demo.P01.Frequency", "readWrite")
        },
        Properties: new Dictionary<string, string>
        {
            ["displayLabel"] = "P01"
        },
        Context: new Dictionary<string, string>
        {
            ["area"] = "Demo",
            ["process"] = "Discharge"
        }));

    registry.UpsertDynamo(new DynamoEngineeringDto(
        Id: null,
        Key: "dynamo.pump.standard",
        Name: "Standard Pump Dynamo",
        TemplateKey: "pump.standard",
        Bindings: templateBindings,
        Properties: new Dictionary<string, string>
        {
            ["symbol"] = "pump"
        },
        Context: new Dictionary<string, string>
        {
            ["usage"] = "process-screen"
        }));

    return registry;
});
builder.Services.AddSingleton<IEngineeringViewRegistry>(_ =>
{
    var registry = new InMemoryEngineeringViewRegistry();

    registry.UpsertScreen(new ScreenEngineeringDto(
        Id: null,
        Key: "demo.overview",
        Name: "Demo Overview",
        Route: "/demo",
        Elements: new[]
        {
            new VisualElementEngineeringDto(
                Key: "tank01",
                Type: "tank",
                Bindings: new[]
                {
                    new EngineeringBindingDto("level", EngineeringBindingKind.Tag, "Demo.Tank01.Level", "read")
                },
                Properties: new Dictionary<string, string>
                {
                    ["label"] = "Reservatório TK01",
                    ["x"] = "100",
                    ["y"] = "100"
                }),
            new VisualElementEngineeringDto(
                Key: "pump01",
                Type: "dynamo",
                DynamoKey: "dynamo.pump.standard",
                EquipmentPath: "Demo.P01",
                Properties: new Dictionary<string, string>
                {
                    ["x"] = "430",
                    ["y"] = "160"
                }),
            new VisualElementEngineeringDto(
                Key: "pressure",
                Type: "value",
                Bindings: new[]
                {
                    new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Demo.Discharge.Pressure", "read")
                },
                Properties: new Dictionary<string, string>
                {
                    ["label"] = "Pressão"
                }),
            new VisualElementEngineeringDto(
                Key: "flow",
                Type: "value",
                Bindings: new[]
                {
                    new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "Demo.Discharge.Flow", "read")
                },
                Properties: new Dictionary<string, string>
                {
                    ["label"] = "Vazão"
                })
        },
        Properties: new Dictionary<string, string>
        {
            ["canvasWidth"] = "1366",
            ["canvasHeight"] = "768"
        },
        Context: new Dictionary<string, string>
        {
            ["area"] = "Demo",
            ["process"] = "Pumping"
        }));

    registry.UpsertPopup(new PopupEngineeringDto(
        Id: null,
        Key: "popup.pump.standard",
        Name: "Standard Pump Popup",
        TemplateKey: "pump.standard",
        Elements: new[]
        {
            new VisualElementEngineeringDto(
                Key: "current",
                Type: "value",
                Bindings: new[]
                {
                    new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "{equipmentPath}.Current", "read")
                },
                Properties: new Dictionary<string, string> { ["label"] = "Corrente" }),
            new VisualElementEngineeringDto(
                Key: "frequency",
                Type: "value",
                Bindings: new[]
                {
                    new EngineeringBindingDto("value", EngineeringBindingKind.Tag, "{equipmentPath}.Frequency", "readWrite")
                },
                Properties: new Dictionary<string, string> { ["label"] = "Frequência" }),
            new VisualElementEngineeringDto(
                Key: "fault",
                Type: "status",
                Bindings: new[]
                {
                    new EngineeringBindingDto("active", EngineeringBindingKind.Tag, "{equipmentPath}.Fault", "read")
                },
                Properties: new Dictionary<string, string> { ["label"] = "Falha" })
        },
        Properties: new Dictionary<string, string>
        {
            ["width"] = "640",
            ["height"] = "420"
        },
        Context: new Dictionary<string, string>
        {
            ["role"] = "equipment-details"
        }));

    return registry;
});
builder.Services.AddSingleton<IEngineeringExchangeService, EngineeringExchangeService>();
builder.Services.AddSingleton<IProjectPackageService, ProjectPackageService>();
builder.AddOptionalEngineeringPersistence();
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

app.UseCors();
app.UseWebSockets();
app.MapOpenApi();
app.MapProjectPackageEndpoints();
app.MapEngineeringPersistenceEndpoints();

app.MapGet("/health", (ScadaRuntimeFacade runtime, IHistorian historian) =>
{
    var descriptor = runtime.Describe();
    return Results.Ok(new
    {
        status = "ok",
        service = "scada-api",
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
});

app.MapGet("/api/tags", (ScadaRuntimeFacade runtime) =>
{
    var current = runtime.CurrentValues().ToDictionary(x => x.TagId);
    var tags = runtime.Tags().Select(tag => new
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

app.MapGet("/api/tags/current", (ScadaRuntimeFacade runtime) => Results.Ok(runtime.CurrentValues()));

app.MapGet("/api/tags/by-path/{*path}", (string path, ScadaRuntimeFacade runtime) =>
{
    if (!runtime.TryGetTagByPath(path, out var tag) || tag is null) return Results.NotFound();
    runtime.TryGetCurrent(tag.Id, out var current);
    return Results.Ok(new { tag, current });
});

app.MapPost("/api/tags/{id:guid}/write", async (Guid id, TagWriteRequest request, ScadaRuntimeFacade runtime, CancellationToken ct) =>
{
    if (!runtime.TryGetTag(id, out var tag) || tag is null) return Results.NotFound();
    if (tag.ReadOnly) return Results.BadRequest(new { error = "Tag is read-only." });
    await runtime.WriteAsync(id, request.Value, ct);
    return Results.Accepted();
});

app.MapGet("/api/history/{tagId:guid}", (Guid tagId, DateTimeOffset? from, DateTimeOffset? to, int? limit, IHistorian historian) =>
{
    var end = to ?? DateTimeOffset.UtcNow;
    var start = from ?? end.AddMinutes(-15);
    return Results.Ok(historian.Query(tagId, start, end, limit ?? 5000));
});

app.MapGet("/api/alarms", (bool? activeOnly, ScadaRuntimeFacade runtime) =>
    Results.Ok(runtime.Alarms(activeOnly ?? false)));

app.MapGet("/api/alarms/definitions", (ScadaRuntimeFacade runtime) => Results.Ok(runtime.AlarmDefinitions()));

app.MapPost("/api/alarms/{id:guid}/ack", async (Guid id, AlarmAckRequest request, ScadaRuntimeFacade runtime, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.User)) return Results.BadRequest(new { error = "User is required." });
    return await runtime.AcknowledgeAlarmAsync(id, request.User, ct) ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/drivers", (ScadaRuntimeFacade runtime) => Results.Ok(runtime.Drivers()));

app.MapGet("/api/engineering/data-sources", (IDataSourceEngineeringRegistry registry) => Results.Ok(registry.Snapshot()));
app.MapGet("/api/engineering/templates", (IEngineeringAssetRegistry registry) => Results.Ok(registry.SnapshotTemplates()));
app.MapGet("/api/engineering/equipment", (IEngineeringAssetRegistry registry) => Results.Ok(registry.SnapshotEquipment()));
app.MapGet("/api/engineering/dynamos", (IEngineeringAssetRegistry registry) => Results.Ok(registry.SnapshotDynamos()));
app.MapGet("/api/engineering/screens", (IEngineeringViewRegistry registry) => Results.Ok(registry.SnapshotScreens()));
app.MapGet("/api/engineering/popups", (IEngineeringViewRegistry registry) => Results.Ok(registry.SnapshotPopups()));

app.MapGet("/api/engineering/export/json", (IEngineeringExchangeService exchange) =>
    Results.File(Encoding.UTF8.GetBytes(exchange.ExportJson()), "application/json", "scada-engineering.json"));

app.MapGet("/api/engineering/export/tags.csv", (IEngineeringExchangeService exchange) =>
    Results.File(Encoding.UTF8.GetBytes(exchange.ExportTagsCsv()), "text/csv; charset=utf-8", "scada-tags.csv"));

app.MapGet("/api/engineering/export/alarms.csv", (IEngineeringExchangeService exchange) =>
    Results.File(Encoding.UTF8.GetBytes(exchange.ExportAlarmsCsv()), "text/csv; charset=utf-8", "scada-alarms.csv"));

app.MapGet("/api/engineering/export/datasources.csv", (IEngineeringExchangeService exchange) =>
    Results.File(Encoding.UTF8.GetBytes(exchange.ExportDataSourcesCsv()), "text/csv; charset=utf-8", "scada-datasources.csv"));

app.MapPost("/api/engineering/import/json/preview", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseJson(await reader.ReadToEndAsync());
    return Results.Ok(exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate));
});

app.MapPost("/api/engineering/import/json/apply", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseJson(await reader.ReadToEndAsync());
    var preview = exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate);
    if (!preview.CanApply) return Results.BadRequest(preview);
    return Results.Ok(exchange.Apply(package, mode ?? ImportMode.CreateAndUpdate));
});

app.MapPost("/api/engineering/import/tags.csv/preview", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseTagsCsv(await reader.ReadToEndAsync());
    return Results.Ok(exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate));
});

app.MapPost("/api/engineering/import/tags.csv/apply", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseTagsCsv(await reader.ReadToEndAsync());
    var preview = exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate);
    if (!preview.CanApply) return Results.BadRequest(preview);
    return Results.Ok(exchange.Apply(package, mode ?? ImportMode.CreateAndUpdate));
});

app.MapPost("/api/engineering/import/alarms.csv/preview", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseAlarmsCsv(await reader.ReadToEndAsync());
    return Results.Ok(exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate));
});

app.MapPost("/api/engineering/import/alarms.csv/apply", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseAlarmsCsv(await reader.ReadToEndAsync());
    var preview = exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate);
    if (!preview.CanApply) return Results.BadRequest(preview);
    return Results.Ok(exchange.Apply(package, mode ?? ImportMode.CreateAndUpdate));
});

app.MapPost("/api/engineering/import/datasources.csv/preview", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseDataSourcesCsv(await reader.ReadToEndAsync());
    return Results.Ok(exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate));
});

app.MapPost("/api/engineering/import/datasources.csv/apply", async (HttpRequest request, ImportMode? mode, IEngineeringExchangeService exchange) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var package = exchange.ParseDataSourcesCsv(await reader.ReadToEndAsync());
    var preview = exchange.Preview(package, mode ?? ImportMode.CreateAndUpdate);
    if (!preview.CanApply) return Results.BadRequest(preview);
    return Results.Ok(exchange.Apply(package, mode ?? ImportMode.CreateAndUpdate));
});

app.Map("/ws/tags", async (HttpContext context, TagRealtimeHub hub) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    var socket = await context.WebSockets.AcceptWebSocketAsync();
    await hub.HandleAsync(socket, context.RequestAborted);
});

app.Run();

public sealed record TagWriteRequest(object? Value);
public sealed record AlarmAckRequest(string User);
