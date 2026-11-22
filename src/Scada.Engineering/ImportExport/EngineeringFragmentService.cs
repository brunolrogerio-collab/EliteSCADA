using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.ImportExport;

public sealed record EngineeringFragmentExportRequest(
    IReadOnlyCollection<EngineeringFragmentEntityReference> Roots);

public sealed record EngineeringFragmentInspection(
    EngineeringFragmentEnvelope Envelope,
    EngineeringImportContext ImportContext);

public interface IEngineeringFragmentService
{
    byte[] Export(EngineeringFragmentExportRequest request);
    EngineeringFragmentInspection Inspect(ReadOnlyMemory<byte> fragmentBytes);
    EngineeringFragmentPlan Preview(ReadOnlyMemory<byte> fragmentBytes);
    ImportResult Apply(EngineeringFragmentPlan plan);
}

/// <summary>
/// R2 selected Engineering portability authority. .escadafrag is a bounded,
/// structured one-time transfer artifact. It never becomes project/package or
/// reusable-library authority and never mutates before a valid Preview.
/// </summary>
public sealed class EngineeringFragmentService : IEngineeringFragmentService
{
    public const string EnvelopePath = "fragment.json";
    public const int MaximumFragmentBytes = 64 * 1024 * 1024;
    public const int MaximumEnvelopeBytes = 8 * 1024 * 1024;
    public const int MaximumAssetBytes = 32 * 1024 * 1024;
    public const int MaximumEntries = 2048;

    private readonly IEngineeringExchangeService _exchange;
    private readonly IVisualAssetEngineeringRegistry _visualAssets;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public EngineeringFragmentService(
        IEngineeringExchangeService exchange,
        IVisualAssetEngineeringRegistry visualAssets)
    {
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _visualAssets = visualAssets ?? throw new ArgumentNullException(nameof(visualAssets));
    }

    public byte[] Export(EngineeringFragmentExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Roots is null || request.Roots.Count == 0)
            throw new InvalidDataException("Engineering Fragment export requires at least one selected root.");

        var source = _exchange.ExportPackage();
        var resolver = new EngineeringFragmentDependencyResolver(source);
        var roots = request.Roots
            .Select(resolver.ResolveRoot)
            .Distinct(EngineeringFragmentReferenceComparer.Instance)
            .OrderBy(reference => reference, EngineeringFragmentReferenceComparer.Instance)
            .ToArray();

        var closure = resolver.ResolveClosure(roots);
        var rootIdentities = roots
            .Select(EngineeringFragmentReferenceComparer.Identity)
            .ToHashSet();
        var dependencies = closure
            .Where(reference => !rootIdentities.Contains(EngineeringFragmentReferenceComparer.Identity(reference)))
            .OrderBy(reference => reference, EngineeringFragmentReferenceComparer.Instance)
            .ToArray();

        var engineering = EngineeringFragmentPackageBuilder.Build(source, closure);
        var envelope = new EngineeringFragmentEnvelope(
            EngineeringFragmentContract.Schema,
            EngineeringFragmentContract.SchemaVersion,
            DateTimeOffset.UtcNow,
            new EngineeringFragmentManifest(roots, dependencies),
            engineering);

