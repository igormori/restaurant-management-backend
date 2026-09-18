namespace RestaurantManagement.Shared.Services.Identity
{
    public interface IUserRoleAssigner
    {
        Task AssignRoleAsync(Guid userId, Guid organizationId, string role);
    }
}
