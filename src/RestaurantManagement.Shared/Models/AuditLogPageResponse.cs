namespace RestaurantManagement.Shared.Models
{
    public class AuditLogPageResponse
    {
        public List<AuditLogEntryResponse> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
