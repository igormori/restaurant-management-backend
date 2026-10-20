using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Modules.Identity.Entities
{
    /// <summary>One default level of a role in a module. A level of None is not stored.</summary>
    public class OrganizationRoleLevel : TenantEntity
    {
        public Guid RoleId { get; set; }

        public AccessModule Module { get; set; }

        public AccessLevel Level { get; set; }
    }
}
