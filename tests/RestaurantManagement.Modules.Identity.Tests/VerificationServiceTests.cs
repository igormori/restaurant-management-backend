using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class VerificationServiceTests
    {
        private static VerificationService CreateSut(
            IdentityDbContext context,
            IEmailService emailService,
            int resendCooldownSeconds = 60,
            int verificationCodeExpiryMinutes = 15)
        {
            var logger = Substitute.For<ILogger<VerificationService>>();
            var securityOptions = TestSecurityOptions.Create(
                verificationCodeExpiryMinutes: verificationCodeExpiryMinutes,
                resendCooldownSeconds: resendCooldownSeconds);

            return new VerificationService(context, securityOptions, TestLocalizer.Create(), emailService, logger);
        }

        [Fact]
        public async Task VerifyEmailAsync_CodeDoesNotMatch_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "654321" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeFalse();
        }

        [Fact]
        public async Task VerifyEmailAsync_CodeExpired_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task VerifyEmailAsync_CodeAlreadyUsed_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = true
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task VerifyEmailAsync_UserNotFound_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "nobody@test.com", Code = "123456" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(400);
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_UserNotFound_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService);

            // Act
            var act = () => sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "nobody@test.com" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(404);
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_UserAlreadyVerified_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User { Email = "verified@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = true });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService);

            // Act
            var act = () => sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "verified@test.com" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(400);
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_WithinCooldown_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService, resendCooldownSeconds: 60);

            // Act
            var act = () => sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(429);

            using var verifyContext = database.CreateContext();
            var codeCount = await verifyContext.UserVerificationCodes.CountAsync(v => v.UserId == user.Id);
            codeCount.Should().Be(1);
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task VerifyEmailAsync_ValidCode_MarksUserVerifiedAndCodeUsed()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            using var verifyContext = database.CreateContext();
            var reloadedUser = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloadedUser.IsVerified.Should().BeTrue();

            var reloadedCode = await verifyContext.UserVerificationCodes.SingleAsync(v => v.UserId == user.Id);
            reloadedCode.IsUsed.Should().BeTrue();
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_OutsideCooldown_InvalidatesPreviousCodesAndSendsNewOne()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.AddRange(
                new UserVerificationCode
                {
                    UserId = user.Id,
                    Code = "111111",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                },
                new UserVerificationCode
                {
                    UserId = user.Id,
                    Code = "222222",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService, resendCooldownSeconds: 60);

            // Act
            await sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            using var verifyContext = database.CreateContext();
            var codes = await verifyContext.UserVerificationCodes.Where(v => v.UserId == user.Id).ToListAsync();
            codes.Where(c => c.Code == "111111" || c.Code == "222222").Should().OnlyContain(c => c.IsUsed);

            var newCode = codes.SingleOrDefault(c => !c.IsUsed);
            newCode.Should().NotBeNull();
            newCode!.Code.Should().NotBe("111111").And.NotBe("222222");

            await emailService.Received(1).SendVerificationEmailAsync("user@test.com", newCode.Code);
        }
    }
}
