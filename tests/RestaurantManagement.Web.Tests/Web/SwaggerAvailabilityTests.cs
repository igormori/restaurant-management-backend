using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The API documentation (Swagger) is served only in Development.
/// </summary>
public class SwaggerAvailabilityTests : IDisposable
{
    private const string SwaggerDocumentUrl = "/swagger/v1/swagger.json";
    private const string StrongKey = "kitchen-signing-key-with-32-chars!";

    private readonly TestHttpsCertificate _certificate = new();
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        _disposables.ForEach(d => d.Dispose());
        _certificate.Dispose();
    }

    private HttpClient CreateClient(string environment, Dictionary<string, string?> settings)
    {
        // Program.cs reads Https:* while building, so it must come in as host settings.
        var httpsSettings = settings.Where(s => s.Key.StartsWith("Https:")).ToList();
        var otherSettings = settings.Except(httpsSettings);
        var factory = new AuthApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            httpsSettings.ForEach(s => builder.UseSetting(s.Key, s.Value));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(otherSettings));
        });
        _disposables.Add(factory);
        return factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    [Fact]
    [Trait("Rule", "W21")]
    public async Task SwaggerDocument_InDevelopment_IsServed()
    {
        // Arrange
        var client = CreateClient(Environments.Development, new());

        // Act
        var response = await client.GetAsync(SwaggerDocumentUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "developers read the API document in Development");
    }

    [Fact]
    [Trait("Rule", "W21")]
    public async Task SwaggerDocument_InProduction_IsNotServed()
    {
        // Arrange
        var client = CreateClient(Environments.Production, new()
        {
            ["Jwt:Key"] = StrongKey,
            ["Https:Port"] = "0",
            ["Https:CertificatePath"] = _certificate.FilePath,
            ["Https:CertificatePassword"] = TestHttpsCertificate.Password,
        });

        // Act
        var response = await client.GetAsync(SwaggerDocumentUrl);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldNotBe(HttpStatusCode.OK, "the API document must not be public in production");
        body.ShouldNotContain("\"openapi\"", Case.Insensitive, "the API document must not leak in production");
        body.ShouldNotContain("\"paths\"", Case.Insensitive, "the API document must not leak in production");
    }
}
