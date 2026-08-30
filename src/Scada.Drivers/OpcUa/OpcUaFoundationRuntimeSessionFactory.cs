using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;
using Opc.Ua;
using Opc.Ua.Client;
using Scada.Core.Tags;

namespace Scada.Drivers.OpcUa;

/// <summary>
/// OPC Foundation .NET Standard 1.5 runtime adapter. All SDK-specific objects stay
/// behind the IOpcUaRuntimeSessionFactory/IOpcUaRuntimeSession boundary.
/// Secure sessions fail closed and require an explicitly approved server certificate pin.
/// </summary>
public sealed class OpcUaFoundationRuntimeSessionFactory : IOpcUaRuntimeSessionFactory
{
    private const string ApplicationName = "EliteSCADA OPC UA Client";
    private const int DefaultOperationTimeoutMilliseconds = 15_000;

    private static readonly ITelemetryContext Telemetry = DefaultTelemetry.Create(_ => { });

    private readonly OpcUaRuntimeConnectionOptions _options;
    private readonly IOpcUaRuntimeSecurityMaterialProvider _securityMaterialProvider;

    public OpcUaFoundationRuntimeSessionFactory(
        OpcUaRuntimeConnectionOptions options,
        IOpcUaRuntimeSecurityMaterialProvider securityMaterialProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(securityMaterialProvider);

        options.Validate();
        _options = options;
        _securityMaterialProvider = securityMaterialProvider;
    }

    public async Task<IOpcUaRuntimeSession> ConnectAsync(
        IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count == 0)
        {
            throw new ArgumentException("At least one OPC UA runtime binding is required.", nameof(bindings));
        }

        cancellationToken.ThrowIfCancellationRequested();

        X509Certificate2? applicationCertificate = null;
        X509Certificate2? userCertificate = null;
        UserIdentity? userIdentity = null;
        ApplicationConfiguration? configuration = null;
        CertificateValidationEventHandler? certificateValidationHandler = null;
        ISession? sessionForCleanup = null;

