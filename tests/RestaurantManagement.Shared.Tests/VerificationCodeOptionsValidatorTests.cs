using Microsoft.Extensions.Options;
using RestaurantManagement.Shared.Options;
using Shouldly;

namespace RestaurantManagement.Shared.Tests
{
    public class VerificationCodeOptionsValidatorTests
    {
        private static ValidateOptionsResult Validate(string? hashKey)
        {
            var sut = new VerificationCodeOptionsValidator();
            return sut.Validate(null, new VerificationCodeOptions { HashKey = hashKey! });
        }

        // Hash verification codes: Scenario 1
        [Fact]
        public void Validate_HashKeyMissing_FailsNamingHashKey()
        {
            // Arrange
            string? missingKey = null;

            // Act
            var result = Validate(missingKey);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("VerificationCode:HashKey");
        }

        // Hash verification codes: Scenario 2
        [Fact]
        public void Validate_HashKeyEmpty_FailsNamingHashKey()
        {
            // Arrange
            var emptyKey = string.Empty;

            // Act
            var result = Validate(emptyKey);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("VerificationCode:HashKey");
        }

        // Hash verification codes: Scenario 3
        [Fact]
        public void Validate_HashKeyOf31Bytes_FailsNamingHashKey()
        {
            // Arrange
            var thirtyOneByteKey = new string('k', 31);

            // Act
            var result = Validate(thirtyOneByteKey);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("VerificationCode:HashKey");
        }

        // Hash verification codes: Scenario 7
        [Fact]
        public void Validate_HashKeyOfExactly32Bytes_Succeeds()
        {
            // Arrange
            var thirtyTwoByteKey = new string('k', 32);

            // Act
            var result = Validate(thirtyTwoByteKey);

            // Assert
            result.Failed.ShouldBeFalse();
        }

        // Hash verification codes: Scenario 8
        [Fact]
        public void Validate_HashKeyOf16TwoByteCharacters_Succeeds()
        {
            // Arrange: 16 characters, but 32 bytes in UTF-8, so the length is counted in bytes
            var sixteenAccentedChars = new string('ç', 16);

            // Act
            var result = Validate(sixteenAccentedChars);

            // Assert
            result.Failed.ShouldBeFalse();
        }

        // Hash verification codes: Scenario 9
        [Fact]
        public void Validate_RandomBase64HashKey_Succeeds()
        {
            // Arrange: the shape produced by `openssl rand -base64 32`
            var generatedKey = "q8Jf3mZ0vT1bW6yR9pL2sK7xN4cH5dA0eG3uV8iO1tM=";

            // Act
            var result = Validate(generatedKey);

            // Assert
            result.Failed.ShouldBeFalse();
        }
    }
}
