using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Whether a user may work in a location: a role for that location, or a role for all
    /// locations of the organization (no location on the role).
    /// </summary>
    public class UserRoleLookupLocationAccessTests
    {
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private const string BatelLocation = "44444444-dddd-4ddd-8ddd-000000000001";
        private const string CentroLocation = "44444444-dddd-4ddd-8ddd-000000000002";
        private const string MercadoMunicipalLocation = "44444444-dddd-4ddd-8ddd-000000000003";
        private static readonly Guid BatelLocationId = Guid.Parse(BatelLocation);
        private static readonly Guid CentroLocationId = Guid.Parse(CentroLocation);
        private static readonly Guid MercadoMunicipalLocationId = Guid.Parse(MercadoMunicipalLocation);

        private static async Task<Guid> SeedUserAsync(IdentityTestDatabase database, params UserRole[] roles)
        {
            using var context = database.CreateContext();
            var user = new User
            {
                Email = "ana@cantinadanonna.com.br",
                PasswordHash = "not-used-in-these-tests",
                FirstName = "Ana",
                LastName = "Pereira"
            };
            foreach (var role in roles)
                user.Roles.Add(role);

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user.Id;
        }

        private static async Task<bool> HasLocationAccessAsync(IdentityTestDatabase database, Guid userId, Guid organizationId, Guid locationId)
        {
            using var context = database.CreateContext();
            return await new UserRoleLookup(context).HasLocationAccessAsync(userId, organizationId, locationId);
        }

        // Scenario 29
        [Fact]
        [Trait("Rule", "T14")]
        public async Task HasLocationAccessAsync_RoleForAllLocations_ReturnsTrueForAnyLocation()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database,
                new UserRole { OrganizationId = CantinaId, LocationId = null, Role = Roles.Manager });

            // Act
            var hasAccess = await HasLocationAccessAsync(database, userId, CantinaId, MercadoMunicipalLocationId);

            // Assert
            hasAccess.ShouldBeTrue("a role for all locations allows any location of the organization");
        }

        // Scenario 30
        [Fact]
        [Trait("Rule", "T14")]
        public async Task HasLocationAccessAsync_RolesOnlyInAnotherOrganization_ReturnsFalse()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database,
                new UserRole { OrganizationId = TrattoriaId, LocationId = BatelLocationId, Role = Roles.Waiter },
                new UserRole { OrganizationId = TrattoriaId, LocationId = null, Role = Roles.Owner });

            // Act
            var hasAccess = await HasLocationAccessAsync(database, userId, CantinaId, BatelLocationId);

            // Assert
            hasAccess.ShouldBeFalse("a role in another organization never gives access to this organization's locations");
        }

        // Scenario 31
        [Theory]
        [InlineData(BatelLocation, true)]
        [InlineData(CentroLocation, true)]
        [InlineData(MercadoMunicipalLocation, false)]
        [Trait("Rule", "T14")]
        public async Task HasLocationAccessAsync_RolesForTwoLocations_AllowsOnlyThoseTwo(string location, bool expected)
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database,
                new UserRole { OrganizationId = CantinaId, LocationId = BatelLocationId, Role = Roles.Waiter },
                new UserRole { OrganizationId = CantinaId, LocationId = CentroLocationId, Role = Roles.Cashier });

            // Act
            var hasAccess = await HasLocationAccessAsync(database, userId, CantinaId, Guid.Parse(location));

            // Assert
            hasAccess.ShouldBe(expected, "a role for given locations allows only those locations");
        }
    }
}
