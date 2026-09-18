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
    /// <summary>
    /// Cross-endpoint check that none of login, verify, or resend's folded failure cases
    /// leak which specific case occurred: every folded case for a given endpoint must
    /// collapse to exactly one distinct message.
    /// </summary>
    public class AuthUniformResponseTests
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

        private static SessionService CreateSessionService(IdentityDbContext context)
        {
            return new SessionService(context, TestSecurityOptions.Create(), CreateJwtOptions(), TestLocalizer.Create());
        }

        private static VerificationService CreateVerificationService(IdentityDbContext context)
        {
            return new VerificationService(
                context,
                TestSecurityOptions.Create(),
                TestLocalizer.Create(),
                Substitute.For<IEmailService>(),
                Substitute.For<ILogger<VerificationService>>());
        }

        [Fact]
        public async Task AuthFailures_AcrossFoldedCases_UseOneMessagePerEndpoint()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var verifiedUser = new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            };
            var unverifiedUser = new User
            {
                Email = "unverified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Un",
                LastName = "Verified",
                IsVerified = false
            };
            seedContext.Users.AddRange(verifiedUser, unverifiedUser);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = unverifiedUser.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            // Act: login's three folded cases
            using var loginUnknownContext = database.CreateContext();
            var loginUnknownAct = () => CreateSessionService(loginUnknownContext)
                .LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = Password });

            using var loginWrongPasswordContext = database.CreateContext();
            var loginWrongPasswordAct = () => CreateSessionService(loginWrongPasswordContext)
                .LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" });

            using var loginUnverifiedContext = database.CreateContext();
            var loginUnverifiedAct = () => CreateSessionService(loginUnverifiedContext)
                .LoginAsync(new LoginRequest { Email = "unverified@test.com", Password = Password });

            var loginUnknownThrown = await loginUnknownAct.Should().ThrowAsync<BusinessException>();
            var loginWrongPasswordThrown = await loginWrongPasswordAct.Should().ThrowAsync<BusinessException>();
            var loginUnverifiedThrown = await loginUnverifiedAct.Should().ThrowAsync<BusinessException>();

            // Act: verify's two folded cases
            using var verifyUnknownContext = database.CreateContext();
            var verifyUnknownAct = () => CreateVerificationService(verifyUnknownContext)
                .VerifyEmailAsync(new VerifyEmailRequest { Email = "nobody@test.com", Code = "000000" });

            using var verifyInvalidCodeContext = database.CreateContext();
            var verifyInvalidCodeAct = () => CreateVerificationService(verifyInvalidCodeContext)
                .VerifyEmailAsync(new VerifyEmailRequest { Email = "unverified@test.com", Code = "000000" });

            var verifyUnknownThrown = await verifyUnknownAct.Should().ThrowAsync<BusinessException>();
            var verifyInvalidCodeThrown = await verifyInvalidCodeAct.Should().ThrowAsync<BusinessException>();

            // Act: resend's three folded cases
            using var resendUnknownContext = database.CreateContext();
            var resendUnknownMessage = await CreateVerificationService(resendUnknownContext)
                .ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "nobody@test.com" });

            using var resendAlreadyVerifiedContext = database.CreateContext();
            var resendAlreadyVerifiedMessage = await CreateVerificationService(resendAlreadyVerifiedContext)
                .ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "verified@test.com" });

            // Assert: each endpoint's folded cases share one status code and one message
            var loginStatusCodes = new[] { loginUnknownThrown.Which.StatusCode, loginWrongPasswordThrown.Which.StatusCode, loginUnverifiedThrown.Which.StatusCode };
            var loginMessages = new[] { loginUnknownThrown.Which.Message, loginWrongPasswordThrown.Which.Message, loginUnverifiedThrown.Which.Message };
            loginStatusCodes.Distinct().Should().ContainSingle();
            loginMessages.Distinct().Should().ContainSingle();

            var verifyStatusCodes = new[] { verifyUnknownThrown.Which.StatusCode, verifyInvalidCodeThrown.Which.StatusCode };
            var verifyMessages = new[] { verifyUnknownThrown.Which.Message, verifyInvalidCodeThrown.Which.Message };
            verifyStatusCodes.Distinct().Should().ContainSingle();
            verifyMessages.Distinct().Should().ContainSingle();

            var resendMessages = new[] { resendUnknownMessage.Message, resendAlreadyVerifiedMessage.Message };
            resendMessages.Distinct().Should().ContainSingle();
        }
    }
}
