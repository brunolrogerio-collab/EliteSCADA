using System.Globalization;
using System.Xml.Linq;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.AllenBradley;

public sealed class AllenBradleyLogixEngineeringAdapter :
    ICommunicationDriverConnectionTester,
    ICommunicationDriverBrowser,
    ICommunicationDriverFileImporter,
    ICommunicationDriverReconciler
{
    public const string DriverType = "rockwell.logix.eip";
    private readonly ILogixProtocolClientFactory _clientFactory;

    public AllenBradleyLogixEngineeringAdapter(ILogixProtocolClientFactory? clientFactory = null)
    {
        _clientFactory = clientFactory ?? new LogixEtherNetIpClientFactory();
    }

    public CommunicationDriverTypeDescriptor Descriptor { get; } = CreateDescriptor();

    public async ValueTask<DriverConnectionTestResult> TestConnectionAsync(
        DriverEngineeringDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryCreateOptions(context, out var options, out var optionIssues))
            return new DriverConnectionTestResult(false, null, null, Issues: optionIssues);

        await using var client = _clientFactory.Create();
        try
        {
            await client.ConnectAsync(options!, cancellationToken);
            var identity = await client.GetIdentityAsync(cancellationToken);
            return new DriverConnectionTestResult(
                true,
                options!.Endpoint,
                identity.DisplayIdentity,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["vendorId"] = identity.VendorId.ToString(CultureInfo.InvariantCulture),
                    ["deviceType"] = identity.DeviceType.ToString(CultureInfo.InvariantCulture),
                    ["productCode"] = identity.ProductCode.ToString(CultureInfo.InvariantCulture),
                    ["revision"] = $"{identity.RevisionMajor}.{identity.RevisionMinor}",
                    ["serialNumber"] = identity.SerialNumber.ToString("X8", CultureInfo.InvariantCulture),
                    ["profile"] = options.Profile.ToString(),
                    ["route"] = options.RouteDisplay,
                    ["messagingMode"] = "unconnected-explicit"
                });
        }
        catch (NotSupportedException ex)
        {
            return FailedConnection(options!, "LOGIX_SECURE_TRANSPORT_UNSUPPORTED", ex.Message);
        }
        catch (TimeoutException ex)
        {
            return FailedConnection(options!, "LOGIX_CONNECTION_TIMEOUT", ex.Message);
        }
        catch (IOException ex)
        {
            return FailedConnection(options!, "LOGIX_CONNECTION_FAILED", ex.Message);
        }
    }

    public async ValueTask<DriverBrowsePage> BrowseAsync(
        DriverBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.IsNullOrWhiteSpace(request.ParentNodeId))
        {
            return new DriverBrowsePage(
                Array.Empty<DriverBrowseNode>(),
                Issues:
                [
                    new DriverEngineeringIssue(
                        "LOGIX_BROWSE_PARENT_UNSUPPORTED",
                        DriverEngineeringIssueSeverity.Warning,
                        "The first-cut online browser enumerates controller-scope symbols at the root only. Program-scope discovery is available through L5X import until online program browse is proven on hardware.")
                ]);
        }

        if (!TryCreateOptions(request.Context, out var options, out var optionIssues))
            return new DriverBrowsePage(Array.Empty<DriverBrowseNode>(), Issues: optionIssues);

        uint startInstance = 0;
        if (!string.IsNullOrWhiteSpace(request.ContinuationToken) &&
            !uint.TryParse(request.ContinuationToken, NumberStyles.None, CultureInfo.InvariantCulture, out startInstance))
        {
            return new DriverBrowsePage(
                Array.Empty<DriverBrowseNode>(),
                Issues:
                [
                    new DriverEngineeringIssue(
                        "LOGIX_BROWSE_CONTINUATION_INVALID",
                        DriverEngineeringIssueSeverity.Error,
                        "The Logix browse continuation token is invalid.")
                ]);
        }

        if (startInstance > ushort.MaxValue)
        {
            return new DriverBrowsePage(
                Array.Empty<DriverBrowseNode>(),
                IsPartial: true,
                Issues:
                [
                    new DriverEngineeringIssue(
                        "LOGIX_BROWSE_INSTANCE_RANGE",
                        DriverEngineeringIssueSeverity.Warning,
                        "The first-cut Symbol Object browser reached an instance number above its currently proven 16-bit request-path range. Results are partial and hardware/library validation is required before expanding this range.")
                ]);
        }

        await using var client = _clientFactory.Create();
        try
        {
            await client.ConnectAsync(options!, cancellationToken);
            var page = await client.BrowseControllerSymbolsAsync(startInstance, cancellationToken);
            var limit = Math.Clamp(request.PageSize ?? 100, 1, 1000);
            var selected = page.Symbols.Take(limit).ToArray();
            var nodes = selected.Select(CreateBrowseNode).ToArray();

            uint? nextInstance = null;
            if (page.Symbols.Count > selected.Length && selected.Length > 0)
                nextInstance = selected[^1].InstanceId == uint.MaxValue ? null : selected[^1].InstanceId + 1u;
            else
                nextInstance = page.NextInstance;

            var issues = new List<DriverEngineeringIssue>();
            if (nextInstance is > ushort.MaxValue)
            {
                issues.Add(new DriverEngineeringIssue(
                    "LOGIX_BROWSE_INSTANCE_RANGE",
                    DriverEngineeringIssueSeverity.Warning,
                    "Additional symbols exist beyond the first-cut 16-bit Symbol Object continuation range; this browse result is explicitly partial."));
                nextInstance = null;
            }

            return new DriverBrowsePage(
                nodes,
                nextInstance?.ToString(CultureInfo.InvariantCulture),
                page.IsPartial || page.Symbols.Count > selected.Length || issues.Count > 0,
                issues.Count == 0 ? null : issues);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or NotSupportedException)
        {
            return new DriverBrowsePage(
                Array.Empty<DriverBrowseNode>(),
                Issues:
                [
                    new DriverEngineeringIssue(
                        "LOGIX_BROWSE_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        Sanitize(ex.Message))
                ]);
        }
    }

    public async IAsyncEnumerable<DriverImportCandidate> ImportAsync(
        DriverImportRequest request,
        Stream content,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        XDocument? document = null;
        string? loadError = null;
        try
        {
            document = await XDocument.LoadAsync(content, LoadOptions.None, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException or IOException)
        {
            loadError = Sanitize(ex.Message);
        }

        if (document is null)
        {
            yield return new DriverImportCandidate(
                "l5x:document-error",
                "l5x:document-error",
                request.SourceName,
                "invalid:l5x",
                false,
                false,
                Issues:
                [
                    new DriverEngineeringIssue(
                        "LOGIX_L5X_PARSE_FAILED",
                        DriverEngineeringIssueSeverity.Error,
                        $"L5X parse failed: {loadError ?? "unknown XML error"}.")
                ]);
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in document.Descendants().Where(static x => x.Name.LocalName == "Tag"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = TryCreateL5xCandidate(request.SourceName, element);
            if (candidate is null) continue;
            if (!seen.Add(candidate.StableIdentity)) continue;
            yield return candidate;
        }
    }

    public async IAsyncEnumerable<DriverReconcileResult> ReconcileAsync(
        DriverReconcileRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryCreateOptions(request.Context, out var options, out var optionIssues))
        {
            foreach (var address in request.PortableAddresses)
            {
                yield return new DriverReconcileResult(
                    address,
                    DriverReconcileStatus.Error,
                    Issues: optionIssues);
            }
            yield break;
        }

        await using var client = _clientFactory.Create();
        Exception? connectionError = null;
        try
        {
            await client.ConnectAsync(options!, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or NotSupportedException)
        {
            connectionError = ex;
        }

        if (connectionError is not null)
        {
            foreach (var address in request.PortableAddresses)
            {
                yield return new DriverReconcileResult(
                    address,
                    DriverReconcileStatus.Error,
                    Issues:
                    [
                        new DriverEngineeringIssue(
                            "LOGIX_RECONCILE_CONNECTION_FAILED",
                            DriverEngineeringIssueSeverity.Error,
                            Sanitize(connectionError.Message))
                    ]);
            }
            yield break;
        }

        foreach (var address in request.PortableAddresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LogixPortableAddress.TryParse(address, out var reference, out var access, out var constant, out var parseError) || reference is null)
            {
                yield return new DriverReconcileResult(
                    address,
                    DriverReconcileStatus.Error,
                    Issues:
                    [
                        new DriverEngineeringIssue(
                            "LOGIX_BINDING_INVALID",
                            DriverEngineeringIssueSeverity.Error,
                            parseError ?? "Invalid Logix portable address.")
                    ]);
                continue;
            }

            if (!LogixValueCodec.IsFirstCutRuntimeReadable(reference.NativeType))
            {
                yield return new DriverReconcileResult(
                    address,
                    DriverReconcileStatus.Unsupported,
                    ResolvedIdentity: reference.StableIdentity,
                    ResolvedPortableAddress: address,
                    ObservedDataType: TryCanonicalType(reference.NativeType),
                    IsReadable: false,
                    IsWritable: false,
                    Issues:
                    [
                        new DriverEngineeringIssue(
                            "LOGIX_TYPE_RUNTIME_UNSUPPORTED",
                            DriverEngineeringIssueSeverity.Warning,
                            $"Native type '{reference.NativeType}' is preserved by Engineering but is not enabled by the first-cut runtime codec.")
                    ]);
                continue;
            }

            LogixReadResult? readResult = null;
            Exception? readError = null;
            try
            {
                var results = await client.ReadManyAsync([reference], cancellationToken);
                if (results.Count == 1) readResult = results[0];
                else readError = new InvalidDataException("Logix reconcile read returned an unexpected result count.");
            }
            catch (Exception ex) when (ex is IOException or TimeoutException)
            {
                readError = ex;
            }

            if (readError is not null)
            {
                yield return new DriverReconcileResult(
                    address,
                    DriverReconcileStatus.Error,
                    ResolvedIdentity: reference.StableIdentity,
                    ResolvedPortableAddress: address,
                    Issues:
                    [
                        new DriverEngineeringIssue(
                            "LOGIX_RECONCILE_READ_FAILED",
                            DriverEngineeringIssueSeverity.Error,
                            Sanitize(readError.Message))
                    ]);
                continue;
            }

            if (readResult is null || !readResult.Succeeded)
            {
                var error = readResult?.Error ?? LogixProtocolError.ProtocolFault;
                yield return new DriverReconcileResult(
                    address,
                    ToReconcileStatus(error),
                    ResolvedIdentity: reference.StableIdentity,
                    ResolvedPortableAddress: address,
                    ObservedDataType: TryCanonicalType(reference.NativeType),
                    IsReadable: false,
                    IsWritable: false,
                    Issues:
                    [
                        new DriverEngineeringIssue(
                            "LOGIX_RECONCILE_POINT_FAILED",
                            DriverEngineeringIssueSeverity.Warning,
                            readResult?.Message ?? error.ToString())
                    ]);
                continue;
            }

            var readable = access != LogixExternalAccess.None;
            var writable = access == LogixExternalAccess.ReadWrite &&
                           !constant &&
                           LogixValueCodec.IsFirstCutRuntimeWritable(reference.NativeType);
            yield return new DriverReconcileResult(
                address,
                DriverReconcileStatus.Unchanged,
                ResolvedIdentity: reference.StableIdentity,
                ResolvedPortableAddress: LogixPortableAddress.Format(reference, access, constant),
                ObservedDataType: TryCanonicalType(reference.NativeType),
                IsReadable: readable,
                IsWritable: writable,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["scope"] = reference.Scope.ToString(),
                    ["nativeType"] = reference.NativeType.ToString(),
                    ["externalAccess"] = access.ToString(),
                    ["constant"] = constant.ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    public static bool TryCreateOptions(
        DriverEngineeringDataSourceContext context,
        out AllenBradleyLogixOptions? options,
        out IReadOnlyCollection<DriverEngineeringIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(context);
        var result = new List<DriverEngineeringIssue>();
        var host = GetSetting(context.Settings, "host");
        if (string.IsNullOrWhiteSpace(host))
            result.Add(Issue("LOGIX_HOST_REQUIRED", "host", "Allen-Bradley host/IP is required."));

        var port = ParseInt(context.Settings, "port", 44818, 1, 65535, result);
        var scanMs = ParseInt(context.Settings, "scanIntervalMs", 1000, 50, 3_600_000, result);
        var timeoutMs = ParseInt(context.Settings, "requestTimeoutMs", 3000, 1, 60_000, result);
        var reconnectMinMs = ParseInt(context.Settings, "reconnectMinimumMs", 500, 1, 60_000, result);
        var reconnectMaxMs = ParseInt(context.Settings, "reconnectMaximumMs", 15_000, 1, 300_000, result);
        var maxBatchSize = ParseInt(context.Settings, "maxBatchSize", 16, 1, 64, result);

        var profileText = GetSetting(context.Settings, "profile") ?? LogixControllerProfile.CompactLogix.ToString();
        if (!Enum.TryParse<LogixControllerProfile>(profileText, true, out var profile))
        {
            result.Add(Issue("LOGIX_PROFILE_INVALID", "profile", $"Unsupported Logix controller profile '{profileText}'."));
            profile = LogixControllerProfile.CompactLogix;
        }

        var securityText = GetSetting(context.Settings, "securityMode") ?? LogixSecurityMode.Unsecured.ToString();
        if (!Enum.TryParse<LogixSecurityMode>(securityText, true, out var securityMode))
        {
            result.Add(Issue("LOGIX_SECURITY_MODE_INVALID", "securityMode", $"Unsupported Logix security mode '{securityText}'."));
            securityMode = LogixSecurityMode.Unsecured;
        }
        if (securityMode == LogixSecurityMode.CipSecurityRequired)
        {
            result.Add(new DriverEngineeringIssue(
                "LOGIX_CIP_SECURITY_NOT_IMPLEMENTED",
                DriverEngineeringIssueSeverity.Error,
                "CIP Security was requested, but this first-cut driver does not implement it. Unsecured fallback is intentionally forbidden.",
                "securityMode"));
        }

        IReadOnlyList<CipRouteSegment> route = Array.Empty<CipRouteSegment>();
        var routeText = GetSetting(context.Settings, "route");
        if (!string.IsNullOrWhiteSpace(routeText) && !TryParseRoute(routeText, out route, out var routeError))
            result.Add(Issue("LOGIX_ROUTE_INVALID", "route", routeError!));

        if (reconnectMaxMs < reconnectMinMs)
            result.Add(Issue("LOGIX_RECONNECT_RANGE_INVALID", "reconnectMaximumMs", "Reconnect maximum must be greater than or equal to reconnect minimum."));

        if (result.Any(static x => x.Severity == DriverEngineeringIssueSeverity.Error))
        {
            options = null;
            issues = result;
            return false;
        }

        options = new AllenBradleyLogixOptions(
            host!,
            port,
            profile,
            route,
            TimeSpan.FromMilliseconds(scanMs),
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(reconnectMinMs),
            TimeSpan.FromMilliseconds(reconnectMaxMs),
            maxBatchSize,
            securityMode);
        issues = result;
        return true;
    }

    private static CommunicationDriverTypeDescriptor CreateDescriptor() => new(
        DriverType,
        "Allen-Bradley Logix EtherNet/IP",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest |
                                 DriverEngineeringCapabilities.Browse |
                                 DriverEngineeringCapabilities.FileImport |
                                 DriverEngineeringCapabilities.Reconcile,
        AcquisitionModes: [DriverAcquisitionMode.Polling],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            "elitescada.driver.rockwell.logix.eip",
            1,
            [
                new DriverConfigurationFieldDescriptor("host", DriverConfigurationValueKind.Host, Required: true, DisplayName: "Host / IP"),
                new DriverConfigurationFieldDescriptor("port", DriverConfigurationValueKind.Port, DefaultValue: "44818", Minimum: 1, Maximum: 65535),
                new DriverConfigurationFieldDescriptor("profile", DriverConfigurationValueKind.Enum, Required: true, DefaultValue: "CompactLogix", AllowedValues: ["ControlLogix", "CompactLogix"]),
                new DriverConfigurationFieldDescriptor("route", DriverConfigurationValueKind.String, Description: "Ordered CIP port,link hops separated by '/'. Example: 1,0."),
                new DriverConfigurationFieldDescriptor("scanIntervalMs", DriverConfigurationValueKind.Integer, DefaultValue: "1000", Minimum: 50, Maximum: 3_600_000),
                new DriverConfigurationFieldDescriptor("requestTimeoutMs", DriverConfigurationValueKind.Integer, DefaultValue: "3000", Minimum: 1, Maximum: 60_000),
                new DriverConfigurationFieldDescriptor("reconnectMinimumMs", DriverConfigurationValueKind.Integer, DefaultValue: "500", Minimum: 1, Maximum: 60_000, Advanced: true),
                new DriverConfigurationFieldDescriptor("reconnectMaximumMs", DriverConfigurationValueKind.Integer, DefaultValue: "15000", Minimum: 1, Maximum: 300_000, Advanced: true),
                new DriverConfigurationFieldDescriptor("maxBatchSize", DriverConfigurationValueKind.Integer, DefaultValue: "16", Minimum: 1, Maximum: 64, Advanced: true),
                new DriverConfigurationFieldDescriptor("securityMode", DriverConfigurationValueKind.Enum, Required: true, DefaultValue: "Unsecured", AllowedValues: ["Unsecured", "CipSecurityRequired"])
            ],
            [
                new DriverConfigurationFieldDescriptor("scope", DriverConfigurationValueKind.Enum, Required: true, DefaultValue: "Controller", AllowedValues: ["Controller", "Program"]),
                new DriverConfigurationFieldDescriptor("programName", DriverConfigurationValueKind.String),
                new DriverConfigurationFieldDescriptor("symbolPath", DriverConfigurationValueKind.String, Required: true),
                new DriverConfigurationFieldDescriptor("nativeType", DriverConfigurationValueKind.Enum, Required: true, AllowedValues: ["Bool", "Sint", "Int", "Dint", "Lint", "Real", "Lreal", "String"]),
                new DriverConfigurationFieldDescriptor("externalAccess", DriverConfigurationValueKind.Enum, DefaultValue: "Unknown", AllowedValues: ["Unknown", "ReadWrite", "ReadOnly", "None"]),
                new DriverConfigurationFieldDescriptor("constant", DriverConfigurationValueKind.Boolean, DefaultValue: "false"),
                new DriverConfigurationFieldDescriptor("bitIndex", DriverConfigurationValueKind.Integer, Minimum: 0, Maximum: 63, Description: "Optional physical integer-bit selector; exact range is validated from nativeType.")
            ]),
        Description: "ControlLogix/CompactLogix symbolic TAG access using bounded EtherNet/IP explicit messaging.");

    private static DriverBrowseNode CreateBrowseNode(LogixBrowseSymbol symbol)
    {
        var issues = new List<DriverEngineeringIssue>();
        var supported = TryMapSymbolType(symbol.SymbolType, out var nativeType);
        TagDataType? dataType = supported ? TryCanonicalType(nativeType) : null;
        LogixSymbolReference? reference = null;
        string? portableAddress = null;

        if (supported)
        {
            try
            {
                reference = new LogixSymbolReference(LogixTagScope.Controller, symbol.Name, nativeType);
                reference.Validate();
                portableAddress = LogixPortableAddress.Format(reference);
                if (!LogixValueCodec.IsFirstCutRuntimeReadable(nativeType))
                {
                    issues.Add(new DriverEngineeringIssue(
                        "LOGIX_TYPE_RUNTIME_UNSUPPORTED",
                        DriverEngineeringIssueSeverity.Warning,
                        $"Symbol '{symbol.Name}' has native type '{nativeType}', which is visible to Engineering but not enabled by the first-cut runtime codec."));
                }
            }
            catch (ArgumentException ex)
            {
                supported = false;
                issues.Add(new DriverEngineeringIssue(
                    "LOGIX_SYMBOL_PATH_UNSUPPORTED",
                    DriverEngineeringIssueSeverity.Warning,
                    Sanitize(ex.Message)));
            }
        }
        else
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_SYMBOL_TYPE_UNSUPPORTED",
                DriverEngineeringIssueSeverity.Warning,
                $"Symbol '{symbol.Name}' uses unsupported/structured Symbol Object type 0x{symbol.SymbolType:X4}."));
        }

        return new DriverBrowseNode(
            $"symbol:{symbol.InstanceId.ToString(CultureInfo.InvariantCulture)}",
            reference?.StableIdentity ?? $"unsupported:{symbol.Name}",
            symbol.Name,
            false,
            supported && LogixValueCodec.IsFirstCutRuntimeReadable(nativeType),
            false,
            portableAddress,
            dataType,
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["symbolInstance"] = symbol.InstanceId.ToString(CultureInfo.InvariantCulture),
                ["symbolType"] = $"0x{symbol.SymbolType:X4}",
                ["scope"] = "Controller",
                ["externalAccess"] = "Unknown"
            },
            Issues: issues.Count == 0 ? null : issues);
    }

    private static DriverImportCandidate? TryCreateL5xCandidate(string sourceName, XElement tag)
    {
        var name = ((string?)tag.Attribute("Name"))?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;

        var program = tag.Ancestors().FirstOrDefault(static x => x.Name.LocalName == "Program");
        var scope = program is null ? LogixTagScope.Controller : LogixTagScope.Program;
        var programName = ((string?)program?.Attribute("Name"))?.Trim();
        var dataTypeText = ((string?)tag.Attribute("DataType"))?.Trim();
        var dimensions = ((string?)tag.Attribute("Dimensions"))?.Trim();
        var externalAccess = ParseExternalAccess((string?)tag.Attribute("ExternalAccess"));
        var constant = bool.TryParse((string?)tag.Attribute("Constant"), out var parsedConstant) && parsedConstant;
        var issues = new List<DriverEngineeringIssue>();

        if (!TryMapL5xDataType(dataTypeText, out var nativeType))
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_L5X_TYPE_UNSUPPORTED",
                DriverEngineeringIssueSeverity.Warning,
                $"L5X TAG '{name}' uses unsupported/structured data type '{dataTypeText ?? "<missing>"}'."));
            return new DriverImportCandidate(
                $"l5x:{scope}:{programName}:{name}",
                scope == LogixTagScope.Controller ? $"controller:{name}" : $"program:{programName}:{name}",
                name,
                BuildUnsupportedPortableAddress(scope, programName, name, dataTypeText),
                false,
                false,
                Metadata: CreateL5xMetadata(sourceName, scope, programName, dataTypeText, dimensions, externalAccess, constant),
                Issues: issues);
        }

        LogixSymbolReference reference;
        try
        {
            reference = new LogixSymbolReference(scope, name, nativeType, programName);
            reference.Validate();
        }
        catch (ArgumentException ex)
        {
            return new DriverImportCandidate(
                $"l5x:{scope}:{programName}:{name}",
                scope == LogixTagScope.Controller ? $"controller:{name}" : $"program:{programName}:{name}",
                name,
                BuildUnsupportedPortableAddress(scope, programName, name, dataTypeText),
                false,
                false,
                Issues:
                [
                    new DriverEngineeringIssue(
                        "LOGIX_L5X_SYMBOL_INVALID",
                        DriverEngineeringIssueSeverity.Warning,
                        Sanitize(ex.Message))
                ]);
        }

        var array = !string.IsNullOrWhiteSpace(dimensions) && dimensions != "0" && dimensions != "0 0 0";
        if (array)
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_ARRAY_BINDING_REQUIRES_ELEMENT",
                DriverEngineeringIssueSeverity.Warning,
                $"L5X TAG '{name}' is an array ({dimensions}). The first-cut importer preserves it as a candidate but requires explicit element/member selection before Runtime binding."));
        }
        if (!LogixValueCodec.IsFirstCutRuntimeReadable(nativeType))
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_TYPE_RUNTIME_UNSUPPORTED",
                DriverEngineeringIssueSeverity.Warning,
                $"L5X TAG '{name}' uses native type '{nativeType}', which is preserved by Engineering but not enabled in the first-cut Runtime."));
        }
        if (nativeType == LogixNativeType.Bool && externalAccess == LogixExternalAccess.ReadWrite)
        {
            issues.Add(new DriverEngineeringIssue(
                "LOGIX_BOOL_DIRECT_WRITE_DEFERRED",
                DriverEngineeringIssueSeverity.Information,
                "Direct BOOL writes remain disabled until packed BOOL/type-position metadata is proven. Integer physical-bit bindings use coordinated read-modify-write instead."));
        }

        var readable = !array && externalAccess != LogixExternalAccess.None && LogixValueCodec.IsFirstCutRuntimeReadable(nativeType);
        var writable = readable &&
                       externalAccess == LogixExternalAccess.ReadWrite &&
                       !constant &&
                       LogixValueCodec.IsFirstCutRuntimeWritable(nativeType);

        return new DriverImportCandidate(
            $"l5x:{reference.StableIdentity}",
            reference.StableIdentity,
            name,
            LogixPortableAddress.Format(reference, externalAccess, constant),
            readable,
            writable,
            TryCanonicalType(nativeType),
            Metadata: CreateL5xMetadata(sourceName, scope, programName, nativeType.ToString(), dimensions, externalAccess, constant),
            Issues: issues.Count == 0 ? null : issues);
    }

    private static IReadOnlyDictionary<string, string> CreateL5xMetadata(
        string sourceName,
        LogixTagScope scope,
        string? programName,
        string? nativeType,
        string? dimensions,
        LogixExternalAccess externalAccess,
        bool constant)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = sourceName,
            ["scope"] = scope.ToString(),
            ["nativeType"] = nativeType ?? string.Empty,
            ["externalAccess"] = externalAccess.ToString(),
            ["constant"] = constant.ToString(CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(programName)) metadata["programName"] = programName;
        if (!string.IsNullOrWhiteSpace(dimensions)) metadata["dimensions"] = dimensions;
        return metadata;
    }

    private static bool TryMapSymbolType(ushort symbolType, out LogixNativeType nativeType)
    {
        var baseType = (ushort)(symbolType & 0x0FFF);
        nativeType = baseType switch
        {
            LogixValueCodec.CipTypeBool => LogixNativeType.Bool,
            LogixValueCodec.CipTypeSint => LogixNativeType.Sint,
            LogixValueCodec.CipTypeInt => LogixNativeType.Int,
            LogixValueCodec.CipTypeDint => LogixNativeType.Dint,
            LogixValueCodec.CipTypeLint => LogixNativeType.Lint,
            LogixValueCodec.CipTypeReal => LogixNativeType.Real,
            LogixValueCodec.CipTypeLreal => LogixNativeType.Lreal,
            _ => (LogixNativeType)(-1)
        };
        return Enum.IsDefined(nativeType);
    }

    private static bool TryMapL5xDataType(string? value, out LogixNativeType nativeType)
    {
        nativeType = value?.Trim().ToUpperInvariant() switch
        {
            "BOOL" => LogixNativeType.Bool,
            "SINT" => LogixNativeType.Sint,
            "INT" => LogixNativeType.Int,
            "DINT" => LogixNativeType.Dint,
            "LINT" => LogixNativeType.Lint,
            "REAL" => LogixNativeType.Real,
            "LREAL" => LogixNativeType.Lreal,
            "STRING" => LogixNativeType.String,
            _ => (LogixNativeType)(-1)
        };
        return Enum.IsDefined(nativeType);
    }

    private static LogixExternalAccess ParseExternalAccess(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "read/write" or "readwrite" => LogixExternalAccess.ReadWrite,
        "read only" or "read-only" or "readonly" => LogixExternalAccess.ReadOnly,
        "none" => LogixExternalAccess.None,
        _ => LogixExternalAccess.Unknown
    };

    private static TagDataType? TryCanonicalType(LogixNativeType nativeType) =>
        LogixValueCodec.TryGetCanonicalDataType(nativeType, out var dataType) ? dataType : null;

    private static DriverReconcileStatus ToReconcileStatus(LogixProtocolError error) => error switch
    {
        LogixProtocolError.SymbolNotFound => DriverReconcileStatus.Missing,
        LogixProtocolError.TypeMismatch => DriverReconcileStatus.DataTypeChangedBreaking,
        LogixProtocolError.AccessDenied or LogixProtocolError.ConstantOrReadOnly => DriverReconcileStatus.AccessChanged,
        _ => DriverReconcileStatus.Error
    };

    private static DriverConnectionTestResult FailedConnection(AllenBradleyLogixOptions options, string code, string message) =>
        new(
            false,
            options.Endpoint,
            null,
            Issues:
            [
                new DriverEngineeringIssue(
                    code,
                    DriverEngineeringIssueSeverity.Error,
                    Sanitize(message))
            ]);

    private static DriverEngineeringIssue Issue(string code, string field, string message) =>
        new(code, DriverEngineeringIssueSeverity.Error, message, field);

    private static int ParseInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        ICollection<DriverEngineeringIssue> issues)
    {
        var text = GetSetting(settings, key);
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
        {
            issues.Add(Issue("LOGIX_SETTING_INVALID", key, $"Setting '{key}' must be an integer from {minimum} to {maximum}."));
            return defaultValue;
        }
        return value;
    }

    private static string? GetSetting(IReadOnlyDictionary<string, string> settings, string key)
    {
        foreach (var pair in settings)
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        return null;
    }

    private static bool TryParseRoute(string value, out IReadOnlyList<CipRouteSegment> route, out string? error)
    {
        var result = new List<CipRouteSegment>();
        foreach (var segmentText in value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segmentText.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
                !byte.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var link) ||
                port is 0 or > 14)
            {
                route = Array.Empty<CipRouteSegment>();
                error = $"Invalid CIP route segment '{segmentText}'. Use numeric port,link hops such as '1,0/2,5'.";
                return false;
            }
            result.Add(new CipRouteSegment(port, link));
        }
        route = result;
        error = null;
        return true;
    }

    private static string BuildUnsupportedPortableAddress(LogixTagScope scope, string? programName, string symbol, string? nativeType)
    {
        var parts = new List<string>
        {
            LogixPortableAddress.Prefix,
            $"scope={(scope == LogixTagScope.Controller ? "controller" : "program")}",
            $"symbol={Uri.EscapeDataString(symbol)}",
            $"native={Uri.EscapeDataString(nativeType ?? "unsupported")}",
            "supported=false"
        };
        if (scope == LogixTagScope.Program && !string.IsNullOrWhiteSpace(programName))
            parts.Insert(2, $"program={Uri.EscapeDataString(programName)}");
        return string.Join(';', parts);
    }

    private static string Sanitize(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }
}
