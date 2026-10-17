using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestaurantManagement.Web.Hosting;

namespace RestaurantManagement.Web.Tests;

public class HttpsHostingExtensionsTests : IDisposable
{
    private const string PingPath = "/ping";
    private const string PingBody = "pong";
    private const string LoopbackAddress = "127.0.0.1";
    private const string DefaultHttpsPort = "8443";

    private readonly TestHttpsCertificate _certificate = new();

    public void Dispose()
    {
        _certificate.Dispose();
    }

    // Ports come from 20000–29999, outside the operating system's random range (macOS 49152+,
    // Linux 32768+) that Docker also maps test database containers into. A port taken from the
    // random range could be handed to a Postgres container between this check and the server
    // starting, and database tests running in parallel then reached this HTTPS server instead.
    private const int FirstTestPort = 20000;
    private const int TestPortCount = 10000;

    private static int GetFreePort()
    {
        var offset = Random.Shared.Next(TestPortCount);
        for (var attempt = 0; attempt < TestPortCount; attempt++)
        {
            var port = FirstTestPort + (offset + attempt) % TestPortCount;
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
                // In use: try the next one.
            }
        }

        throw new InvalidOperationException($"No free port between {FirstTestPort} and {FirstTestPort + TestPortCount - 1}.");
    }

    private Dictionary<string, string?> CertificateSettings(int? port) => new()
    {
        ["Https:Port"] = port?.ToString(),
        ["Https:CertificatePath"] = _certificate.FilePath,
        ["Https:CertificatePassword"] = TestHttpsCertificate.Password,
    };

    private static WebApplicationBuilder CreateBuilder(string environment, Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }

    private static async Task<WebApplication> StartAsync(WebApplicationBuilder builder)
    {
        var app = builder.Build();
        app.MapGet(PingPath, () => PingBody);
        await app.StartAsync();
        return app;
    }

    private static HttpClient CreateTrustingClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        return new HttpClient(handler);
    }

    // Scenario 24
    [Fact]
    [Trait("Rule", "W12")]
    public void UseHttpsOnlyOutsideDevelopment_ProductionWithoutCertificate_ThrowsNamingCertificatePath()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Production, new());

        // Act
        var act = () => builder.UseHttpsOnlyOutsideDevelopment();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Https:CertificatePath*", "outside Development the app must refuse to start without a usable certificate and name the setting to fix");
    }

    // Scenario 25
    [Fact]
    [Trait("Rule", "W13")]
    public void UseHttpsOnlyOutsideDevelopment_ProductionWithCertificateFileMissing_ThrowsNamingCertificatePath()
    {
        // Arrange
        var settings = CertificateSettings(GetFreePort());
        settings["Https:CertificatePath"] = Path.Combine(Path.GetTempPath(), "missing-api-certificate.pfx");
        var builder = CreateBuilder(Environments.Production, settings);

        // Act
        var act = () => builder.UseHttpsOnlyOutsideDevelopment();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Https:CertificatePath*", "outside Development the app must refuse to start without a usable certificate and name the setting to fix");
    }

    // Scenario 26
    [Fact]
    [Trait("Rule", "W16")]
    public async Task UseHttpsOnlyOutsideDevelopment_DevelopmentWithoutCertificate_StartsWithoutThrowing()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Development, new() { ["urls"] = $"http://{LoopbackAddress}:0" });

        // Act
        var act = async () =>
        {
            builder.UseHttpsOnlyOutsideDevelopment();
            await using var app = await StartAsync(builder);
        };

        // Assert
        await act.Should().NotThrowAsync("in Development the app may start without a certificate so developers can work locally");
    }

    // Scenario 27
    [Fact]
    [Trait("Rule", "W14")]
    public async Task UseHttpsOnlyOutsideDevelopment_ProductionWithCertificate_ServesHttps()
    {
        // Arrange
        var port = GetFreePort();
        var builder = CreateBuilder(Environments.Production, CertificateSettings(port));
        builder.UseHttpsOnlyOutsideDevelopment();
        await using var app = await StartAsync(builder);
        using var client = CreateTrustingClient();

        // Act
        var response = await client.GetAsync($"https://{LoopbackAddress}:{port}{PingPath}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "outside Development the API is served over HTTPS with the configured certificate");
        (await response.Content.ReadAsStringAsync()).Should().Be(PingBody, "the HTTPS endpoint must answer normally");
    }

    // Scenario 28
    [Fact]
    [Trait("Rule", "W14")]
    public async Task UseHttpsOnlyOutsideDevelopment_ProductionWithCertificate_RefusesPlainHttpOnSamePort()
    {
        // Arrange
        var port = GetFreePort();
        var builder = CreateBuilder(Environments.Production, CertificateSettings(port));
        builder.UseHttpsOnlyOutsideDevelopment();
        await using var app = await StartAsync(builder);
        using var client = CreateTrustingClient();

        // Act
        var act = () => client.GetAsync($"http://{LoopbackAddress}:{port}{PingPath}");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>("outside Development plain HTTP must not be served at all");
    }

    // Scenario 29
    [Fact]
    [Trait("Rule", "W14")]
    public async Task UseHttpsOnlyOutsideDevelopment_ProductionWithHttpUrlsConfigured_DoesNotServePlainHttp()
    {
        // Arrange
        var httpsPort = GetFreePort();
        var httpPort = GetFreePort();
        var settings = CertificateSettings(httpsPort);
        settings["urls"] = $"http://{LoopbackAddress}:{httpPort}";
        var builder = CreateBuilder(Environments.Production, settings);
        builder.UseHttpsOnlyOutsideDevelopment();
        await using var app = await StartAsync(builder);
        using var client = CreateTrustingClient();

        // Act
        var act = () => client.GetAsync($"http://{LoopbackAddress}:{httpPort}{PingPath}");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>("outside Development plain HTTP must not be served at all");
    }

    // Scenario 30
    [Fact]
    [Trait("Rule", "W15")]
    public async Task UseHttpsOnlyOutsideDevelopment_ProductionWithoutPort_ListensOnPort8443()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Production, CertificateSettings(port: null));
        builder.UseHttpsOnlyOutsideDevelopment();
        await using var app = await StartAsync(builder);

        // Act
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;

        // Assert
        addresses.Should().ContainSingle().Which.Should().StartWith("https://", "the only address is HTTPS").And.EndWith($":{DefaultHttpsPort}", "without a configured port the API listens on the default HTTPS port");
    }
}
