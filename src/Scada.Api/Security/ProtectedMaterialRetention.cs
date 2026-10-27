using System.Security.Cryptography;
using System.Text.Json;

namespace Scada.Api.Security;

/// <summary>
/// Optional host maintenance; exposes neither a material catalogue nor plaintext. The owning
/// consumer must hold exclusive cross-process write ownership of the scope throughout collection.
/// A readable, committed reference is required before any obsolete material may be removed.
/// </summary>
public interface IProtectedMaterialRetentionAuthority
{
    ValueTask<int> CollectUnreferencedAsync(ProtectedMaterialScope scope, string retainedReference,
        ProtectedMaterialMutationContext mutation, CancellationToken cancellationToken = default);
}

public sealed partial class FileHostProtectedMaterialAuthority
{
    public async ValueTask<int> CollectUnreferencedAsync(ProtectedMaterialScope scope, string retainedReference,
        ProtectedMaterialMutationContext mutation, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(mutation);
        mutation.DemandAuthorized();
        retainedReference = NormalizeReference(retainedReference);
        // Refuse a missing/wrong-key/wrong-scope anchor before considering any deletion.
        await using (var retained = await ResolveAsync(scope, retainedReference, cancellationToken)) { }
        var scopeHash = scope.ComputeScopeHash();
        var scopeHashHex = Convert.ToHexString(scopeHash);
        try
        {
            var deleted = 0;
            foreach (var file in Directory.EnumerateFiles(_rootPath, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProtectedMaterialEnvelope? envelope;
                try
                {
                    var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                    envelope = JsonSerializer.Deserialize<ProtectedMaterialEnvelope>(bytes, Json);
                }
                catch (JsonException) { continue; } // Unknown/corrupt material is never swept blindly.
                if (envelope is null || !EnvelopeShapeIsValid(envelope) ||
                    envelope.ScopeHash != scopeHashHex ||
                    envelope.Reference == retainedReference ||
                    Path.GetFileName(file) != envelope.Reference[ReferencePrefix.Length..] + ".json")
                    continue;
                // Existing scoped deletion authenticates ciphertext/key and audits each removal.
                await DeleteAsync(scope, envelope.Reference, mutation, cancellationToken);
                deleted++;
                if (deleted == 128) break; // Bounded deletion batch; a later pass continues.
            }
            return deleted;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw StoreUnavailable();
        }
        finally { CryptographicOperations.ZeroMemory(scopeHash); }
    }
}
