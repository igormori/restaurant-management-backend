using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class RegistrationServiceTests : IClassFixture<IdentityPostgresFixture>
    {
        private readonly IdentityPostgresFixture _postgresFixture;

        public RegistrationServiceTests(IdentityPostgresFixture postgresFixture)
        {
            _postgresFixture = postgresFixture;
        }

        [Fact]
        [Trait("Rule", "R3")]
        public async Task RegisterAsync_EmailAlreadyRegistered_ThrowsBusinessExceptionAndSendsNoEmail()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "existing@test.com",
                PasswordHash = "hash",
                FirstName = "Existing",
                LastName = "User"
            });
            await seedContext.SaveChangesAsync();

            var emailService = Substitute.For<IEmailService>();
            var logger = Substitute.For<ILogger<RegistrationService>>();

            using var context = database.CreateContext();
            var sut = new RegistrationService(context, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger, TestVerificationCodeHasher.Create(), TestPasswordHasher.Create());

            var request = new RegisterRequest
            {
                Email = "existing@test.com",
                Password = "Password1!",
                FirstName = "New",
                LastName = "Attempt"
            };

            // Act
            var act = () => sut.RegisterAsync(request);

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(400);
            await emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        [Trait("Rule", "R1")]
        public async Task RegisterAsync_ValidRequest_SendsVerificationEmailWithFirstNameAndConfiguredExpiry()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var emailService = Substitute.For<IEmailService>();
            var logger = Substitute.For<ILogger<RegistrationService>>();
            var securityOptions = TestSecurityOptions.Create(verificationCodeExpiryMinutes: 15);

            using var context = database.CreateContext();
            var sut = new RegistrationService(context, securityOptions, TestLocalizer.Create(), emailService, logger, TestVerificationCodeHasher.Create(), TestPasswordHasher.Create());

            var request = new RegisterRequest
            {
                Email = "newuser@test.com",
                Password = "Password1!",
                FirstName = "New",
                LastName = "User"
            };

            var beforeRegistration = DateTime.UtcNow;

            // Act
            await sut.RegisterAsync(request);

            // Assert
            using var verifyContext = database.CreateContext();
            var user = await verifyContext.Users.SingleAsync(u => u.Email == "newuser@test.com");
            user.IsVerified.Should().BeFalse();

            var code = await verifyContext.UserVerificationCodes.SingleAsync(v => v.UserId == user.Id);
            code.ExpiresAt.Should().BeCloseTo(beforeRegistration.AddMinutes(15), TimeSpan.FromSeconds(5));

            await emailService.Received(1).SendVerificationEmailAsync(user.Email, "New", Arg.Any<string>(), 15);
        }

        [Fact]
        [Trait("Rule", "R1")]
        [Trait("Rule", "R12")]
        public async Task RegisterAsync_EmailSendingFails_StillCreatesUnverifiedUser()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var emailService = Substitute.For<IEmailService>();
            emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(Task.FromException(new Exception("SMTP unavailable")));
            var logger = Substitute.For<ILogger<RegistrationService>>();

            using var context = database.CreateContext();
            var sut = new RegistrationService(context, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger, TestVerificationCodeHasher.Create(), TestPasswordHasher.Create());

            var request = new RegisterRequest
            {
                Email = "failedemail@test.com",
                Password = "Password1!",
                FirstName = "Failed",
                LastName = "Email"
            };

            // Act
            var act = () => sut.RegisterAsync(request);

            // Assert
            await act.Should().NotThrowAsync();

            using var verifyContext = database.CreateContext();
            var user = await verifyContext.Users.SingleAsync(u => u.Email == "failedemail@test.com");
            user.IsVerified.Should().BeFalse();
            (await verifyContext.UserVerificationCodes.AnyAsync(v => v.UserId == user.Id)).Should().BeTrue();
        }

        [Fact]
        [Trait("Rule", "R4")]
        public async Task RegisterAsync_ExistingEmailDifferentCasing_RejectsDuplicate()
        {
            // Arrange: seed a row with mixed-case email directly, bypassing RegisterRequest's
            // own lowercasing, to simulate data written through another path (e.g. a future
            // admin tool). Its exact string differs from the incoming request, so the AnyAsync
            // pre-check cannot catch it and only the database's case-insensitive index can.
            using var seedContext = _postgresFixture.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "User@Example.com",
                PasswordHash = "hash",
                FirstName = "Existing",
                LastName = "User"
            });
            await seedContext.SaveChangesAsync();

            var emailService = Substitute.For<IEmailService>();
            var logger = Substitute.For<ILogger<RegistrationService>>();

            using var context = _postgresFixture.CreateContext();
            var sut = new RegistrationService(context, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger, TestVerificationCodeHasher.Create(), TestPasswordHasher.Create());

            var request = new RegisterRequest
            {
                Email = "user@example.com",
                Password = "Password1!",
                FirstName = "New",
                LastName = "Attempt"
            };

            // Act
            var act = () => sut.RegisterAsync(request);

            // Assert
            await act.Should().ThrowAsync<BusinessException>();

            using var verifyContext = _postgresFixture.CreateContext();
            (await verifyContext.Users.CountAsync(u => u.Email == "User@Example.com" || u.Email == "user@example.com"))
                .Should().Be(1);
        }

        [Fact]
        [Trait("Rule", "R5")]
        public async Task RegisterAsync_UniqueViolationOnSave_ThrowsBusinessExceptionWithPreCheckMessage()
        {
            // Arrange: same setup as the casing test above, which bypasses the AnyAsync
            // pre-check and forces the database's unique index to reject the insert.
            using var seedContext = _postgresFixture.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "Race@Example.com",
                PasswordHash = "hash",
                FirstName = "Existing",
                LastName = "User"
            });
            await seedContext.SaveChangesAsync();

            var emailService = Substitute.For<IEmailService>();
            var logger = Substitute.For<ILogger<RegistrationService>>();

            using var context = _postgresFixture.CreateContext();
            var sut = new RegistrationService(context, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger, TestVerificationCodeHasher.Create(), TestPasswordHasher.Create());

            var request = new RegisterRequest
            {
                Email = "race@example.com",
                Password = "Password1!",
                FirstName = "New",
                LastName = "Attempt"
            };

            // Act
            var act = () => sut.RegisterAsync(request);

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(400);
            thrown.Which.Message.Should().Be("EmailAlreadyRegistered");
        }

        // Hash verification codes: Scenario 27
        [Fact]
        [Trait("Rule", "R13")]
        public async Task RegisterAsync_ValidRequest_StoresFingerprintOfEmailedCodeNotTheCode()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var emailService = Substitute.For<IEmailService>();
            string? emailedCode = null;
            emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(code => emailedCode = code), Arg.Any<int>()).Returns(Task.CompletedTask);
            var logger = Substitute.For<ILogger<RegistrationService>>();
            var hasher = TestVerificationCodeHasher.Create();

            using var context = database.CreateContext();
            var sut = new RegistrationService(context, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger, hasher, TestPasswordHasher.Create());

            var request = new RegisterRequest
            {
                Email = "joao.pereira@cantina.com.br",
                Password = "Password1!",
                FirstName = "João",
                LastName = "Pereira"
            };

            // Act
            await sut.RegisterAsync(request);

            // Assert
            using var verifyContext = database.CreateContext();
            var user = await verifyContext.Users.SingleAsync(u => u.Email == "joao.pereira@cantina.com.br");
            var stored = await verifyContext.UserVerificationCodes.SingleAsync(v => v.UserId == user.Id);
            emailedCode.ShouldNotBeNull();
            stored.CodeHash.ShouldNotContain(emailedCode);
            hasher.IsMatch(user.Id, emailedCode, stored.CodeHash).ShouldBeTrue();
        }
    }
}
