using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models.Members
{
    /// <summary>Body of editing a member's access at one location.</summary>
    public class SetMemberAccessRequest
    {
        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public List<string> RoleNames { get; set; } = new();

        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public Dictionary<string, string> Levels { get; set; } = new();
    }
}
