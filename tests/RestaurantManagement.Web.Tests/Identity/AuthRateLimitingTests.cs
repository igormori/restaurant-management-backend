using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using Sentry;

namespace RestaurantManagement.Web.Tests;

public class AuthRateLimitingTests : IDisposable
{
    private const string LoginUrl = "/api/auth/login";
    private const string RegisterUrl = "/api/auth/register";
    private const string IpA = "203.0.113.10";
    private const string IpB = "203.0.113.11";
    private const string ProxyIp = "10.0.0.1";
    private const string ForwardedForHeader = "X-Forwarded-For";
    private const string PortugueseCulture = "pt-PT";
    private const string EnglishMessage = "Too many requests. Please try again later.";
    private const string PortugueseMessage = "Muitas solicitações. Tente novamente mais tarde.";

    private readonly AuthApiFactory _factory = new();
    private readonly List<IDisposable> _disposables = new();

    public AuthRateLimitingTests()
    {
        _factory.SessionService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<ClientInfo>())
            .Returns(new SessionResult
            {
                Auth = new AuthResponse { Email = "user@example.com", FirstName = "A", LastName = "B" },
                RefreshToken = "cmF0ZS1saW1pdC10ZXN0LXJlZnJlc2gtdG9rZW4=",
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30)
            });
        _factory.RegistrationService.RegisterAsync(Arg.Any<RegisterRequest>())
            .Returns(new AuthResponse { Email = "user@example.com", FirstName = "A", LastName = "B" });
    }

    public void Dispose()
    {
        _disposables.ForEach(d => d.Dispose());
        _factory.Dispose();
    }

    private HttpClient CreateClient(Dictionary<string, string?>? settings = null, IHub? hub = null)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(settings ?? new Dictionary<string, string?>()));
            builder.ConfigureTestServices(services =>
            {
                if (hub is null) return;
                services.RemoveAll<IHub>();
                services.AddSingleton(hub);
            });
        });
        _disposables.Add(factory);
        return factory.CreateClient();
    }

    private static Dictionary<string, string?> LoginLimit(int permitLimit) => new()
    {
        ["AuthRateLimit:Login:PermitLimit"] = permitLimit.ToString(),
        ["AuthRateLimit:Login:WindowSeconds"] = "60",
    };

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string remoteIp, string? forwardedFor = null, string? culture = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl)
        {
            Content = JsonContent.Create(new { email = "user@example.com", password = "Sup3r$ecret!" })
        };
        request.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, remoteIp);
        if (forwardedFor is not null) request.Headers.Add(ForwardedForHeader, forwardedFor);
        if (culture is not null) request.Headers.Add("Accept-Language", culture);
        return client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ExhaustLoginLimitAsync(HttpClient client, string remoteIp, int limit)
    {
        HttpResponseMessage last = null!;
        for (var i = 0; i < limit; i++) last = await PostLoginAsync(client, remoteIp);
        return last;
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_LimitExceeded_Returns429WithRetryAfter()
    {
        var client = CreateClient(LoginLimit(2));
        await ExhaustLoginLimitAsync(client, IpA, 2);

        var response = await PostLoginAsync(client, IpA);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 60);
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_LimitExceededWithPortugueseCulture_ReturnsLocalizedMessage()
    {
        var client = CreateClient(LoginLimit(1));
        await ExhaustLoginLimitAsync(client, IpA, 1);

        var response = await PostLoginAsync(client, IpA, culture: PortugueseCulture);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["message"].Should().Be(PortugueseMessage);
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_LimitExceededWithoutCulture_ReturnsEnglishMessage()
    {
        var client = CreateClient(LoginLimit(1));
        await ExhaustLoginLimitAsync(client, IpA, 1);

        var response = await PostLoginAsync(client, IpA);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["message"].Should().Be(EnglishMessage);
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Register_LoginLimitExhausted_ReturnsOk()
    {
        var client = CreateClient(LoginLimit(1));
        await ExhaustLoginLimitAsync(client, IpA, 1);
        var request = new HttpRequestMessage(HttpMethod.Post, RegisterUrl)
        {
            Content = JsonContent.Create(new
            {
                email = "new@example.com",
                password = "Sup3r$ecret!",
                firstName = "New",
                lastName = "User"
            })
        };
        request.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, IpA);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_OtherIpExhaustedLimit_ReturnsOk()
    {
        var client = CreateClient(LoginLimit(1));
        await ExhaustLoginLimitAsync(client, IpA, 1);

        var response = await PostLoginAsync(client, IpB);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "L17")]
    public async Task Login_ForwardedForFromUntrustedSender_PartitionsByRemoteIp()
    {
        var client = CreateClient(LoginLimit(1));
        await PostLoginAsync(client, IpA, forwardedFor: "198.51.100.1");

        var response = await PostLoginAsync(client, IpA, forwardedFor: "198.51.100.2");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    [Trait("Rule", "L17")]
    public async Task Login_ForwardedForFromTrustedProxy_PartitionsByForwardedIp()
    {
        var settings = LoginLimit(1);
        settings["AuthRateLimit:TrustedProxies:0"] = ProxyIp;
        var client = CreateClient(settings);
        await PostLoginAsync(client, ProxyIp, forwardedFor: "198.51.100.1");

        var otherClient = await PostLoginAsync(client, ProxyIp, forwardedFor: "198.51.100.2");
        var sameClient = await PostLoginAsync(client, ProxyIp, forwardedFor: "198.51.100.1");

        otherClient.StatusCode.Should().Be(HttpStatusCode.OK);
        sameClient.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_RateLimited_DoesNotCallLoginService()
    {
        var client = CreateClient(LoginLimit(1));
        await ExhaustLoginLimitAsync(client, IpA, 1);
        _factory.SessionService.ClearReceivedCalls();

        await PostLoginAsync(client, IpA);

        await _factory.SessionService.DidNotReceive().LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<ClientInfo>());
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_RepeatedRejectionsInWindow_SendsOneSentryWarning()
    {
        var hub = Substitute.For<IHub>();
        var client = CreateClient(LoginLimit(1), hub);
        await ExhaustLoginLimitAsync(client, IpA, 1);

        await PostLoginAsync(client, IpA);
        await PostLoginAsync(client, IpA);
        await PostLoginAsync(client, IpA);

        CapturedEvents(hub).Should().ContainSingle()
            .Which.Event.Level.Should().Be(SentryLevel.Warning);
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_Rejected_SentryEventHasOnlyEndpointAndIp()
    {
        var hub = Substitute.For<IHub>();
        var client = CreateClient(LoginLimit(1), hub);
        await ExhaustLoginLimitAsync(client, IpA, 1);

        await PostLoginAsync(client, IpA);

        var (sentryEvent, scope) = CapturedEvents(hub).Single();
        scope!.Tags.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["endpoint"] = LoginUrl,
            ["client_ip"] = IpA,
        });
        scope.Request.Data.Should().BeNull();
        scope.Extra.Should().BeEmpty();
        sentryEvent.Request.Data.Should().BeNull();
        sentryEvent.User.Email.Should().BeNull();
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_WithinLimit_ReturnsServiceResponse()
    {
        var client = CreateClient(LoginLimit(2));

        var response = await PostLoginAsync(client, IpA);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Email.Should().Be("user@example.com");
    }

    [Fact]
    [Trait("Rule", "L16")]
    public async Task Login_ConfiguredLimitOverride_RejectsAfterConfiguredCount()
    {
        var client = CreateClient(LoginLimit(3));

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++) statuses.Add((await PostLoginAsync(client, IpA)).StatusCode);

        statuses.Should().Equal(
            HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests);
    }

    private static List<(SentryEvent Event, Scope? Scope)> CapturedEvents(IHub hub)
    {
        return hub.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IHub.CaptureEvent))
            .Select(call =>
            {
                var arguments = call.GetArguments();
                return ((SentryEvent)arguments[0]!, (Scope?)arguments[1]);
            })
            .ToList();
    }

    // Password reset: Scenario 40
    [Theory]
    [InlineData("/api/auth/forgot-password", "ForgotPassword")]
    [InlineData("/api/auth/reset-password", "ResetPassword")]
    [InlineData("/api/auth/change-password", "ChangePassword")]
    [Trait("Rule", "C10")]
    [Trait("Rule", "P13")]
    public async Task PasswordEndpoint_LimitExceeded_ReturnsTooManyRequests(string url, string policy)
    {
        // Arrange: one request per 10 minutes; the password services are substituted
        var resetService = Substitute.For<IPasswordResetService>();
        resetService.RequestResetCodeAsync(Arg.Any<ForgotPasswordRequest>()).Returns(new MessageResponse { Message = "PasswordResetRequested" });
        resetService.ResetPasswordAsync(Arg.Any<ResetPasswordRequest>()).Returns(new MessageResponse { Message = "PasswordReset" });
        var changeService = Substitute.For<IPasswordChangeService>();
        changeService.ChangePasswordAsync(Arg.Any<Guid>(), Arg.Any<ChangePasswordRequest>()).Returns(new MessageResponse { Message = "PasswordChanged" });
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"AuthRateLimit:{policy}:PermitLimit"] = "1",
                [$"AuthRateLimit:{policy}:WindowSeconds"] = "600",
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordResetService>();
                services.RemoveAll<IPasswordChangeService>();
                services.AddSingleton(resetService);
                services.AddSingleton(changeService);
            });
        });
        _disposables.Add(factory);
        var client = factory.CreateClient();
        var accessToken = TestAccessTokens.Create(factory.Services, Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a"), "Owner");
        HttpRequestMessage PasswordRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new
                {
                    email = "maria@cantinadanonna.com.br",
                    code = "482913",
                    currentPassword = "Massa@Fresca2026",
                    newPassword = "Nhoque@Batata2027"
                })
            };
            request.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, IpA);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return request;
        }
        await client.SendAsync(PasswordRequest());

        // Act
        var response = await client.SendAsync(PasswordRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData("/api/auth/register", "Register")]
    [InlineData("/api/auth/verify", "Verify")]
    [InlineData("/api/auth/resend-verification", "ResendVerification")]
    [InlineData("/api/auth/refresh", "Refresh")]
    [Trait("Rule", "F8")]
    [Trait("Rule", "R14")]
    [Trait("Rule", "RS5")]
    [Trait("Rule", "V10")]
    public async Task PublicAuthEndpoint_LimitExceeded_ReturnsTooManyRequestsWithRetryAfter(string url, string policy)
    {
        // Arrange: one request per 10 minutes; the services are substituted
        _factory.VerificationService.VerifyEmailAsync(Arg.Any<VerifyEmailRequest>())
            .Returns(new MessageResponse { Message = "EmailVerified" });
        _factory.VerificationService.ResendVerificationCodeAsync(Arg.Any<ResendVerificationRequest>())
            .Returns(new MessageResponse { Message = "VerificationCodeSent" });
        _factory.SessionService.RefreshTokenAsync(Arg.Any<string?>())
            .Returns(new SessionResult
            {
                Auth = new AuthResponse { Email = "user@example.com", FirstName = "A", LastName = "B" },
                RefreshToken = "cmF0ZS1saW1pdC10ZXN0LXJlZnJlc2gtdG9rZW4=",
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30)
            });
        var client = CreateClient(new Dictionary<string, string?>
        {
            [$"AuthRateLimit:{policy}:PermitLimit"] = "1",
            [$"AuthRateLimit:{policy}:WindowSeconds"] = "600",
        });
        HttpRequestMessage PublicRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new
                {
                    email = "maria@cantinadanonna.com.br",
                    password = "Massa@Fresca2026",
                    firstName = "Maria",
                    lastName = "Souza",
                    code = "482913"
                })
            };
            request.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, IpA);
            return request;
        }
        await client.SendAsync(PublicRequest());

        // Act
        var response = await client.SendAsync(PublicRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter.ShouldNotBeNull();
    }
}
