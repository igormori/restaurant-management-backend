using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Identity.Models.Passwords;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>The 72 UTF-8 byte limit alone, used by the login request.</summary>
    public class MaxPasswordBytesAttributeTests
    {
        // 14 ASCII bytes + 29 two-byte letters = 72 bytes in 43 characters; one more byte makes 73.
        private static readonly string Password72Bytes = "Massa@Fresca26" + new string('é', 29);
        private static readonly string Password73Bytes = Password72Bytes + "x";

        private static ValidationResult? Validate(object? value)
            => new MaxPasswordBytesAttribute().GetValidationResult(value, new ValidationContext(new object()) { MemberName = "Password" });

        [Fact]
        [Trait("Rule", "L13")]
        public void GetValidationResult_PasswordOver72Bytes_ReturnsError()
        {
            // Act
            var result = Validate(Password73Bytes);

            // Assert
            result.ShouldNotBe(ValidationResult.Success, "a password over 72 bytes is refused");
        }

        [Fact]
        [Trait("Rule", "L13")]
        public void GetValidationResult_PasswordOf72Bytes_ReturnsSuccess()
        {
            // Act
            var result = Validate(Password72Bytes);

            // Assert
            result.ShouldBe(ValidationResult.Success, "a password of exactly 72 bytes is accepted");
        }

        [Fact]
        [Trait("Rule", "L13")]
        public void GetValidationResult_NullValue_ReturnsSuccess()
        {
            // Act
            var result = Validate(null);

            // Assert
            result.ShouldBe(ValidationResult.Success, "a missing password is not rejected by the byte limit; other rules handle it");
        }
    }
}
