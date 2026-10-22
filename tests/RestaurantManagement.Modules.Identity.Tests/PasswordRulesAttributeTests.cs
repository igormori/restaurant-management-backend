using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Identity.Models.Passwords;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The registration password rules in one reusable attribute (plan T4): 8 to 64 characters, at
    /// least one uppercase letter, one digit and one symbol. The reset and change requests use the
    /// same rules for their new password (plan T7 and T8).
    /// </summary>
    public class PasswordRulesAttributeTests
    {
        // 16 characters repeated 4 times is exactly 64.
        private const string Password64 =
            "Massa@Fresca2026" + "Massa@Fresca2026" + "Massa@Fresca2026" + "Massa@Fresca2026";

        // Requests carrying a weak new password; the key is shown in the test results.
        private static readonly Dictionary<string, object> RequestsWithWeakNewPassword = new()
        {
            ["reset"] = new ResetPasswordRequest { Email = "maria@cantinadanonna.com.br", Code = "482913", NewPassword = "massa" },
            ["change"] = new ChangePasswordRequest { CurrentPassword = "Massa@Fresca2026", NewPassword = "massa" }
        };

        // 14 ASCII bytes + 29 two-byte letters = 72 bytes in 43 characters; one more byte makes 73.
        private static readonly string Password72Bytes = "Massa@Fresca26" + new string('é', 29);
        private static readonly string Password73Bytes = Password72Bytes + "x";

        private static ValidationResult? Validate(string password)
            => new PasswordRulesAttribute().GetValidationResult(password, new ValidationContext(new object()) { MemberName = "Password" });

        // Scenario 7
        [Theory]
        [InlineData("Massa@1")]                        // 7 characters
        [InlineData(Password64 + "x")]                // 65 characters
        [InlineData("massa@fresca2026")]               // no uppercase letter
        [InlineData("Massa@Fresca")]                   // no digit
        [InlineData("MassaFresca2026")]                // no symbol
        [Trait("Rule", "R6")]
        [Trait("Rule", "R7")]
        public void GetValidationResult_PasswordBreaksARule_ReturnsError(string password)
        {
            // Act
            var result = Validate(password);

            // Assert
            result.ShouldNotBe(ValidationResult.Success, "a password that is too short, too long, or missing an uppercase letter, digit or symbol is refused");
        }

        [Theory]
        [InlineData("Massa_Fresca2026")]               // underscore is not a symbol
        [InlineData("Massa Fresca2026")]               // space is not a symbol
        [Trait("Rule", "R8")]
        public void GetValidationResult_OnlyNonLetterNonDigitIsUnderscoreOrSpace_ReturnsError(string password)
        {
            // Act
            var result = Validate(password);

            // Assert
            result.ShouldNotBe(ValidationResult.Success, "an underscore or a space does not count as a symbol, so the password is refused");
        }

        // Scenario 8
        [Theory]
        [InlineData("Massa@26")]                       // exactly 8 characters
        [InlineData(Password64)]                      // exactly 64 characters
        [Trait("Rule", "R9")]
        public void GetValidationResult_PasswordAtLengthLimits_ReturnsSuccess(string password)
        {
            // Act
            var result = Validate(password);

            // Assert
            result.ShouldBe(ValidationResult.Success, "a password of exactly 8 or exactly 64 characters is accepted");
        }

        // Scenario 24
        [Theory]
        [InlineData("reset")]
        [InlineData("change")]
        [Trait("Rule", "C4")]
        [Trait("Rule", "P11")]
        public void Validate_WeakNewPassword_ReturnsNewPasswordError(string requestName)
        {
            // Arrange
            var request = RequestsWithWeakNewPassword[requestName];
            var results = new List<ValidationResult>();

            // Act
            var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

            // Assert
            isValid.ShouldBeFalse("a weak new password is refused on reset and on change");
            results.ShouldContain(r => r.MemberNames.Contains("NewPassword"), "the error is reported on the new password field");
        }

        [Fact]
        [Trait("Rule", "R6")]
        public void GetValidationResult_PasswordOver72BytesWithin64Characters_ReturnsError()
        {
            // Act
            var result = Validate(Password73Bytes);

            // Assert
            result.ShouldNotBe(ValidationResult.Success, "a password over 72 bytes is refused even when it is within 64 characters");
            result!.MemberNames.ShouldContain("Password", "the error is reported on the password field");
        }

        [Fact]
        [Trait("Rule", "R9")]
        public void GetValidationResult_PasswordOfExactly72Bytes_ReturnsSuccess()
        {
            // Act
            var result = Validate(Password72Bytes);

            // Assert
            result.ShouldBe(ValidationResult.Success, "a password of exactly 72 bytes is accepted");
        }
    }
}
