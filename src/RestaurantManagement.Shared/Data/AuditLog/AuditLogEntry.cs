using RestaurantManagement.Shared.Data.Entities;

namespace RestaurantManagement.Shared.Data.AuditLog
{
    /// <summary>
    /// One append-only audit log entry. Organization, user (CreatedByUserId) and time (CreatedAt)
    /// are stamped by the audit interceptor; never set them from caller input.
    /// </summary>
    public class AuditLogEntry : TenantEntity
    {
        public Guid? LocationId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string? Before { get; set; }
        public string? After { get; set; }
        public string? Reason { get; set; }
        public string? IpAddress { get; set; }
    }
}
