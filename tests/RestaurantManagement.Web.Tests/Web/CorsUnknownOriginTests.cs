using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A browser page from an origin that is not configured gets no cross-origin permission.
/// </summary>
public class CorsUnknownOriginTests : IDisposable
{
    private const string RefreshUrl = "/api/auth/refresh";
    private const string AllowedOrigin = "https://app.cantinadanonna.com.br";
    private const string UnknownOrigin = "https://evil.example.com";

    private readonly AuthApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    [Trait("Rule", "W24")]
    public async Task Cors_PreflightFromUnknownOrigin_GetsNoCrossOriginPermission()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin })));
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, RefreshUrl);
        request.Headers.Add("Origin", UnknownOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse("an origin that is not configured must not be allowed to read answers");
        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse("an origin that is not configured must not be allowed to send cookies");
    }
}
