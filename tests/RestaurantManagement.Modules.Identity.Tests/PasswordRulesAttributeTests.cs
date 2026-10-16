using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Identity.Models;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The registration password rules in one reusable attribute (plan T4): 8 to 128 characters, at
    /// least one uppercase letter, one digit and one symbol. The reset and change requests use the
    /// same rules for their new password (plan T7 and T8).
    /// </summary>
    public class PasswordRulesAttributeTests
    {
        // 16 characters repeated 8 times is exactly 128.
        private const string Password128 =
            "Massa@Fresca2026" + "Massa@Fresca2026" + "Massa@Fresca2026" + "Massa@Fresca2026"
            + "Massa@Fresca2026" + "Massa@Fresca2026" + "Massa@Fresca2026" + "Massa@Fresca2026";

        // Requests carrying a weak new password; the key is shown in the test results.
        private static readonly Dictionary<string, object> RequestsWithWeakNewPassword = new()
        {
            ["reset"] = new ResetPasswordRequest { Email = "maria@cantinadanonna.com.br", Code = "482913", NewPassword = "massa" },
            ["change"] = new ChangePasswordRequest { CurrentPassword = "Massa@Fresca2026", NewPassword = "massa" }
        };

        private static ValidationResult? Validate(string password)
            => new PasswordRulesAttribute().GetValidationResult(password, new ValidationContext(new object()) { MemberName = "Password" });

        // Scenario 7
        [Theory]
        [InlineData("Massa@1")]                        // 7 characters
        [InlineData(Password128 + "x")]                // 129 characters
        [InlineData("massa@fresca2026")]               // no uppercase letter
        [InlineData("Massa@Fresca")]                   // no digit
        [InlineData("MassaFresca2026")]                // no symbol
        public void GetValidationResult_PasswordBreaksARule_ReturnsError(string password)
        {
            // Act
            var result = Validate(password);

            // Assert
            result.ShouldNotBe(ValidationResult.Success);
        }

        // Scenario 8
        [Theory]
        [InlineData("Massa@26")]                       // exactly 8 characters
        [InlineData(Password128)]                      // exactly 128 characters
        public void GetValidationResult_PasswordAtLengthLimits_ReturnsSuccess(string password)
        {
            // Act
            var result = Validate(password);

            // Assert
            result.ShouldBe(ValidationResult.Success);
        }

        // Scenario 24
        [Theory]
        [InlineData("reset")]
        [InlineData("change")]
        public void Validate_WeakNewPassword_ReturnsNewPasswordError(string requestName)
        {
            // Arrange
            var request = RequestsWithWeakNewPassword[requestName];
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

            // Assert
            isValid.ShouldBeFalse();
            results.ShouldContain(r => r.MemberNames.Contains("NewPassword"));
        }
    }
}
