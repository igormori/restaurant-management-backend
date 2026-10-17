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
        // Program.cs reads Https:* while building, before factory configuration is applied, so it must come in as host settings.
        var httpsSettings = settings.Where(s => s.Key.StartsWith("Https:")).ToList();
        var otherSettings = settings.Except(httpsSettings);
        var factory = new AuthApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            httpsSettings.ForEach(s => builder.UseSetting(s.Key, s.Value));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(otherSettings));
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
    [Trait("Rule", "T4")]
    public void Startup_JwtKeyMissingOrUnder32Bytes_FailsNamingJwtKey(string key)
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["Jwt:Key"] = key });

        // Act
        var error = StartupFailure.Capture(factory);

        // Assert
        error.ToString().ShouldContain("Jwt:Key", Case.Sensitive, "the app must refuse to start with a weak or placeholder signing key, and the error must name the setting to fix");
    }

    // Scenario 15
    [Fact]
    [Trait("Rule", "T4")]
    public void Startup_PlaceholderJwtKeyInProduction_FailsNamingJwtKey()
    {
        // Arrange
        var factory = CreateFactory(Environments.Production, With(ValidProductionSettings(), "Jwt:Key", JwtOptions.PlaceholderKey));

        // Act
        var error = StartupFailure.Capture(factory);

        // Assert
        error.ToString().ShouldContain("Jwt:Key", Case.Sensitive, "the app must refuse to start with a weak or placeholder signing key, and the error must name the setting to fix");
    }

    // Auth hardening rule 4
    [Fact]
    public void Startup_NoCertificateInProduction_FailsNamingHttpsCertificatePath()
    {
        // Arrange
        var settings = With(ValidProductionSettings(), "Https:CertificatePath", null);
        var factory = CreateFactory(Environments.Production, settings);

        // Act
        var error = StartupFailure.Capture(factory);

        // Assert
        error.ToString().ShouldContain("Https:CertificatePath", Case.Sensitive);
    }

    // Scenario 16
    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [Trait("Rule", "T4")]
    public void Startup_NonPositiveJwtExpireMinutes_FailsNamingJwtExpireMinutes(string expireMinutes)
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["Jwt:ExpireMinutes"] = expireMinutes });

        // Act
        var error = StartupFailure.Capture(factory);

        // Assert
        error.ToString().ShouldContain("Jwt:ExpireMinutes", Case.Sensitive, "the app must refuse to start when the access token lifetime is not positive, and the error must name the setting to fix");
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
        var error = StartupFailure.Capture(factory);

        // Assert
        error.ToString().ShouldContain("Sentry:TracesSampleRate", Case.Sensitive);
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
    [Trait("Rule", "L19")]
    public void Startup_DefaultConfiguration_JwtExpireMinutesIs15()
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new());

        // Act
        var jwtOptions = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

        // Assert
        jwtOptions.ExpireMinutes.Should().Be(15, "an access token lasts 15 minutes unless configured otherwise");
    }

    // Hash verification codes: Scenario 4
    [Fact]
    [Trait("Rule", "T5")]
    public void Startup_VerificationHashKeyEmpty_FailsNamingHashKey()
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["VerificationCode:HashKey"] = string.Empty });

        // Act
        var thrown = StartupFailure.Capture(factory);

        // Assert
        thrown.ToString().ShouldContain("VerificationCode:HashKey", customMessage: "the app must refuse to start without a strong code fingerprint key, and the error must name the setting to fix");
    }

    // Hash verification codes: Scenario 5
    [Fact]
    [Trait("Rule", "T5")]
    public void Startup_VerificationHashKeyEmptyInProduction_FailsNamingHashKey()
    {
        // Arrange
        var factory = CreateFactory(Environments.Production, With(ValidProductionSettings(), "VerificationCode:HashKey", string.Empty));

        // Act
        var thrown = StartupFailure.Capture(factory);

        // Assert
        thrown.ToString().ShouldContain("VerificationCode:HashKey", customMessage: "the app must refuse to start without a strong code fingerprint key, and the error must name the setting to fix");
    }

    // Hash verification codes: Scenario 6
    [Fact]
    [Trait("Rule", "T5")]
    public void Startup_VerificationHashKeyOf31Bytes_FailsNamingHashKey()
    {
        // Arrange
        var factory = CreateFactory(Environments.Development, new() { ["VerificationCode:HashKey"] = new string('k', 31) });

        // Act
        var thrown = StartupFailure.Capture(factory);

        // Assert
        thrown.ToString().ShouldContain("VerificationCode:HashKey", customMessage: "the app must refuse to start without a strong code fingerprint key, and the error must name the setting to fix");
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
