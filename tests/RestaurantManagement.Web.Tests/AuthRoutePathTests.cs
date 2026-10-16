using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Browsers match cookie paths case-sensitively, and the refresh_token cookie has Path /api/auth.
/// Generated clients call the route template as written, so it must start with that exact path.
/// </summary>
public class AuthRoutePathTests : IDisposable
{
    private const string CookiePath = "/api/auth";

    private readonly AuthApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Theory]
    [InlineData("login")]
    [InlineData("refresh")]
    [InlineData("logout")]
    [InlineData("select-organization")]
    public void AuthEndpoint_RouteTemplate_StartsWithRefreshCookiePathExactly(string action)
    {
        // Arrange
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

        // Act
        var template = endpoints.SingleOrDefault(raw =>
            raw.EndsWith("/" + action, StringComparison.OrdinalIgnoreCase)
            && raw.StartsWith("api/auth", StringComparison.OrdinalIgnoreCase));

        // Assert
        template.ShouldNotBeNull();
        ("/" + template).ShouldStartWith(CookiePath + "/", Case.Sensitive);
    }
}
