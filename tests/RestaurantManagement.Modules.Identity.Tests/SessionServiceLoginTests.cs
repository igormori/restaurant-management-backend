using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using NSubstitute;
using NSubstitute.Core;
using Sentry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class SessionServiceLoginTests
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

        private static SessionService CreateSut(
            IdentityDbContext context,
            int maxFailedLoginAttempts = 5,
            int lockoutDurationMinutes = 15,
            IHub? hub = null)
        {
            var securityOptions = TestSecurityOptions.Create(
                maxFailedLoginAttempts: maxFailedLoginAttempts,
                lockoutDurationMinutes: lockoutDurationMinutes);

            var jwtOptions = CreateJwtOptions();
            return new SessionService(context, securityOptions, jwtOptions, TestLocalizer.Create(), hub ?? Substitute.For<IHub>(), Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), TestPasswordHasher.Create());
        }

        private static List<SentryEvent> CapturedEvents(IHub hub)
        {
            return hub.ReceivedCalls()
                .Where(call => call.GetMethodInfo().Name == nameof(IHub.CaptureEvent))
                .Select(call => (SentryEvent)call.GetArguments()[0]!)
                .ToList();
        }

        [Fact]
        public async Task LoginAsync_UnknownEmailAndWrongPassword_ReturnSameStatusAndMessage()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            using var unknownEmailContext = database.CreateContext();
            var unknownEmailSut = CreateSut(unknownEmailContext);

            using var wrongPasswordContext = database.CreateContext();
            var wrongPasswordSut = CreateSut(wrongPasswordContext);

            // Act
            var unknownEmailAct = () => unknownEmailSut.LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = Password }, TestClients.KitchenTablet);
            var wrongPasswordAct = () => wrongPasswordSut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet);

            // Assert
            var unknownEmailThrown = await unknownEmailAct.Should().ThrowAsync<BusinessException>();
            var wrongPasswordThrown = await wrongPasswordAct.Should().ThrowAsync<BusinessException>();

            unknownEmailThrown.Which.StatusCode.Should().Be(401);
            unknownEmailThrown.Which.StatusCode.Should().Be(wrongPasswordThrown.Which.StatusCode);
            unknownEmailThrown.Which.Message.Should().Be(wrongPasswordThrown.Which.Message);
        }

        [Fact]
        public async Task LoginAsync_UnverifiedCorrectPassword_MatchesGenericFailure()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            });
            seedContext.Users.Add(new User
            {
                Email = "unverified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Un",
                LastName = "Verified",
                IsVerified = false
            });
            await seedContext.SaveChangesAsync();

            using var wrongPasswordContext = database.CreateContext();
            var wrongPasswordSut = CreateSut(wrongPasswordContext);

            using var unverifiedContext = database.CreateContext();
            var unverifiedSut = CreateSut(unverifiedContext);

            // Act
            var wrongPasswordAct = () => wrongPasswordSut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet);
            var unverifiedAct = () => unverifiedSut.LoginAsync(new LoginRequest { Email = "unverified@test.com", Password = Password }, TestClients.KitchenTablet);

            // Assert
            var wrongPasswordThrown = await wrongPasswordAct.Should().ThrowAsync<BusinessException>();
            var unverifiedThrown = await unverifiedAct.Should().ThrowAsync<BusinessException>();

            unverifiedThrown.Which.StatusCode.Should().Be(401);
            unverifiedThrown.Which.StatusCode.Should().Be(wrongPasswordThrown.Which.StatusCode);
            unverifiedThrown.Which.Message.Should().Be(wrongPasswordThrown.Which.Message);
        }

        // Scenario 10
        [Fact]
        public async Task LoginAsync_LockedAccountCorrectPassword_Throws401MatchingWrongPassword()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "locked@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true,
                LockedUntil = DateTime.UtcNow.AddMinutes(15)
            });
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            using var lockedContext = database.CreateContext();
            var lockedSut = CreateSut(lockedContext);

            using var wrongPasswordContext = database.CreateContext();
            var wrongPasswordSut = CreateSut(wrongPasswordContext);

            // Act: correct password supplied, but the account is locked
            var lockedAct = () => lockedSut.LoginAsync(new LoginRequest { Email = "locked@test.com", Password = Password }, TestClients.KitchenTablet);
            var wrongPasswordAct = () => wrongPasswordSut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet);

            // Assert
            var lockedThrown = await lockedAct.Should().ThrowAsync<BusinessException>();
            var wrongPasswordThrown = await wrongPasswordAct.Should().ThrowAsync<BusinessException>();

            lockedThrown.Which.StatusCode.Should().Be(401);
            lockedThrown.Which.Message.Should().Be("InvalidEmailOrPassword");
            lockedThrown.Which.StatusCode.Should().Be(wrongPasswordThrown.Which.StatusCode);
            lockedThrown.Which.Message.Should().Be(wrongPasswordThrown.Which.Message);
        }

        // Scenario 11
        [Fact]
        public async Task LoginAsync_LockedAccountWrongPassword_Throws401WithoutCountingAttemptOrExtendingLock()
        {
            // Arrange
            var lockedUntil = DateTime.UtcNow.AddMinutes(10);
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "locked@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true,
                FailedAttempts = 0,
                LockedUntil = lockedUntil
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, maxFailedLoginAttempts: 1);

            // Act
            var act = () => sut.LoginAsync(new LoginRequest { Email = "locked@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet);

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401);

            using var assertContext = database.CreateContext();
            var user = await assertContext.Users.SingleAsync(u => u.Email == "locked@test.com");
            user.FailedAttempts.Should().Be(0);
            user.LockedUntil.Should().BeCloseTo(lockedUntil, TimeSpan.FromSeconds(1));
        }

        // Scenario 12
        [Fact]
        public async Task LoginAsync_FailedAttemptsReachLimit_SentryMessageHasUserIdAndNoEmail()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "marina.souza@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Marina",
                LastName = "Souza",
                IsVerified = true
            };
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();

            var hub = Substitute.For<IHub>();
            hub.IsEnabled.Returns(true);
            using var context = database.CreateContext();
            var sut = CreateSut(context, maxFailedLoginAttempts: 1, hub: hub);

            // Act
            var act = () => sut.LoginAsync(new LoginRequest { Email = "marina.souza@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet);
            await act.Should().ThrowAsync<BusinessException>();

            // Assert
            var lockoutMessage = CapturedEvents(hub).Should().ContainSingle().Subject.Message?.Formatted;
            lockoutMessage.Should().Contain(user.Id.ToString());
            lockoutMessage.Should().NotContain("marina.souza@test.com");
        }

        // Scenario 13
        [Fact]
        public async Task LoginAsync_JwtExpireMinutesLeftAtDefault_TokenExpiresIn15Minutes()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            var defaultExpiryJwtOptions = Options.Create(new JwtOptions
            {
                Key = "test-signing-key-at-least-32-characters-long",
                Issuer = "RestaurantManagementTests",
                Audience = "RestaurantManagementTests"
            });
            using var context = database.CreateContext();
            var sut = new SessionService(
                context,
                TestSecurityOptions.Create(),
                defaultExpiryJwtOptions,
                TestLocalizer.Create(),
                Substitute.For<IHub>(),
                Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(),
                new RefreshTokenService(context, defaultExpiryJwtOptions),
                TestPasswordHasher.Create());

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = Password }, TestClients.KitchenTablet);

            // Assert
            var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Auth.Token);
            token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsTokens()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "verified@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "A",
                LastName = "B",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = Password }, TestClients.KitchenTablet);

            // Assert
            response.Auth.Token.Should().NotBeNullOrEmpty();
            response.RefreshToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task LoginAsync_UnknownEmail_VerifiesAgainstAWorkFactor12DummyHash()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var hasher = Substitute.For<IPasswordHasher>();
            var jwtOptions = CreateJwtOptions();
            var sut = new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), hasher);

            // Act
            var act = () => sut.LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = Password }, TestClients.KitchenTablet);
            await act.Should().ThrowAsync<BusinessException>();

            // Assert
            hasher.Received(1).Verify(Password, Arg.Is<string>(hash => hash.StartsWith("$2a$12$")));
        }

        [Fact]
        public async Task LoginAsync_LockExpiredCorrectPassword_IssuesTokensAndClearsLock()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "maria@cantinadanonna.com.br",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true,
                FailedAttempts = 0,
                LockedUntil = DateTime.UtcNow.AddMinutes(-1)
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "maria@cantinadanonna.com.br", Password = Password }, TestClients.KitchenTablet);

            // Assert
            response.Auth.Token.ShouldNotBeNullOrEmpty();
            response.RefreshToken.ShouldNotBeNullOrEmpty();
            using var readContext = database.CreateContext();
            var maria = await readContext.Users.SingleAsync();
            maria.LockedUntil.ShouldBeNull();
        }
    }
}
