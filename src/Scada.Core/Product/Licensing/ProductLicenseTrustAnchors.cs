using System.Security.Cryptography;

namespace Scada.Core.Product.Licensing;

public static class ProductLicenseTrustAnchors
{
    public const string ProductionKeyId = "elite-prod-2026-01";
    public const string ProductionPublicKeySha256 = "62244a1ca23f4a03d581e3df8fb46508264e29cd13d8747992710d3b0b4aac72";

    private const string ProductionPublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAwi9N7igHBxvq8uktCkC0
+4ylZVXMJyk5pzgvHpqdkSqkEZr4UMeuxiWSpo8YztydY7LGnn/+XlLIKN2Z1WsS
zPVd6UCtyxIPNT1cyj4LbsXKPtO33sZ/n8PGmIJyhHBjofur2fuAcDiUeASCHRGA
z1XveZmOWs164RUf06pI+J/CWn3PcuyQepP+F38IYFtyFatFlBu2pB7Qwj6dJMWW
I5taeb/yOBk06CdqWMY7LQRTjWy3kkZGtAKzpg5OAKHONjSzSipfUa4IrOiz0ees
EvyW/zsnTafxS4vCixCHecainCUvBzMOKGCaIneDs682iWbjQkiPQsADPtQkEe4d
6MeJ1oa+TVzF1tdmImMW6mjaI4DOkbe/xcEjAPS4Nss+Or1g7OvcONuriIwNmb8G
0uFvx1ff1fufF6TqY/25efpZVHDRi0X809CpotoOUxHnb76B3yVv/USLEeAGC4NV
dSAubEMGJCxcjjMATLWZhvaoUjJxhBOSsxJZaWNbzWffAgMBAAE=
-----END PUBLIC KEY-----
""";

    public static RSA CreateProductionPublicKey()
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(ProductionPublicKeyPem);
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    public static Dictionary<string, RSA> CreateBuiltInVerificationKeys() =>
        new(StringComparer.Ordinal)
        {
            [ProductionKeyId] = CreateProductionPublicKey()
        };
}
