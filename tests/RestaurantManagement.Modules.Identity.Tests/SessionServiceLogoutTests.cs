using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Entities.Sessions;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Models.Sessions;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using RestaurantManagement.Shared.Services.Organization;
using Sentry;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Logout signs out only the device whose cookie is sent; "sign out everywhere" revokes every
    /// token of the caller and nobody else's. Maria works at Cantina da Nonna, João at Trattoria do Sol.
    /// </summary>
    public class SessionServiceLogoutTests
    {
        private const string? NoCookie = null;

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");

        private static SessionService CreateSut(IdentityDbContext context)
        {
            var jwtOptions = TestJwtOptions.Create();
            return new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), Substitute.For<IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), TestPasswordHasher.Create());
        }

        private static async Task<IdentityTestDatabase> CreateDatabaseWithUsersAsync()
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var maria = new User { Id = MariaId, Email = "maria@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true };
            var joao = new User { Id = JoaoId, Email = "joao@trattoriadosol.com.br", PasswordHash = "hash", FirstName = "João", LastName = "Lima", IsVerified = true };
            context.Users.AddRange(maria, joao);
            context.Set<OrganizationMember>().AddRange(TestMembers.Owner(MariaId, CantinaId), TestMembers.Member(JoaoId, TrattoriaId));
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task<IssuedRefreshToken> IssueTokenAsync(IdentityTestDatabase database, Guid userId, Guid organizationId, ClientInfo client)
        {
            using var context = database.CreateContext();
            return await new RefreshTokenService(context, TestJwtOptions.Create())
                .IssueAsync(userId, organizationId, client.DeviceDescription, client.IpAddress);
        }

        private static async Task LogoutAsync(IdentityTestDatabase database, string? refreshToken)
        {
            using var context = database.CreateContext();
            await CreateSut(context).LogoutAsync(refreshToken);
        }

        private static async Task LogoutAllAsync(IdentityTestDatabase database, Guid userId)
        {
            using var context = database.CreateContext();
            await CreateSut(context).LogoutAllAsync(userId);
        }

        private static async Task<List<RefreshToken>> ReadTokensAsync(IdentityTestDatabase database, Guid userId)
        {
            using var context = database.CreateContext();
            return await context.Set<RefreshToken>().AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
        }

        // Scenario 53
        [Fact]
        [Trait("Rule", "O1")]
        public async Task LogoutAsync_NoCookie_CompletesAndChangesNothing()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);

            // Act
            await LogoutAsync(database, NoCookie);

            // Assert
            (await ReadTokensAsync(database, MariaId)).ShouldHaveSingleItem().RevokedAt.ShouldBeNull("logging out without a cookie changes nothing");
        }

        // Scenario 54
        [Fact]
        [Trait("Rule", "O1")]
        public async Task LogoutAsync_UnknownToken_CompletesAndChangesNothing()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);

            // Act
            await LogoutAsync(database, "bm90LWEtcmVhbC10b2tlbi1mb3ItY2FudGluYQ==");

            // Assert
            (await ReadTokensAsync(database, MariaId)).ShouldHaveSingleItem().RevokedAt.ShouldBeNull("logging out with an unknown cookie changes nothing");
        }

        // Scenario 55
        [Fact]
        [Trait("Rule", "O2")]
        public async Task LogoutAsync_OneOfTwoDevices_OnlyThatDevicesTokenRevoked()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var tablet = await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);
            var laptop = await IssueTokenAsync(database, MariaId, CantinaId, TestClients.ManagerLaptop);

            // Act
            await LogoutAsync(database, tablet.Token);

            // Assert
            var tokens = await ReadTokensAsync(database, MariaId);
            tokens.Single(t => t.FamilyId == tablet.FamilyId).RevokedAt.ShouldNotBeNull("logging out revokes the token of the device that logged out");
            tokens.Single(t => t.FamilyId == laptop.FamilyId).RevokedAt.ShouldBeNull("logging out on one device leaves the other device signed in");
        }

        // Scenario 56
        [Fact]
        [Trait("Rule", "O2")]
        public async Task LogoutAsync_OneOfTwoDevices_OtherDeviceStillRefreshes()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var tablet = await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);
            var laptop = await IssueTokenAsync(database, MariaId, CantinaId, TestClients.ManagerLaptop);
            await LogoutAsync(database, tablet.Token);
            using var context = database.CreateContext();

            // Act
            var refreshed = await CreateSut(context).RefreshTokenAsync(laptop.Token);

            // Assert
            refreshed.Auth.Token.ShouldNotBeNullOrWhiteSpace("the other device can still refresh after one device logs out");
            refreshed.RefreshToken.ShouldNotBe(laptop.Token, "the other device gets a new refresh token");
        }

        // Scenario 57
        [Fact]
        [Trait("Rule", "O2")]
        public async Task LogoutAsync_CookieOfUserInAnotherOrganization_OtherUsersTokensStayActive()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.ManagerLaptop);
            var joao = await IssueTokenAsync(database, JoaoId, TrattoriaId, TestClients.KitchenTablet);

            // Act
            await LogoutAsync(database, joao.Token);

            // Assert
            (await ReadTokensAsync(database, MariaId)).ShouldAllBe(t => t.RevokedAt == null, "logging out as one user never revokes another user's tokens");
        }

        // Scenario 58
        [Fact]
        [Trait("Rule", "O5")]
        public async Task LogoutAllAsync_UserOnTwoDevices_NoActiveTokenLeft()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.ManagerLaptop);

            // Act
            await LogoutAllAsync(database, MariaId);

            // Assert
            var tokens = await ReadTokensAsync(database, MariaId);
            tokens.Count.ShouldBe(2, "the user was signed in on two devices");
            tokens.ShouldAllBe(t => t.RevokedAt != null, "logout-all revokes every token of the user");
        }

        // Scenario 59
        [Fact]
        [Trait("Rule", "O5")]
        public async Task LogoutAllAsync_UserOfOrganizationA_UserOfOrganizationBKeepsActiveToken()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);
            await IssueTokenAsync(database, JoaoId, TrattoriaId, TestClients.ManagerLaptop);

            // Act
            await LogoutAllAsync(database, MariaId);

            // Assert
            (await ReadTokensAsync(database, JoaoId)).ShouldHaveSingleItem().RevokedAt.ShouldBeNull("logout-all for one user leaves another user's session active");
        }

        // Scenario 76
        [Fact]
        [Trait("Rule", "O3")]
        public async Task LogoutAsync_AlreadyRotatedToken_CompletesAndRevokesThatDevicesFamily()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            var tablet = await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);
            using (var context = database.CreateContext())
            {
                await new RefreshTokenService(context, TestJwtOptions.Create()).RotateAsync(tablet.Token);
            }

            // Act
            await LogoutAsync(database, tablet.Token);

            // Assert
            var family = await ReadTokensAsync(database, MariaId);
            family.Count.ShouldBe(2, "the family holds the original token and its replacement");
            family.ShouldAllBe(t => t.RevokedAt != null, "logout with a reused token revokes that device's whole family");
            family.ShouldContain(t => t.RevocationReason == RefreshToken.ReasonReuseDetected, "the family is marked as revoked because of token reuse");
        }
    }
}
