using Microsoft.AspNetCore.Http;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Shared.Services.AuditLog
{
    // Stub created by the tester: T1 implements it.
    public class AuditLogWriter : IAuditLog
    {
        private readonly AuditLogDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogWriter(AuditLogDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task RecordAsync(AuditEntryDto entry, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
