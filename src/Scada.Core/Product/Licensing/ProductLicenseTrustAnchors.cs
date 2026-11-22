using System.Security.Cryptography;

namespace Scada.Core.Product.Licensing;

public static class ProductLicenseTrustAnchors
{
    public const string ProductionKeyId = "elite-prod-2026-01";
    public const string ProductionPublicKeySha256 = "62244a1ca23f4a03d581e3df8fb46508264e29cd13d8747992710d3b0b4aac72";

    public const string OnlineLeaseKeyId = "scadivo-online-lease-2026-01";
    public const string OnlineLeasePublicKeySha256 = "26a33c58eeeb1329e8901b2722776545be5a8a266016cc44295bf115960d59aa";

    private const string OnlineLeasePublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEA2RVDtiq3QaAkoSSFayX6
wUKaN+m7ij90u09GsPnEMAg/ZEFA00RGihISBKQSKFL4tZ53006+klH5jRU5LSjJ
N9JQ1LDjB+U4JdEjR0GRtpdSrkUvdPiQEKyiusROU0bcENuu1I+lFPT0h/h1uJJq
HgbaIxuCKgAs0VFgTdVG1HmBHc0E9aPlFflN7TbrqkGDNsCEAokDKGxB4qY4UGgu
NMmuxoxesMXu15IjxCeSpERbfaddt4aPjaXS1HjtrCyoEjOpQa7HdXhcXe+hjG3R
/d/H+pecGr1IZXtFDLqf03/wL9P3ipCYI6dvMlMpNfRjxS3OreBrBu7+7XwTjMZz
HFg90wGlOW0Kdw9/uc4T9Oj1uolh3SRXZ5/3h0j5LvsI3R/U/XcxZcozkp9r/nS7
BPE/b6n2NJQsGwbf0EjF7oHo2er6KuDsTeRlOGh9SEC9S/4d4bbUfluwiIs9Q7sK
vEE9aroH5FICSL4d7CHi00PNqpSYBUpVTdNPkUjaHpaxAgMBAAE=
-----END PUBLIC KEY-----
""";

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

    /// <summary>Separate trust purpose: never add this key to the offline verification dictionary.</summary>
    public static RSA CreateOnlineLeasePublicKey()
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(OnlineLeasePublicKeyPem);
            if (rsa.KeySize != 3072 || !CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()),
                    Convert.FromHexString(OnlineLeasePublicKeySha256)))
                throw new CryptographicException("Online lease trust anchor does not match its pinned identity.");
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }
}
