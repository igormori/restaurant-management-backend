using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
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
            maria.Roles.Add(new UserRole { Role = "Owner", OrganizationId = CantinaId });
            var joao = new User { Id = JoaoId, Email = "joao@trattoriadosol.com.br", PasswordHash = "hash", FirstName = "João", LastName = "Lima", IsVerified = true };
            joao.Roles.Add(new UserRole { Role = "Staff", OrganizationId = TrattoriaId });
            context.Users.AddRange(maria, joao);
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
        public async Task LogoutAsync_NoCookie_CompletesAndChangesNothing()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);

            // Act
            await LogoutAsync(database, NoCookie);

            // Assert
            (await ReadTokensAsync(database, MariaId)).ShouldHaveSingleItem().RevokedAt.ShouldBeNull();
        }

        // Scenario 54
        [Fact]
        public async Task LogoutAsync_UnknownToken_CompletesAndChangesNothing()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);

            // Act
            await LogoutAsync(database, "bm90LWEtcmVhbC10b2tlbi1mb3ItY2FudGluYQ==");

            // Assert
            (await ReadTokensAsync(database, MariaId)).ShouldHaveSingleItem().RevokedAt.ShouldBeNull();
        }

        // Scenario 55
        [Fact]
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
            tokens.Single(t => t.FamilyId == tablet.FamilyId).RevokedAt.ShouldNotBeNull();
            tokens.Single(t => t.FamilyId == laptop.FamilyId).RevokedAt.ShouldBeNull();
        }

        // Scenario 56
        [Fact]
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
            refreshed.Auth.Token.ShouldNotBeNullOrWhiteSpace();
            refreshed.RefreshToken.ShouldNotBe(laptop.Token);
        }

        // Scenario 57
        [Fact]
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
            (await ReadTokensAsync(database, MariaId)).ShouldAllBe(t => t.RevokedAt == null);
        }

        // Scenario 58
        [Fact]
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
            tokens.Count.ShouldBe(2);
            tokens.ShouldAllBe(t => t.RevokedAt != null);
        }

        // Scenario 59
        [Fact]
        public async Task LogoutAllAsync_UserOfOrganizationA_UserOfOrganizationBKeepsActiveToken()
        {
            // Arrange
            using var database = await CreateDatabaseWithUsersAsync();
            await IssueTokenAsync(database, MariaId, CantinaId, TestClients.KitchenTablet);
            await IssueTokenAsync(database, JoaoId, TrattoriaId, TestClients.ManagerLaptop);

            // Act
            await LogoutAllAsync(database, MariaId);

            // Assert
            (await ReadTokensAsync(database, JoaoId)).ShouldHaveSingleItem().RevokedAt.ShouldBeNull();
        }

        // Scenario 76
        [Fact]
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
            family.Count.ShouldBe(2);
            family.ShouldAllBe(t => t.RevokedAt != null);
            family.ShouldContain(t => t.RevocationReason == RefreshToken.ReasonReuseDetected);
        }
    }
}
