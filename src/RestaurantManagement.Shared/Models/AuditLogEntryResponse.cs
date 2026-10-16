namespace RestaurantManagement.Shared.Models
{
    public class AuditLogEntryResponse
    {
        public Guid Id { get; set; }
        public Guid? LocationId { get; set; }
        public Guid? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string? Before { get; set; }
        public string? After { get; set; }
        public string? Reason { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
