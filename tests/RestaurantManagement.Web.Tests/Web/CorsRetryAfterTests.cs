using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Models.Sessions;
using RestaurantManagement.Modules.Identity.Models.Registration;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A page on an allowed origin can read how long to wait after a 429 (Too Many Requests).
/// </summary>
public class CorsRetryAfterTests : IDisposable
{
    private const string RegisterUrl = "/api/auth/register";
    private const string AllowedOrigin = "http://localhost:5173";
    private const string ClientIp = "203.0.113.20";

    private readonly AuthApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    [Trait("Rule", "W25")]
    public async Task Register_RateLimitedFromAllowedOrigin_ExposesRetryAfter()
    {
        // Arrange
        _factory.RegistrationService.RegisterAsync(Arg.Any<RegisterRequest>())
            .Returns(new AuthResponse { Email = "marina@cantinadanonna.com.br", FirstName = "Marina", LastName = "Rossi" });
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins:0"] = AllowedOrigin,
                    ["AuthRateLimit:Register:PermitLimit"] = "1",
                    ["AuthRateLimit:Register:WindowSeconds"] = "60",
                })));
        var client = factory.CreateClient();
        await client.SendAsync(RegisterRequest());

        // Act
        var response = await client.SendAsync(RegisterRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, "the second registration in the window is limited");
        response.Headers.TryGetValues("Access-Control-Expose-Headers", out var exposed).ShouldBeTrue("an allowed origin must be told which headers it may read");
        string.Join(",", exposed!).ShouldContain("Retry-After", customMessage: "the page needs Retry-After to know how long to wait before registering again");
    }

    private static HttpRequestMessage RegisterRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RegisterUrl)
        {
            Content = JsonContent.Create(new
            {
                email = "marina@cantinadanonna.com.br",
                password = "Sup3r$ecret!",
                firstName = "Marina",
                lastName = "Rossi"
            })
        };
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, ClientIp);
        return request;
    }
}
