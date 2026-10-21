using RestaurantManagement.Modules.Identity.Entities.Users;

namespace RestaurantManagement.Modules.Identity.Entities.Sessions
{
    public class RefreshToken
    {
        public const string ReasonRotated = "rotated";
        public const string ReasonLogout = "logout";
        public const string ReasonLogoutEverywhere = "logout_everywhere";
        public const string ReasonReuseDetected = "reuse_detected";
        public const string ReasonPasswordChanged = "password_changed";

        public const int DeviceDescriptionMaxLength = 512;
        public const int IpAddressMaxLength = 45;

        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        // Cross-module reference: ID only, no foreign key to the Organization module.
        public Guid? OrganizationId { get; set; }

        public Guid FamilyId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevocationReason { get; set; }
        public Guid? ReplacedByTokenId { get; set; }
        public string? DeviceDescription { get; set; }
        public string? IpAddress { get; set; }

        public User User { get; set; } = null!;
    }
}
