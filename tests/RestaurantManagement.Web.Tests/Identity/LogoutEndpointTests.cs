using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// POST api/auth/logout (public, cookie only) and POST api/auth/logout-all (signed in).
/// The session service is substituted; revocation rules are covered by the Identity unit tests.
/// </summary>
public class LogoutEndpointTests : IDisposable
{
    private const string LogoutUrl = "/api/auth/logout";
    private const string LogoutAllUrl = "/api/auth/logout-all";
    private const string CookieName = "refresh_token";
    private const string CookiePath = "/api/auth";
    private const string TabletCookieToken = "a2l0Y2hlbi10YWJsZXQtcmVmcmVzaC10b2tlbi0wMDAx";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

    private readonly AuthApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static HttpRequestMessage LogoutRequest(string? cookieToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LogoutUrl);
        if (cookieToken is not null)
            request.Headers.Add("Cookie", $"{CookieName}={cookieToken}");
        return request;
    }

    private HttpRequestMessage LogoutAllRequest(string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LogoutAllUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestAccessTokens.Create(_factory.Services, MariaId, role));
        request.Headers.Add("Cookie", $"{CookieName}={TabletCookieToken}");
        return request;
    }

    private static void ShouldClearRefreshCookie(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values).ShouldBeTrue("the response must clear the cookie");
        var cookie = SetCookieHeaderValue.ParseList(values!.ToList()).Single(c => c.Name == CookieName);
        cookie.Value.ToString().ShouldBeEmpty("the cleared cookie must carry no token");
        cookie.Path.ToString().ShouldBe(CookiePath, "the browser only removes a cookie sent with the same path it was set with");
        cookie.Expires.ShouldNotBeNull("a cleared cookie needs an expiry date so the browser deletes it");
        cookie.Expires.Value.ShouldBeLessThan(DateTimeOffset.UtcNow, "the expiry must be in the past so the browser deletes the cookie now");
    }

    // Scenario 60
    [Fact]
    [Trait("Rule", "O4")]
    public async Task LogoutAll_NoAccessToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, LogoutAllUrl));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "logging out of every device requires being signed in");
        await _factory.SessionService.DidNotReceive().LogoutAllAsync(Arg.Any<Guid>());
    }

    // Scenario 61
    [Fact]
    [Trait("Rule", "O1")]
    public async Task Logout_NoCookie_ReturnsNoContent()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(LogoutRequest(cookieToken: null));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, "logging out without a cookie is not an error and changes nothing");
        await _factory.SessionService.Received(1).LogoutAsync(Arg.Is<string?>(t => t == null));
    }

    // Scenario 62
    [Fact]
    [Trait("Rule", "O2")]
    public async Task Logout_WithCookie_SendsCookieTokenToSessionAndReturnsNoContent()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(LogoutRequest(TabletCookieToken));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, "logging out with a valid cookie succeeds without a body");
        await _factory.SessionService.Received(1).LogoutAsync(TabletCookieToken);
    }

    // Scenario 63
    [Fact]
    [Trait("Rule", "O2")]
    public async Task Logout_WithCookie_ClearsRefreshCookie()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(LogoutRequest(TabletCookieToken));

        // Assert
        ShouldClearRefreshCookie(response);
    }

    // Scenario 64
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Staff")]
    [Trait("Rule", "O5")]
    public async Task LogoutAll_SignedInWithAnyRole_ReturnsNoContent(string role)
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(LogoutAllRequest(role));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, "any signed-in role may log out of all its devices");
    }

    // Scenario 65
    [Fact]
    [Trait("Rule", "O5")]
    public async Task LogoutAll_SignedIn_RevokesTokensOfTheCallerOnly()
    {
        // Arrange
        var client = CreateClient();

        // Act
        await client.SendAsync(LogoutAllRequest("Staff"));

        // Assert
        await _factory.SessionService.Received(1).LogoutAllAsync(MariaId);
        await _factory.SessionService.DidNotReceive().LogoutAllAsync(Arg.Is<Guid>(id => id != MariaId));
    }

    // Scenario 66
    [Fact]
    [Trait("Rule", "O5")]
    public async Task LogoutAll_SignedIn_ClearsRefreshCookie()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(LogoutAllRequest("Owner"));

        // Assert
        ShouldClearRefreshCookie(response);
    }

    // Scenario 77
    [Fact]
    [Trait("Rule", "O6")]
    public async Task Logout_RefreshLimitExceeded_ReturnsTooManyRequests()
    {
        // Arrange: the Refresh policy allows one call per minute per address
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AuthRateLimit:Refresh:PermitLimit"] = "1",
                    ["AuthRateLimit:Refresh:WindowSeconds"] = "60",
                })));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var first = LogoutRequest(TabletCookieToken);
        first.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, "203.0.113.10");
        await client.SendAsync(first);
        var second = LogoutRequest(TabletCookieToken);
        second.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, "203.0.113.10");

        // Act
        var response = await client.SendAsync(second);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, "logout shares the refresh limit of one request per window here and this is the second");
    }
}
