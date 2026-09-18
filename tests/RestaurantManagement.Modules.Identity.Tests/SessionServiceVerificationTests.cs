using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class SessionServiceVerificationTests
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

        private static SessionService CreateSut(IdentityDbContext context)
        {
            return new SessionService(context, TestSecurityOptions.Create(), CreateJwtOptions(), TestLocalizer.Create());
        }

        [Fact]
        public async Task LoginAsync_UserNotVerified_ThrowsBusinessExceptionAndIssuesNoTokens()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "unverified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Un",
                LastName = "Verified",
                IsVerified = false
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var act = () => sut.LoginAsync(new LoginRequest { Email = "unverified@test.com", Password = Password });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "unverified@test.com");
            reloaded.RefreshTokenHash.Should().BeNull();
        }

        [Fact]
        public async Task LoginAsync_AfterVerification_ReturnsTokens()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "toverify@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "To",
                LastName = "Verify",
                IsVerified = false
            };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            using (var verificationContext = database.CreateContext())
            {
                var verificationService = new VerificationService(
                    verificationContext,
                    TestSecurityOptions.Create(),
                    TestLocalizer.Create(),
                    Substitute.For<IEmailService>(),
                    Substitute.For<ILogger<VerificationService>>());

                await verificationService.VerifyEmailAsync(new VerifyEmailRequest { Email = "toverify@test.com", Code = "123456" });
            }

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "toverify@test.com", Password = Password });

            // Assert
            response.Token.Should().NotBeNullOrEmpty();
            response.RefreshToken.Should().NotBeNullOrEmpty();
        }
    }
}
