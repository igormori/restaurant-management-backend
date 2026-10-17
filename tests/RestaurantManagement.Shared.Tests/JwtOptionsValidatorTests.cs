using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Shared.Tests
{
    public class JwtOptionsValidatorTests
    {
        private const string StrongKey = "kitchen-signing-key-with-32-chars!";

        private static JwtOptionsValidator CreateSut(string environmentName)
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);
            return new JwtOptionsValidator(environment);
        }

        private static JwtOptions CreateOptions(string key = StrongKey, int expireMinutes = 15)
        {
            return new JwtOptions { Key = key, ExpireMinutes = expireMinutes };
        }

        // Scenario 1
        [Fact]
        [Trait("Rule", "SH21")]
        public void Validate_EmptyKey_FailsNamingJwtKey()
        {
            // Arrange
            var sut = CreateSut(Environments.Development);

            // Act
            var result = sut.Validate(null, CreateOptions(key: string.Empty));

            // Assert
            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("Jwt:Key");
        }

        // Scenario 2
        [Fact]
        [Trait("Rule", "SH21")]
        public void Validate_KeyOf31Bytes_FailsNamingJwtKey()
        {
            // Arrange
            var sut = CreateSut(Environments.Development);
            var thirtyOneByteKey = new string('k', 31);

            // Act
            var result = sut.Validate(null, CreateOptions(key: thirtyOneByteKey));

            // Assert
            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("Jwt:Key");
        }

        // Scenario 3
        [Fact]
        [Trait("Rule", "SH21")]
        public void Validate_PlaceholderKeyInProduction_FailsNamingJwtKey()
        {
            // Arrange
            var sut = CreateSut(Environments.Production);

            // Act
            var result = sut.Validate(null, CreateOptions(key: JwtOptions.PlaceholderKey));

            // Assert
            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("Jwt:Key");
        }

        // Scenario 4
        [Fact]
        [Trait("Rule", "SH21")]
        public void Validate_PlaceholderKeyInStaging_FailsNamingJwtKey()
        {
            // Arrange
            var sut = CreateSut(Environments.Staging);

            // Act
            var result = sut.Validate(null, CreateOptions(key: JwtOptions.PlaceholderKey));

            // Assert
            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("Jwt:Key");
        }

        // Scenario 5
        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [Trait("Rule", "SH21")]
        public void Validate_NonPositiveExpireMinutes_FailsNamingJwtExpireMinutes(int expireMinutes)
        {
            // Arrange
            var sut = CreateSut(Environments.Production);

            // Act
            var result = sut.Validate(null, CreateOptions(expireMinutes: expireMinutes));

            // Assert
            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("Jwt:ExpireMinutes");
        }

        // Scenario 6
        [Fact]
        [Trait("Rule", "SH21")]
        public void Validate_PlaceholderKeyInDevelopment_Succeeds()
        {
            // Arrange
            var sut = CreateSut(Environments.Development);

            // Act
            var result = sut.Validate(null, CreateOptions(key: JwtOptions.PlaceholderKey));

            // Assert
            result.Succeeded.Should().BeTrue();
        }

        // Scenario 7
        [Fact]
        [Trait("Rule", "SH21")]
        public void Validate_KeyOfExactly32Bytes_Succeeds()
        {
            // Arrange
            var sut = CreateSut(Environments.Production);
            var thirtyTwoByteKey = new string('k', 32);

            // Act
            var result = sut.Validate(null, CreateOptions(key: thirtyTwoByteKey));

            // Assert
            result.Succeeded.Should().BeTrue();
        }

        // Scenario 8
        [Fact]
        [Trait("Rule", "SH21")]
        public void Validate_Key17MultiByteCharsTotaling34Bytes_Succeeds()
        {
            // Arrange
            var sut = CreateSut(Environments.Production);
            var seventeenAccentedChars = new string('é', 17);

            // Act
            var result = sut.Validate(null, CreateOptions(key: seventeenAccentedChars));

            // Assert
            result.Succeeded.Should().BeTrue();
        }

        // Scenario 9
        [Fact]
        [Trait("Rule", "SH21")]
        public void Validate_StrongKeyAndPositiveExpiryInProduction_Succeeds()
        {
            // Arrange
            var sut = CreateSut(Environments.Production);

            // Act
            var result = sut.Validate(null, CreateOptions());

            // Assert
            result.Succeeded.Should().BeTrue();
        }
    }
}
