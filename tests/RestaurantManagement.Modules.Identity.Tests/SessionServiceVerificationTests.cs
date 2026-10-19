using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentry;
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
            var jwtOptions = CreateJwtOptions();
            return new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), TestPasswordHasher.Create());
        }

        [Fact]
        [Trait("Rule", "L12")]
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
            var act = () => sut.LoginAsync(new LoginRequest { Email = "unverified@test.com", Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>("an unverified account cannot log in");
            thrown.Which.StatusCode.Should().Be(401, "an unverified account gets the generic 401 login failure");

            // refresh-tokens-own-table Scenario 41: a refused sign-in stores no refresh token
            using var verifyContext = database.CreateContext();
            (await verifyContext.Set<RefreshToken>().CountAsync()).Should().Be(0, "a refused login stores no refresh token");
        }

        [Fact]
        [Trait("Rule", "L1")]
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
                CodeHash = TestVerificationCodeHasher.Create().ComputeHash(user.Id, "123456"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            using (var verificationContext = database.CreateContext())
            {
                var verificationService = TestVerificationService.Create(verificationContext);

                await verificationService.VerifyEmailAsync(new VerifyEmailRequest { Email = "toverify@test.com", Code = "123456" });
            }

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "toverify@test.com", Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            response.Auth.Token.Should().NotBeNullOrEmpty("once the email is verified the user can log in and gets an access token");
            response.RefreshToken.Should().NotBeNullOrEmpty("a verified login also gets a refresh token");
        }
    }
}
