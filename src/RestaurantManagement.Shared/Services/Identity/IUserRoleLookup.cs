using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Shared.Services.Identity
{
    public interface IUserRoleLookup
    {
        Task<bool> UserExistsAsync(Guid userId);
        Task<string?> GetRoleAsync(Guid userId, Guid organizationId);

        /// <summary>
        /// Every role the user holds in the organization (never another organization's), each with
        /// its location or none for all locations. Empty when the user has none.
        /// </summary>
        Task<List<RoleAssignmentDto>> GetRoleAssignmentsAsync(Guid userId, Guid organizationId);

        Task<List<Guid>> GetOrganizationIdsForUserAsync(Guid userId);

        /// <summary>
        /// True when the user has a role in the organization for that location, or for all of its
        /// locations (no location on the role).
        /// </summary>
        Task<bool> HasLocationAccessAsync(Guid userId, Guid organizationId, Guid locationId);
    }
}
