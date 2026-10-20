using NSubstitute;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Modules.Menu.Tests
{
    /// <summary>
    /// Builds the access guard the Menu services ask. Access is decided by the real guard over a
    /// grid the test states (the Owner flag and cells of one user), so a test says who may do what
    /// at which location. Tests where access is not what is being checked use <see cref="Permissive"/>.
    /// </summary>
    public static class TestAccess
    {
        public static AccessGridDto OwnerGrid => new() { IsOwner = true };

        /// <summary>A member of the organization who has been given nothing yet (a new Admin).</summary>
        public static AccessGridDto NoAccessGrid => new();

        public static AccessGridDto Grid(params (Guid LocationId, AccessModule Module, AccessLevel Level)[] cells) => new()
        {
            Cells = cells.Select(c => new AccessCellDto { LocationId = c.LocationId, Module = c.Module, Level = c.Level }).ToList()
        };

        /// <summary>
        /// The real guard for one signed-in user in one organization, reading the given grid. The
        /// organization has the given non-closed locations (used by actions that are not tied to one location).
        /// </summary>
        public static IAccessGuard Guard(Guid userId, Guid organizationId, AccessGridDto grid, params Guid[] nonClosedLocationIds)
        {
            var currentUser = Substitute.For<ICurrentUser>();
            currentUser.UserId.Returns(userId);
            currentUser.OrganizationId.Returns(organizationId);
            var memberAccess = Substitute.For<IMemberAccessLookup>();
            memberAccess.GetAccessGridAsync(userId, organizationId).Returns(grid);
            var organizations = Substitute.For<IOrganizationLookup>();
            organizations.GetNonClosedLocationIdsAsync(organizationId).Returns(nonClosedLocationIds.ToList());
            return new AccessGuard(currentUser, memberAccess, organizations, TestLocalizer.Create());
        }

        public static IAccessGuard OwnerGuard(Guid userId, Guid organizationId) => Guard(userId, organizationId, OwnerGrid);

        /// <summary>A guard that allows everything, for tests that are not about access.</summary>
        public static IAccessGuard Permissive() => Substitute.For<IAccessGuard>();
    }
}
