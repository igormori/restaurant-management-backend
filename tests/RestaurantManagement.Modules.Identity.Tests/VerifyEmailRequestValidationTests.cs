using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Identity.Models.Passwords;
using RestaurantManagement.Modules.Identity.Models.Registration;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>The code field of the verify-email and reset-password requests: required, at most 6 characters.</summary>
    public class VerifyEmailRequestValidationTests
    {
        private static List<ValidationResult> Validate(object request)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
            return results;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1234567")]
        [Trait("Rule", "V9")]
        public void Validate_VerifyCodeMissingOrOverSixCharacters_ReturnsErrorOnCode(string? code)
        {
            // Arrange
            var request = new VerifyEmailRequest { Email = "ana@cantinadanonna.com.br", Code = code! };

            // Act
            var results = Validate(request);

            // Assert
            results.ShouldContain(r => r.MemberNames.Contains("Code"), "a missing code or one over 6 characters is refused with an error on the code");
        }

        [Fact]
        [Trait("Rule", "V9")]
        public void Validate_VerifyCodeOfSixCharacters_ReturnsNoErrorOnCode()
        {
            // Arrange
            var request = new VerifyEmailRequest { Email = "ana@cantinadanonna.com.br", Code = "123456" };

            // Act
            var results = Validate(request);

            // Assert
            results.ShouldNotContain(r => r.MemberNames.Contains("Code"), "a code of 6 characters is accepted");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1234567")]
        [Trait("Rule", "V9")]
        public void Validate_ResetCodeMissingOrOverSixCharacters_ReturnsErrorOnCode(string? code)
        {
            // Arrange
            var request = new ResetPasswordRequest { Email = "ana@cantinadanonna.com.br", Code = code!, NewPassword = "Massa@Fresca2026" };

            // Act
            var results = Validate(request);

            // Assert
            results.ShouldContain(r => r.MemberNames.Contains("Code"), "a missing reset code or one over 6 characters is refused with an error on the code");
        }
    }
}
