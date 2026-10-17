using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Auth;
using Sentry;
using Shouldly;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class SessionServiceOrganizationClaimTests
    {
        private const string Password = "Password1!";
        private const string UserEmail = "maria@cantinadanonna.com.br";
        private const string OwnerRole = "Owner";
        private const string StaffRole = "Staff";
        private const string AdminRole = "Admin";

        private static readonly Guid CantinaOrganizationId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaOrganizationId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid CentroLocationId = Guid.Parse("d4d4d4d4-0000-4000-8000-000000000004");

        private static SessionService CreateSut(IdentityDbContext context)
        {
            var jwtOptions = TestJwtOptions.Create();
            return new SessionService(context, TestSecurityOptions.Create(), jwtOptions, TestLocalizer.Create(), Substitute.For<IHub>(), Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(), new RefreshTokenService(context, jwtOptions), TestPasswordHasher.Create());
        }

        private static async Task SeedUserAsync(IdentityTestDatabase database, params UserRole[] roles)
        {
            using var seedContext = database.CreateContext();
            var user = new User
            {
                Email = UserEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true
            };
            foreach (var role in roles)
                user.Roles.Add(role);

            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        private static async Task<SessionResult> LoginAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await CreateSut(context).LoginAsync(new LoginRequest { Email = UserEmail, Password = Password }, TestClients.KitchenTablet, null);
        }

        private static string? OrganizationClaimOf(string? token)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .SingleOrDefault(c => c.Type == AppClaimTypes.OrganizationId)?.Value;
        }

        private static List<string> RoleClaimsOf(string? token)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }

        // Scenario 29
        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserWithRolesInTwoOrganizations_TokenHasNoOrganizationClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId },
                new UserRole { Role = StaffRole, OrganizationId = TrattoriaOrganizationId });

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Auth.Token).ShouldBeNull("a user with roles in two organizations gets no organization in the token until they choose one");
        }

        // Scenario 30
        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserWithNoRoles_TokenHasNoOrganizationClaim()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database);

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Auth.Token).ShouldBeNull("a user with no roles gets no organization in the token");
        }

        // Scenario 32
        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserWithOneRoleInOneOrganization_TokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId });

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Auth.Token).ShouldBe(CantinaOrganizationId.ToString(), "a user with a single organization gets that organization in the token");
        }

        // Scenario 33
        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserWithTwoRolesInSameOrganization_TokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId },
                new UserRole { Role = StaffRole, OrganizationId = CantinaOrganizationId, LocationId = CentroLocationId });

            // Act
            var response = await LoginAsync(database);

            // Assert
            OrganizationClaimOf(response.Auth.Token).ShouldBe(CantinaOrganizationId.ToString(), "two roles in the same organization still count as one organization");
        }

        // Scenario 34
        [Fact]
        [Trait("Rule", "F6")]
        public async Task RefreshTokenAsync_UserWithOneOrganization_NewTokenCarriesThatOrganizationId()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database, new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId });
            var loginResponse = await LoginAsync(database);
            using var refreshContext = database.CreateContext();
            var sut = CreateSut(refreshContext);

            // Act
            var refreshed = await sut.RefreshTokenAsync(loginResponse.RefreshToken);

            // Assert
            OrganizationClaimOf(refreshed.Auth.Token).ShouldBe(CantinaOrganizationId.ToString(), "a refreshed token keeps the user's single organization");
        }

        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserWithRolesInTwoOrganizations_TokenHasNoRoleClaims()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId },
                new UserRole { Role = StaffRole, OrganizationId = TrattoriaOrganizationId });

            // Act
            var response = await LoginAsync(database);

            // Assert
            RoleClaimsOf(response.Auth.Token).ShouldBeEmpty("a token with no organization carries no role, so a role held in one organization cannot count as proof of access in another");
        }

        [Fact]
        [Trait("Rule", "L14")]
        public async Task LoginAsync_UserWithTwoRolesInOneOrganization_TokenCarriesBothRoles()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId },
                new UserRole { Role = AdminRole, OrganizationId = CantinaOrganizationId, LocationId = CentroLocationId });

            // Act
            var response = await LoginAsync(database);

            // Assert
            var roles = RoleClaimsOf(response.Auth.Token).OrderBy(r => r).ToList();
            roles.ShouldBe(new[] { AdminRole, OwnerRole }, ignoreOrder: false, customMessage: "a user of a single organization keeps all their roles in it");
        }

        [Fact]
        [Trait("Rule", "F10")]
        [Trait("Rule", "F6")]
        public async Task RefreshTokenAsync_TwoOrganizationsNoneChosen_NewTokenHasNoRolesAndNoOrganization()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            await SeedUserAsync(database,
                new UserRole { Role = OwnerRole, OrganizationId = CantinaOrganizationId },
                new UserRole { Role = StaffRole, OrganizationId = TrattoriaOrganizationId });
            var loginResponse = await LoginAsync(database);
            using var refreshContext = database.CreateContext();

            // Act
            var refreshed = await CreateSut(refreshContext).RefreshTokenAsync(loginResponse.RefreshToken);

            // Assert
            RoleClaimsOf(refreshed.Auth.Token).ShouldBeEmpty("refreshing without a chosen organization gives a two-organization user no roles");
            OrganizationClaimOf(refreshed.Auth.Token).ShouldBeNull("refreshing without a chosen organization gives a two-organization user no organization");
        }
    }
}
