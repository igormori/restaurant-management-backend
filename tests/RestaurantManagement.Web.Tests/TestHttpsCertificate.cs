using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Writes a throwaway self-signed PFX to a temp folder so tests can supply a certificate
/// through configuration without committing one.
/// </summary>
public sealed class TestHttpsCertificate : IDisposable
{
    public const string Password = "kitchen-cert-password";

    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public string FilePath { get; }

    public TestHttpsCertificate()
    {
        Directory.CreateDirectory(_directory);
        FilePath = System.IO.Path.Combine(_directory, "api.pfx");

        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        File.WriteAllBytes(FilePath, certificate.Export(X509ContentType.Pfx, Password));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
