using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The HTTP side of per-device refresh tokens: the token travels only in the refresh_token cookie
/// (HttpOnly, Secure, SameSite=Strict, Path /api/auth) and never in a response body.
/// The session service is substituted; its rules are covered by the Identity unit tests.
/// </summary>
public class RefreshTokenCookieEndpointTests : IDisposable
{
    private const string LoginUrl = "/api/auth/login";
    private const string RefreshUrl = "/api/auth/refresh";
    private const string SelectOrganizationUrl = "/api/auth/select-organization";
    private const string CookieName = "refresh_token";
    private const string CookiePath = "/api/auth";
    private const string AllowedOrigin = "https://app.cantinadanonna.com.br";

    private const string IssuedToken = "a2l0Y2hlbi10YWJsZXQtcmVmcmVzaC10b2tlbi0wMDAx";
    private const string CurrentCookieToken = "Y3VycmVudC1jb29raWUtcmVmcmVzaC10b2tlbi0wMDAy";
    private const string TabletUserAgent = "Mozilla/5.0 (iPad; CPU OS 17_4 like Mac OS X) Safari/604.1";
    private const string TabletIp = "203.0.113.10";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly DateTime TokenExpiry = new(2026, 11, 14, 18, 30, 0, DateTimeKind.Utc);

    private readonly AuthApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static SessionResult IssuedSession() => new()
    {
        Auth = new AuthResponse
        {
            UserId = MariaId,
            Email = "maria@cantinadanonna.com.br",
            FirstName = "Maria",
            LastName = "Souza",
            Token = "access-pass-for-maria"
        },
        RefreshToken = IssuedToken,
        RefreshTokenExpiresAt = TokenExpiry
    };

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static HttpRequestMessage LoginRequest()
        => new(HttpMethod.Post, LoginUrl)
        {
            Content = JsonContent.Create(new { email = "maria@cantinadanonna.com.br", password = "Sup3r$ecret!" })
        };

