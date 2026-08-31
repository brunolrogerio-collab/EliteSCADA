using System.Globalization;
using Scada.Core.Tags;

namespace Scada.Drivers.AllenBradley;

public enum LogixControllerProfile
{
    ControlLogix,
    CompactLogix
}

public enum LogixTagScope
{
    Controller,
    Program
}

public enum LogixNativeType
{
    Bool,
    Sint,
    Int,
    Dint,
    Lint,
    Real,
    Lreal,
    String
}

public enum LogixExternalAccess
{
    Unknown,
    ReadWrite,
    ReadOnly,
    None
}

public enum LogixSecurityMode
{
    Unsecured,
    CipSecurityRequired
}

public enum LogixProtocolError
{
    None,
    TransportUnavailable,
    SessionRegistrationFailed,
    RouteRejected,
    TargetIdentityMismatch,
    ControllerResourceUnavailable,
    SymbolNotFound,
    AccessDenied,
    ConstantOrReadOnly,
    TypeMismatch,
    PacketTooLarge,
    FragmentationFailed,
    Timeout,
    SecureTransportRequiredOrUnsupported,
    ProtocolFault
}

public sealed record CipRouteSegment(byte Port, byte LinkAddress)
{
    public override string ToString() => $"{Port.ToString(CultureInfo.InvariantCulture)},{LinkAddress.ToString(CultureInfo.InvariantCulture)}";
}

public sealed record AllenBradleyLogixOptions(
    string Host,
    int Port = 44818,
    LogixControllerProfile Profile = LogixControllerProfile.CompactLogix,
    IReadOnlyList<CipRouteSegment>? Route = null,
    TimeSpan? ScanInterval = null,
    TimeSpan? RequestTimeout = null,
    TimeSpan? ReconnectMinimum = null,
    TimeSpan? ReconnectMaximum = null,
    int MaxBatchSize = 16,
    LogixSecurityMode SecurityMode = LogixSecurityMode.Unsecured)
{
    public TimeSpan EffectiveScanInterval => ScanInterval ?? TimeSpan.FromSeconds(1);
    public TimeSpan EffectiveRequestTimeout => RequestTimeout ?? TimeSpan.FromSeconds(3);
    public TimeSpan EffectiveReconnectMinimum => ReconnectMinimum ?? TimeSpan.FromMilliseconds(500);
    public TimeSpan EffectiveReconnectMaximum => ReconnectMaximum ?? TimeSpan.FromSeconds(15);
    public IReadOnlyList<CipRouteSegment> EffectiveRoute => Route ?? Array.Empty<CipRouteSegment>();

    public string Endpoint => $"{Host.Trim()}:{Port.ToString(CultureInfo.InvariantCulture)}";
    public string RouteDisplay => EffectiveRoute.Count == 0 ? "direct" : string.Join("/", EffectiveRoute);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new ArgumentException("Allen-Bradley host is required.", nameof(Host));
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (EffectiveScanInterval < TimeSpan.FromMilliseconds(50))
            throw new ArgumentOutOfRangeException(nameof(ScanInterval), "Scan interval must be at least 50 ms.");
        if (EffectiveRequestTimeout <= TimeSpan.Zero || EffectiveRequestTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (EffectiveReconnectMinimum <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ReconnectMinimum));
        if (EffectiveReconnectMaximum < EffectiveReconnectMinimum)
            throw new ArgumentOutOfRangeException(nameof(ReconnectMaximum));
        if (MaxBatchSize is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(MaxBatchSize));
        foreach (var segment in EffectiveRoute)
        {
            if (segment.Port is 0 or > 14)
                throw new ArgumentOutOfRangeException(nameof(Route), "The first-cut CIP route encoder supports numeric ports 1..14 only.");
        }
    }
}

public sealed record LogixSymbolReference(
    LogixTagScope Scope,
    string SymbolPath,
    LogixNativeType NativeType,
    string? ProgramName = null)
{
    public string StableIdentity => Scope == LogixTagScope.Controller
        ? $"controller:{SymbolPath}"
        : $"program:{ProgramName}:{SymbolPath}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SymbolPath))
            throw new ArgumentException("Logix symbolic path is required.", nameof(SymbolPath));
        if (Scope == LogixTagScope.Program && string.IsNullOrWhiteSpace(ProgramName))
            throw new ArgumentException("Program-scoped Logix symbols require a program name.", nameof(ProgramName));
        if (Scope == LogixTagScope.Controller && !string.IsNullOrWhiteSpace(ProgramName))
            throw new ArgumentException("Controller-scoped Logix symbols cannot carry a program name.", nameof(ProgramName));
        LogixCipCodec.ValidateSymbolPath(SymbolPath);
        if (!string.IsNullOrWhiteSpace(ProgramName)) LogixCipCodec.ValidateSymbolName(ProgramName);
    }
}

