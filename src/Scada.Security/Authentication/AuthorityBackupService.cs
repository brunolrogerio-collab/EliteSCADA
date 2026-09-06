using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scada.Security.Authentication;

public sealed record AuthorityBackupKdfDto(
    string Algorithm,
    int Version,
    int Iterations,
    string SaltBase64);

public sealed record AuthorityBackupEncryptionDto(
    string Algorithm,
    int Version,
    string NonceBase64,
    string TagBase64);

public sealed record AuthorityBackupEnvelope(
    string Format,
    int FormatVersion,
    DateTimeOffset CreatedAtUtc,
    AuthorityBackupKdfDto Kdf,
    AuthorityBackupEncryptionDto Encryption,
    string CiphertextBase64);

public sealed record AuthorityBackupUserSummary(
    Guid Id,
    string Username,
    string DisplayName,
    bool IsEnabled,
    IReadOnlyCollection<string> Roles);

public sealed record AuthorityBackupPreview(
    string Format,
    int FormatVersion,
    DateTimeOffset CreatedAtUtc,
    int UserCount,
    int EnabledUserCount,
    int EnabledAdministratorCount,
    IReadOnlyCollection<AuthorityBackupUserSummary> Users);

public sealed record AuthorityBackupOpenResult(
    AuthorityBackupPreview Preview,
    IReadOnlyCollection<LocalUserAccount> Accounts);

public sealed class AuthorityBackupService
{
    public const string CurrentFormat = "elitescada.authority-backup";
    public const int CurrentFormatVersion = 1;
    public const string CurrentPayloadSchema = "elitescada.authority";
    public const int CurrentPayloadSchemaVersion = 1;
    public const string PasswordKdfAlgorithm = "PBKDF2-SHA256";
    public const int PasswordKdfVersion = 1;
    public const int PasswordKdfIterations = 310_000;
    public const string EncryptionAlgorithm = "AES-256-GCM";
    public const int EncryptionVersion = 1;
    public const int MinimumBackupPasswordLength = 12;
    public const int MaximumBackupPasswordLength = 1024;

