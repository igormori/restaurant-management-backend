using Microsoft.AspNetCore.Http;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Shared.Services.AuditLog
{
    public class AuditLogWriter : IAuditLog
    {
        private readonly AuditLogDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogWriter(AuditLogDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        // Organization, user and time are stamped by the audit interceptor, never copied from the caller.
        public Task RecordAsync(AuditEntryDto entry, CancellationToken cancellationToken = default)
        {
            _db.AuditLogEntries.Add(new AuditLogEntry
            {
                Action = entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                LocationId = entry.LocationId,
                Before = entry.Before,
                After = entry.After,
                Reason = entry.Reason,
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()
            });
            return _db.SaveChangesAsync(cancellationToken);
        }
    }
}
