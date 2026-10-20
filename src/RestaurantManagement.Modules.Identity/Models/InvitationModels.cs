namespace RestaurantManagement.Modules.Identity.Models
{
    /// <summary>Role names and changed cells for one location; Levels maps a module name to a level name (None, Read, Manage).</summary>
    public class InvitationLocationRequest
    {
        public Guid LocationId { get; set; }

        public List<string> RoleNames { get; set; } = new();

        public Dictionary<string, string> Levels { get; set; } = new();
    }

    public class CreateInvitationRequest
    {
        public string Email { get; set; } = string.Empty;

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
        public string Token { get; set; } = string.Empty;
    }

    public class AcceptInvitationWithAccountRequest
    {
        public string Token { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Body of editing a member's access at one location.</summary>
    public class SetMemberAccessRequest
    {
        public List<string> RoleNames { get; set; } = new();

        public Dictionary<string, string> Levels { get; set; } = new();
    }
}