    private const int KdfSaltSize = 32;
    private const int EncryptionKeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string PasswordCredentialAlgorithm = "PBKDF2-SHA256";
    private const int PasswordCredentialAlgorithmVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = false
    };

    public string Export(
        IEnumerable<LocalUserAccount> accounts,
        string backupPassword,
        DateTimeOffset? createdAtUtc = null)
    {
        ValidateBackupPassword(backupPassword);
        var validatedAccounts = ValidateAndCopyAccounts(accounts);
        var createdAt = createdAtUtc ?? DateTimeOffset.UtcNow;
        if (createdAt == default)
            throw new ArgumentOutOfRangeException(nameof(createdAtUtc), "Backup creation timestamp is required.");

        var payload = new AuthorityBackupPayload(
            CurrentPayloadSchema,
            CurrentPayloadSchemaVersion,
            createdAt,
            validatedAccounts.Select(ToPayloadUser).ToArray());
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var salt = RandomNumberGenerator.GetBytes(KdfSaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var key = DeriveBackupKey(backupPassword, salt, PasswordKdfIterations);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        try
        {
            var kdf = new AuthorityBackupKdfDto(
                PasswordKdfAlgorithm,
                PasswordKdfVersion,
                PasswordKdfIterations,
                Convert.ToBase64String(salt));
            var encryptionWithoutTag = new AuthorityBackupEncryptionDto(
                EncryptionAlgorithm,
                EncryptionVersion,
                Convert.ToBase64String(nonce),
                string.Empty);
            var aad = BuildAssociatedData(
                CurrentFormat,
                CurrentFormatVersion,
                createdAt,
                kdf,
                encryptionWithoutTag);

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

            var envelope = new AuthorityBackupEnvelope(
                CurrentFormat,
                CurrentFormatVersion,
                createdAt,
                kdf,
                encryptionWithoutTag with { TagBase64 = Convert.ToBase64String(tag) },
                Convert.ToBase64String(ciphertext));
            return JsonSerializer.Serialize(envelope, JsonOptions);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public AuthorityBackupPreview Preview(string envelopeJson, string backupPassword) =>
        Open(envelopeJson, backupPassword).Preview;

    public AuthorityBackupOpenResult Open(string envelopeJson, string backupPassword)
    {
        ValidateBackupPassword(backupPassword);
        var envelope = ParseEnvelope(envelopeJson);
        ValidateEnvelopeContract(envelope);

        var kdf = envelope.Kdf!;
        var encryption = envelope.Encryption!;
        var salt = DecodeBase64(kdf.SaltBase64, KdfSaltSize, "Authority backup KDF salt");
        var nonce = DecodeBase64(encryption.NonceBase64, NonceSize, "Authority backup nonce");
        var tag = DecodeBase64(encryption.TagBase64, TagSize, "Authority backup authentication tag");
        var ciphertext = DecodeBase64(envelope.CiphertextBase64, expectedLength: null, "Authority backup ciphertext");
        if (ciphertext.Length == 0)
            throw new InvalidDataException("Authority backup ciphertext is empty.");

        var key = DeriveBackupKey(backupPassword, salt, kdf.Iterations);
        var plaintext = new byte[ciphertext.Length];
        try
        {
            var aad = BuildAssociatedData(
                envelope.Format,
                envelope.FormatVersion,
                envelope.CreatedAtUtc,
                kdf,
                encryption with { TagBase64 = string.Empty });

            try
            {
                using var aes = new AesGcm(key, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            }
            catch (CryptographicException exception)
            {
                throw new InvalidDataException(
                    "Authority backup authentication failed. The password is wrong or the backup was modified/corrupted.",
                    exception);
            }

            AuthorityBackupPayload payload;
            try
            {
                payload = JsonSerializer.Deserialize<AuthorityBackupPayload>(plaintext, JsonOptions)
                    ?? throw new InvalidDataException("Authority backup payload is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Authority backup payload is not valid JSON.", exception);
            }

            ValidatePayloadContract(payload, envelope);
            var accounts = ValidateAndCopyAccounts(payload.Users!.Select(FromPayloadUser));
            var summaries = accounts.Select(account => new AuthorityBackupUserSummary(
                account.Id,
                account.Username,
                account.DisplayName,
                account.IsEnabled,
                account.Roles.ToArray())).ToArray();
            var enabled = accounts.Count(account => account.IsEnabled);
            var enabledAdministrators = accounts.Count(IsEnabledAdministrator);
            var preview = new AuthorityBackupPreview(
                envelope.Format,
                envelope.FormatVersion,
                envelope.CreatedAtUtc,
                accounts.Count,
                enabled,
                enabledAdministrators,
                summaries);

            return new AuthorityBackupOpenResult(
                preview,
                accounts.Select(account => account.DeepCopy()).ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static void ValidateBackupPassword(string backupPassword)
    {
        if (string.IsNullOrWhiteSpace(backupPassword) || backupPassword.Length < MinimumBackupPasswordLength)
            throw new ArgumentException(
                $"Authority backup password must contain at least {MinimumBackupPasswordLength} characters.",
                nameof(backupPassword));
        if (backupPassword.Length > MaximumBackupPasswordLength)
            throw new ArgumentOutOfRangeException(nameof(backupPassword), "Authority backup password is too long.");
    }

    public static IReadOnlyCollection<LocalUserAccount> ValidateAndCopyAccounts(IEnumerable<LocalUserAccount> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        var result = accounts.Select(account =>
        {
            if (account is null)
                throw new InvalidDataException("Authority backup contains a null local identity.");
            return ValidateAndCopyAccount(account);
        }).ToArray();

        if (result.Length == 0)
            throw new InvalidDataException("Authority backup must contain at least one local identity.");

        if (result.Select(account => account.Id).Distinct().Count() != result.Length)
            throw new InvalidDataException("Authority backup contains duplicate local user IDs.");

        if (result.Select(account => account.NormalizedUsername)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != result.Length)
            throw new InvalidDataException("Authority backup contains duplicate local usernames.");

        if (!result.Any(IsEnabledAdministrator))
            throw new InvalidDataException(
                $"Authority backup must contain at least one enabled identity assigned '{LocalIdentityBootstrapService.InitialAdministratorRole}'.");

        return result;
    }

    private static LocalUserAccount ValidateAndCopyAccount(LocalUserAccount account)
    {
        if (account.Id == Guid.Empty)
            throw new InvalidDataException("Authority backup local user ID is required.");

        string normalizedUsername;
        try
        {
            normalizedUsername = LocalIdentityNormalization.NormalizeUsername(account.Username);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Authority backup username is invalid.", exception);
        }

        if (!string.Equals(normalizedUsername, account.NormalizedUsername, StringComparison.Ordinal))
            throw new InvalidDataException("Authority backup normalized username does not match username.");
        if (string.IsNullOrWhiteSpace(account.DisplayName) || account.DisplayName.Trim().Length > 300)
            throw new InvalidDataException("Authority backup display name is invalid.");
        if (account.CreatedAtUtc == default || account.UpdatedAtUtc == default || account.UpdatedAtUtc < account.CreatedAtUtc)
            throw new InvalidDataException("Authority backup local user timestamps are invalid.");
        if (account.Credential is null)
            throw new InvalidDataException("Authority backup password credential is missing.");

        IReadOnlyCollection<string> roles;
        try
        {
            roles = LocalIdentityNormalization.NormalizeRoles(account.Roles ?? Array.Empty<string>());
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Authority backup role assignments are invalid.", exception);
        }

        if (account.Credential.Iterations < 100_000 || account.Credential.Salt is null ||
            account.Credential.Hash is null || account.Credential.Salt.Length < 16 || account.Credential.Hash.Length != 32)
            throw new InvalidDataException("Authority backup password credential metadata is invalid.");

        return account with
        {
            Username = account.Username.Trim(),
            DisplayName = account.DisplayName.Trim(),
            Roles = roles.ToArray(),
            Credential = account.Credential.DeepCopy()
        };
    }

    private static bool IsEnabledAdministrator(LocalUserAccount account) =>
        account.IsEnabled && account.Roles.Any(role =>
            string.Equals(
                role,
                LocalIdentityBootstrapService.InitialAdministratorRole,
                StringComparison.OrdinalIgnoreCase));

    private static AuthorityBackupEnvelope ParseEnvelope(string envelopeJson)
    {
        if (string.IsNullOrWhiteSpace(envelopeJson))
            throw new InvalidDataException("Authority backup envelope is empty.");

        try
        {
            return JsonSerializer.Deserialize<AuthorityBackupEnvelope>(envelopeJson, JsonOptions)
                ?? throw new InvalidDataException("Authority backup envelope is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Authority backup envelope is not valid JSON.", exception);
        }
    }

    private static void ValidateEnvelopeContract(AuthorityBackupEnvelope envelope)
    {
        if (!string.Equals(envelope.Format, CurrentFormat, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported Authority backup format '{envelope.Format}'.");
        if (envelope.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported Authority backup format version {envelope.FormatVersion}.");
        if (envelope.CreatedAtUtc == default)
            throw new InvalidDataException("Authority backup creation timestamp is required.");
        if (envelope.Kdf is null)
            throw new InvalidDataException("Authority backup password KDF metadata is missing.");
        if (envelope.Encryption is null)
            throw new InvalidDataException("Authority backup encryption metadata is missing.");
        if (!string.Equals(envelope.Kdf.Algorithm, PasswordKdfAlgorithm, StringComparison.Ordinal) ||
            envelope.Kdf.Version != PasswordKdfVersion)
            throw new InvalidDataException("Unsupported Authority backup password KDF.");
        if (envelope.Kdf.Iterations != PasswordKdfIterations)
            throw new InvalidDataException("Unsupported Authority backup password KDF parameters for format v1.");
        if (!string.Equals(envelope.Encryption.Algorithm, EncryptionAlgorithm, StringComparison.Ordinal) ||
            envelope.Encryption.Version != EncryptionVersion)
            throw new InvalidDataException("Unsupported Authority backup encryption algorithm.");
    }

    private static void ValidatePayloadContract(AuthorityBackupPayload payload, AuthorityBackupEnvelope envelope)
    {
        if (!string.Equals(payload.Schema, CurrentPayloadSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported Authority backup payload schema '{payload.Schema}'.");
        if (payload.SchemaVersion != CurrentPayloadSchemaVersion)
            throw new InvalidDataException($"Unsupported Authority backup payload schema version {payload.SchemaVersion}.");
        if (payload.ExportedAtUtc == default || payload.ExportedAtUtc != envelope.CreatedAtUtc)
            throw new InvalidDataException("Authority backup payload timestamp does not match its authenticated envelope.");
        if (payload.Users is null)
            throw new InvalidDataException("Authority backup payload user collection is missing.");
    }

    private static AuthorityBackupUserPayload ToPayloadUser(LocalUserAccount account) => new(
        account.Id,
        account.Username,
        account.NormalizedUsername,
        account.DisplayName,
        account.IsEnabled,
        account.Roles.ToArray(),
        new AuthorityBackupPasswordCredentialPayload(
            PasswordCredentialAlgorithm,
            PasswordCredentialAlgorithmVersion,
            account.Credential.Iterations,
            Convert.ToBase64String(account.Credential.Salt),
            Convert.ToBase64String(account.Credential.Hash)),
        account.CreatedAtUtc,
        account.UpdatedAtUtc);

    private static LocalUserAccount FromPayloadUser(AuthorityBackupUserPayload? user)
    {
        if (user is null)
            throw new InvalidDataException("Authority backup payload contains a null local identity.");
        if (user.Credential is null)
            throw new InvalidDataException("Authority backup payload password credential is missing.");
        if (!string.Equals(user.Credential.Algorithm, PasswordCredentialAlgorithm, StringComparison.Ordinal) ||
            user.Credential.Version != PasswordCredentialAlgorithmVersion)
            throw new InvalidDataException("Unsupported local password credential algorithm in Authority backup.");

        var salt = DecodeBase64(user.Credential.SaltBase64, expectedLength: null, "Local password credential salt");
        var hash = DecodeBase64(user.Credential.HashBase64, 32, "Local password credential hash");
        return new LocalUserAccount(
            user.Id,
            user.Username,
            user.NormalizedUsername,
            user.DisplayName,
            user.IsEnabled,
            user.Roles ?? Array.Empty<string>(),
            new PasswordCredential(salt, hash, user.Credential.Iterations),
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }

    private static byte[] DeriveBackupKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            EncryptionKeySize);

    private static byte[] BuildAssociatedData(
        string format,
        int formatVersion,
        DateTimeOffset createdAtUtc,
        AuthorityBackupKdfDto kdf,
        AuthorityBackupEncryptionDto encryption) =>
        Encoding.UTF8.GetBytes(string.Join(
            "\n",
            format,
            formatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            createdAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            kdf.Algorithm,
            kdf.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            kdf.Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            kdf.SaltBase64,
            encryption.Algorithm,
            encryption.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            encryption.NonceBase64));

    private static byte[] DecodeBase64(string? value, int? expectedLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{field} is required.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException($"{field} is not valid Base64.", exception);
        }

        if (expectedLength.HasValue && bytes.Length != expectedLength.Value)
            throw new InvalidDataException($"{field} has an invalid length.");
        return bytes;
    }

    private sealed record AuthorityBackupPayload(
        string Schema,
        int SchemaVersion,
        DateTimeOffset ExportedAtUtc,
        IReadOnlyCollection<AuthorityBackupUserPayload?>? Users);

    private sealed record AuthorityBackupUserPayload(
        Guid Id,
        string Username,
        string NormalizedUsername,
        string DisplayName,
        bool IsEnabled,
        IReadOnlyCollection<string>? Roles,
        AuthorityBackupPasswordCredentialPayload? Credential,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    private sealed record AuthorityBackupPasswordCredentialPayload(
        string Algorithm,
        int Version,
        int Iterations,
        string SaltBase64,
        string HashBase64);
}
