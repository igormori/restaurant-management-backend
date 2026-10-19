using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// A user deactivated after asking for a code must not be able to use it: the reset is rejected
    /// exactly like a wrong code and changes nothing.
    /// </summary>
    public class PasswordResetInactiveUserTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string OldPassword = "Massa@Fresca2026";
        private const string NewPassword = "Nhoque@Batata2027";

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

        [Fact]
        [Trait("Rule", "P9")]
        public async Task ResetPasswordAsync_DeactivatedAfterRequestingCode_RejectedLikeWrongCodeAndNothingChanges()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using (var seed = database.CreateContext())
            {
                seed.Users.Add(new User
                {
                    Id = MariaId,
                    Email = MariaEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(OldPassword),
                    FirstName = "Maria",
                    LastName = "Souza",
                    IsVerified = true
                });
                await seed.SaveChangesAsync();
            }

            string? emailedCode = null;
            var auditRecorder = Substitute.For<IPasswordAuditRecorder>();

            PasswordResetService CreateSut(RestaurantManagement.Modules.Identity.Data.IdentityDbContext context) => new(
                context,
                TestSecurityOptions.Create(resendCooldownSeconds: 0, maxVerificationAttempts: 5),
                TestLocalizer.Create(),
                Substitute.For<ILogger<PasswordResetService>>(),
                TestVerificationCodeHasher.Create(),
                new RefreshTokenService(context, TestJwtOptions.Create()),
                auditRecorder,
                TestPasswordHasher.Create());

            using (var context = database.CreateContext())
            {
                emailedCode = "482913";
                context.PasswordResetCodes.Add(new PasswordResetCode
                {
                    UserId = MariaId,
                    CodeHash = TestVerificationCodeHasher.Create().ComputeHash(MariaId, emailedCode),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                });
                await context.SaveChangesAsync();
                await new RefreshTokenService(context, TestJwtOptions.Create())
                    .IssueAsync(MariaId, null, TestClients.KitchenTablet.DeviceDescription, TestClients.KitchenTablet.IpAddress);
            }

            using (var context = database.CreateContext())
            {
                (await context.Users.SingleAsync(u => u.Id == MariaId)).IsActive = false;
                await context.SaveChangesAsync();
            }

            BusinessException wrongCode;
            using (var context = database.CreateContext())
            {
                wrongCode = await Should.ThrowAsync<BusinessException>(() => CreateSut(context)
                    .ResetPasswordAsync(new ResetPasswordRequest { Email = MariaEmail, Code = "000000", NewPassword = NewPassword }));
            }

            // Act
            BusinessException rejection;
            using (var context = database.CreateContext())
            {
                rejection = await Should.ThrowAsync<BusinessException>(() => CreateSut(context)
                    .ResetPasswordAsync(new ResetPasswordRequest { Email = MariaEmail, Code = emailedCode!, NewPassword = NewPassword }));
            }

            // Assert
            rejection.StatusCode.ShouldBe(wrongCode.StatusCode, "a deactivated user gets the same status as a wrong code, so nothing reveals the account state");
            rejection.Message.ShouldBe(wrongCode.Message, "a deactivated user gets the same message as a wrong code");
            using var verify = database.CreateContext();
            BCrypt.Net.BCrypt.Verify(OldPassword, (await verify.Users.SingleAsync(u => u.Id == MariaId)).PasswordHash).ShouldBeTrue("the old password must still work because the reset was refused");
            (await verify.RefreshTokens.Where(t => t.UserId == MariaId).ToListAsync()).ShouldAllBe(t => t.RevokedAt == null, "a refused reset does not sign the user out of any device");
            await auditRecorder.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default);
        }
    }
}
