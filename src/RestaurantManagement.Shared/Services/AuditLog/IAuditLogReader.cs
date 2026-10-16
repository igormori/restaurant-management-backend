using RestaurantManagement.Shared.Models;

namespace RestaurantManagement.Shared.Services.AuditLog
{
    public interface IAuditLogReader
    {
        Task<AuditLogPageResponse> GetEntriesAsync(AuditLogQuery query);
    }
}
