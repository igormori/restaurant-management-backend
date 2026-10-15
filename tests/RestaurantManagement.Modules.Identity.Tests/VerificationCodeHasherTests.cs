using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class VerificationCodeHasherTests
    {
        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        private const string EmailedCode = "482913";

        // Hash verification codes: Scenario 11
        [Fact]
        public void IsMatch_WrongCode_ReturnsFalse()
        {
            // Arrange
            var sut = TestVerificationCodeHasher.Create();
            var storedHash = sut.ComputeHash(MariaId, EmailedCode);

            // Act
            var matches = sut.IsMatch(MariaId, "482914", storedHash);

            // Assert
            matches.ShouldBeFalse();
        }

        // Hash verification codes: Scenario 12
        [Fact]
        public void IsMatch_SameCodeForAnotherUser_ReturnsFalse()
        {
            // Arrange
            var sut = TestVerificationCodeHasher.Create();
            var storedHash = sut.ComputeHash(JoaoId, EmailedCode);

            // Act
            var matches = sut.IsMatch(MariaId, EmailedCode, storedHash);

            // Assert
            matches.ShouldBeFalse();
        }

        // Hash verification codes: Scenario 13
        [Fact]
        public void IsMatch_HashMadeWithDifferentKey_ReturnsFalse()
        {
            // Arrange
            var otherServer = TestVerificationCodeHasher.Create("another-server-verification-hash-key-9876543210");
            var storedHash = otherServer.ComputeHash(MariaId, EmailedCode);
            var sut = TestVerificationCodeHasher.Create();

            // Act
            var matches = sut.IsMatch(MariaId, EmailedCode, storedHash);

            // Assert
            matches.ShouldBeFalse();
        }

        // Hash verification codes: Scenario 14
        [Fact]
        public void IsMatch_EmptyStoredHash_ReturnsFalse()
        {
            // Arrange
            var sut = TestVerificationCodeHasher.Create();

            // Act
            var matches = sut.IsMatch(MariaId, EmailedCode, string.Empty);

            // Assert
            matches.ShouldBeFalse();
        }

        // Hash verification codes: Scenario 15
        [Fact]
        public void IsMatch_ReadableCodeStoredInsteadOfHash_ReturnsFalse()
        {
            // Arrange: a row written before release holds the readable code itself
            var sut = TestVerificationCodeHasher.Create();

            // Act
            var matches = sut.IsMatch(MariaId, EmailedCode, EmailedCode);

            // Assert
            matches.ShouldBeFalse();
        }

        // Hash verification codes: Scenario 16
        [Fact]
        public void ComputeHash_SixDigitCode_DoesNotContainTheCode()
        {
            // Arrange
            var sut = TestVerificationCodeHasher.Create();

            // Act
            var hash = sut.ComputeHash(MariaId, EmailedCode);

            // Assert
            hash.ShouldNotContain(EmailedCode);
        }

        // Hash verification codes: Scenario 17
        [Fact]
        public void ComputeHash_SixDigitCode_Returns64LowercaseHexCharacters()
        {
            // Arrange
            var sut = TestVerificationCodeHasher.Create();

            // Act
            var hash = sut.ComputeHash(MariaId, EmailedCode);

            // Assert
            hash.ShouldMatch("^[0-9a-f]{64}$");
        }

        // Hash verification codes: Scenario 18
        [Fact]
        public void IsMatch_SameUserAndCode_ReturnsTrue()
        {
            // Arrange
            var sut = TestVerificationCodeHasher.Create();
            var storedHash = sut.ComputeHash(MariaId, EmailedCode);

            // Act
            var matches = sut.IsMatch(MariaId, EmailedCode, storedHash);

            // Assert
            matches.ShouldBeTrue();
        }
    }
}
