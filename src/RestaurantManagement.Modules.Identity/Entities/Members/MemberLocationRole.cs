using RestaurantManagement.Shared.Data.Entities;

namespace RestaurantManagement.Modules.Identity.Entities.Members
{
    /// <summary>
    /// A role label of a member at a location (display and filtering only; access is decided by
    /// the levels). Points to the role, so renaming a role shows everywhere.
    /// </summary>
    public class MemberLocationRole : TenantEntity
    {
        public Guid UserId { get; set; }

        public Guid LocationId { get; set; }

        public Guid RoleId { get; set; }
    }
}
