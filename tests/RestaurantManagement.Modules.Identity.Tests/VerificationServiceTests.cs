using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services.Registration;
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

            return TestVerificationService.Create(context, securityOptions, logger, Hasher);
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
            thrown.Which.StatusCode.Should().Be(401, "the wrong code that trips the lock gets the same 401 as any wrong code, so the lock is not revealed");
            thrown.Which.Message.Should().Be("InvalidEmailOrCode", "the wrong code that trips the lock gets the same generic message as any wrong code, so the lock is not revealed");

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
            thrown.Which.StatusCode.Should().Be(401, "a locked account is refused with the generic 401 even with the correct code");
            thrown.Which.Message.Should().Be("InvalidEmailOrCode", "a locked account gets the same generic message as any wrong code, so the lock is not revealed");

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeFalse("a locked account stays unverified even with the correct code");
        }

        [Fact]
        [Trait("Rule", "V11")]
        public async Task VerifyEmailAsync_LockedAccountWithCorrectCode_RunsTheCodeLookupLikeAnUnknownEmail()
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

            var queries = new CommandTextRecorder();
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(seedContext.Database.GetDbConnection())
                .AddInterceptors(queries)
                .Options;
            using var context = new IdentityDbContext(options);
            var sut = CreateSut(context, Substitute.For<IEmailService>());

            // Act: the code is correct, but the account is locked out
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "123456" });

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401, "a locked account is refused with the generic 401 even with the correct code");
            queries.Commands.Should().Contain(c => c.Contains("UserVerificationCodes"),
                "a locked account runs the same code lookup as an unknown email, so the answer takes the same time and the lock is not revealed");

            using var verifyContext = database.CreateContext();
            var reloaded = await verifyContext.Users.SingleAsync(u => u.Email == "user@test.com");
            reloaded.IsVerified.Should().BeFalse("a locked account stays unverified even with the correct code");
        }

        private sealed class CommandTextRecorder : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
        {
            public List<string> Commands { get; } = new();

            public override Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
                System.Data.Common.DbCommand command,
                Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
                Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result)
            {
                Commands.Add(command.CommandText);
                return result;
            }

            public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
                System.Data.Common.DbCommand command,
                Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
                Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                Commands.Add(command.CommandText);
                return ValueTask.FromResult(result);
            }
        }

        [Fact]
        [Trait("Rule", "V11")]
        public async Task VerifyEmailAsync_LockedAccountAndUnknownEmail_ReturnSameStatusAndMessage()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "user@test.com",
                PasswordHash = "hash",
                FirstName = "A",
                LastName = "B",
                IsVerified = false,
                VerificationLockedUntil = DateTime.UtcNow.AddMinutes(15)
            });
            await seedContext.SaveChangesAsync();

            using var lockedContext = database.CreateContext();
            var lockedSut = CreateSut(lockedContext, Substitute.For<IEmailService>());
            using var unknownContext = database.CreateContext();
            var unknownSut = CreateSut(unknownContext, Substitute.For<IEmailService>());

            // Act
            var lockedAct = () => lockedSut.VerifyEmailAsync(new VerifyEmailRequest { Email = "user@test.com", Code = "654321" });
            var unknownAct = () => unknownSut.VerifyEmailAsync(new VerifyEmailRequest { Email = "nobody@test.com", Code = "654321" });

            // Assert
            var lockedThrown = await lockedAct.Should().ThrowAsync<BusinessException>();
            var unknownThrown = await unknownAct.Should().ThrowAsync<BusinessException>();
            lockedThrown.Which.StatusCode.Should().Be(unknownThrown.Which.StatusCode, "a locked account gets the same status as an unknown email on verify");
            lockedThrown.Which.Message.Should().Be(unknownThrown.Which.Message, "a locked account gets the same message as an unknown email on verify");
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
    }
}
