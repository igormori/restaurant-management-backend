using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// A password is checked exactly as typed: no trimming and no case folding.
    /// </summary>
    public class PasswordExactMatchTests
    {
        private const string StoredPassword = "Massa Fresca@2026";
        private readonly IPasswordHasher _sut = new BcryptPasswordHasher();

        [Theory]
        [InlineData("massa fresca@2026")]
        [InlineData("MASSA FRESCA@2026")]
        [InlineData(" Massa Fresca@2026")]
        [InlineData("Massa Fresca@2026 ")]
        [InlineData("MassaFresca@2026")]
        [Trait("Rule", "T20")]
        public void Verify_PasswordWithDifferentCasingOrSpaces_ReturnsFalse(string typed)
        {
            // Arrange
            var hash = _sut.Hash(StoredPassword);

            // Act
            var result = _sut.Verify(typed, hash);

            // Assert
            result.ShouldBeFalse("a password is checked exactly as typed, so casing and spaces matter");
        }

        [Fact]
        [Trait("Rule", "T20")]
        public void Verify_PasswordWithInnerSpaceAndCasingAsStored_ReturnsTrue()
        {
            // Arrange
            var hash = _sut.Hash(StoredPassword);

            // Act
            var result = _sut.Verify(StoredPassword, hash);

            // Assert
            result.ShouldBeTrue("the exact password, spaces and casing kept, is accepted");
        }
    }
}
