using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.Entities;

namespace RestaurantManagement.Modules.Identity.Entities.Invitations
{
    /// <summary>One final cell of the grid an invitation grants at a location (None is never stored).</summary>
    public class OrganizationInvitationLevel : TenantEntity
    {
        public Guid InvitationId { get; set; }

        public Guid LocationId { get; set; }

        public AccessModule Module { get; set; }

        public AccessLevel Level { get; set; }
    }
}
