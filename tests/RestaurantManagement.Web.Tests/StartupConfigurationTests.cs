using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Options;
using Sentry.AspNetCore;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

public class StartupConfigurationTests : IDisposable
{
    private const string StrongKey = "kitchen-signing-key-with-32-chars!";

    private readonly TestHttpsCertificate _certificate = new();
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        _disposables.ForEach(d => d.Dispose());
        _certificate.Dispose();
    }

    private Dictionary<string, string?> ValidProductionSettings() => new()
    {
        ["Jwt:Key"] = StrongKey,
        ["Https:Port"] = "0",
        ["Https:CertificatePath"] = _certificate.FilePath,
        ["Https:CertificatePassword"] = TestHttpsCertificate.Password,
    };

    private WebApplicationFactory<Program> CreateFactory(string environment, Dictionary<string, string?> settings)
    {
        var factory = new AuthApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        });
        _disposables.Add(factory);
        return factory;
    }

    private static Dictionary<string, string?> With(Dictionary<string, string?> settings, string key, string? value)
    {
        settings[key] = value;
        return settings;
    }

    private SentryAspNetCoreOptions ResolveSentryOptions(string environment, Dictionary<string, string?> settings)
    {
        var factory = CreateFactory(environment, settings);
        return factory.Services.GetRequiredService<IOptions<SentryAspNetCoreOptions>>().Value;
    }

    // Scenario 14
    [Theory]
    [InlineData("")]
    [InlineData("kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk")]
    public void Startup_JwtKeyMissingOrUnder32Bytes_FailsNamingJwtKey(string key)
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["Jwt:Key"] = key });

        // Act
        var act = () => factory.CreateClient();

        // Assert
        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("Jwt:Key"));
    }

    // Scenario 15
    [Fact]
    public void Startup_PlaceholderJwtKeyInProduction_FailsNamingJwtKey()
    {
        // Arrange
        var factory = CreateFactory(Environments.Production, With(ValidProductionSettings(), "Jwt:Key", JwtOptions.PlaceholderKey));

        // Act
        var act = () => factory.CreateClient();

        // Assert
        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("Jwt:Key"));
    }

    // Scenario 16
    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public void Startup_NonPositiveJwtExpireMinutes_FailsNamingJwtExpireMinutes(string expireMinutes)
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["Jwt:ExpireMinutes"] = expireMinutes });

        // Act
        var act = () => factory.CreateClient();

        // Assert
        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("Jwt:ExpireMinutes"));
    }

    // Scenario 17
    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public void Startup_SentryTracesSampleRateInvalid_FailsNamingSentryTracesSampleRate(string rate)
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["Sentry:TracesSampleRate"] = rate });

        // Act
        var act = () => factory.CreateClient();

        // Assert
        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("Sentry:TracesSampleRate"));
    }

    // Scenario 18
    [Fact]
    public void Startup_SentryDebugInProduction_IsOff()
    {
        // Arrange
        var settings = ValidProductionSettings();

        // Act
        var options = ResolveSentryOptions(Environments.Production, settings);

        // Assert
        options.Debug.Should().BeFalse();
    }

    // Scenario 19
    [Fact]
    public void Startup_SentryTracesSampleRateUnsetInProduction_DefaultsTo0Point1()
    {
        // Arrange
        var settings = ValidProductionSettings();

        // Act
        var options = ResolveSentryOptions(Environments.Production, settings);

        // Assert
        options.TracesSampleRate.Should().Be(0.1);
    }

    // Scenario 20
    [Fact]
    public void Startup_SentryTracesSampleRateConfiguredInProduction_UsesConfiguredValue()
    {
        // Arrange
        var settings = With(ValidProductionSettings(), "Sentry:TracesSampleRate", "0.5");

        // Act
        var options = ResolveSentryOptions(Environments.Production, settings);

        // Assert
        options.TracesSampleRate.Should().Be(0.5);
    }

    // Scenario 21
    [Fact]
    public void Startup_SentryTracesSampleRateConfiguredInDevelopment_UsesConfiguredValue()
    {
        // Arrange
        var settings = new Dictionary<string, string?> { ["Sentry:TracesSampleRate"] = "0.25" };

        // Act
        var options = ResolveSentryOptions(Environments.Development, settings);

        // Assert
        options.TracesSampleRate.Should().Be(0.25);
    }

    // Scenario 22
    [Fact]
    public void Startup_SentryTracesSampleRateAtLowerBoundary_AcceptsZero()
    {
        // Arrange
        var settings = new Dictionary<string, string?> { ["Sentry:TracesSampleRate"] = "0" };

        // Act
        var options = ResolveSentryOptions(Environments.Development, settings);

        // Assert
        options.TracesSampleRate.Should().Be(0.0);
    }

    // Scenario 23
    [Fact]
    public void Startup_DefaultConfiguration_JwtExpireMinutesIs15()
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new());

        // Act
        var jwtOptions = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

        // Assert
        jwtOptions.ExpireMinutes.Should().Be(15);
    }

    // Hash verification codes: Scenario 4
    [Fact]
    public void Startup_VerificationHashKeyEmpty_FailsNamingHashKey()
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["VerificationCode:HashKey"] = string.Empty });

        // Act
        var thrown = Should.Throw<Exception>(() => factory.CreateClient());

        // Assert
        thrown.ToString().ShouldContain("VerificationCode:HashKey");
    }

    // Hash verification codes: Scenario 5
    [Fact]
    public void Startup_VerificationHashKeyEmptyInProduction_FailsNamingHashKey()
    {
        // Arrange
        var factory = CreateFactory(Environments.Production, With(ValidProductionSettings(), "VerificationCode:HashKey", string.Empty));

        // Act
        var thrown = Should.Throw<Exception>(() => factory.CreateClient());

        // Assert
        thrown.ToString().ShouldContain("VerificationCode:HashKey");
    }

    // Hash verification codes: Scenario 6
    [Fact]
    public void Startup_VerificationHashKeyOf31Bytes_FailsNamingHashKey()
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["VerificationCode:HashKey"] = new string('k', 31) });

        // Act
        var thrown = Should.Throw<Exception>(() => factory.CreateClient());

        // Assert
        thrown.ToString().ShouldContain("VerificationCode:HashKey");
    }

    // Hash verification codes: Scenario 10
    [Fact]
    public void Startup_ValidVerificationHashKey_BindsHashKeyFromConfiguration()
    {
        // Arrange
        const string configuredKey = "q8Jf3mZ0vT1bW6yR9pL2sK7xN4cH5dA0eG3uV8iO1tM=";
        var factory = CreateFactory(Environments.Development, new() { ["VerificationCode:HashKey"] = configuredKey });

        // Act
        var options = factory.Services.GetRequiredService<IOptions<VerificationCodeOptions>>().Value;

        // Assert
        options.HashKey.ShouldBe(configuredKey);
    }

    // Hash verification codes: Scenario 19
    [Fact]
    public void Startup_DefaultConfiguration_RegistersVerificationCodeHasher()
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new());
        using var scope = factory.Services.CreateScope();

        // Act
        var hasher = scope.ServiceProvider.GetService<IVerificationCodeHasher>();

        // Assert
        hasher.ShouldNotBeNull();
    }
}
