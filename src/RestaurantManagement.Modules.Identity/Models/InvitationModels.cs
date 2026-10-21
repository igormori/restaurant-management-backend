using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    /// <summary>Role names and changed cells for one location; Levels maps a module name to a level name (None, Read, Manage).</summary>
    public class InvitationLocationRequest
    {
        public Guid LocationId { get; set; }

        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public List<string> RoleNames { get; set; } = new();

        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public Dictionary<string, string> Levels { get; set; } = new();
    }

    public class CreateInvitationRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [EmailAddress(ErrorMessage = "EmailAddressAttribute_ValidationError")]
        [MaxLength(320, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public List<InvitationLocationRequest> Locations { get; set; } = new();
    }

    /// <summary>Never carries the token.</summary>
    public class InvitationResponse
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }

    public class AcceptInvitationRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(200, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Token { get; set; } = string.Empty;
    }

    public class AcceptInvitationWithAccountRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(200, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [PasswordRules]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Body of editing a member's access at one location.</summary>
    public class SetMemberAccessRequest
    {
        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public List<string> RoleNames { get; set; } = new();

        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public Dictionary<string, string> Levels { get; set; } = new();
    }
}
