using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Modules.Identity.Entities
{
    /// <summary>
    /// One cell of a member's stored grid: the level in a module at a location. One row per cell;
    /// None means no row. The location is an ID only (it belongs to the Organization module).
    /// </summary>
    public class MemberLocationLevel : TenantEntity
    {
        public Guid UserId { get; set; }

        public Guid LocationId { get; set; }

        public AccessModule Module { get; set; }

        public AccessLevel Level { get; set; }
    }
}
