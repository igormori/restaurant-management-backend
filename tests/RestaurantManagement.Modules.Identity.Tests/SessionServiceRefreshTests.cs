using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Sentry;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Sign-in and refresh with per-device refresh tokens. Tokens are seeded through the real
    /// refresh token service, and every call uses a fresh context, like separate HTTP requests.
    /// </summary>
    public class SessionServiceRefreshTests
    {
        private const string Password = "Password1!";
        private const string UserEmail = "maria@cantinadanonna.com.br";
        private const string OwnerRole = "Owner";
        private const int UnauthorizedStatusCode = 401;
        private const string InvalidEmailOrPasswordKey = "InvalidEmailOrPassword";
        private const string InvalidOrExpiredTokenKey = "InvalidOrExpiredToken";
        private const string? NoCookie = null;

        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");

        private static SessionService CreateSut(IdentityDbContext context)
        {
            var jwtOptions = TestJwtOptions.Create();
            return new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), Substitute.For<IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), TestPasswordHasher.Create());
        }

        private static async Task<Guid> SeedUserAsync(
            IdentityTestDatabase database,
            bool isActive = true,
            bool isVerified = true,
            DateTime? lockedUntil = null,
            string? role = null,
            Guid? organizationId = null)
        {
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = UserEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Maria",
                LastName = "Souza",
                IsActive = isActive,
                IsVerified = isVerified,
                LockedUntil = lockedUntil
            };
            if (role != null)
                user.Roles.Add(new UserRole { Role = role, OrganizationId = organizationId ?? CantinaId });

            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
            return user.Id;
        }

        private static async Task<IssuedRefreshToken> IssueTokenAsync(IdentityTestDatabase database, Guid userId, Guid? organizationId = null, ClientInfo? client = null)
        {
            var device = client ?? TestClients.KitchenTablet;
            using var context = database.CreateContext();
            return await new RefreshTokenService(context, TestJwtOptions.Create())
                .IssueAsync(userId, organizationId, device.DeviceDescription, device.IpAddress);
        }

        private static async Task<SessionResult> LoginAsync(IdentityTestDatabase database, ClientInfo client)
        {
            using var context = database.CreateContext();
            return await CreateSut(context).LoginAsync(new LoginRequest { Email = UserEmail, Password = Password }, client);
        }

        private static async Task<int> CountTokensAfterRefusedLoginAsync(IdentityTestDatabase database, string password)
        {
            using (var context = database.CreateContext())
            {
                var act = () => CreateSut(context).LoginAsync(new LoginRequest { Email = UserEmail, Password = password }, TestClients.KitchenTablet);
                (await Should.ThrowAsync<BusinessException>(act)).StatusCode.ShouldBe(UnauthorizedStatusCode, "a refused login answers 401");
            }

            using var assertContext = database.CreateContext();
            return await assertContext.Set<RefreshToken>().CountAsync();
        }

        private static async Task<SessionResult> RefreshAsync(IdentityTestDatabase database, string? refreshToken)
        {
            using var context = database.CreateContext();
            return await CreateSut(context).RefreshTokenAsync(refreshToken);
        }

        private static async Task<List<RefreshToken>> ReadTokensAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.Set<RefreshToken>().AsNoTracking().ToListAsync();
        }

        private static async Task AssertRefreshRejectedAsync(IdentityTestDatabase database, string? refreshToken)
        {
            var thrown = await Should.ThrowAsync<BusinessException>(() => RefreshAsync(database, refreshToken));
            thrown.StatusCode.ShouldBe(UnauthorizedStatusCode, "a refresh that is not allowed answers 401");
            thrown.Message.ShouldBe(InvalidOrExpiredTokenKey, "every refused refresh gets the same generic message, whatever the reason");
        }

        [Fact]
        [Trait("Rule", "L5")]
        public async Task LoginAsync_InactiveUserCorrectPassword_ThrowsGeneric401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, isActive: false);

            // Act
            var act = () => LoginAsync(database, TestClients.KitchenTablet);

            // Assert
            var thrown = await act.Should().ThrowAsync<BusinessException>();
            thrown.Which.StatusCode.Should().Be(UnauthorizedStatusCode, "a deactivated account gets the generic 401 login failure");
            thrown.Which.Message.Should().Be(InvalidEmailOrPasswordKey, "a deactivated account cannot be told apart from a wrong password");
        }

        // Scenario 27
        [Fact]
        [Trait("Rule", "F2")]
        public async Task RefreshTokenAsync_NoCookie_ThrowsInvalidOrExpiredToken401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database);

            // Act + Assert
            await AssertRefreshRejectedAsync(database, NoCookie);
        }

        // Scenario 28
        [Fact]
        [Trait("Rule", "F2")]
        public async Task RefreshTokenAsync_UnknownToken_ThrowsInvalidOrExpiredToken401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database);
            await IssueTokenAsync(database, userId);

            // Act + Assert
            await AssertRefreshRejectedAsync(database, "bm90LWEtcmVhbC10b2tlbi1mb3ItY2FudGluYQ==");
        }

        // Scenario 29
        [Fact]
        [Trait("Rule", "F2")]
        public async Task RefreshTokenAsync_ExpiredToken_ThrowsInvalidOrExpiredToken401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database);
            var issued = await IssueTokenAsync(database, userId);
            using (var context = database.CreateContext())
            {
                var token = await context.Set<RefreshToken>().SingleAsync();
                token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                await context.SaveChangesAsync();
            }

            // Act + Assert
            await AssertRefreshRejectedAsync(database, issued.Token);
        }

        // Scenario 30
        [Fact]
        [Trait("Rule", "F3")]
        public async Task RefreshTokenAsync_ReusedRotatedToken_ThrowsInvalidOrExpiredToken401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database);
            var issued = await IssueTokenAsync(database, userId);
            await RefreshAsync(database, issued.Token);

            // Act + Assert
            await AssertRefreshRejectedAsync(database, issued.Token);
        }

        // Scenario 31
        [Fact]
        [Trait("Rule", "F3")]
        public async Task RefreshTokenAsync_ReusedRotatedToken_NewestTokenOfThatDeviceIsRefusedToo()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database);
            var issued = await IssueTokenAsync(database, userId);
            var rotated = await RefreshAsync(database, issued.Token);
            await Should.ThrowAsync<BusinessException>(() => RefreshAsync(database, issued.Token));

            // Act + Assert
            await AssertRefreshRejectedAsync(database, rotated.RefreshToken);
        }

        // Scenario 32
        [Fact]
        [Trait("Rule", "F4")]
        public async Task RefreshTokenAsync_InactiveUser_ThrowsInvalidOrExpiredToken401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, isActive: false);
            var issued = await IssueTokenAsync(database, userId);

            // Act + Assert
            await AssertRefreshRejectedAsync(database, issued.Token);
        }

        // Scenario 33
        [Fact]
        [Trait("Rule", "F4")]
        public async Task RefreshTokenAsync_UnverifiedUser_ThrowsInvalidOrExpiredToken401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, isVerified: false);
            var issued = await IssueTokenAsync(database, userId);

            // Act + Assert
            await AssertRefreshRejectedAsync(database, issued.Token);
        }

        // Scenario 34
        [Fact]
        [Trait("Rule", "F4")]
        public async Task RefreshTokenAsync_LockedUser_ThrowsInvalidOrExpiredToken401()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, lockedUntil: DateTime.UtcNow.AddMinutes(15));
            var issued = await IssueTokenAsync(database, userId);

            // Act + Assert
            await AssertRefreshRejectedAsync(database, issued.Token);
        }

        // Scenario 35
        [Fact]
        [Trait("Rule", "F5")]
        public async Task RefreshTokenAsync_TokenForOrganizationUserIsNotMemberOf_ThrowsInvalidOrExpiredToken401()
        {
            // Arrange: Maria is Owner of Cantina only; the token claims Trattoria
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, role: OwnerRole, organizationId: CantinaId);
            var issued = await IssueTokenAsync(database, userId, organizationId: TrattoriaId);

            // Act + Assert
            await AssertRefreshRejectedAsync(database, issued.Token);
        }

        // Scenario 36
        [Fact]
        [Trait("Rule", "L1")]
        public async Task LoginAsync_ValidCredentials_CreatesOneActiveTokenForThisDevice()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, role: OwnerRole, organizationId: CantinaId);

            // Act
            var result = await LoginAsync(database, TestClients.KitchenTablet);

            // Assert
            result.RefreshToken.ShouldNotBeNullOrWhiteSpace("a login issues a refresh token");
            result.RefreshTokenExpiresAt.ShouldBe(DateTime.UtcNow.AddDays(TestJwtOptions.RefreshTokenExpireDays), TimeSpan.FromMinutes(1));
            var token = (await ReadTokensAsync(database)).ShouldHaveSingleItem();
            token.UserId.ShouldBe(userId, "the login stores one refresh token for the user");
            token.OrganizationId.ShouldBeNull();
            token.RevokedAt.ShouldBeNull("the new refresh token is active");
            token.DeviceDescription.ShouldBe(TestClients.KitchenTablet.DeviceDescription, "the login records which device was used");
            token.IpAddress.ShouldBe(TestClients.KitchenTablet.IpAddress, "the login records the IP address of the device");
        }

        // Scenario 37
        [Fact]
        [Trait("Rule", "L15")]
        public async Task LoginAsync_SecondDevice_FirstDeviceTokenStaysActive()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database);
            await LoginAsync(database, TestClients.KitchenTablet);

            // Act
            await LoginAsync(database, TestClients.ManagerLaptop);

            // Assert
            var tokens = await ReadTokensAsync(database);
            tokens.Count.ShouldBe(2, "each device has its own refresh token");
            tokens.ShouldAllBe(t => t.RevokedAt == null, "logging in on a second device keeps the first device signed in");
            tokens.Select(t => t.FamilyId).Distinct().Count().ShouldBe(2, "each device has its own session family");
        }

        // Scenario 38
        [Fact]
        [Trait("Rule", "F7")]
        public async Task RefreshTokenAsync_TwoDevicesSignedIn_BothRefreshSucceed()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database);
            var tablet = await LoginAsync(database, TestClients.KitchenTablet);
            var laptop = await LoginAsync(database, TestClients.ManagerLaptop);

            // Act
            var tabletRefreshed = await RefreshAsync(database, tablet.RefreshToken);
            var laptopRefreshed = await RefreshAsync(database, laptop.RefreshToken);

            // Assert
            tabletRefreshed.Auth.Token.ShouldNotBeNullOrWhiteSpace("the tablet can refresh its session");
            laptopRefreshed.Auth.Token.ShouldNotBeNullOrWhiteSpace("the laptop can refresh its session independently of the tablet");
        }

        // Scenario 39
        [Fact]
        [Trait("Rule", "F1")]
        public async Task RefreshTokenAsync_ActiveToken_ReplacesItWithNewTokenInSameFamily()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database);
            var issued = await IssueTokenAsync(database, userId);

            // Act
            var result = await RefreshAsync(database, issued.Token);

            // Assert
            result.RefreshToken.ShouldNotBe(issued.Token, "a refresh issues a new refresh token");
            var tokens = await ReadTokensAsync(database);
            tokens.Count.ShouldBe(2, "the family holds the old token and its replacement");
            tokens.ShouldAllBe(t => t.FamilyId == issued.FamilyId, "the replacement stays in the same session family");
            tokens.Count(t => t.RevokedAt == null).ShouldBe(1, "only the new token is still active after a refresh");
        }

        // Scenario 40
        [Fact]
        [Trait("Rule", "F1")]
        public async Task RefreshTokenAsync_ActiveToken_NewTokenExpiresInRefreshTokenExpireDays()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database);
            var issued = await IssueTokenAsync(database, userId);

            // Act
            var result = await RefreshAsync(database, issued.Token);

            // Assert
            result.RefreshTokenExpiresAt.ShouldBe(DateTime.UtcNow.AddDays(TestJwtOptions.RefreshTokenExpireDays), TimeSpan.FromMinutes(1));
        }

        [Fact]
        [Trait("Rule", "F6")]
        public async Task RefreshTokenAsync_UserWithRole_JwtContainsRoleClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, role: OwnerRole);
            var issued = await IssueTokenAsync(database, userId);

            // Act
            var result = await RefreshAsync(database, issued.Token);

            // Assert
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Auth.Token);
            jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
                .Should().ContainSingle().Which.Should().Be(OwnerRole, "the refreshed access token carries the user's role");
        }

        [Fact]
        [Trait("Rule", "F1")]
        public async Task RefreshTokenAsync_ActiveVerifiedUnlockedUser_ReturnsNewTokens()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, lockedUntil: DateTime.UtcNow.AddMinutes(-5));
            var issued = await IssueTokenAsync(database, userId);

            // Act
            var result = await RefreshAsync(database, issued.Token);

            // Assert
            result.Auth.Token.Should().NotBeNullOrEmpty("a user whose lock has expired can refresh");
            result.RefreshToken.Should().NotBeNullOrEmpty().And.NotBe(issued.Token, "the refresh issues a different refresh token");
        }

        // Scenario 71
        [Fact]
        [Trait("Rule", "L12")]
        public async Task LoginAsync_WrongPassword_StoresNoToken()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database);

            // Act
            var tokenCount = await CountTokensAfterRefusedLoginAsync(database, "WrongPassword1!");

            // Assert
            tokenCount.ShouldBe(0, "a wrong password stores no refresh token");
        }

        // Scenario 72
        [Fact]
        [Trait("Rule", "L9")]
        public async Task LoginAsync_LockedAccountCorrectPassword_StoresNoToken()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, lockedUntil: DateTime.UtcNow.AddMinutes(15));

            // Act
            var tokenCount = await CountTokensAfterRefusedLoginAsync(database, Password);

            // Assert
            tokenCount.ShouldBe(0, "a locked account stores no refresh token even with the correct password");
        }

        // Scenario 73
        [Fact]
        [Trait("Rule", "L12")]
        public async Task LoginAsync_InactiveUserCorrectPassword_StoresNoToken()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, isActive: false);

            // Act
            var tokenCount = await CountTokensAfterRefusedLoginAsync(database, Password);

            // Assert
            tokenCount.ShouldBe(0, "a deactivated account stores no refresh token even with the correct password");
        }

        // Scenario 74
        [Fact]
        [Trait("Rule", "F4")]
        public async Task RefreshTokenAsync_InactiveUser_LeavesNoActiveTokenBehind()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database, isActive: false);
            var issued = await IssueTokenAsync(database, userId);

            // Act
            await Should.ThrowAsync<BusinessException>(() => RefreshAsync(database, issued.Token));

            // Assert
            (await ReadTokensAsync(database)).ShouldAllBe(t => t.RevokedAt != null, "refusing a deactivated user also revokes their remaining tokens");
        }
    }
}
