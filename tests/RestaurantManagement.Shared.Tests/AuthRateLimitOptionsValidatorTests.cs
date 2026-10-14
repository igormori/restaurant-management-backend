using FluentAssertions;
using Microsoft.Extensions.Options;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Shared.Tests
{
    public class AuthRateLimitOptionsValidatorTests
    {
        private readonly AuthRateLimitOptionsValidator _validator = new();

        [Fact]
        public void Validate_DefaultOptions_Succeeds()
        {
            var result = _validator.Validate(null, new AuthRateLimitOptions());

            result.Succeeded.Should().BeTrue();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
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
        public void Validate_ValidProxy_Succeeds(string proxy)
        {
            var options = new AuthRateLimitOptions { TrustedProxies = new[] { proxy } };

            var result = _validator.Validate(null, options);

            result.Succeeded.Should().BeTrue();
        }
    }
}
