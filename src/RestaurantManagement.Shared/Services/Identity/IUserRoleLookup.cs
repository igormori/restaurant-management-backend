namespace RestaurantManagement.Shared.Services.Identity
{
    public interface IUserRoleLookup
    {
        Task<bool> UserExistsAsync(Guid userId);
        Task<string?> GetRoleAsync(Guid userId, Guid organizationId);
        Task<List<Guid>> GetOrganizationIdsForUserAsync(Guid userId);
    }
}
