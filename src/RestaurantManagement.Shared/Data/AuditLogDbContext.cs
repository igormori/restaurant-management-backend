using Microsoft.EntityFrameworkCore;

namespace RestaurantManagement.Shared.Data
{
    public class AuditLogDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;

        public AuditLogDbContext(DbContextOptions<AuditLogDbContext> options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        public DbSet<AuditLogEntry> AuditLogEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AuditLogEntryConfiguration());
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyTenantQueryFilters(() => _tenantContext);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            RefuseChangesToStoredEntries();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            RefuseChangesToStoredEntries();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // The log is append-only: a stored entry can never be edited or removed.
        private void RefuseChangesToStoredEntries()
        {
            var tampered = ChangeTracker.Entries<AuditLogEntry>()
                .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
            if (tampered)
                throw new InvalidOperationException("Audit log entries cannot be changed or removed.");
        }
    }
}
