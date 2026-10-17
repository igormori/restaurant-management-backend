using FluentAssertions;
using Microsoft.Extensions.Options;
using RestaurantManagement.Shared.Options;
using Shouldly;

namespace RestaurantManagement.Shared.Tests
{
    public class AuthRateLimitOptionsValidatorTests
    {
        private readonly AuthRateLimitOptionsValidator _validator = new();

        // Sets one limit of the forgot, reset or change password policy; the key is shown in the test results.
        private static readonly Dictionary<string, Action<AuthRateLimitOptions, int>> PasswordPolicyLimits = new()
        {
            ["ForgotPassword:PermitLimit"] = (options, value) => options.ForgotPassword.PermitLimit = value,
            ["ForgotPassword:WindowSeconds"] = (options, value) => options.ForgotPassword.WindowSeconds = value,
            ["ResetPassword:PermitLimit"] = (options, value) => options.ResetPassword.PermitLimit = value,
            ["ResetPassword:WindowSeconds"] = (options, value) => options.ResetPassword.WindowSeconds = value,
            ["ChangePassword:PermitLimit"] = (options, value) => options.ChangePassword.PermitLimit = value,
            ["ChangePassword:WindowSeconds"] = (options, value) => options.ChangePassword.WindowSeconds = value
        };

        // Password reset Scenario 4
        [Theory]
        [InlineData("ForgotPassword:PermitLimit", 0)]
        [InlineData("ForgotPassword:WindowSeconds", -600)]
        [InlineData("ResetPassword:PermitLimit", -1)]
        [InlineData("ResetPassword:WindowSeconds", 0)]
        [InlineData("ChangePassword:PermitLimit", 0)]
        [InlineData("ChangePassword:WindowSeconds", -60)]
        [Trait("Rule", "SH23")]
        public void Validate_PasswordPolicyLimitZeroOrNegative_FailsNamingTheSetting(string setting, int value)
        {
            // Arrange
            var options = new AuthRateLimitOptions();
            PasswordPolicyLimits[setting](options, value);

            // Act
            var result = _validator.Validate(null, options);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain(setting);
        }

        [Fact]
        [Trait("Rule", "SH23")]
        public void Validate_DefaultOptions_Succeeds()
        {
            var result = _validator.Validate(null, new AuthRateLimitOptions());

            result.Succeeded.Should().BeTrue();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [Trait("Rule", "SH23")]
        public void Validate_NonPositivePermitLimit_Fails(int permitLimit)
        {
            var options = new AuthRateLimitOptions();
            options.Login.PermitLimit = permitLimit;

            var result = _validator.Validate(null, options);

            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("Login:PermitLimit");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-30)]
        [Trait("Rule", "SH23")]
        public void Validate_NonPositiveWindow_Fails(int windowSeconds)
        {
            var options = new AuthRateLimitOptions();
            options.Register.WindowSeconds = windowSeconds;

            var result = _validator.Validate(null, options);

            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("Register:WindowSeconds");
        }

        [Theory]
        [InlineData("not-an-ip")]
        [InlineData("10.0.0.1/99")]
        [InlineData("")]
        [Trait("Rule", "SH23")]
        public void Validate_MalformedProxy_Fails(string proxy)
        {
            var options = new AuthRateLimitOptions { TrustedProxies = new[] { proxy } };

            var result = _validator.Validate(null, options);

            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("TrustedProxies");
        }

        [Theory]
        [InlineData("10.0.0.1")]
        [InlineData("10.0.0.0/8")]
        [InlineData("::1")]
        [InlineData("fd00::/8")]
        [Trait("Rule", "SH23")]
        public void Validate_ValidProxy_Succeeds(string proxy)
        {
            var options = new AuthRateLimitOptions { TrustedProxies = new[] { proxy } };

            var result = _validator.Validate(null, options);

            result.Succeeded.Should().BeTrue();
        }
    }
}
