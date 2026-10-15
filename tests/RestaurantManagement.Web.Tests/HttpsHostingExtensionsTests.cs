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

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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
    public void UseHttpsOnlyOutsideDevelopment_ProductionWithoutCertificate_ThrowsNamingCertificatePath()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Production, new());

        // Act
        var act = () => builder.UseHttpsOnlyOutsideDevelopment();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Https:CertificatePath*");
    }

    // Scenario 25
    [Fact]
    public void UseHttpsOnlyOutsideDevelopment_ProductionWithCertificateFileMissing_ThrowsNamingCertificatePath()
    {
        // Arrange
        var settings = CertificateSettings(GetFreePort());
        settings["Https:CertificatePath"] = Path.Combine(Path.GetTempPath(), "missing-api-certificate.pfx");
        var builder = CreateBuilder(Environments.Production, settings);

        // Act
        var act = () => builder.UseHttpsOnlyOutsideDevelopment();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Https:CertificatePath*");
    }

    // Scenario 26
    [Fact]
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
        await act.Should().NotThrowAsync();
    }

    // Scenario 27
    [Fact]
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(PingBody);
    }

    // Scenario 28
    [Fact]
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
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // Scenario 29
    [Fact]
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
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // Scenario 30
    [Fact]
    public async Task UseHttpsOnlyOutsideDevelopment_ProductionWithoutPort_ListensOnPort8443()
    {
        // Arrange
        var builder = CreateBuilder(Environments.Production, CertificateSettings(port: null));
        builder.UseHttpsOnlyOutsideDevelopment();
        await using var app = await StartAsync(builder);

        // Act
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;

        // Assert
        addresses.Should().ContainSingle().Which.Should().StartWith("https://").And.EndWith($":{DefaultHttpsPort}");
    }
}