        try
        {
            var secure = !IsSecurityModeNone(_options.SecurityMode);
            var approvedPin = _options.NormalizedApprovedServerCertificateSha256;

            if (secure && approvedPin is null)
            {
                throw new InvalidOperationException(
                    "A secure OPC UA runtime session requires an explicitly approved server certificate SHA-256 pin. " +
                    "EliteSCADA does not auto-trust or persist an untrusted server certificate.");
            }

            if (secure)
            {
                applicationCertificate = await _securityMaterialProvider
                    .ResolveCertificateAsync(
                        _options.ClientCertificateReference!,
                        cancellationToken)
                    .ConfigureAwait(false);

                ValidatePrivateKeyCertificate(applicationCertificate, "client application");
            }

            (userIdentity, userCertificate) = await CreateUserIdentityAsync(
                applicationCertificate,
                cancellationToken).ConfigureAwait(false);

            configuration = await CreateApplicationConfigurationAsync(
                applicationCertificate,
                cancellationToken).ConfigureAwait(false);

            if (approvedPin is not null)
            {
                certificateValidationHandler = CreatePinnedCertificateValidationHandler(approvedPin);
                configuration.CertificateValidator.CertificateValidation += certificateValidationHandler;
            }

            EndpointDescription selectedEndpoint = await DiscoverAndSelectEndpointAsync(
                configuration,
                approvedPin,
                cancellationToken).ConfigureAwait(false);

            var endpointConfiguration = EndpointConfiguration.Create(configuration);
            var configuredEndpoint = new ConfiguredEndpoint(
                null,
                selectedEndpoint,
                endpointConfiguration);

            var sessionFactory = new DefaultSessionFactory(Telemetry);
            ISession connectedSession = await sessionFactory
                .CreateAsync(
                    configuration,
                    configuredEndpoint,
                    updateBeforeConnect: false,
                    checkDomain: true,
                    sessionName: ApplicationName,
                    sessionTimeout: ToUInt32Milliseconds(_options.EffectiveSessionTimeout),
                    identity: userIdentity,
                    preferredLocales: null,
                    ct: cancellationToken)
                .ConfigureAwait(false);

            sessionForCleanup = connectedSession;
            if (!connectedSession.Connected)
            {
                throw new InvalidOperationException("OPC UA Foundation session was created but is not connected.");
            }

            var ownedCertificates = CollectOwnedCertificates(applicationCertificate, userCertificate);
            var result = new OpcUaFoundationRuntimeSession(
                connectedSession,
                bindings,
                _options.EffectivePublishingInterval,
                configuration,
                certificateValidationHandler,
                userIdentity,
                ownedCertificates);

            sessionForCleanup = null;
            configuration = null;
            certificateValidationHandler = null;
            userIdentity = null;
            applicationCertificate = null;
            userCertificate = null;

            return result;
        }
        catch
        {
            sessionForCleanup?.Dispose();

            if (configuration is not null && certificateValidationHandler is not null)
            {
                configuration.CertificateValidator.CertificateValidation -= certificateValidationHandler;
            }

            userIdentity?.Dispose();
            DisposeDistinct(applicationCertificate, userCertificate);
            throw;
        }
    }

    private async Task<(UserIdentity Identity, X509Certificate2? UserCertificate)> CreateUserIdentityAsync(
        X509Certificate2? applicationCertificate,
        CancellationToken cancellationToken)
    {
        switch (_options.AuthenticationMode)
        {
            case OpcUaRuntimeAuthenticationMode.Anonymous:
                return (new UserIdentity(), null);

            case OpcUaRuntimeAuthenticationMode.UserName:
            {
                string password = await _securityMaterialProvider
                    .ResolveSecretAsync(
                        _options.PasswordSecretReference!,
                        cancellationToken)
                    .ConfigureAwait(false);

                byte[] utf8Password = Encoding.UTF8.GetBytes(password);
                try
                {
                    return (
                        new UserIdentity(
                            _options.UserName!,
                            utf8Password.AsSpan()),
                        null);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(utf8Password);
                }
            }

            case OpcUaRuntimeAuthenticationMode.Certificate:
            {
                X509Certificate2 certificate;
                if (applicationCertificate is not null &&
                    string.Equals(
                        _options.UserCertificateReference,
                        _options.ClientCertificateReference,
                        StringComparison.Ordinal))
                {
                    certificate = applicationCertificate;
                }
                else
                {
                    certificate = await _securityMaterialProvider
                        .ResolveCertificateAsync(
                            _options.UserCertificateReference!,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                ValidatePrivateKeyCertificate(certificate, "user identity");
                return (new UserIdentity(certificate), certificate);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(_options.AuthenticationMode));
        }
    }

    private static async Task<ApplicationConfiguration> CreateApplicationConfigurationAsync(
        X509Certificate2? applicationCertificate,
        CancellationToken cancellationToken)
    {
        var security = new SecurityConfiguration
        {
            AutoAcceptUntrustedCertificates = false,
            AddAppCertToTrustedStore = false,
            MaxRejectedCertificates = -1
        };

        if (applicationCertificate is not null)
        {
            security.ApplicationCertificates.Add(
                new CertificateIdentifier
                {
                    Certificate = applicationCertificate
                });
        }

        var configuration = new ApplicationConfiguration(Telemetry)
        {
            ApplicationName = ApplicationName,
            ApplicationUri = ResolveApplicationUri(applicationCertificate),
            ApplicationType = ApplicationType.Client,
            ClientConfiguration = new ClientConfiguration(),
            SecurityConfiguration = security,
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = DefaultOperationTimeoutMilliseconds
            }
        };

        await configuration
            .ValidateAsync(ApplicationType.Client, cancellationToken)
            .ConfigureAwait(false);

        await configuration.CertificateValidator
            .UpdateAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        return configuration;
    }

    private async Task<EndpointDescription> DiscoverAndSelectEndpointAsync(
        ApplicationConfiguration configuration,
        string? approvedPin,
        CancellationToken cancellationToken)
    {
        Uri discoveryUri = CoreClientUtils.GetDiscoveryUrl(_options.EndpointUrl.Trim());
        var endpointConfiguration = EndpointConfiguration.Create(configuration);
        endpointConfiguration.OperationTimeout = DefaultOperationTimeoutMilliseconds;

        using DiscoveryClient discoveryClient = await DiscoveryClient
            .CreateAsync(
                discoveryUri,
                endpointConfiguration,
                Telemetry,
                ct: cancellationToken)
            .ConfigureAwait(false);

        EndpointDescriptionCollection endpoints = await discoveryClient
            .GetEndpointsAsync(null, cancellationToken)
            .ConfigureAwait(false);

        MessageSecurityMode expectedSecurityMode = ParseSecurityMode(_options.SecurityMode);
        UserTokenType expectedUserTokenType = ToUserTokenType(_options.AuthenticationMode);
        string normalizedEndpointUrl = NormalizeEndpointUrl(_options.EndpointUrl);

        EndpointDescription[] matchingEndpoints = endpoints
            .Where(endpoint =>
                endpoint is not null &&
                string.Equals(
                    NormalizeEndpointUrl(endpoint.EndpointUrl),
                    normalizedEndpointUrl,
                    StringComparison.OrdinalIgnoreCase) &&
                endpoint.SecurityMode == expectedSecurityMode &&
                string.Equals(
                    endpoint.SecurityPolicyUri,
                    _options.SecurityPolicyUri.Trim(),
                    StringComparison.Ordinal) &&
                endpoint.UserIdentityTokens is not null &&
                endpoint.UserIdentityTokens.Any(policy => policy.TokenType == expectedUserTokenType))
            .Where(endpoint =>
                string.IsNullOrWhiteSpace(_options.ApprovedServerApplicationUri) ||
                string.Equals(
                    endpoint.Server?.ApplicationUri,
                    _options.ApprovedServerApplicationUri.Trim(),
                    StringComparison.Ordinal))
            .OrderByDescending(endpoint => endpoint.SecurityLevel)
            .ThenBy(endpoint => endpoint.Server?.ApplicationUri ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.EndpointUrl, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matchingEndpoints.Length == 0)
        {
            throw new InvalidOperationException(
                "No discovered OPC UA endpoint exactly matches the configured endpoint URL, security mode, " +
                "security policy, authentication mode and approved server identity. No security downgrade was attempted.");
        }

        EndpointDescription selected = matchingEndpoints[0];

        if (expectedSecurityMode != MessageSecurityMode.None)
        {
            if (selected.ServerCertificate is not { Length: > 0 })
            {
                throw new InvalidOperationException(
                    "The selected secure OPC UA endpoint did not provide a server certificate.");
            }

            if (approvedPin is null ||
                !OpcUaRuntimeProtocolSupport.CertificateMatchesSha256Pin(
                    selected.ServerCertificate,
                    approvedPin))
            {
                throw new InvalidOperationException(
                    "The selected OPC UA server certificate does not match the explicitly approved SHA-256 pin.");
            }
        }

        return selected;
    }

    private static CertificateValidationEventHandler CreatePinnedCertificateValidationHandler(
        string approvedPin) =>
        (_, args) =>
        {
            if (args.Error.StatusCode == StatusCodes.BadCertificateUntrusted &&
                args.Certificate is not null &&
                OpcUaRuntimeProtocolSupport.CertificateMatchesSha256Pin(
                    args.Certificate.RawData,
                    approvedPin))
            {
                args.Accept = true;
            }
        };

    private static string ResolveApplicationUri(X509Certificate2? certificate)
    {
        if (certificate is null)
        {
            return "urn:elitescada:opcua:client";
        }

        string? applicationUri = X509Utils
            .GetApplicationUrisFromCertificate(certificate)
            .FirstOrDefault(uri => !string.IsNullOrWhiteSpace(uri));

        if (string.IsNullOrWhiteSpace(applicationUri))
        {
            throw new InvalidOperationException(
                "The OPC UA client application certificate does not contain an ApplicationUri in SubjectAltName.");
        }

        return applicationUri;
    }

    private static void ValidatePrivateKeyCertificate(
        X509Certificate2? certificate,
        string role)
    {
        if (certificate is null)
        {
            throw new InvalidOperationException($"The resolved OPC UA {role} certificate is null.");
        }

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"The resolved OPC UA {role} certificate does not contain a private key.");
        }
    }

    private static MessageSecurityMode ParseSecurityMode(string securityMode)
    {
        if (string.Equals(securityMode.Trim(), "None", StringComparison.OrdinalIgnoreCase))
        {
            return MessageSecurityMode.None;
        }

        if (string.Equals(securityMode.Trim(), "Sign", StringComparison.OrdinalIgnoreCase))
        {
            return MessageSecurityMode.Sign;
        }

        if (string.Equals(securityMode.Trim(), "SignAndEncrypt", StringComparison.OrdinalIgnoreCase))
        {
            return MessageSecurityMode.SignAndEncrypt;
        }

        throw new ArgumentException(
            $"Unsupported OPC UA security mode '{securityMode}'.",
            nameof(securityMode));
    }

    private static UserTokenType ToUserTokenType(
        OpcUaRuntimeAuthenticationMode authenticationMode) =>
        authenticationMode switch
        {
            OpcUaRuntimeAuthenticationMode.Anonymous => UserTokenType.Anonymous,
            OpcUaRuntimeAuthenticationMode.UserName => UserTokenType.UserName,
            OpcUaRuntimeAuthenticationMode.Certificate => UserTokenType.Certificate,
            _ => throw new ArgumentOutOfRangeException(nameof(authenticationMode))
        };

    private static bool IsSecurityModeNone(string securityMode) =>
        string.Equals(securityMode.Trim(), "None", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEndpointUrl(string endpointUrl)
    {
        var trimmed = endpointUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return trimmed.TrimEnd('/');
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static uint ToUInt32Milliseconds(TimeSpan value)
    {
        double milliseconds = value.TotalMilliseconds;
        if (milliseconds <= 0 || milliseconds > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "OPC UA timeout must fit into the protocol UInt32 millisecond range.");
        }

        return checked((uint)Math.Ceiling(milliseconds));
    }

    private static IReadOnlyCollection<X509Certificate2> CollectOwnedCertificates(
        X509Certificate2? applicationCertificate,
        X509Certificate2? userCertificate)
    {
        var certificates = new List<X509Certificate2>(2);
        if (applicationCertificate is not null)
        {
            certificates.Add(applicationCertificate);
        }

        if (userCertificate is not null &&
            !ReferenceEquals(userCertificate, applicationCertificate))
        {
            certificates.Add(userCertificate);
        }

        return certificates;
    }

    private static void DisposeDistinct(
        X509Certificate2? first,
        X509Certificate2? second)
    {
        first?.Dispose();
        if (second is not null && !ReferenceEquals(first, second))
        {
            second.Dispose();
        }
    }
}

