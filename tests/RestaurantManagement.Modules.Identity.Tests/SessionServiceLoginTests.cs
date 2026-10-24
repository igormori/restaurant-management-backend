using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using NSubstitute;
using NSubstitute.Core;
using Sentry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Lockout;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Models.Sessions;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Modules.Identity.Services.Sessions;
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
        [Trait("Rule", "L5")]
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
            var unknownEmailAct = () => unknownEmailSut.LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = Password }, TestClients.KitchenTablet, null);
            var wrongPasswordAct = () => wrongPasswordSut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet, null);

            // Assert
            var unknownEmailThrown = await unknownEmailAct.Should().ThrowAsync<BusinessException>();
            var wrongPasswordThrown = await wrongPasswordAct.Should().ThrowAsync<BusinessException>();

            unknownEmailThrown.Which.StatusCode.Should().Be(401, "an unknown email is refused with a 401");
            unknownEmailThrown.Which.StatusCode.Should().Be(wrongPasswordThrown.Which.StatusCode, "an unknown email gets the same status as a wrong password, so nobody can tell which accounts exist");
            unknownEmailThrown.Which.Message.Should().Be(wrongPasswordThrown.Which.Message, "an unknown email gets the same message as a wrong password");
        }

        [Fact]
        [Trait("Rule", "L5")]
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
            var wrongPasswordAct = () => wrongPasswordSut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet, null);
            var unverifiedAct = () => unverifiedSut.LoginAsync(new LoginRequest { Email = "unverified@test.com", Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            var wrongPasswordThrown = await wrongPasswordAct.Should().ThrowAsync<BusinessException>();
            var unverifiedThrown = await unverifiedAct.Should().ThrowAsync<BusinessException>();

            unverifiedThrown.Which.StatusCode.Should().Be(401, "an unverified account is refused with a 401 even with the correct password");
            unverifiedThrown.Which.StatusCode.Should().Be(wrongPasswordThrown.Which.StatusCode, "an unverified account gets the same status as a wrong password");
            unverifiedThrown.Which.Message.Should().Be(wrongPasswordThrown.Which.Message, "an unverified account gets the same message as a wrong password");
        }

        // Scenario 10
        [Fact]
        [Trait("Rule", "L5")]
        [Trait("Rule", "L9")]
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
            var lockedAct = () => lockedSut.LoginAsync(new LoginRequest { Email = "locked@test.com", Password = Password }, TestClients.KitchenTablet, null);
            var wrongPasswordAct = () => wrongPasswordSut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet, null);

            // Assert
            var lockedThrown = await lockedAct.Should().ThrowAsync<BusinessException>();
            var wrongPasswordThrown = await wrongPasswordAct.Should().ThrowAsync<BusinessException>();

            lockedThrown.Which.StatusCode.Should().Be(401, "a locked account is refused with a 401 even with the correct password");
            lockedThrown.Which.Message.Should().Be("InvalidEmailOrPassword", "a locked account gets the generic login failure message");
            lockedThrown.Which.StatusCode.Should().Be(wrongPasswordThrown.Which.StatusCode, "a locked account gets the same status as a wrong password");
            lockedThrown.Which.Message.Should().Be(wrongPasswordThrown.Which.Message, "a locked account gets the same message as a wrong password");
        }

        // Scenario 11
        [Fact]
        [Trait("Rule", "L10")]
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
                LockedUntil = lockedUntil
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context, maxFailedLoginAttempts: 1);

            // Act
            var act = () => sut.LoginAsync(new LoginRequest { Email = "locked@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet, null);

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(401, "a wrong password on a locked account is refused with a 401");

            using var assertContext = database.CreateContext();
            var user = await assertContext.Users.SingleAsync(u => u.Email == "locked@test.com");
            (await assertContext.Set<LoginFailure>().CountAsync()).Should().Be(0, "a wrong password while locked is not counted");
            user.LockedUntil.Should().BeCloseTo(lockedUntil, TimeSpan.FromSeconds(1), "a wrong password while locked does not extend the lock");
        }

        // Scenario 12
        [Fact]
        [Trait("Rule", "L18")]
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
            var act = () => sut.LoginAsync(new LoginRequest { Email = "marina.souza@test.com", Password = "WrongPassword1!" }, TestClients.KitchenTablet, null);
            await act.Should().ThrowAsync<BusinessException>();

            // Assert
            var lockoutMessage = CapturedEvents(hub).Should().ContainSingle().Subject.Message?.Formatted;
            lockoutMessage.Should().Contain(user.Id.ToString(), "the lockout alert identifies the user by ID");
            lockoutMessage.Should().NotContain("marina.souza@test.com", "the lockout alert must never contain the user's email");
            lockoutMessage.Should().NotContain(TestClients.KitchenTablet.IpAddress!, "the lockout alert must never contain the IP address");
        }

        // Scenario 13
        [Fact]
        [Trait("Rule", "L19")]
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
            var response = await sut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Auth.Token);
            token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1), "the access token lives for 15 minutes by default");
        }

        [Fact]
        [Trait("Rule", "L1")]
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
            var response = await sut.LoginAsync(new LoginRequest { Email = "verified@test.com", Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            response.Auth.Token.Should().NotBeNullOrEmpty("a valid login returns an access token");
            response.RefreshToken.Should().NotBeNullOrEmpty("a valid login returns a refresh token for the cookie");
        }

        [Fact]
        [Trait("Rule", "L3")]
        public async Task LoginAsync_EmailInOtherCasingWithSurroundingSpaces_IssuesTokens()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "ana@cantinadanonna.com.br",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Ana",
                LastName = "Souza",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "  Ana@CantinaDaNonna.com.br ", Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            response.Auth.Token.Should().NotBeNullOrEmpty("an email in other casing or with spaces still logs in");
        }

        [Fact]
        [Trait("Rule", "L6")]
        public async Task LoginAsync_UnknownEmail_VerifiesAgainstAWorkFactor12DummyHash()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var hasher = Substitute.For<IPasswordHasher>();
            var jwtOptions = CreateJwtOptions();
            var sut = new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), hasher);

            // Act
            var act = () => sut.LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = Password }, TestClients.KitchenTablet, null);
            await act.Should().ThrowAsync<BusinessException>();

            // Assert
            hasher.Received(1).Verify(Password, Arg.Is<string>(hash => hash.StartsWith("$2a$12$")));
        }

        [Fact]
        [Trait("Rule", "L6")]
        public async Task LoginAsync_UnknownEmail_RunsAsManyDatabaseCommandsAsAWrongPasswordForARealAccount()
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
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            var knownCommands = new QueryCommandRecorder();
            var unknownCommands = new QueryCommandRecorder();
            using var knownContext = database.CreateContext(knownCommands);
            using var unknownContext = database.CreateContext(unknownCommands);

            // Act
            var knownAct = () => CreateSut(knownContext).LoginAsync(new LoginRequest { Email = "maria@cantinadanonna.com.br", Password = "Wrong-Password9" }, TestClients.KitchenTablet, null);
            await knownAct.Should().ThrowAsync<BusinessException>();
            var unknownAct = () => CreateSut(unknownContext).LoginAsync(new LoginRequest { Email = "nobody@cantinadanonna.com.br", Password = "Wrong-Password9" }, TestClients.KitchenTablet, null);
            await unknownAct.Should().ThrowAsync<BusinessException>();

            // Assert
            unknownCommands.Commands.Count.ShouldBe(knownCommands.Commands.Count, "an unknown email costs as many database round trips as a wrong password, so response time does not reveal which emails have an account");
        }

        [Fact]
        [Trait("Rule", "L6")]
        public async Task LoginAsync_AccountLocked_RunsAsManyDatabaseCommandsAsAnUnknownEmail()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "maria@cantinadanonna.com.br",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true,
                LockedUntil = DateTime.UtcNow.AddMinutes(10)
            };
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();

            var lockedCommands = new QueryCommandRecorder();
            var unknownCommands = new QueryCommandRecorder();
            using var lockedContext = database.CreateContext(lockedCommands);
            using var unknownContext = database.CreateContext(unknownCommands);

            // Act
            var lockedAct = () => CreateSut(lockedContext).LoginAsync(new LoginRequest { Email = "maria@cantinadanonna.com.br", Password = "Wrong-Password9" }, TestClients.KitchenTablet, null);
            await lockedAct.Should().ThrowAsync<BusinessException>();
            var unknownAct = () => CreateSut(unknownContext).LoginAsync(new LoginRequest { Email = "nobody@cantinadanonna.com.br", Password = "Wrong-Password9" }, TestClients.KitchenTablet, null);
            await unknownAct.Should().ThrowAsync<BusinessException>();

            // Assert
            lockedCommands.Commands.Count.ShouldBe(unknownCommands.Commands.Count, "a locked login costs as many database round trips as an unknown email, so a lock does not show that the account exists");
        }

        [Fact]
        [Trait("Rule", "L6")]
        public async Task LoginAsync_AddressLocked_RunsAsManyDatabaseCommandsAsAnUnknownEmail()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = "maria@cantinadanonna.com.br",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true,
                LockedUntil = null
            };
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
            seedContext.LoginFailures.Add(new LoginFailure { UserId = user.Id, IpAddress = TestClients.KitchenTablet.IpAddress!, FailedAt = DateTime.UtcNow, AddressLockedUntil = DateTime.UtcNow.AddMinutes(10) });
            await seedContext.SaveChangesAsync();

            var lockedCommands = new QueryCommandRecorder();
            var unknownCommands = new QueryCommandRecorder();
            using var lockedContext = database.CreateContext(lockedCommands);
            using var unknownContext = database.CreateContext(unknownCommands);

            // Act
            var lockedAct = () => CreateSut(lockedContext).LoginAsync(new LoginRequest { Email = "maria@cantinadanonna.com.br", Password = "Wrong-Password9" }, TestClients.KitchenTablet, null);
            await lockedAct.Should().ThrowAsync<BusinessException>();
            var unknownAct = () => CreateSut(unknownContext).LoginAsync(new LoginRequest { Email = "nobody@cantinadanonna.com.br", Password = "Wrong-Password9" }, TestClients.KitchenTablet, null);
            await unknownAct.Should().ThrowAsync<BusinessException>();

            // Assert
            lockedCommands.Commands.Count.ShouldBe(unknownCommands.Commands.Count, "a locked login costs as many database round trips as an unknown email, so a lock does not show that the account exists");
        }

        [Fact]
        [Trait("Rule", "L6")]
        public async Task LoginAsync_UnknownEmail_StoresNoLoginFailure()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var act = () => sut.LoginAsync(new LoginRequest { Email = "nobody@cantinadanonna.com.br", Password = "Wrong-Password9" }, TestClients.KitchenTablet, null);
            await act.Should().ThrowAsync<BusinessException>();

            // Assert
            (await context.LoginFailures.CountAsync()).ShouldBe(0, "the matching database work for an unknown email must not store anything");
        }

        [Fact]
        [Trait("Rule", "L11")]
        [Trait("Rule", "L4")]
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
                LockedUntil = DateTime.UtcNow.AddMinutes(-1)
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "maria@cantinadanonna.com.br", Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            response.Auth.Token.ShouldNotBeNullOrEmpty("once the lock has expired the correct password logs in");
            response.RefreshToken.ShouldNotBeNullOrEmpty("once the lock has expired a refresh token is issued");
            using var readContext = database.CreateContext();
            var maria = await readContext.Users.SingleAsync();
            maria.LockedUntil.ShouldBeNull("logging in after the lock expired clears the lock");
        }

        [Fact]
        [Trait("Rule", "L4")]
        public async Task LoginAsync_CorrectPasswordAfterFailures_ClearsAddressCountAndRecordsLoginTime()
        {
            // Arrange
            var (database, email) = await SeedMarinaAsync();
            using var _ = database;
            await FailLoginsAsync(database, email, 3);

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            await sut.LoginAsync(new LoginRequest { Email = email, Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            using var readContext = database.CreateContext();
            var marina = await readContext.Users.SingleAsync();
            (await readContext.Set<LoginFailure>().CountAsync(f => f.CountsForAddress)).ShouldBe(0, "a successful login stops that address's earlier failures counting");
            marina.LockedUntil.ShouldBeNull("a successful login leaves no lock");
            marina.LastLoginAt.ShouldNotBeNull("a successful login records the last login time");
            marina.LastLoginAt.Value.ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        [Trait("Rule", "L4")]
        public async Task LoginAsync_UserWithPhoneNumber_ResponseHasNoPhoneNumber()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "marina@cantinadanonna.com.br",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Marina",
                LastName = "Souza",
                PhoneNumber = "+5541999990000",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var sut = CreateSut(context);

            // Act
            var response = await sut.LoginAsync(new LoginRequest { Email = "marina@cantinadanonna.com.br", Password = Password }, TestClients.KitchenTablet, null);

            // Assert
            response.Auth.PhoneNumber.ShouldBeNull("the login response never returns the phone number");
        }

        private async Task<(IdentityTestDatabase Database, string Email)> SeedMarinaAsync()
        {
            var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User
            {
                Email = "marina@cantinadanonna.com.br",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Marina",
                LastName = "Souza",
                IsVerified = true
            });
            await seedContext.SaveChangesAsync();
            return (database, "marina@cantinadanonna.com.br");
        }

        private async Task FailLoginsAsync(IdentityTestDatabase database, string email, int times)
        {
            for (var attempt = 0; attempt < times; attempt++)
            {
                using var context = database.CreateContext();
                var sut = CreateSut(context, maxFailedLoginAttempts: 5, lockoutDurationMinutes: 15);
                var act = () => sut.LoginAsync(new LoginRequest { Email = email, Password = "WrongPassword1!" }, TestClients.KitchenTablet, null);
                await act.ShouldThrowAsync<BusinessException>();
            }
        }

        [Fact]
        [Trait("Rule", "L7")]
        public async Task LoginAsync_FourWrongPasswords_CountsFourAttemptsWithoutLocking()
        {
            // Arrange
            var (database, email) = await SeedMarinaAsync();
            using var _ = database;

            // Act
            await FailLoginsAsync(database, email, 4);

            // Assert
            using var readContext = database.CreateContext();
            var marina = await readContext.Users.SingleAsync();
            (await readContext.Set<LoginFailure>().CountAsync(f => f.CountsForAddress)).ShouldBe(4, "each of the first four wrong passwords from one address is counted");
            marina.LockedUntil.ShouldBeNull("four wrong passwords do not lock the account");
            (await readContext.Set<LoginFailure>().CountAsync(f => f.AddressLockedUntil != null)).ShouldBe(0, "four wrong passwords do not lock the address yet");
        }

        [Fact]
        [Trait("Rule", "L8")]
        public async Task LoginAsync_FifthWrongPassword_LocksThatAddressFor15MinutesAndNotTheAccount()
        {
            // Arrange
            var (database, email) = await SeedMarinaAsync();
            using var _ = database;

            // Act
            await FailLoginsAsync(database, email, 5);

            // Assert
            using var readContext = database.CreateContext();
            var marina = await readContext.Users.SingleAsync();
            var addressLockEnd = (await readContext.Set<LoginFailure>().ToListAsync()).Max(f => f.AddressLockedUntil);
            addressLockEnd.ShouldNotBeNull("the fifth wrong password from one address locks that address");
            addressLockEnd.Value.ShouldBe(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1), "the address lock lasts 15 minutes");
            marina.LockedUntil.ShouldBeNull("an address lock never locks the whole account");
            var correctFromLockedAddress = () => CreateSut(readContext).LoginAsync(new LoginRequest { Email = email, Password = Password }, TestClients.KitchenTablet, null);
            await correctFromLockedAddress.ShouldThrowAsync<BusinessException>("a correct password from the locked address is refused during the lock");
        }
    }
}
