using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The role lookup returns every role the user holds in the asked organization, never one
    /// arbitrary role and never another organization's.
    /// </summary>
    public class UserRoleLookupRoleAssignmentsTests
    {
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid BatelLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");

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

        private static async Task<List<RoleAssignmentDto>> GetAssignmentsAsync(IdentityTestDatabase database, Guid userId, Guid organizationId)
        {
            using var context = database.CreateContext();
            return await new UserRoleLookup(context).GetRoleAssignmentsAsync(userId, organizationId);
        }

        // Scenario 9
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Rule", "T27")]
        public async Task GetRoleAssignmentsAsync_ManagerForAllLocationsAndWaiterAtOneLocation_ReturnsBothInEveryStorageOrder(bool managerStoredFirst)
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var manager = new UserRole { OrganizationId = CantinaId, LocationId = null, Role = Roles.Manager };
            var waiter = new UserRole { OrganizationId = CantinaId, LocationId = BatelLocationId, Role = Roles.Waiter };
            var userId = managerStoredFirst
                ? await SeedUserAsync(database, manager, waiter)
                : await SeedUserAsync(database, waiter, manager);

            // Act
            var assignments = await GetAssignmentsAsync(database, userId, CantinaId);

            // Assert
            assignments.Count.ShouldBe(2, "every role the user holds in the organization is returned, not just the first row found");
            assignments.ShouldContain(a => a.Role == Roles.Manager && a.LocationId == null, "the all-locations Manager role has no location");
            assignments.ShouldContain(a => a.Role == Roles.Waiter && a.LocationId == BatelLocationId, "the Waiter role keeps its location");
        }

        // Scenario 10
        [Fact]
        [Trait("Rule", "T28")]
        public async Task GetRoleAssignmentsAsync_RolesInTwoOrganizations_ReturnsOnlyTheAskedOrganizationsRoles()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database,
                new UserRole { OrganizationId = TrattoriaId, LocationId = null, Role = Roles.Admin },
                new UserRole { OrganizationId = CantinaId, LocationId = BatelLocationId, Role = Roles.Waiter });

            // Act
            var assignments = await GetAssignmentsAsync(database, userId, CantinaId);

            // Assert
            assignments.Select(a => a.Role).ShouldBe(new[] { Roles.Waiter }, "a role held in another organization is never returned");
        }

        // Scenario 11
        [Fact]
        [Trait("Rule", "T29")]
        public async Task GetRoleAssignmentsAsync_UserWithoutRoleInTheOrganization_ReturnsEmptyList()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var userId = await SeedUserAsync(database,
                new UserRole { OrganizationId = TrattoriaId, LocationId = null, Role = Roles.Owner });

            // Act
            var assignments = await GetAssignmentsAsync(database, userId, CantinaId);

            // Assert
            assignments.ShouldBeEmpty("a user with no role in the organization has no assignments");
        }
    }
}