internal interface IOpcUaFoundationSessionAccessor
{
    ISession FoundationSession { get; }
}

internal sealed class OpcUaFoundationRuntimeSession :
    IOpcUaRuntimeSession,
    IOpcUaFoundationSessionAccessor
{
    private readonly ISession _session;
    private readonly IReadOnlyList<OpcUaRuntimeBinding> _bindings;
    private readonly IReadOnlyDictionary<Guid, NodeId> _nodesByTagId;
    private readonly TimeSpan _publishingInterval;
    private readonly ApplicationConfiguration _configuration;
    private readonly CertificateValidationEventHandler? _certificateValidationHandler;
    private readonly IDisposable? _userIdentity;
    private readonly IReadOnlyCollection<X509Certificate2> _ownedCertificates;
    private readonly object _notificationGate = new();

    private Channel<OpcUaRuntimeDataValue>? _notificationChannel;
    private int _subscriptionStarted;
    private int _disposed;

    public OpcUaFoundationRuntimeSession(
        ISession session,
        IReadOnlyCollection<OpcUaRuntimeBinding> bindings,
        TimeSpan publishingInterval,
        ApplicationConfiguration configuration,
        CertificateValidationEventHandler? certificateValidationHandler,
        IDisposable? userIdentity,
        IReadOnlyCollection<X509Certificate2> ownedCertificates)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(ownedCertificates);

        _session = session;
        _bindings = bindings.ToArray();
        _publishingInterval = publishingInterval;
        _configuration = configuration;
        _certificateValidationHandler = certificateValidationHandler;
        _userIdentity = userIdentity;
        _ownedCertificates = ownedCertificates;

        _nodesByTagId = _bindings.ToDictionary(
            binding => binding.Tag.Id,
            binding => NodeId.Parse(
                OpcUaRuntimeProtocolSupport.ResolveSessionNodeId(
                    binding.Node,
                    namespaceUri => _session.NamespaceUris.GetIndex(namespaceUri))));

        _session.KeepAlive += OnKeepAlive;
    }

    ISession IOpcUaFoundationSessionAccessor.FoundationSession
    {
        get
        {
            ThrowIfDisposed();
            return _session;
        }
    }

    public async Task<OpcUaRuntimeDataValue> ReadAsync(
        OpcUaRuntimeBinding binding,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(binding);

        NodeId nodeId = GetNode(binding);
        var nodesToRead = new ReadValueIdCollection
        {
            new ReadValueId
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value
            }
        };

        ReadResponse response = await _session
            .ReadAsync(
                null,
                0,
                TimestampsToReturn.Both,
                nodesToRead,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.Results is null || response.Results.Count != 1)
        {
            throw new InvalidOperationException(
                $"OPC UA read for TAG '{binding.Tag.Path}' returned an invalid result count.");
        }

        return MapDataValue(binding.Tag.Id, response.Results[0]);
    }

    public async Task WriteAsync(
        OpcUaRuntimeBinding binding,
        object value,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(value);

        NodeId nodeId = GetNode(binding);
        var nodesToWrite = new WriteValueCollection
        {
            new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue
                {
                    Value = value
                }
            }
        };

        WriteResponse response = await _session
            .WriteAsync(
                null,
                nodesToWrite,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.Results is null || response.Results.Count != 1)
        {
            throw new InvalidOperationException(
                $"OPC UA write for TAG '{binding.Tag.Path}' returned an invalid result count.");
        }

        StatusCode statusCode = response.Results[0];
        if (!StatusCode.IsGood(statusCode))
        {
            throw new InvalidOperationException(
                $"OPC UA write for TAG '{binding.Tag.Path}' failed with status '{statusCode}'.");
        }
    }

    public async IAsyncEnumerable<OpcUaRuntimeDataValue> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref _subscriptionStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "This OPC UA runtime session already has an active subscription stream.");
        }

        var channel = Channel.CreateBounded<OpcUaRuntimeDataValue>(
            new BoundedChannelOptions(CalculateNotificationCapacity(_bindings.Count))
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
                AllowSynchronousContinuations = false
            });

        lock (_notificationGate)
        {
            _notificationChannel = channel;
        }

        var subscription = new Subscription(_session.DefaultSubscription)
        {
            DisplayName = "EliteSCADA OPC UA Runtime",
            PublishingEnabled = true,
            PublishingInterval = _publishingInterval.TotalMilliseconds,
            KeepAliveCount = 5
        };

        _session.AddSubscription(subscription);

        try
        {
            await subscription.CreateAsync(cancellationToken).ConfigureAwait(false);

            foreach (OpcUaRuntimeBinding binding in _bindings)
            {
                var monitoredItem = new MonitoredItem(subscription.DefaultItem)
                {
                    StartNodeId = GetNode(binding),
                    AttributeId = Attributes.Value,
                    DisplayName = binding.Tag.Path,
                    SamplingInterval = binding.SamplingInterval.TotalMilliseconds,
                    QueueSize = binding.QueueSize,
                    DiscardOldest = binding.DiscardOldest
                };

                monitoredItem.Notification += (_, args) =>
                {
                    if (args.NotificationValue is not MonitoredItemNotification notification)
                    {
                        return;
                    }

                    OpcUaRuntimeDataValue observed = MapDataValue(
                        binding.Tag.Id,
                        notification.Value);

                    channel.Writer.TryWrite(observed);
                };

                subscription.AddItem(monitoredItem);
            }

            await subscription.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);

            await foreach (OpcUaRuntimeDataValue value in channel.Reader
                .ReadAllAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return value;
            }
        }
        finally
        {
            channel.Writer.TryComplete();
            lock (_notificationGate)
            {
                if (ReferenceEquals(_notificationChannel, channel))
                {
                    _notificationChannel = null;
                }
            }
        }
    }

    private void OnKeepAlive(ISession session, KeepAliveEventArgs args)
    {
        if (!ReferenceEquals(session, _session) || !ServiceResult.IsBad(args.Status))
        {
            return;
        }

        args.CancelKeepAlive = true;

        lock (_notificationGate)
        {
            _notificationChannel?.Writer.TryComplete(
                new InvalidOperationException(
                    $"OPC UA session keep-alive failed with status '{args.Status}'."));
        }
    }

    private NodeId GetNode(OpcUaRuntimeBinding binding)
    {
        if (!_nodesByTagId.TryGetValue(binding.Tag.Id, out var nodeId))
        {
            throw new KeyNotFoundException(
                $"OPC UA TAG '{binding.Tag.Id}' is not bound in the active runtime session.");
        }

        return nodeId;
    }

    private static OpcUaRuntimeDataValue MapDataValue(Guid tagId, DataValue dataValue)
    {
        ArgumentNullException.ThrowIfNull(dataValue);

        return new OpcUaRuntimeDataValue(
            tagId,
            dataValue.Value,
            MapStatusCode(dataValue.StatusCode),
            OpcUaRuntimeProtocolSupport.NormalizeProtocolTimestamp(dataValue.SourceTimestamp),
            OpcUaRuntimeProtocolSupport.NormalizeProtocolTimestamp(dataValue.ServerTimestamp));
    }

    private static TagQuality MapStatusCode(StatusCode statusCode)
    {
        if (StatusCode.IsGood(statusCode))
        {
            return TagQuality.Good;
        }

        if (StatusCode.IsUncertain(statusCode))
        {
            return TagQuality.Uncertain;
        }

        return TagQuality.Bad;
    }

    private static int CalculateNotificationCapacity(int bindingCount)
    {
        long capacity = Math.Max(256L, (long)bindingCount * 8L);
        return (int)Math.Min(capacity, 65_536L);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(OpcUaFoundationRuntimeSession));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _session.KeepAlive -= OnKeepAlive;

        lock (_notificationGate)
        {
            _notificationChannel?.Writer.TryComplete();
            _notificationChannel = null;
        }

        try
        {
            if (_session.Connected)
            {
                await _session.CloseAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            // Dispose must still release socket/certificate resources after a broken channel.
        }
        finally
        {
            _session.Dispose();

            if (_certificateValidationHandler is not null)
            {
                _configuration.CertificateValidator.CertificateValidation -= _certificateValidationHandler;
            }

            _userIdentity?.Dispose();
            foreach (X509Certificate2 certificate in _ownedCertificates)
            {
                certificate.Dispose();
            }
        }
    }
}
