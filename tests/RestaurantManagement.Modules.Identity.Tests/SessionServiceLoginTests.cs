using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class SessionServiceLoginTests
    {
        private const string Password = "Password1!";

        private static IOptions<JwtOptions> CreateJwtOptions()
        {
            return Options.Create(new JwtOptions
            {
                Key = "test-signing-key-at-least-32-characters-long",
                Issuer = "RestaurantManagementTests",
                Audience = "RestaurantManagementTests",
                ExpireMinutes = 60,
                RefreshTokenExpireDays = 30
            });
        }

        private static SessionService CreateSut(
            IdentityDbContext context,
            int maxFailedLoginAttempts = 5,
            int lockoutDurationMinutes = 15)
        {
            var securityOptions = TestSecurityOptions.Create(
                maxFailedLoginAttempts: maxFailedLoginAttempts,
                lockoutDurationMinutes: lockoutDurationMinutes);

            return new SessionService(context, securityOptions, CreateJwtOptions(), TestLocalizer.Create());
        }

        [Fact]
        public async Task LoginAsync_UnknownEmailAndWrongPassword_ReturnSameStatusAndMessage()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            using var unknownEmailContext = database.CreateContext();
            var unknownEmailSut = CreateSut(unknownEmailContext);

            using var wrongPasswordContext = database.CreateContext();
            var wrongPasswordSut = CreateSut(wrongPasswordContext);

            // Act
            var unknownEmailAct = () => unknownEmailSut.LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = Password });
            var wrongPasswordAct = () => wrongPasswordSut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" });

            // Assert
            var unknownEmailThrown = await unknownEmailAct.Should().ThrowAsync<BusinessException>();
            var wrongPasswordThrown = await wrongPasswordAct.Should().ThrowAsync<BusinessException>();

            unknownEmailThrown.Which.StatusCode.Should().Be(401);
            unknownEmailThrown.Which.StatusCode.Should().Be(wrongPasswordThrown.Which.StatusCode);
            unknownEmailThrown.Which.Message.Should().Be(wrongPasswordThrown.Which.Message);
        }

        [Fact]
        public async Task LoginAsync_UnverifiedCorrectPassword_MatchesGenericFailure()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            });
            seedContext.Users.Add(new User
            {
                Email = "unverified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Un",
                LastName = "Verified",
                IsVerified = false
            });
            await seedContext.SaveChangesAsync();

            using var wrongPasswordContext = database.CreateContext();
            var wrongPasswordSut = CreateSut(wrongPasswordContext);

            using var unverifiedContext = database.CreateContext();
            var unverifiedSut = CreateSut(unverifiedContext);

            // Act
            var wrongPasswordAct = () => wrongPasswordSut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" });
            var unverifiedAct = () => unverifiedSut.LoginAsync(new LoginRequest { Email = "unverified@test.com", Password = Password });

            // Assert
            var wrongPasswordThrown = await wrongPasswordAct.Should().ThrowAsync<BusinessException>();
            var unverifiedThrown = await unverifiedAct.Should().ThrowAsync<BusinessException>();

            unverifiedThrown.Which.StatusCode.Should().Be(401);
            unverifiedThrown.Which.StatusCode.Should().Be(wrongPasswordThrown.Which.StatusCode);
            unverifiedThrown.Which.Message.Should().Be(wrongPasswordThrown.Which.Message);
        }

        [Fact]
        public async Task LoginAsync_LockedAccount_Throws423WithUnlockTime()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "locked@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true,
                LockedUntil = DateTime.UtcNow.AddMinutes(15)
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act: correct password supplied, but the account is locked
            var act = () => sut.LoginAsync(new LoginRequest { Email = "locked@test.com", Password = Password });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(423);
            thrown.Which.Message.Should().NotBe("InvalidEmailOrPassword");
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsTokens()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = Password });

            // Assert
            response.Token.Should().NotBeNullOrEmpty();
            response.RefreshToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RefreshTokenAsync_ValidRefreshToken_RenewsExpiryByRefreshTokenExpireDays()
        {
            // Arrange
            const string refreshToken = "valid-refresh-token";
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true,
                RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(1)
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            await sut.RefreshTokenAsync(new RefreshRequest { Email = "verified@test.com", RefreshToken = refreshToken });

            // Assert
            using var assertContext = database.CreateContext();
            var user = await assertContext.Users.SingleAsync(u => u.Email == "verified@test.com");

            var expectedExpiry = DateTime.UtcNow.AddDays(30);
            user.RefreshTokenExpiry.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
        }
    }
}
