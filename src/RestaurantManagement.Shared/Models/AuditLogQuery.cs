namespace RestaurantManagement.Shared.Models
{
    /// <summary>
    /// Filters for reading the audit log. There is no organization field: it always comes from the token.
    /// </summary>
    public class AuditLogQuery
    {
        public DateTimeOffset? From { get; set; }
        public DateTimeOffset? To { get; set; }
        public string? Action { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }
}
