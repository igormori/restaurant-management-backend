using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Shared.Data.Tenancy;

namespace RestaurantManagement.Modules.Organization.Data
{
    public class OrganizationDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;

        public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        public DbSet<Entities.Organization> Organizations { get; set; }
        public DbSet<OrganizationSettings> OrganizationSettings { get; set; }
        public DbSet<Location> Locations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply any entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
            modelBuilder.Entity<Entities.Organization>()
                .Property(o => o.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Entities.Organization>()
                .Property(o => o.Currency)
                .HasConversion<string>()
                .HasMaxLength(3);

            modelBuilder.Entity<OrganizationSettings>()
                .Property(s => s.ServiceChargeRate)
                .HasPrecision(5, 2);

            modelBuilder.Entity<OrganizationSettings>()
                .Property(s => s.DiscountApprovalLimit)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Location>()
                .HasOne(l => l.Organization)
                .WithMany(o => o.Locations)
                .HasForeignKey(l => l.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Location>(location =>
            {
                location.Property(l => l.TimeZone).HasMaxLength(64);
                location.Property(l => l.Type).HasConversion<string>().HasMaxLength(20)
                    .HasDefaultValue(LocationType.Store);
                location.Property(l => l.Code).HasMaxLength(10);
                location.HasIndex(l => new { l.OrganizationId, l.Code }).IsUnique();
            });

            modelBuilder.Entity<OrganizationSettings>()
                .HasOne(s => s.Organization)
                .WithOne(o => o.Settings)
                .HasForeignKey<OrganizationSettings>(s => s.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyTenantQueryFilters(() => _tenantContext);
        }
    }
}
