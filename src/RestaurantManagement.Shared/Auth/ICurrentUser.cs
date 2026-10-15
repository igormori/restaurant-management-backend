namespace RestaurantManagement.Shared.Auth
{
    public interface ICurrentUser
    {
        Guid? UserId { get; }
        Guid? OrganizationId { get; }
    }
}
