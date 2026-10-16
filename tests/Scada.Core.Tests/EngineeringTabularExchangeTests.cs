using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Scada.Core.Events;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringTabularExchangeTests
{
    private readonly EngineeringTabularExchange _exchange = new();

    [Theory]
    [InlineData(EngineeringTabularEntity.Tags)]
    [InlineData(EngineeringTabularEntity.Alarms)]
    [InlineData(EngineeringTabularEntity.DataSources)]
    [InlineData(EngineeringTabularEntity.GatewayRoutes)]
    [InlineData(EngineeringTabularEntity.OperationalEvents)]
    public void Csv_and_xlsx_round_trip_only_the_selected_flat_collection(EngineeringTabularEntity entity)
    {
        var current = Package();

        var csv = _exchange.ExportCsv(current, entity);
        var csvImport = _exchange.ParseCsv(current, entity, csv);
        var xlsx = _exchange.ExportXlsx(current, entity);
        var xlsxImport = _exchange.ParseXlsx(current, entity, xlsx);

        AssertEntity(entity, csvImport);
        AssertEntity(entity, xlsxImport);
        Assert.Equal(entity == EngineeringTabularEntity.Tags ? 1 : 0, csvImport.Tags.Count);
        Assert.Equal(entity == EngineeringTabularEntity.Alarms ? 1 : 0, csvImport.Alarms.Count);
        Assert.Equal(entity == EngineeringTabularEntity.DataSources ? 1 : 0, csvImport.DataSources?.Count ?? 0);
        Assert.Equal(entity == EngineeringTabularEntity.GatewayRoutes ? 1 : 0, csvImport.Gateways?.Count ?? 0);
        Assert.Equal(entity == EngineeringTabularEntity.OperationalEvents ? 1 : 0, csvImport.OperationalEvents?.Count ?? 0);
        Assert.Equal(current.AuthorityPolicyReference, csvImport.AuthorityPolicyReference);
        Assert.Equal(current.EngineeringLock, csvImport.EngineeringLock);
        Assert.Equal(current.RuntimePresentation, csvImport.RuntimePresentation);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public void TabularTagFormatsPreserveVersion2UnitsAndTheirSchemaCapability(string format)
    {
        var unit = new ControlledEngineeringUnitReference(2, "unit.power.kilowatt");
        var package = Package();
        var current = package with
        {
            SchemaVersion = 27,
            Tags = [package.Tags.Single() with { ControlledEngineeringUnit = unit }]
        };
        var imported = format == "csv"
            ? _exchange.ParseCsv(current, EngineeringTabularEntity.Tags,
                _exchange.ExportCsv(current, EngineeringTabularEntity.Tags))
            : _exchange.ParseXlsx(current, EngineeringTabularEntity.Tags,
                _exchange.ExportXlsx(current, EngineeringTabularEntity.Tags));

        Assert.Equal(27, imported.SchemaVersion);
        Assert.Equal(unit, Assert.Single(imported.Tags).ControlledEngineeringUnit);
    }

    [Fact]
    public void Csv_round_trip_preserves_multiline_and_formula_leading_text_without_evaluating_it()
    {
        var rows = new IReadOnlyList<string?>[]
        {
            new string?[] { "key", "note", "numeric" },
            new string?[] { "row-1", "line 1, \"quoted\"\nline 2", "-12.5" },
            new string?[] { "row-2", "  =HYPERLINK(\"https://example.invalid\")", "+value" }
        };

        var csv = CsvCodec.Write(rows);
        Assert.Contains("'  =HYPERLINK", csv, StringComparison.Ordinal);
        var parsed = CsvCodec.Read(csv);

        Assert.Equal("line 1, \"quoted\"\nline 2", parsed[1][1]);
        Assert.Equal("-12.5", parsed[1][2]);
        Assert.Equal("  =HYPERLINK(\"https://example.invalid\")", parsed[2][1]);
        Assert.Equal("+value", parsed[2][2]);
    }

    [Fact]
    public void Csv_writer_rejects_output_over_16_MiB_before_building_an_unbounded_string()
    {
        var cell = new string('x', 64 * 1024);
        var rows = Enumerable.Range(0, 257)
            .Select(_ => (IReadOnlyList<string?>)new string?[] { cell })
            .ToArray();

        var error = Assert.Throws<InvalidDataException>(() => CsvCodec.Write(rows));

        Assert.Contains("16 MiB", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Xlsx_writer_bounds_both_the_compressed_file_and_expanded_worksheet()
    {
        var expandedCell = new string('x', 64 * 1024);
        var expandedRows = Enumerable.Range(0, 513).Select(_ => new[] { expandedCell }).ToArray();
        var expandedError = Assert.Throws<InvalidDataException>(() => XlsxCodec.Write(expandedRows, "Tags"));
        Assert.Contains("expanded", expandedError.Message, StringComparison.OrdinalIgnoreCase);

        var random = new Random(597);
        var noisyRows = new string[401][];
        noisyRows[0] = ["Value"];
        for (var row = 1; row < noisyRows.Length; row++)
        {
            var chars = new char[64 * 1024];
            for (var index = 0; index < chars.Length; index++)
                chars[index] = (char)random.Next(32, 127);
            noisyRows[row] = [new string(chars)];
        }

        var outputError = Assert.Throws<InvalidDataException>(() => XlsxCodec.Write(noisyRows, "Tags"));
        Assert.Contains("16 MiB", outputError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Xlsx_rejects_duplicate_worksheet_row_indices()
    {
        var package = Package();
        var bytes = _exchange.ExportXlsx(package, EngineeringTabularEntity.Tags);
        var malicious = RewriteXlsx(bytes, "xl/worksheets/sheet1.xml", xml =>
        {
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var rows = xml.Descendants(main + "row").ToArray();
            Assert.Equal(2, rows.Length);
            rows[1].SetAttributeValue("r", "1");
        });

        var error = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseXlsx(package, EngineeringTabularEntity.Tags, malicious));

        Assert.Contains("duplicate row index", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("A1trailing")]
    [InlineData("A0")]
    [InlineData("A3")]
    public void Xlsx_rejects_incomplete_or_incompatible_cell_references(string reference)
    {
        var package = Package();
        var bytes = _exchange.ExportXlsx(package, EngineeringTabularEntity.Tags);
        var malicious = RewriteXlsx(bytes, "xl/worksheets/sheet1.xml", xml =>
        {
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var firstCell = xml.Descendants(main + "row").First().Element(main + "c")!;
            firstCell.SetAttributeValue("r", reference);
        });

        var error = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseXlsx(package, EngineeringTabularEntity.Tags, malicious));

        Assert.Contains("cell reference", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Xlsx_rejects_duplicate_cell_references_in_one_row()
    {
        var package = Package();
        var bytes = _exchange.ExportXlsx(package, EngineeringTabularEntity.Tags);
        var malicious = RewriteXlsx(bytes, "xl/worksheets/sheet1.xml", xml =>
        {
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var cells = xml.Descendants(main + "row").First().Elements(main + "c").ToArray();
            Assert.True(cells.Length > 1);
            cells[1].SetAttributeValue("r", (string)cells[0].Attribute("r")!);
        });

        var error = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseXlsx(package, EngineeringTabularEntity.Tags, malicious));

        Assert.Contains("duplicate cell references", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Id,Id\n", "duplicate")]
    [InlineData("Id,Name\n1,\"unterminated", "unterminated")]
    [InlineData("Id,Name\n1,abc\"def\n", "quote")]
    public void Csv_rejects_malformed_headers_and_cells_with_clear_diagnostics(string csv, string expectedText)
    {
        var error = Assert.Throws<InvalidDataException>(() => CsvCodec.Read(csv));
        Assert.Contains(expectedText, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(EngineeringTabularEntity.Tags)]
    [InlineData(EngineeringTabularEntity.Alarms)]
    [InlineData(EngineeringTabularEntity.DataSources)]
    [InlineData(EngineeringTabularEntity.GatewayRoutes)]
    [InlineData(EngineeringTabularEntity.OperationalEvents)]
    public void Csv_and_xlsx_reject_unsupported_entity_columns(EngineeringTabularEntity entity)
    {
        const string unsupportedHeader = "FutureEngineeringField";
        var package = Package();
        var exportedRows = CsvCodec.Read(_exchange.ExportCsv(package, entity));
        var projectedRows = exportedRows.Select((row, index) =>
            row.Append(index == 0 ? unsupportedHeader : string.Empty).ToArray()).ToArray();

        var csvError = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseCsv(package, entity, CsvCodec.Write(projectedRows
                .Select(row => (IReadOnlyList<string?>)row))));

        Assert.Contains("unsupported column", csvError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(unsupportedHeader, csvError.Message, StringComparison.Ordinal);

        var xlsxError = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseXlsx(package, entity, XlsxCodec.Write(projectedRows, SheetName(entity))));

        Assert.Contains("unsupported column", xlsxError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(unsupportedHeader, xlsxError.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EngineeringTabularEntity.Tags)]
    [InlineData(EngineeringTabularEntity.Alarms)]
    [InlineData(EngineeringTabularEntity.DataSources)]
    [InlineData(EngineeringTabularEntity.GatewayRoutes)]
    [InlineData(EngineeringTabularEntity.OperationalEvents)]
    public void Csv_and_xlsx_accept_known_columns_in_any_order(EngineeringTabularEntity entity)
    {
        var package = Package();
        var reversedRows = CsvCodec.Read(_exchange.ExportCsv(package, entity))
            .Select(row => row.Reverse().ToArray())
            .ToArray();
        var csv = CsvCodec.Write(reversedRows.Select(row => (IReadOnlyList<string?>)row));

        AssertEntity(entity, _exchange.ParseCsv(package, entity, csv));
        AssertEntity(entity, _exchange.ParseXlsx(package, entity, XlsxCodec.Write(reversedRows, SheetName(entity))));
    }

    [Fact]
    public void Csv_and_xlsx_reject_duplicate_headers_with_a_clear_diagnostic()
    {
        var package = Package();
        var duplicateRows = new[]
        {
            new[] { "Id", "Name", "Path", "DataType", "name" },
            new[] { string.Empty, "Pump", "Plant.Pump", "Float", "Duplicate" }
        };

        var csvError = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseCsv(package, EngineeringTabularEntity.Tags,
                CsvCodec.Write(duplicateRows.Select(row => (IReadOnlyList<string?>)row))));
        Assert.Contains("duplicate column", csvError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", csvError.Message, StringComparison.OrdinalIgnoreCase);

        var xlsxError = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseXlsx(package, EngineeringTabularEntity.Tags, XlsxCodec.Write(duplicateRows, "Tags")));
        Assert.Contains("duplicate column", xlsxError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name", xlsxError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Csv_numbers_and_blank_optional_values_have_locale_independent_null_and_default_semantics()
    {
        const string csv = "Id,Path,Name,DataType,Unit,ScaleMinimum,ScaleMaximum,ReadOnly,HistorianEnabled,HistorianStrategy,PeriodMilliseconds,MaximumPeriodMilliseconds\n" +
                           ",Plant.Locale,Locale,Float, ,1.25,, , , ,1000,\n";
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var imported = _exchange.ParseCsv(Package(), EngineeringTabularEntity.Tags, csv);
            var tag = Assert.Single(imported.Tags);

            Assert.Null(tag.EngineeringUnit);
            Assert.Equal(1.25, tag.ScaleMinimum);
            Assert.Null(tag.ScaleMaximum);
            Assert.True(tag.ReadOnly);
            Assert.False(tag.Historian!.Enabled);
            Assert.Equal("none", tag.Historian.Strategy);
            Assert.Equal(1000, tag.Historian.PeriodMilliseconds);
            Assert.Null(tag.Historian.MaximumPeriodMilliseconds);

            var exported = _exchange.ExportCsv(imported, EngineeringTabularEntity.Tags);
            Assert.Contains("1.25", exported, StringComparison.Ordinal);
            Assert.DoesNotContain("1,25", exported, StringComparison.Ordinal);
            var reparsed = Assert.Single(_exchange.ParseCsv(imported, EngineeringTabularEntity.Tags, exported).Tags);
            Assert.Equal(tag.ScaleMinimum, reparsed.ScaleMinimum);
            Assert.Null(reparsed.ScaleMaximum);
            Assert.True(reparsed.ReadOnly);
            Assert.False(reparsed.Historian!.Enabled);

            var localizedDecimal = csv.Replace("1.25", "\"1,25\"", StringComparison.Ordinal);
            var error = Assert.Throws<InvalidDataException>(() =>
                _exchange.ParseCsv(Package(), EngineeringTabularEntity.Tags, localizedDecimal));
            Assert.Contains("invariant-culture number", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Csv_typed_parse_reports_source_row_and_rejects_invalid_boolean_instead_of_defaulting()
    {
        var current = Package();
        const string csv = "Id,Key,Name,Driver,Enabled,SettingsJson,SecretReferencesJson,MetadataJson\n" +
                           ",source,Source,ModbusTcp,yes,,,\n";

        var error = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseCsv(current, EngineeringTabularEntity.DataSources, csv));

        Assert.Contains("row 2", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("true or false", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_refuses_plaintext_data_source_secrets_without_echoing_the_secret()
    {
        var secret = "do-not-export-this-value";
        var package = Package() with
        {
            DataSources = [new DataSourceEngineeringDto(Guid.NewGuid(), "plc", "PLC", "ModbusTcp",
                Settings: new Dictionary<string, string> { ["password"] = secret })]
        };

        var error = Assert.Throws<InvalidDataException>(() =>
            _exchange.ExportCsv(package, EngineeringTabularEntity.DataSources));

        Assert.DoesNotContain(secret, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Operational_event_preview_accepts_existing_tag_reference_without_reimporting_the_tag()
    {
        var tags = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Flow", "Plant.Flow", TagDataType.Float);
        tags.Register(tag);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var canonicalExchange = new EngineeringExchangeService(tags, alarms);
        var current = canonicalExchange.ExportPackage();
        var source = Package() with
        {
            Tags = [],
            OperationalEvents = [new OperationalEventEngineeringDto(Guid.NewGuid(), "flow.changed", "Flow changed",
                "state-change", "process", "gateway", TagId: tag.Id, TagPath: tag.Path)]
        };

        var csv = _exchange.ExportCsv(source, EngineeringTabularEntity.OperationalEvents);
        var imported = _exchange.ParseCsv(current, EngineeringTabularEntity.OperationalEvents, csv);
        var preview = canonicalExchange.Preview(imported, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply, string.Join("; ", preview.Items.SelectMany(item => item.Issues).Select(issue => issue.Message)));
        var result = canonicalExchange.Apply(imported, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.Equal(tag.Id, canonicalExchange.ExportPackage().OperationalEvents!.Single().TagId);
    }

    [Theory]
    [InlineData(EngineeringTabularEntity.Tags)]
    [InlineData(EngineeringTabularEntity.Alarms)]
    [InlineData(EngineeringTabularEntity.DataSources)]
    [InlineData(EngineeringTabularEntity.GatewayRoutes)]
    [InlineData(EngineeringTabularEntity.OperationalEvents)]
    public void Tabular_import_uses_canonical_preview_apply_and_all_three_merge_modes(EngineeringTabularEntity entity)
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var canonicalExchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var seed = SeedPackage();
        var seeded = canonicalExchange.Apply(seed, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(seeded.Issues, issue => issue.IsError);
        var current = canonicalExchange.ExportPackage();
        var csv = _exchange.ExportCsv(current, entity);

        foreach (var mode in Enum.GetValues<ImportMode>())
        {
            var package = _exchange.ParseCsv(current, entity, csv);
            var preview = canonicalExchange.Preview(package, mode);
            Assert.True(preview.CanApply, string.Join("; ", preview.Items.SelectMany(item => item.Issues).Select(issue => issue.Message)));
            var result = canonicalExchange.Apply(package, mode);
            Assert.DoesNotContain(result.Issues, issue => issue.IsError);
            if (mode == ImportMode.CreateOnly)
                Assert.True(preview.SkipCount > 0);
            else
                Assert.True(preview.UpdateCount > 0);
        }
    }

    [Theory]
    [InlineData(EngineeringTabularEntity.Tags, "csv")]
    [InlineData(EngineeringTabularEntity.Tags, "xlsx")]
    [InlineData(EngineeringTabularEntity.Alarms, "csv")]
    [InlineData(EngineeringTabularEntity.Alarms, "xlsx")]
    [InlineData(EngineeringTabularEntity.DataSources, "csv")]
    [InlineData(EngineeringTabularEntity.DataSources, "xlsx")]
    [InlineData(EngineeringTabularEntity.GatewayRoutes, "csv")]
    [InlineData(EngineeringTabularEntity.GatewayRoutes, "xlsx")]
    [InlineData(EngineeringTabularEntity.OperationalEvents, "csv")]
    [InlineData(EngineeringTabularEntity.OperationalEvents, "xlsx")]
    public void Added_tabular_row_round_trips_through_the_canonical_preview_and_apply(
        EngineeringTabularEntity entity,
        string format)
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var canonicalExchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var seedResult = canonicalExchange.Apply(SeedPackage(), ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(seedResult.Issues, issue => issue.IsError);

        var current = canonicalExchange.ExportPackage();
        var edited = AppendRow(current, entity);
        var imported = format == "csv"
            ? _exchange.ParseCsv(current, entity, _exchange.ExportCsv(edited, entity))
            : _exchange.ParseXlsx(current, entity, _exchange.ExportXlsx(edited, entity));

        var preview = canonicalExchange.Preview(imported, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply, string.Join("; ", preview.Items.SelectMany(item => item.Issues).Select(issue => issue.Message)));
        Assert.Equal(1, preview.CreateCount);

        var result = canonicalExchange.Apply(imported, ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.Equal(CountRows(edited, entity), CountRows(canonicalExchange.ExportPackage(), entity));
    }

    [Fact]
    public void Xlsx_rejects_formula_cells_instead_of_importing_cached_values()
    {
        var package = Package();
        var bytes = _exchange.ExportXlsx(package, EngineeringTabularEntity.Tags);
        var malicious = RewriteXlsx(bytes, "xl/worksheets/sheet1.xml", xml =>
        {
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var cell = xml.Descendants(main + "c").First();
            cell.SetAttributeValue("t", null);
            cell.Elements().Remove();
            cell.Add(new XElement(main + "f", "1+1"), new XElement(main + "v", "2"));
        });

        var error = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseXlsx(package, EngineeringTabularEntity.Tags, malicious));

        Assert.Contains("formula", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Xlsx_requires_exactly_one_worksheet()
    {
        var package = Package();
        var bytes = _exchange.ExportXlsx(package, EngineeringTabularEntity.Tags);
        var malicious = RewriteXlsx(bytes, "xl/workbook.xml", xml =>
        {
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheets = xml.Root!.Element(main + "sheets")!;
            sheets.Add(new XElement(main + "sheet", new XAttribute("name", "Hidden"), new XAttribute("sheetId", "2"),
                new XAttribute(XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships") + "id", "rId1")));
        });

        var error = Assert.Throws<InvalidDataException>(() =>
            _exchange.ParseXlsx(package, EngineeringTabularEntity.Tags, malicious));

        Assert.Contains("single-sheet", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_gateway_route_from_xlsx_preview_blocks_apply_without_mutating_the_workspace()
    {
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var canonicalExchange = new EngineeringExchangeService(new InMemoryTagRegistry(), alarms);
        var seedResult = canonicalExchange.Apply(SeedPackage(), ImportMode.CreateAndUpdate);
        Assert.DoesNotContain(seedResult.Issues, issue => issue.IsError);
        var current = canonicalExchange.ExportPackage();
        var destination = current.Tags.Single(item => item.Path == "Plant.SecondaryDestination");
        var invalidRoute = new GatewayRouteEngineeringDto(
            Guid.NewGuid(),
            "invalid-sheet-route",
            "Invalid spreadsheet route",
            Guid.NewGuid(),
            "Plant.MissingSource",
            destination.Id,
            destination.Path);
        var edited = current with { Gateways = current.Gateways!.Append(invalidRoute).ToArray() };
        var sheet = _exchange.ExportXlsx(edited, EngineeringTabularEntity.GatewayRoutes);
        var imported = _exchange.ParseXlsx(current, EngineeringTabularEntity.GatewayRoutes, sheet);

        var preview = canonicalExchange.Preview(imported, ImportMode.CreateAndUpdate);
        Assert.False(preview.CanApply);
        Assert.True(preview.ErrorCount > 0);
        Assert.Contains(preview.Items.SelectMany(item => item.Issues), issue =>
            issue.IsError && issue.Code == "GATEWAY_SOURCE_TAG_NOT_FOUND");

        var apply = canonicalExchange.Apply(imported, ImportMode.CreateAndUpdate);
        Assert.Equal(0, apply.Created);
        Assert.Equal(0, apply.Updated);
        Assert.Contains(apply.Issues, issue => issue.IsError);

        var after = canonicalExchange.ExportPackage();
        Assert.Equal(JsonSerializer.Serialize(current.Tags), JsonSerializer.Serialize(after.Tags));
        Assert.Equal(JsonSerializer.Serialize(current.DataSources), JsonSerializer.Serialize(after.DataSources));
        Assert.Equal(JsonSerializer.Serialize(current.Gateways), JsonSerializer.Serialize(after.Gateways));
    }

    private static EngineeringPackage Package()
    {
        var sourceId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(tagId, "Temperature", "Plant.Temperature", TagDataType.Float,
                Source: "plc", Address: "40001", EngineeringUnit: "°C", ReadOnly: false,
                Historian: new HistorianSettingsDto(true, "periodic", 0.25, 1000, 5000),
                Metadata: new Dictionary<string, string> { ["note"] = "line 1\nline 2" },
                DataSourceId: sourceId, HistorianCaptureProfileId: Guid.NewGuid())],
            [new AlarmEngineeringDto(Guid.NewGuid(), "High temperature", tagId, "Plant.Temperature",
                AlarmType.High, AlarmPriority.High, 80.5, Message: "High, \"hot\"")],
            DataSources: [new DataSourceEngineeringDto(sourceId, "plc", "PLC", "ModbusTcp", true,
                new Dictionary<string, string> { ["host"] = "192.0.2.10" },
                new Dictionary<string, string> { ["password"] = "env://SCADA_PLC_PASSWORD" })],
            Gateways: [new GatewayRouteEngineeringDto(Guid.NewGuid(), "mirror", "Mirror", tagId, "Plant.Temperature",
                destinationId, "Plant.Mirror", GatewayTransferMode.Periodic, GatewayQualityPolicy.GoodOnly,
                GatewayConversionPolicy.CheckedNumeric, GatewayInitialTransferPolicy.WaitForNextAcceptableValue,
                1.25, -0.5, 0.2, 100, 250, "route, note", true,
                new Dictionary<string, string> { ["group"] = "A" })],
            OperationalEvents: [new OperationalEventEngineeringDto(Guid.NewGuid(), "pump.started", "Pump started",
                "state-change", "process", "gateway", "Area A", "Plant.Pump1", tagId, "Plant.Temperature",
                "Started, ready", true, new Dictionary<string, string> { ["origin"] = "PLC" })],
            EngineeringLock: new EngineeringLockEngineeringDto(false),
            AuthorityPolicyReference: new AuthorityPolicyReferenceEngineeringDto("scada.authority-policy", 1, 42, [], []),
            RuntimePresentation: new RuntimePresentationEngineeringDto());
    }

    private static EngineeringPackage SeedPackage()
    {
        var sourceId = Guid.NewGuid();
        var sourceTagId = Guid.NewGuid();
        var destinationTagId = Guid.NewGuid();
        var secondaryDestinationTagId = Guid.NewGuid();
        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [
                new TagEngineeringDto(sourceTagId, "Source", "Plant.Source", TagDataType.Float,
                    Source: "plc", Address: "40001", ReadOnly: false, DataSourceId: sourceId),
                new TagEngineeringDto(destinationTagId, "Destination", "Plant.Destination", TagDataType.Float,
                    Source: "plc", Address: "40002", ReadOnly: false, DataSourceId: sourceId),
                new TagEngineeringDto(secondaryDestinationTagId, "Secondary destination", "Plant.SecondaryDestination",
                    TagDataType.Float, Source: "plc", Address: "40003", ReadOnly: false, DataSourceId: sourceId)
            ],
            [new AlarmEngineeringDto(Guid.NewGuid(), "High source", sourceTagId, "Plant.Source", AlarmType.High,
                AlarmPriority.High, 50)],
            DataSources: [new DataSourceEngineeringDto(sourceId, "plc", "PLC", "ModbusTcp",
                Settings: new Dictionary<string, string> { ["host"] = "127.0.0.1" })],
            Gateways: [new GatewayRouteEngineeringDto(Guid.NewGuid(), "route", "Route", sourceTagId, "Plant.Source",
                destinationTagId, "Plant.Destination")],
            OperationalEvents: [new OperationalEventEngineeringDto(Guid.NewGuid(), "source.updated", "Source updated",
                "value-change", "process", "plc", TagId: sourceTagId, TagPath: "Plant.Source")]);
    }

    private static EngineeringPackage AppendRow(EngineeringPackage package, EngineeringTabularEntity entity)
    {
        var tag = package.Tags.First();
        var gatewaySource = package.Tags.Single(item => item.Path == "Plant.Source");
        var gatewayDestination = package.Tags.Single(item => item.Path == "Plant.SecondaryDestination");
        var addedId = Guid.NewGuid();
        return entity switch
        {
            EngineeringTabularEntity.Tags => package with
            {
                Tags = package.Tags.Append(new TagEngineeringDto(addedId, "Imported", "Plant.Imported",
                    TagDataType.Float, Source: "plc", Address: "40003", ReadOnly: false,
                    DataSourceId: package.DataSources!.First().Id)).ToArray()
            },
            EngineeringTabularEntity.Alarms => package with
            {
                Alarms = package.Alarms.Append(new AlarmEngineeringDto(addedId, "Imported high alarm", tag.Id,
                    tag.Path, AlarmType.High, AlarmPriority.High, 75)).ToArray()
            },
            EngineeringTabularEntity.DataSources => package with
            {
                DataSources = package.DataSources!.Append(new DataSourceEngineeringDto(addedId, "imported-plc",
                    "Imported PLC", "ModbusTcp", Settings: new Dictionary<string, string> { ["host"] = "192.0.2.11" })).ToArray()
            },
            EngineeringTabularEntity.GatewayRoutes => package with
            {
                Gateways = package.Gateways!.Append(new GatewayRouteEngineeringDto(addedId, "imported-route",
                    "Imported route", gatewaySource.Id, gatewaySource.Path,
                    gatewayDestination.Id, gatewayDestination.Path)).ToArray()
            },
            EngineeringTabularEntity.OperationalEvents => package with
            {
                OperationalEvents = package.OperationalEvents!.Append(new OperationalEventEngineeringDto(addedId,
                    "imported.updated", "Imported update", "value-change", "process", "plc",
                    TagId: tag.Id, TagPath: tag.Path)).ToArray()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(entity))
        };
    }

    private static int CountRows(EngineeringPackage package, EngineeringTabularEntity entity) => entity switch
    {
        EngineeringTabularEntity.Tags => package.Tags.Count,
        EngineeringTabularEntity.Alarms => package.Alarms.Count,
        EngineeringTabularEntity.DataSources => package.DataSources?.Count ?? 0,
        EngineeringTabularEntity.GatewayRoutes => package.Gateways?.Count ?? 0,
        EngineeringTabularEntity.OperationalEvents => package.OperationalEvents?.Count ?? 0,
        _ => throw new ArgumentOutOfRangeException(nameof(entity))
    };

    private static string SheetName(EngineeringTabularEntity entity) => entity switch
    {
        EngineeringTabularEntity.Tags => "Tags",
        EngineeringTabularEntity.Alarms => "Alarms",
        EngineeringTabularEntity.DataSources => "DataSources",
        EngineeringTabularEntity.GatewayRoutes => "GatewayRoutes",
        EngineeringTabularEntity.OperationalEvents => "OperationalEvents",
        _ => throw new ArgumentOutOfRangeException(nameof(entity))
    };

    private static void AssertEntity(EngineeringTabularEntity entity, EngineeringPackage package)
    {
        switch (entity)
        {
            case EngineeringTabularEntity.Tags:
                Assert.Single(package.Tags);
                Assert.Equal("Plant.Temperature", package.Tags.Single().Path);
                Assert.NotNull(package.Tags.Single().DataSourceId);
                Assert.NotNull(package.Tags.Single().HistorianCaptureProfileId);
                break;
            case EngineeringTabularEntity.Alarms:
                Assert.Single(package.Alarms);
                Assert.Equal("High temperature", package.Alarms.Single().Name);
                break;
            case EngineeringTabularEntity.DataSources:
                Assert.Single(package.DataSources ?? []);
                Assert.Equal("env://SCADA_PLC_PASSWORD", package.DataSources!.Single().SecretReferences!["password"]);
                break;
            case EngineeringTabularEntity.GatewayRoutes:
                var routes = package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>();
                Assert.Single(routes);
                Assert.Equal(GatewayTransferMode.Periodic, routes.Single().TransferMode);
                Assert.Equal(100, routes.Single().MinimumIntervalMilliseconds);
                break;
            case EngineeringTabularEntity.OperationalEvents:
                var events = package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>();
                Assert.Single(events);
                Assert.Equal("pump.started", events.Single().Key);
                Assert.Equal("Plant.Temperature", events.Single().TagPath);
                break;
        }
    }

    private static byte[] RewriteXlsx(byte[] original, string entryPath, Action<XDocument> edit)
    {
        using var sourceStream = new MemoryStream(original, writable: false);
        using var source = new ZipArchive(sourceStream, ZipArchiveMode.Read);
        using var outputStream = new MemoryStream();
        using (var output = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var destination = output.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var destinationStream = destination.Open();
                using var entryStream = entry.Open();
                if (!string.Equals(entry.FullName, entryPath, StringComparison.Ordinal))
                {
                    entryStream.CopyTo(destinationStream);
                    continue;
                }

                var document = XDocument.Load(entryStream);
                edit(document);
                document.Save(destinationStream);
            }
        }
        return outputStream.ToArray();
    }
}