    private static HttpRequestMessage RefreshRequest(string? cookieToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RefreshUrl);
        if (cookieToken is not null)
            request.Headers.Add("Cookie", $"{CookieName}={cookieToken}");
        return request;
    }

    private static SetCookieHeaderValue RefreshCookieOf(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values).ShouldBeTrue("the response must set a cookie");
        return SetCookieHeaderValue.ParseList(values!.ToList()).Single(c => c.Name == CookieName);
    }

    private static void ShouldBeSecureRefreshCookie(SetCookieHeaderValue cookie)
    {
        cookie.Value.ToString().ShouldBe(IssuedToken, "the refresh token travels in the cookie");
        cookie.HttpOnly.ShouldBeTrue("page scripts must not be able to read the refresh token");
        cookie.Secure.ShouldBeTrue("the refresh token must only be sent over HTTPS");
        cookie.SameSite.ShouldBe(SameSiteMode.Strict, "the refresh token must never be sent on requests started by other sites");
        cookie.Path.ToString().ShouldBe(CookiePath, "the cookie is only sent to the auth routes");
        cookie.Expires.ShouldNotBeNull("the cookie must expire together with the refresh token");
        cookie.Expires.Value.UtcDateTime.ShouldBe(TokenExpiry, TimeSpan.FromSeconds(1), "the cookie must expire when the refresh token does");
    }

    // Scenario 46
    [Fact]
    [Trait("Rule", "F2")]
    public async Task Refresh_NoCookie_ReturnsUnauthorized()
    {
        // Arrange
        _factory.SessionService.RefreshTokenAsync(Arg.Is<string?>(t => t == null))
            .Throws(new BusinessException("InvalidOrExpiredToken", 401));
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(RefreshRequest(cookieToken: null));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a refresh without a cookie has no session to renew and must be refused");
    }

    // Scenario 47
    [Fact]
    [Trait("Rule", "L2")]
    public async Task Login_ValidCredentials_SetsHttpOnlySecureStrictCookieOnAuthPath()
    {
        // Arrange
        _factory.SessionService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<ClientInfo>()).Returns(IssuedSession());
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(LoginRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "valid credentials log the person in");
        ShouldBeSecureRefreshCookie(RefreshCookieOf(response));
    }

    // Scenario 48
    [Fact]
    [Trait("Rule", "L1")]
    public async Task Login_ValidCredentials_ResponseBodyHasNoRefreshToken()
    {
        // Arrange
        _factory.SessionService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<ClientInfo>()).Returns(IssuedSession());
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(LoginRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "valid credentials log the person in");
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("access-pass-for-maria", customMessage: "the access token is returned in the body");
        body.ShouldNotContain(IssuedToken, customMessage: "the refresh token must never appear in the body, only in the cookie");
        body.ShouldNotContain("refreshToken", Case.Insensitive, "the body must not even have a refresh token field");
    }

    // Scenario 49
    [Fact]
    [Trait("Rule", "L15")]
    public async Task Login_FromKitchenTablet_PassesUserAgentAndIpAddressToSession()
    {
        // Arrange
        _factory.SessionService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<ClientInfo>()).Returns(IssuedSession());
        var client = CreateClient();
        var request = LoginRequest();
        request.Headers.UserAgent.ParseAdd(TabletUserAgent);
        request.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, TabletIp);

        // Act
        await client.SendAsync(request);

        // Assert
        await _factory.SessionService.Received(1).LoginAsync(
            Arg.Any<LoginRequest>(),
            Arg.Is<ClientInfo>(c => c.DeviceDescription == TabletUserAgent && c.IpAddress == TabletIp));
    }

    // Scenario 50
    [Fact]
    [Trait("Rule", "F1")]
    public async Task Refresh_WithCookie_SendsCookieTokenToSessionAndSetsReplacementCookie()
    {
        // Arrange
        _factory.SessionService.RefreshTokenAsync(CurrentCookieToken).Returns(IssuedSession());
        var client = CreateClient();

        // Act
        var response = await client.SendAsync(RefreshRequest(CurrentCookieToken));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "a valid refresh cookie renews the session");
        ShouldBeSecureRefreshCookie(RefreshCookieOf(response));
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(IssuedToken, customMessage: "the new refresh token must never appear in the body, only in the cookie");
    }

    // Scenario 51
    [Fact]
    [Trait("Rule", "S7")]
    public async Task SelectOrganization_SignedInWithCookie_SendsCookieTokenToSessionAndSetsReplacementCookie()
    {
        // Arrange
        _factory.SessionService.SelectOrganizationAsync(
                MariaId, Arg.Is<SelectOrganizationRequest>(r => r.OrganizationId == CantinaId), CurrentCookieToken, Arg.Any<ClientInfo>())
            .Returns(IssuedSession());
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, SelectOrganizationUrl)
        {
            Content = JsonContent.Create(new SelectOrganizationRequest { OrganizationId = CantinaId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestAccessTokens.Create(_factory.Services, MariaId, "Owner"));
        request.Headers.Add("Cookie", $"{CookieName}={CurrentCookieToken}");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "choosing a member organization succeeds");
        ShouldBeSecureRefreshCookie(RefreshCookieOf(response));
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(IssuedToken, customMessage: "the replacement refresh token must never appear in the body, only in the cookie");
    }

    // Scenario 52
    [Fact]
    [Trait("Rule", "T11")]
    [Trait("Rule", "W23")]
    public async Task Cors_PreflightFromAllowedOrigin_AllowsCredentials()
    {
        // Arrange
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = AllowedOrigin })));
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, RefreshUrl);
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).ShouldBeTrue("a configured origin must be told it is allowed");
        origins!.ShouldBe(new[] { AllowedOrigin });
        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials).ShouldBeTrue("the allowed origin may send cookies, which the refresh cookie needs");
        credentials!.ShouldBe(new[] { "true" });
    }
}