public sealed record LogixTagBinding(
    TagDefinition Tag,
    LogixSymbolReference Reference,
    bool Writable = false,
    LogixExternalAccess ExternalAccess = LogixExternalAccess.Unknown,
    bool Constant = false)
{
    public TagValueSelector? AddressSelector => Tag.AddressSelector;
    public string PortableAddress => LogixPortableAddress.Format(Reference, ExternalAccess, Constant);

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Tag);
        ArgumentNullException.ThrowIfNull(Reference);
        Reference.Validate();
        if (!LogixValueCodec.IsFirstCutRuntimeReadable(Reference.NativeType))
            throw new ArgumentException($"Logix native type '{Reference.NativeType}' remains Engineering/import-visible but is not enabled by the first-cut runtime codec.");

        if (Writable && Tag.ReadOnly)
            throw new ArgumentException($"TAG '{Tag.Path}' is read-only but the Logix binding is marked writable.");
        if (Writable && ExternalAccess != LogixExternalAccess.ReadWrite)
            throw new ArgumentException($"TAG '{Tag.Path}' cannot be writable unless External Access is explicitly ReadWrite.");
        if (Writable && Constant)
            throw new ArgumentException($"TAG '{Tag.Path}' is a Logix constant and cannot be writable.");
        if (Writable && AddressSelector is null && !LogixValueCodec.IsFirstCutRuntimeWritable(Reference.NativeType))
            throw new ArgumentException($"Direct writes for Logix native type '{Reference.NativeType}' remain disabled until a safe type-specific write contract is proven.");

        if (AddressSelector is not null)
        {
            if (AddressSelector.Kind != TagValueSelectorKind.Bit)
                throw new ArgumentException($"Logix binding '{Tag.Path}' uses an unsupported address selector kind '{AddressSelector.Kind}'.");
            var width = LogixValueCodec.GetNativeIntegerBitWidth(Reference.NativeType);
            if (width is null)
                throw new ArgumentException($"Logix native type '{Reference.NativeType}' does not support physical bit selection.");
            if (AddressSelector.Index < 0 || AddressSelector.Index >= width.Value)
                throw new ArgumentOutOfRangeException(nameof(Tag.AddressSelector), $"Bit index must be from 0 to {width.Value - 1} for {Reference.NativeType}.");
            if (Tag.DataType != TagDataType.Boolean)
                throw new ArgumentException("Physical Logix bit bindings require a Boolean canonical TAG.");
            return;
        }

        if (!LogixValueCodec.TryGetCanonicalDataType(Reference.NativeType, out var expected))
            throw new ArgumentException($"Logix native type '{Reference.NativeType}' is not supported by the first-cut scalar runtime.");
        if (Tag.DataType != expected)
            throw new ArgumentException($"TAG '{Tag.Path}' data type '{Tag.DataType}' does not match Logix native type '{Reference.NativeType}' mapping '{expected}'.");
    }
}

public sealed record LogixControllerIdentity(
    ushort VendorId,
    ushort DeviceType,
    ushort ProductCode,
    byte RevisionMajor,
    byte RevisionMinor,
    uint SerialNumber,
    string ProductName)
{
    public string DisplayIdentity => $"{ProductName} rev {RevisionMajor}.{RevisionMinor} serial {SerialNumber:X8}";
}

public sealed record LogixReadResult(
    LogixSymbolReference Reference,
    bool Succeeded,
    object? NativeValue = null,
    LogixProtocolError Error = LogixProtocolError.None,
    string? Message = null);

public sealed record LogixBrowseSymbol(
    uint InstanceId,
    string Name,
    ushort SymbolType);

public sealed record LogixSymbolBrowsePage(
    IReadOnlyList<LogixBrowseSymbol> Symbols,
    uint? NextInstance,
    bool IsPartial);

public sealed record LogixTransportDiagnosticSnapshot(
    bool Connected,
    long RequestAttempts,
    long SuccessfulRequests,
    long FailedRequests,
    long TimeoutCount,
    long ConnectionCount,
    long DisconnectionCount,
    long ReconnectCount,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastDisconnectedAt,
    string? LastError);

public interface ILogixProtocolClient : IAsyncDisposable
{
    bool IsConnected { get; }
    ValueTask ConnectAsync(AllenBradleyLogixOptions options, CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
    ValueTask<LogixControllerIdentity> GetIdentityAsync(CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<LogixReadResult>> ReadManyAsync(
        IReadOnlyList<LogixSymbolReference> references,
        CancellationToken cancellationToken = default);
    ValueTask<LogixSymbolBrowsePage> BrowseControllerSymbolsAsync(uint startInstance = 0, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(LogixSymbolReference reference, object? nativeValue, CancellationToken cancellationToken = default);
    LogixTransportDiagnosticSnapshot GetDiagnostics();
}

public interface ILogixProtocolClientFactory
{
    ILogixProtocolClient Create();
}

public sealed class LogixEtherNetIpClientFactory : ILogixProtocolClientFactory
{
    public ILogixProtocolClient Create() => new LogixEtherNetIpClient();
}
