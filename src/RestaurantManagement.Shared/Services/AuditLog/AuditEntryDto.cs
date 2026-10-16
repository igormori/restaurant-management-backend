namespace RestaurantManagement.Shared.Services.AuditLog
{
    /// <summary>
    /// What a module tells the audit log. Organization, user, time and IP address are never part
    /// of it: they come from the token, the stamps and the current request.
    /// </summary>
    public class AuditEntryDto
    {
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public Guid? LocationId { get; set; }
        public string? Before { get; set; }
        public string? After { get; set; }
        public string? Reason { get; set; }
    }
}
