using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
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
        private static readonly IVerificationCodeHasher Hasher = TestVerificationCodeHasher.Create();

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

            return new VerificationService(context, securityOptions, TestLocalizer.Create(), emailService, logger, Hasher);
        }

        [Fact]
        [Trait("Rule", "V2")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            thrown.Which.StatusCode.Should().Be(401, "a wrong code is refused with the generic 401");

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeFalse("a wrong code must not verify the account");
        }

        // Hash verification codes: Scenario 23
        [Fact]
        [Trait("Rule", "V2")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            thrown.Which.StatusCode.Should().Be(401, "an expired code is refused with the generic 401");
        }

        // Hash verification codes: Scenario 24
        [Fact]
        [Trait("Rule", "V2")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            thrown.Which.StatusCode.Should().Be(401, "a code that was already used is refused with the generic 401");
        }

        // Hash verification codes: Scenario 25
        [Fact]
        [Trait("Rule", "V2")]
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
                CodeHash = Hasher.ComputeHash(other.Id, "123456"),
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
            thrown.Which.StatusCode.Should().Be(401, "another account's code is refused with the generic 401");

            using var verifyContext = database.CreateContext();
            var reloadedOwner = await verifyContext.Users.SingleAsync(u => u.Email == "owner@test.com");
            reloadedOwner.IsVerified.Should().BeFalse("another account's code must not verify this account");
        }

        [Fact]
        [Trait("Rule", "V1")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            reloaded.IsVerified.Should().BeTrue("a correct, unexpired code verifies the account");
        }

        [Fact]
        [Trait("Rule", "V8")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            thrown.Which.StatusCode.Should().Be(401, "the same code cannot be used a second time");
        }

        [Fact]
        [Trait("Rule", "V3")]
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
            thrown.Which.StatusCode.Should().Be(401, "an unknown email is refused with the same 401 as a wrong code");
            thrown.Which.Message.Should().Be("InvalidEmailOrCode", "an unknown email gets the generic message, so nobody can tell which emails exist");
        }

        [Fact]
        [Trait("Rule", "RS1")]
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
            response.Message.Should().Be("VerificationResendAcknowledged", "resend answers the same generic acknowledgement for an unknown email");
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        [Trait("Rule", "RS1")]
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
            response.Message.Should().Be("VerificationResendAcknowledged", "resend answers the same generic acknowledgement for an already verified account");
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        // Hash verification codes: Scenario 32
        [Fact]
        [Trait("Rule", "RS1")]
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
            response.Message.Should().Be("VerificationResendAcknowledged", "a resend within 60 seconds gets the same generic acknowledgement");

            using var verifyContext = database.CreateContext();
            var codeCount = await verifyContext.UserVerificationCodes.CountAsync(v => v.UserId == user.Id);
            codeCount.Should().Be(1, "a resend within 60 seconds creates no new code");
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        [Trait("Rule", "RS1")]
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
            unknownEmailResponse.Message.Should().Be(alreadyVerifiedResponse.Message, "resend must answer the same for an unknown email and a verified one");
            await unknownEmailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
            await verifiedEmailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        // Hash verification codes: Scenario 33
        [Fact]
        [Trait("Rule", "V3")]
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

            unknownEmailThrown.Which.StatusCode.Should().Be(invalidCodeThrown.Which.StatusCode, "an unknown email gets the same status as a wrong code");
            unknownEmailThrown.Which.Message.Should().Be(invalidCodeThrown.Which.Message, "an unknown email gets the same message as a wrong code");
        }

        [Fact]
        [Trait("Rule", "RS2")]
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
            await act.Should().NotThrowAsync("once the 60-second wait has passed a resend is accepted");
            await emailService.Received(1).SendVerificationEmailAsync(user.Email, user.FirstName, Arg.Any<string>(), Arg.Any<int>());
        }

        // Hash verification codes: Scenario 26
        [Fact]
        [Trait("Rule", "V7")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "111111"),
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
            thrown.Which.StatusCode.Should().Be(401, "a code issued before a resend is refused");

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeFalse("the replaced code must not verify the account");
        }

        // Hash verification codes: Scenario 29
        [Fact]
        [Trait("Rule", "V1")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            var response = await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            response.Message.Should().Be("UserVerified", "a correct code answers with the verified message");

            using var verifyContext = database.CreateContext();
            var reloadedUser = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloadedUser.IsVerified.Should().BeTrue("a correct code verifies the account");

            var reloadedCode = await verifyContext.UserVerificationCodes.SingleAsync(v => v.UserId == user.Id);
            reloadedCode.IsUsed.Should().BeTrue("the code is marked used once it verifies the account");
        }

        // Hash verification codes: Scenario 34
        [Fact]
        [Trait("Rule", "RS2")]
        public async Task ResendVerificationCodeAsync_OutsideCooldown_MarksEarlierCodesUsedAndEmailsNewCode()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "user@test.com", PasswordHash = "hash", FirstName = "A", LastName = "B", IsVerified = false };
            seedContext.Users.Add(user);
            var firstCode = new UserVerificationCode
            {
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            var secondCode = new UserVerificationCode
            {
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            seedContext.UserVerificationCodes.AddRange(firstCode, secondCode);
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            var sut = CreateSut(context, emailService, resendCooldownSeconds: 60);

            // Act
            var response = await sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "user@test.com" });

            // Assert
            response.Message.Should().Be("VerificationCodeResent", "after the 60-second wait a resend answers that a new code was sent");

            using var verifyContext = database.CreateContext();
            var codes = await verifyContext.UserVerificationCodes.Where(v => v.UserId == user.Id).ToListAsync();
            codes.Where(c => c.Id == firstCode.Id || c.Id == secondCode.Id).Should().HaveCount(2).And.OnlyContain(c => c.IsUsed, "a resend cancels every older open code");
            codes.Should().ContainSingle(c => !c.IsUsed && c.Id != firstCode.Id && c.Id != secondCode.Id, "a resend leaves exactly one new open code");

            await emailService.Received(1).SendVerificationEmailAsync(user.Email, user.FirstName, Arg.Any<string>(), 15);
        }

        [Fact]
        [Trait("Rule", "RS6")]
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
            var thrown = await act.Should().ThrowAsync<BusinessException>("a resend whose email cannot be sent reports the failure");
            thrown.Which.StatusCode.Should().Be(500, "a failed email send is a 500");

            using var verifyContext = database.CreateContext();
            (await verifyContext.UserVerificationCodes.AnyAsync(v => v.UserId == user.Id)).Should().BeTrue("the new code is still stored when the email fails");
        }

        // Hash verification codes: Scenario 20
        [Fact]
        [Trait("Rule", "V2")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            thrown.Which.StatusCode.Should().Be(401, "a wrong code is refused with the generic 401");

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.VerificationFailedAttempts.Should().Be(1, "each wrong code adds one to the failed-attempt counter");
        }

        // Hash verification codes: Scenario 22
        [Fact]
        [Trait("Rule", "V4")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            thrown.Which.StatusCode.Should().Be(423, "the last allowed wrong code locks verification");

            using var verifyContext = database.CreateContext();
            var reloadedCode = await verifyContext.UserVerificationCodes.SingleAsync(v => v.UserId == user.Id);
            reloadedCode.IsUsed.Should().BeTrue("locking verification cancels every open code");

            var reloadedUser = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloadedUser.VerificationLockedUntil.Should().NotBeNull("the last wrong code starts a verification lock");
            reloadedUser.VerificationLockedUntil!.Value.Should().BeAfter(DateTime.UtcNow, "the verification lock lies in the future");
        }

        // Hash verification codes: Scenario 21
        [Fact]
        [Trait("Rule", "V5")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            thrown.Which.StatusCode.Should().Be(423, "a locked account is refused with a 423 even with the correct code");

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeFalse("a locked account stays unverified even with the correct code");
        }

        // Hash verification codes: Scenario 35
        [Fact]
        [Trait("Rule", "RS3")]
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
            thrown.Which.StatusCode.Should().Be(423, "a locked account cannot ask for a new code");
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());

            using var verifyContext = database.CreateContext();
            var codeCount = await verifyContext.UserVerificationCodes.CountAsync(v => v.UserId == user.Id);
            codeCount.Should().Be(1, "no new code is created while verification is locked");
        }

        // Hash verification codes: Scenario 31
        [Fact]
        [Trait("Rule", "V6")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            reloaded.IsVerified.Should().BeTrue("once the lock has expired the correct code verifies the account");
        }

        [Fact]
        [Trait("Rule", "V1")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            reloaded.IsVerified.Should().BeTrue("a correct code still verifies after fewer wrong codes than the limit");
        }

        [Fact]
        [Trait("Rule", "V1")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            reloaded.VerificationFailedAttempts.Should().Be(0, "a successful verification resets the failed-attempt counter");
            reloaded.VerificationLockedUntil.Should().BeNull("a successful verification leaves no lock");
        }

        [Fact]
        [Trait("Rule", "RS2")]
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
            reloaded.VerificationFailedAttempts.Should().Be(0, "a resend after the wait resets the failed-attempt counter");

            var newCode = await verifyContext.UserVerificationCodes.SingleOrDefaultAsync(v => v.UserId == user.Id && !v.IsUsed);
            newCode.Should().NotBeNull("a resend leaves one new open code");
        }

        [Fact]
        [Trait("Rule", "V1")]
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
                CodeHash = Hasher.ComputeHash(user.Id, "123456"),
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
            reloaded.IsVerified.Should().BeTrue("a correct code verifies the account");
            reloaded.VerificationFailedAttempts.Should().Be(0, "a correct code leaves the failed-attempt counter at zero");
        }

        [Fact]
        [Trait("Rule", "RS4")]
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
            reloaded.VerificationFailedAttempts.Should().Be(0, "a resend after the lock expired resets the failed-attempt counter");
        }

        // Hash verification codes: Scenario 28
        [Fact]
        [Trait("Rule", "RS2")]
        public async Task ResendVerificationCodeAsync_OutsideCooldown_StoresFingerprintOfEmailedCodeNotTheCode()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "maria.souza@cantina.com.br", PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = false };
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var emailService = Substitute.For<IEmailService>();
            string? emailedCode = null;
            emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(code => emailedCode = code), Arg.Any<int>()).Returns(Task.CompletedTask);
            var sut = CreateSut(context, emailService);

            // Act
            await sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "maria.souza@cantina.com.br" });

            // Assert
            using var verifyContext = database.CreateContext();
            var stored = await verifyContext.UserVerificationCodes.SingleAsync(v => v.UserId == user.Id && !v.IsUsed);
            emailedCode.ShouldNotBeNull();
            stored.CodeHash.ShouldNotContain(emailedCode, customMessage: "the verification code is never stored readable");
            Hasher.IsMatch(user.Id, emailedCode, stored.CodeHash).ShouldBeTrue("only the fingerprint of the emailed code is stored");
        }

        // Hash verification codes: Scenario 30
        [Fact]
        [Trait("Rule", "V7")]
        public async Task VerifyEmailAsync_NewestCodeAfterResend_MarksUserVerified()
        {
            // Arrange: an earlier code exists, then a resend emails a newer one
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User { Email = "maria.souza@cantina.com.br", PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = false };
            seedContext.Users.Add(user);
            seedContext.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                CodeHash = Hasher.ComputeHash(user.Id, "111111"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            await seedContext.SaveChangesAsync();

            var emailService = Substitute.For<IEmailService>();
            string? emailedCode = null;
            emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(code => emailedCode = code), Arg.Any<int>()).Returns(Task.CompletedTask);
            using (var resendContext = database.CreateContext())
            {
                await CreateSut(resendContext, emailService).ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "maria.souza@cantina.com.br" });
            }

            using var context = database.CreateContext();
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act
            await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "maria.souza@cantina.com.br", Code = emailedCode! });

            // Assert
            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "maria.souza@cantina.com.br");
            reloaded.IsVerified.ShouldBeTrue("the newest code sent by a resend verifies the account");
        }
    }
}
