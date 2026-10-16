namespace RestaurantManagement.Shared.Services.Identity
{
    public interface IUserRoleLookup
    {
        Task<bool> UserExistsAsync(Guid userId);
        Task<string?> GetRoleAsync(Guid userId, Guid organizationId);
        Task<List<Guid>> GetOrganizationIdsForUserAsync(Guid userId);

        /// <summary>
        /// True when the user has a role in the organization for that location, or for all of its
        /// locations (no location on the role).
        /// </summary>
        Task<bool> HasLocationAccessAsync(Guid userId, Guid organizationId, Guid locationId);
    }
}
