using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Organization.Entities;

namespace RestaurantManagement.Modules.Organization.Data
{
    public class OrganizationDbContext : DbContext
    {
        public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
            : base(options) { }

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

            modelBuilder.Entity<Location>()
                .HasOne(l => l.Organization)
                .WithMany(o => o.Locations)
                .HasForeignKey(l => l.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrganizationSettings>()
                .HasOne(s => s.Organization)
                .WithOne(o => o.Settings)
                .HasForeignKey<OrganizationSettings>(s => s.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}
