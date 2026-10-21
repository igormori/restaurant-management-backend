using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class PasswordAuditRecorder : IPasswordAuditRecorder
    {
        private readonly IdentityDbContext _db;
        private readonly IAuditLog _auditLog;
        private readonly ITenantScope _tenantScope;

        public PasswordAuditRecorder(IdentityDbContext db, IAuditLog auditLog, ITenantScope tenantScope)
        {
            _db = db;
            _auditLog = auditLog;
            _tenantScope = tenantScope;
        }

        public async Task RecordAsync(Guid userId, string action, CancellationToken cancellationToken = default)
        {
            var organizationIds = await _db.OrganizationMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.OrganizationId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var organizationId in organizationIds)
            {
                // Reset is anonymous, so the organization comes from the user's memberships, not from a token.
                using (_tenantScope.Begin(organizationId))
                {
                    await _auditLog.RecordAsync(new AuditEntryDto
                    {
                        Action = action,
                        EntityType = "User",
                        EntityId = userId
                    }, cancellationToken);
                }
            }
        }
    }
}
