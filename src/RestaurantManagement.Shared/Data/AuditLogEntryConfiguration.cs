using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RestaurantManagement.Shared.Data
{
    public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
    {
        public void Configure(EntityTypeBuilder<AuditLogEntry> entry)
        {
            entry.Property(e => e.Action).HasMaxLength(64);
            entry.Property(e => e.EntityType).HasMaxLength(64);
            entry.Property(e => e.Reason).HasMaxLength(500);
            entry.Property(e => e.IpAddress).HasMaxLength(45);
            entry.HasIndex(e => new { e.OrganizationId, e.CreatedAt });
        }
    }
}
