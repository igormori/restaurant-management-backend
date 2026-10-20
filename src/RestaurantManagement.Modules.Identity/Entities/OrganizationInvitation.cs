using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Modules.Identity.Entities
{
    /// <summary>
    /// A pending, used, cancelled or replaced invitation to join an organization. Open means none of
    /// UsedAt, CancelledAt and ReplacedAt is set; at most one open invitation exists per organization
    /// and email. The one-time token is stored only as a fingerprint. The per-location roles and final
    /// levels are stored with the invitation (child rows added by the implementation).
    /// </summary>
    public class OrganizationInvitation : TenantEntity
    {
        public string Email { get; set; } = string.Empty;

        public Guid InvitedByUserId { get; set; }

        /// <summary>"en" or "pt": the language of the invitation email.</summary>
        public string Language { get; set; } = "en";

        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public DateTime? UsedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public DateTime? ReplacedAt { get; set; }
    }
}
