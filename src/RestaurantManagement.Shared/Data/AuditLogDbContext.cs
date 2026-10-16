using Microsoft.EntityFrameworkCore;

namespace RestaurantManagement.Shared.Data
{
    // Stub created by the tester: T1 adds the configuration, tenant filter, index and the refusal
    // to save changed or removed entries.
    public class AuditLogDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;

        public AuditLogDbContext(DbContextOptions<AuditLogDbContext> options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        public DbSet<AuditLogEntry> AuditLogEntries { get; set; }
    }
}
