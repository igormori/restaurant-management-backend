using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Modules.Identity.Entities
{
    /// <summary>A role label an invitation offers at a location; becomes a member label on acceptance.</summary>
    public class OrganizationInvitationRole : TenantEntity
    {
        public Guid InvitationId { get; set; }

        public Guid LocationId { get; set; }

        public Guid RoleId { get; set; }
    }
}
