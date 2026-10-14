using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
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
    public class SessionServiceRefreshTests
    {
        private const string Password = "Password1!";
        private const string RefreshToken = "valid-refresh-token";
        private const string UserEmail = "user@test.com";
        private const string OwnerRole = "Owner";
        private const int UnauthorizedStatusCode = 401;
        private const string InvalidEmailOrPasswordKey = "InvalidEmailOrPassword";
        private const string InvalidOrExpiredTokenKey = "InvlideOrExpiredToken";

        private static SessionService CreateSut(IdentityDbContext context)
        {
            var jwtOptions = Options.Create(new JwtOptions
            {
                Key = "test-signing-key-at-least-32-characters-long",
                Issuer = "RestaurantManagementTests",
                Audience = "RestaurantManagementTests",
                ExpireMinutes = 60,
                RefreshTokenExpireDays = 30
            });

            return new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create());
        }

        private static async Task SeedUserAsync(
            IdentityTestDatabase database,
            bool isActive = true,
            bool isVerified = true,
            DateTime? lockedUntil = null,
            string? role = null)
        {
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = UserEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsActive = isActive,
                IsVerified = isVerified,
                LockedUntil = lockedUntil,
                RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(RefreshToken),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(1)
            };
            if (role != null)
                user.Roles.Add(new UserRole { Role = role });

            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        private static async Task AssertRefreshRejectedAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            var sut = CreateSut(context);

            var act = () => sut.RefreshTokenAsync(new RefreshRequest { Email = UserEmail, RefreshToken = RefreshToken });

            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(UnauthorizedStatusCode);
            thrown.Which.Message.Should().Be(InvalidOrExpiredTokenKey);
        }

        [Fact]
        public async Task LoginAsync_InactiveUserCorrectPassword_ThrowsGeneric401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, isActive: false);
            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var act = () => sut.LoginAsync(new LoginRequest { Email = UserEmail, Password = Password });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(UnauthorizedStatusCode);
            thrown.Which.Message.Should().Be(InvalidEmailOrPasswordKey);
        }

        [Fact]
        public async Task RefreshTokenAsync_InactiveUser_ThrowsInvalidOrExpiredToken401()
        {
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, isActive: false);

            await AssertRefreshRejectedAsync(database);
        }

        [Fact]
        public async Task RefreshTokenAsync_UnverifiedUser_ThrowsInvalidOrExpiredToken401()
        {
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, isVerified: false);

            await AssertRefreshRejectedAsync(database);
        }

        [Fact]
        public async Task RefreshTokenAsync_LockedUser_ThrowsInvalidOrExpiredToken401()
        {
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, lockedUntil: DateTime.UtcNow.AddMinutes(15));

            await AssertRefreshRejectedAsync(database);
        }

        [Fact]
        public async Task RefreshTokenAsync_UserWithRole_JwtContainsRoleClaim()
        {
            // Arrange: seeding uses its own context, so roles are not tracked by the SUT's context
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, role: OwnerRole);
            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.RefreshTokenAsync(new RefreshRequest { Email = UserEmail, RefreshToken = RefreshToken });

            // Assert
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
            jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
                .Should().ContainSingle().Which.Should().Be(OwnerRole);
        }

        [Fact]
        public async Task RefreshTokenAsync_ActiveVerifiedUnlockedUser_ReturnsNewTokens()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, lockedUntil: DateTime.UtcNow.AddMinutes(-5));
            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.RefreshTokenAsync(new RefreshRequest { Email = UserEmail, RefreshToken = RefreshToken });

            // Assert
            response.Token.Should().NotBeNullOrEmpty();
            response.RefreshToken.Should().NotBeNullOrEmpty().And.NotBe(RefreshToken);
        }
    }
}
