using RestaurantManagement.Shared.Data.Entities;

namespace RestaurantManagement.Modules.Identity.Entities.Members
{
    /// <summary>
    /// A user's membership of an organization (one row per user and organization). The Owner is a
    /// flag here, not a role.
    /// </summary>
    public class OrganizationMember : TenantEntity
    {
        public Guid UserId { get; set; }

        public bool IsOwner { get; set; }
    }
}