        ValidateEnvelope(envelope);
        var envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, _json);
        if (envelopeBytes.LongLength > MaximumEnvelopeBytes)
            throw new InvalidDataException("Engineering Fragment envelope exceeds its safety limit.");

        var hashes = (engineering.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
            .Select(asset => asset.Sha256)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var payloads = _visualAssets.SnapshotPayloads(hashes);

        foreach (var asset in engineering.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
        {
            if (!payloads.TryGetValue(asset.Sha256, out var payload))
                throw new InvalidDataException(
                    $"Engineering Fragment visual asset '{asset.Key}' payload '{asset.Sha256}' is missing.");
            ValidateAssetPayload(asset, payload);
        }

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, EnvelopePath, envelopeBytes);
            foreach (var payload in payloads.Values.OrderBy(item => item.Sha256, StringComparer.OrdinalIgnoreCase))
                WriteEntry(archive, AssetPath(payload.Sha256), payload.Content);
        }

        if (output.Length > MaximumFragmentBytes)
            throw new InvalidDataException("Engineering Fragment exceeds its package safety limit.");

        return output.ToArray();
    }

    public EngineeringFragmentInspection Inspect(ReadOnlyMemory<byte> fragmentBytes)
    {
        if (fragmentBytes.IsEmpty)
            throw new InvalidDataException("Engineering Fragment is empty.");
        if (fragmentBytes.Length > MaximumFragmentBytes)
            throw new InvalidDataException("Engineering Fragment exceeds its package safety limit.");

        try
        {
            using var input = new MemoryStream(fragmentBytes.ToArray(), writable: false);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
            ValidateArchiveEntries(archive);

            var envelopeEntry = archive.GetEntry(EnvelopePath)
                ?? throw new InvalidDataException("Engineering Fragment envelope is missing.");
            var envelope = JsonSerializer.Deserialize<EngineeringFragmentEnvelope>(
                    ReadEntry(envelopeEntry, MaximumEnvelopeBytes),
                    _json)
                ?? throw new InvalidDataException("Engineering Fragment envelope is invalid.");

            ValidateEnvelope(envelope);
            ValidateNoResolvedSecrets(envelope.Engineering);

            var payloads = new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase);
            var expectedEntries = new HashSet<string>(StringComparer.Ordinal) { EnvelopePath };

            foreach (var asset in envelope.Engineering.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
            {
                var path = AssetPath(asset.Sha256);
                expectedEntries.Add(path);
                var entry = archive.GetEntry(path)
                    ?? throw new InvalidDataException(
                        $"Engineering Fragment visual asset '{asset.Key}' sidecar '{path}' is missing.");
                var payload = VisualAssetPayload.Create(asset.MediaType, ReadEntry(entry, MaximumAssetBytes));
                ValidateAssetPayload(asset, payload);
                payloads[payload.Sha256] = payload;
            }

            var unexpected = archive.Entries.FirstOrDefault(entry => !expectedEntries.Contains(entry.FullName));
            if (unexpected is not null)
                throw new InvalidDataException(
                    $"Engineering Fragment contains unexpected entry '{unexpected.FullName}'.");

            ValidateDeclaredClosure(envelope);
            return new EngineeringFragmentInspection(
                envelope,
                new EngineeringImportContext(payloads));
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException or OverflowException)
        {
            throw new InvalidDataException("Invalid EliteSCADA Engineering Fragment.", ex);
        }
    }

    public EngineeringFragmentPlan Preview(ReadOnlyMemory<byte> fragmentBytes)
    {
        var inspection = Inspect(fragmentBytes);
        return EngineeringFragmentPlanBuilder.Build(
            inspection,
            _exchange.ExportPackage(),
            _exchange,
            _json);
    }

    public ImportResult Apply(EngineeringFragmentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.CanApply)
        {
            var issues = plan.Items
                .Where(item => item.Operation is EngineeringFragmentPlanOperation.Conflict
                    or EngineeringFragmentPlanOperation.Unsupported)
                .Select(item => new ImportIssue(
                    item.Operation == EngineeringFragmentPlanOperation.Conflict
                        ? "FRAGMENT_CONFLICT"
                        : "FRAGMENT_UNSUPPORTED",
                    item.Reason ?? $"Fragment operation '{item.Operation}' cannot be applied.",
                    item.Source.EntityKind,
                    item.Source.EntityKey,
                    true))
                .Concat(plan.CanonicalPreview.Items.SelectMany(item => item.Issues).Where(issue => issue.IsError))
                .ToArray();

            return new ImportResult(
                ImportMode.CreateAndUpdate,
                0,
                0,
                plan.CanonicalPreview.SkipCount,
                issues);
        }

        var currentFingerprint = EngineeringFragmentPlanBuilder.Fingerprint(_exchange.ExportPackage(), _json);
        if (!string.Equals(currentFingerprint, plan.TargetFingerprint, StringComparison.Ordinal))
        {
            var first = plan.Items.FirstOrDefault();
            return new ImportResult(
                ImportMode.CreateAndUpdate,
                0,
                0,
                0,
                [new ImportIssue(
                    "FRAGMENT_PREVIEW_STALE",
                    "Engineering Working changed after Fragment Preview. Re-run Preview before Apply.",
                    first?.Source.EntityKind ?? ImportEntityKind.Tag,
                    first?.Source.EntityKey ?? "fragment",
                    true)]);
        }

        // Canonical Engineering Apply performs Preview again before the first
        // handler mutation, which is the final atomic validation gate.
        return _exchange.Apply(
            plan.Engineering,
            ImportMode.CreateAndUpdate,
            plan.ImportContext);
    }

    private static void ValidateDeclaredClosure(EngineeringFragmentEnvelope envelope)
    {
        var resolver = new EngineeringFragmentDependencyResolver(envelope.Engineering);
        var actualRoots = envelope.Manifest.Roots
            .Select(resolver.ResolveRoot)
            .Distinct(EngineeringFragmentReferenceComparer.Instance)
            .ToHashSet(EngineeringFragmentReferenceComparer.Instance);
        var declaredRoots = envelope.Manifest.Roots
            .ToHashSet(EngineeringFragmentReferenceComparer.Instance);
        if (!actualRoots.SetEquals(declaredRoots))
            throw new InvalidDataException("Engineering Fragment root manifest does not match canonical payload identities.");

        var actualClosure = resolver.ResolveClosure(actualRoots.ToArray());
        var rootIdentities = actualRoots
            .Select(EngineeringFragmentReferenceComparer.Identity)
            .ToHashSet();
        var actualDependencies = actualClosure
            .Where(reference => !rootIdentities.Contains(EngineeringFragmentReferenceComparer.Identity(reference)))
            .ToHashSet(EngineeringFragmentReferenceComparer.Instance);
        var declaredDependencies = (envelope.Manifest.Dependencies ?? Array.Empty<EngineeringFragmentEntityReference>())
            .ToHashSet(EngineeringFragmentReferenceComparer.Instance);

        if (!actualDependencies.SetEquals(declaredDependencies))
            throw new InvalidDataException(
                "Engineering Fragment manifest dependency closure does not match its canonical Engineering payload.");

        var represented = actualClosure
            .Select(EngineeringFragmentReferenceComparer.Identity)
            .ToHashSet();
        var extra = EngineeringFragmentDependencyResolver
            .EnumerateEntityReferences(envelope.Engineering)
            .FirstOrDefault(reference =>
                !represented.Contains(EngineeringFragmentReferenceComparer.Identity(reference)));
        if (extra is not null)
            throw new InvalidDataException(
                $"Engineering Fragment contains unrelated entity '{extra.EntityKind}:{extra.EntityKey}' outside the selected dependency closure.");
    }

    private static void ValidateEnvelope(EngineeringFragmentEnvelope envelope)
    {
        if (!string.Equals(envelope.Schema, EngineeringFragmentContract.Schema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported Engineering Fragment schema '{envelope.Schema}'.");
        if (envelope.SchemaVersion != EngineeringFragmentContract.SchemaVersion)
            throw new InvalidDataException(
                $"Unsupported Engineering Fragment schema version {envelope.SchemaVersion}.");
        if (envelope.Manifest is null)
            throw new InvalidDataException("Engineering Fragment manifest is missing.");
        if (envelope.Manifest.Version != EngineeringFragmentContract.SchemaVersion)
            throw new InvalidDataException(
                $"Unsupported Engineering Fragment manifest version {envelope.Manifest.Version}.");
        if (envelope.Manifest.Roots is null || envelope.Manifest.Roots.Count == 0)
            throw new InvalidDataException("Engineering Fragment requires at least one root.");
        if (envelope.Engineering is null)
            throw new InvalidDataException("Engineering Fragment canonical payload is missing.");
        if (!string.Equals(
                envelope.Engineering.Schema,
                EngineeringExchangeService.CurrentSchema,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Unsupported Engineering payload schema '{envelope.Engineering.Schema}' in Fragment.");
        if (envelope.Engineering.SchemaVersion is < 1 or > EngineeringExchangeService.CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported Engineering payload schema version {envelope.Engineering.SchemaVersion} in Fragment.");

        if ((envelope.Engineering.SecurityRoles?.Count ?? 0) != 0 ||
            (envelope.Engineering.SecurityScopes?.Count ?? 0) != 0 ||
            envelope.Engineering.AuthorityPolicyReference is not null ||
            envelope.Engineering.EngineeringLock is not null ||
            envelope.Engineering.StartupScreenId.HasValue)
        {
            throw new InvalidDataException(
                "Engineering Fragment cannot carry Security Authority, Engineering Lock or project startup authority.");
        }

        foreach (var reference in envelope.Manifest.Roots
                     .Concat(envelope.Manifest.Dependencies ?? Array.Empty<EngineeringFragmentEntityReference>()))
        {
            if (reference is null)
                throw new InvalidDataException("Engineering Fragment manifest contains a null entity reference.");
            if (reference.EntityId is not { } id || id == Guid.Empty)
                throw new InvalidDataException(
                    $"Engineering Fragment entity '{reference.EntityKind}:{reference.EntityKey}' requires stable canonical identity.");
            if (string.IsNullOrWhiteSpace(reference.EntityKey))
                throw new InvalidDataException("Engineering Fragment entity key/path cannot be empty.");
        }
    }

    private static void ValidateNoResolvedSecrets(EngineeringPackage package)
    {
        foreach (var source in package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
        {
            foreach (var setting in source.Settings ?? new Dictionary<string, string>())
            {
                if (LooksSecretLike(setting.Key, setting.Value))
                    throw new InvalidDataException(
                        $"Engineering Fragment Data Source '{source.Key}' contains a resolved secret-like setting '{setting.Key}'.");
            }

            foreach (var metadata in source.Metadata ?? new Dictionary<string, string>())
            {
                if (LooksSecretLike(metadata.Key, metadata.Value))
                    throw new InvalidDataException(
                        $"Engineering Fragment Data Source '{source.Key}' contains resolved secret-like metadata '{metadata.Key}'.");
            }
        }
    }

    private static bool LooksSecretLike(string key, string value)
    {
        var normalized = new string((key ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        if (normalized.Contains("password", StringComparison.Ordinal) ||
            normalized.Contains("passwd", StringComparison.Ordinal) ||
            normalized.Contains("pwd", StringComparison.Ordinal) ||
            normalized.Contains("secret", StringComparison.Ordinal) ||
            normalized.Contains("token", StringComparison.Ordinal) ||
            normalized.Contains("credential", StringComparison.Ordinal) ||
            normalized.Contains("apikey", StringComparison.Ordinal) ||
            normalized.Contains("privatekey", StringComparison.Ordinal) ||
            normalized.Contains("connectionstring", StringComparison.Ordinal))
            return true;

        var candidate = value ?? string.Empty;
        return candidate.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("passwd=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("pwd=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("token=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("apikey=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("api_key=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("clientsecret=", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("client_secret=", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateAssetPayload(
        VisualAssetEngineeringDto asset,
        VisualAssetPayload payload)
    {
        var actualHash = Convert.ToHexString(SHA256.HashData(payload.Content)).ToLowerInvariant();
        if (!actualHash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase) ||
            !payload.Sha256.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase) ||
            payload.ByteLength != asset.ByteLength ||
            !payload.MediaType.Equals(asset.MediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Engineering Fragment visual asset '{asset.Key}' payload does not match canonical metadata.");
        }
    }

    private static string AssetPath(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) ||
            hash.Length != 64 ||
            hash.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("Engineering Fragment visual asset requires a canonical SHA-256.");

        return $"assets/{hash.ToLowerInvariant()}";
    }

    private static void ValidateArchiveEntries(ZipArchive archive)
    {
        if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumEntries)
            throw new InvalidDataException("Engineering Fragment contains an invalid number of entries.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName) ||
                entry.FullName.Contains('\\') ||
                entry.FullName.StartsWith("/", StringComparison.Ordinal) ||
                entry.FullName.Split('/').Any(part => part is "." or "..") ||
                !names.Add(entry.FullName))
            {
                throw new InvalidDataException(
                    $"Unsafe or duplicate Engineering Fragment entry '{entry.FullName}'.");
            }
        }
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, int maximumBytes)
    {
        if (entry.Length < 0 || entry.Length > maximumBytes)
            throw new InvalidDataException(
                $"Engineering Fragment entry '{entry.FullName}' exceeds its safety limit.");

        using var source = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total = checked(total + read);
            if (total > maximumBytes)
                throw new InvalidDataException(
                    $"Engineering Fragment entry '{entry.FullName}' exceeds its safety limit.");
            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] bytes)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }
}
