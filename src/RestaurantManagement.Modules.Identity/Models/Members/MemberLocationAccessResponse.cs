using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Identity.Models.Members
{
    /// <summary>A member's role names and levels at one location.</summary>
    public class MemberLocationAccessResponse
    {
        public Guid LocationId { get; set; }

        public List<string> RoleNames { get; set; } = new();

        public List<AccessCellDto> Levels { get; set; } = new();
    }
}
