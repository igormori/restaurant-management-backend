using Microsoft.Extensions.Configuration;
using RestaurantManagement.Shared.Options;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The security limits the app ships with, read from the Web project's real appsettings.json and
/// bound the way the app binds them. Other tests set their own limits, so without these a change
/// to a shipped number would fail nothing. Deployment can still override them with environment
/// variables, which no test sees.
/// </summary>
public class ShippedLimitsTests
{
    private static readonly IConfiguration Shipped = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(RepositoryRoot(), "src", "RestaurantManagement.Web", "appsettings.json"), optional: false)
        .Build();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RestaurantManagement.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("RestaurantManagement.sln not found above the test output folder.");
    }

    private static T Bind<T>(string section) where T : new()
    {
        var options = new T();
        Shipped.GetSection(section).Bind(options);
        return options;
    }

    [Fact]
    [Trait("Rule", "T16")]
    public void Configuration_ShippedLockoutAndCodeLimits_MatchTheMatrix()
    {
        // Arrange and Act
        var security = Bind<SecurityOptions>("Security");

        // Assert
        security.MaxFailedLoginAttempts.ShouldBe(5, "the fifth wrong password locks login");
        security.LockoutDurationMinutes.ShouldBe(15, "a login lock lasts 15 minutes");
        security.MaxVerificationAttempts.ShouldBe(5, "the fifth wrong code locks verification and reset");
        security.VerificationLockoutDurationMinutes.ShouldBe(15, "a verification or reset lock lasts 15 minutes");
        security.VerificationCodeExpiryMinutes.ShouldBe(10, "emailed codes expire after 10 minutes");
        security.ResendCooldownSeconds.ShouldBe(60, "a new code can be emailed at most once every 60 seconds");
    }

    [Fact]
    [Trait("Rule", "T17")]
    public void Configuration_ShippedTokenLifetimes_MatchTheMatrix()
    {
        // Arrange and Act
        var jwt = Bind<JwtOptions>("Jwt");

        // Assert
        jwt.ExpireMinutes.ShouldBe(15, "an access token lasts 15 minutes");
        jwt.RefreshTokenExpireDays.ShouldBe(30, "a refresh token lasts 30 days");
    }

    [Theory]
    [InlineData("Login", 10, 60)]
    [InlineData("Register", 5, 3600)]
    [InlineData("Verify", 10, 600)]
    [InlineData("ResendVerification", 3, 600)]
    [InlineData("Refresh", 30, 60)]
    [InlineData("ForgotPassword", 3, 600)]
    [InlineData("ResetPassword", 10, 600)]
    [InlineData("ChangePassword", 10, 60)]
    [Trait("Rule", "T18")]
    public void Configuration_ShippedRateLimit_MatchesTheMatrix(string endpoint, int permitLimit, int windowSeconds)
    {
        // Arrange and Act
        var limit = Bind<EndpointRateLimit>($"AuthRateLimit:{endpoint}");

        // Assert
        limit.PermitLimit.ShouldBe(permitLimit, $"{endpoint} allows {permitLimit} requests per IP address in each window");
        limit.WindowSeconds.ShouldBe(windowSeconds, $"the {endpoint} window is {windowSeconds} seconds");
    }
}
