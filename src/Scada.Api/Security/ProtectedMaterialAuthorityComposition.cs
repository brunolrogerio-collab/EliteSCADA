using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.Security;

namespace Scada.Api.Security;

public static class ProtectedMaterialAuthorityComposition
{
    public static void AddHostProtectedMaterialAuthority(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = ProtectedMaterialAuthorityOptions.FromConfiguration(
            builder.Configuration);
        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddSingleton<IMediaSourceCredentialReferenceStore>(_ =>
            new FileMediaSourceCredentialReferenceStore(options.StorePath));
        builder.Services.TryAddSingleton<MediaSourceProtectedCredentialService>();
        builder.Services.TryAddSingleton<IProtectedMaterialAuthority>(services =>
        {
            var key = options.LoadProtectionKey();
            try
            {
                return new FileHostProtectedMaterialAuthority(
                    options,
                    key,
                    services.GetRequiredService<Scada.Security.Audit.IAuditSink>());
            }
            finally
            {
                if (key is not null)
                    CryptographicOperations.ZeroMemory(key);
            }
        });
    }
}
