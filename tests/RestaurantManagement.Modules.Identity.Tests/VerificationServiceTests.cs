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
            int verificationCodeExpiryMinutes = 15,
            int maxVerificationAttempts = 5,
            int verificationLockoutDurationMinutes = 15)
        {
            var logger = Substitute.For<ILogger<VerificationService>>();
            var securityOptions = TestSecurityOptions.Create(
                verificationCodeExpiryMinutes: verificationCodeExpiryMinutes,
                resendCooldownSeconds: resendCooldownSeconds,
                maxVerificationAttempts: maxVerificationAttempts,
                verificationLockoutDurationMinutes: verificationLockoutDurationMinutes);

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
        public async Task VerifyEmailAsync_CodeBelongsToDifferentAccount_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var owner = new User { Email = "owner@test.com", PasswordHash = "hash", FirstName = "Own", LastName = "Er", IsVerified = false };
            var other = new User { Email = "other@test.com", PasswordHash = "hash", FirstName = "Oth", LastName = "Er", IsVerified = false };
            seedContext.Users.AddRange(owner, other);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = other.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act: the code exists and is valid, but for a different account than the one requested
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "owner@test.com", Code = "123456" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);

            using var verifyContext = database.CreateContext();
            var reloadedOwner = await verifyContext.Users.SingleAsync(u => u.Email == "owner@test.com");
            reloadedOwner.IsVerified.Should().BeFalse();
        }

        [Fact]
        public async Task VerifyEmailAsync_CodeNotYetExpired_MarksUserVerified()
        {
            // Arrange: code expires a couple of seconds in the future, i.e. still valid right now
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddSeconds(2),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeTrue();
        }

        [Fact]
        public async Task VerifyEmailAsync_SameCodeSubmittedTwice_SecondAttemptThrowsBusinessException()
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

            var request = new VerifyEmailRequest { Email = "user@test.com", Code = "123456" };

            using (var firstAttemptContext = database.CreateContext())
            {
                var firstSut = CreateSut(firstAttemptContext, Substitute.For<IEmailService>());
                await firstSut.VerifyEmailAsync(request);
            }

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act: submit the same, now-consumed code again
            var act = () => sut.VerifyEmailAsync(request);

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task VerifyEmailAsync_UserNotFound_ThrowsBusinessExceptionWithGenericMessage()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "nobody@test.com", Code = "123456" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);
            thrown.Which.Message.Should().Be("InvalidEmailOrCode");
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_UserNotFound_ReturnsAcknowledgementWithoutSendingEmail()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService);

            // Act
            var response = await sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "nobody@test.com" });

            // Assert
            response.Message.Should().Be("VerificationResendAcknowledged");
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_UserAlreadyVerified_ReturnsAcknowledgementWithoutSendingEmail()
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
            var response = await sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "verified@test.com" });

            // Assert
            response.Message.Should().Be("VerificationResendAcknowledged");
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_WithinCooldown_ReturnsAcknowledgementWithoutSendingEmail()
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
            var response = await sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            response.Message.Should().Be("VerificationResendAcknowledged");

            using var verifyContext = database.CreateContext();
            var codeCount = await verifyContext.UserVerificationCodes.CountAsync(v => v.UserId == user.Id);
            codeCount.Should().Be(1);
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_UnknownEmailAndVerifiedAccount_ReturnSameMessage()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User { Email = "verified@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = true });
            await seedContext.SaveChangesAsync();

            using var unknownContext = database.CreateContext();
            var unknownEmailService = Substitute.For<IEmailService>();
            var unknownSut = CreateSut(unknownContext, unknownEmailService);

            using var verifiedContext = database.CreateContext();
            var verifiedEmailService = Substitute.For<IEmailService>();
            var verifiedSut = CreateSut(verifiedContext, verifiedEmailService);

            // Act
            var unknownEmailResponse = await unknownSut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "nobody@test.com" });
            var alreadyVerifiedResponse = await verifiedSut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "verified@test.com" });

            // Assert
            unknownEmailResponse.Message.Should().Be(alreadyVerifiedResponse.Message);
            await unknownEmailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
            await verifiedEmailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task VerifyEmailAsync_UnknownEmailAndInvalidCode_ReturnSameStatusAndMessage()
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

            using var unknownContext = database.CreateContext();
            var unknownSut = CreateSut(unknownContext, Substitute.For<IEmailService>());

            using var invalidCodeContext = database.CreateContext();
            var invalidCodeSut = CreateSut(invalidCodeContext, Substitute.For<IEmailService>());

            // Act
            var unknownEmailAct = () => unknownSut.VerifyEmailAsync(new VerifyEmailRequest { Email = "nobody@test.com", Code = "654321" });
            var invalidCodeAct = () => invalidCodeSut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "654321" });

            // Assert
            var unknownEmailThrown = await unknownEmailAct.Should().ThrowAsync<BusinessException>();
            var invalidCodeThrown = await invalidCodeAct.Should().ThrowAsync<BusinessException>();

            unknownEmailThrown.Which.StatusCode.Should().Be(invalidCodeThrown.Which.StatusCode);
            unknownEmailThrown.Which.Message.Should().Be(invalidCodeThrown.Which.Message);
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_CooldownJustElapsed_SendsNewCode()
        {
            // Arrange: last code was created just over the cooldown window ago
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
                CreatedAt = DateTime.UtcNow.AddSeconds(-61)
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService, resendCooldownSeconds: 60);

            // Act
            var act = () => sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            await act.Should().NotThrowAsync();
            await emailService.Received(1).SendVerificationEmailAsync(user.Email, user.FirstName, Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task VerifyEmailAsync_WithCodeInvalidatedByResend_ThrowsBusinessException()
        {
            // Arrange: an unused code exists, then a resend invalidates it in favor of a new one
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "111111",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await seedContext.SaveChangesAsync();

            using (var resendContext = database.CreateContext())
            {
                var resendSut = CreateSut(resendContext, Substitute.For<IEmailService>());
                await resendSut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });
            }

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act: attempt to verify using the code issued before the resend
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "111111" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeFalse();
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
            var response = await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            response.Message.Should().Be("UserVerified");

            using var verifyContext = database.CreateContext();
            var reloadedUser = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloadedUser.IsVerified.Should().BeTrue();

            var reloadedCode = await verifyContext.UserVerificationCodes.SingleAsync(v => v.UserId == user.Id);
            reloadedCode.IsUsed.Should().BeTrue();
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_OutsideCooldown_SendsVerificationEmailWithFirstNameAndConfiguredExpiry()
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
            var response = await sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            response.Message.Should().Be("VerificationCodeResent");

            using var verifyContext = database.CreateContext();
            var codes = await verifyContext.UserVerificationCodes.Where(v => v.UserId == user.Id).ToListAsync();
            codes.Where(c => c.Code == "111111" || c.Code == "222222").Should().OnlyContain(c => c.IsUsed);

            var newCode = codes.SingleOrDefault(c => !c.IsUsed);
            newCode.Should().NotBeNull();
            newCode!.Code.Should().NotBe("111111").And.NotBe("222222");

            await emailService.Received(1).SendVerificationEmailAsync(user.Email, user.FirstName, newCode.Code, 15);
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_EmailSendingFails_ThrowsBusinessException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(Task.FromException(new Exception("SMTP unavailable")));
            var sut = CreateSut(context, emailService);

            // Act
            var act = () => sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(500);

            using var verifyContext = database.CreateContext();
            (await verifyContext.UserVerificationCodes.AnyAsync(v => v.UserId == user.Id)).Should().BeTrue();
        }

        [Fact]
        public async Task VerifyEmailAsync_IncorrectCode_IncrementsVerificationFailedAttempts()
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
            reloaded.VerificationFailedAttempts.Should().Be(1);
        }

        [Fact]
        public async Task VerifyEmailAsync_FinalFailedAttemptReachesMax_InvalidatesCodeAndSetsLockout()
        {
            // Arrange: user has already failed twice, so the next failure is the third and final attempt
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationFailedAttempts = 2
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

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>(), maxVerificationAttempts: 3);

            // Act
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "654321" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(423);

            using var verifyContext = database.CreateContext();
            var reloadedCode = await verifyContext.UserVerificationCodes.SingleAsync(v => v.UserId == user.Id);
            reloadedCode.IsUsed.Should().BeTrue();

            var reloadedUser = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloadedUser.VerificationLockedUntil.Should().NotBeNull();
            reloadedUser.VerificationLockedUntil!.Value.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public async Task VerifyEmailAsync_AccountLockedWithCorrectCode_ThrowsAndLeavesUserUnverified()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationLockedUntil = DateTime.UtcNow.AddMinutes(15)
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

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act: the code supplied is correct, but the account is locked out
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(423);

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeFalse();
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_AccountLocked_ThrowsAndSendsNoEmail()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationLockedUntil = DateTime.UtcNow.AddMinutes(15)
            };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "123456",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService);

            // Act
            var act = () => sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(423);
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());

            using var verifyContext = database.CreateContext();
            var codeCount = await verifyContext.UserVerificationCodes.CountAsync(v => v.UserId == user.Id);
            codeCount.Should().Be(1);
        }

        [Fact]
        public async Task VerifyEmailAsync_LockoutExpired_EvaluatesCodeAndVerifiesUser()
        {
            // Arrange: the lockout timestamp is in the past, i.e. no longer in effect
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationLockedUntil = DateTime.UtcNow.AddMinutes(-1)
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

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeTrue();
        }

        [Fact]
        public async Task VerifyEmailAsync_CorrectCodeAfterFailuresBelowMax_MarksUserVerified()
        {
            // Arrange: user has failed max-1 times already; this attempt uses the correct code
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationFailedAttempts = 4
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

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>(), maxVerificationAttempts: 5);

            // Act
            await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeTrue();
        }

        [Fact]
        public async Task VerifyEmailAsync_SuccessAfterPriorFailures_ResetsVerificationFailedAttempts()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationFailedAttempts = 3
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

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.VerificationFailedAttempts.Should().Be(0);
            reloaded.VerificationLockedUntil.Should().BeNull();
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_OutsideLockout_ResetsVerificationFailedAttempts()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationFailedAttempts = 3
            };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "111111",
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
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.VerificationFailedAttempts.Should().Be(0);

            var newCode = await verifyContext.UserVerificationCodes.SingleOrDefaultAsync(v => v.UserId == user.Id && !v.IsUsed);
            newCode.Should().NotBeNull();
        }

        [Fact]
        public async Task VerifyEmailAsync_CorrectCodeOnFirstAttempt_MarksUserVerified()
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
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeTrue();
            reloaded.VerificationFailedAttempts.Should().Be(0);
        }

        [Fact]
        public async Task ResendVerificationCodeAsync_LockoutExpired_SendsNewCodeAndResetsAttempts()
        {
            // Arrange: the lockout timestamp is in the past, i.e. no longer in effect
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationFailedAttempts = 0,
                VerificationLockedUntil = DateTime.UtcNow.AddMinutes(-1)
            };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                Code = "111111",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20)
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService, resendCooldownSeconds: 60);

            // Act
            await sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            await emailService.Received(1).SendVerificationEmailAsync(user.Email, user.FirstName, Arg.Any<string>(), Arg.Any<int>());

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.VerificationFailedAttempts.Should().Be(0);
        }
    }
}
