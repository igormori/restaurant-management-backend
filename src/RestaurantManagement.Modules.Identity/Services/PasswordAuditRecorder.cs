using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Stub (tester): implemented in T5.
    public class PasswordAuditRecorder : IPasswordAuditRecorder
    {
        public PasswordAuditRecorder(IdentityDbContext db, IAuditLog auditLog, ITenantScope tenantScope)
        {
        }

        public Task RecordAsync(Guid userId, string action, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
