using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Organization.Entities;

namespace RestaurantManagement.Modules.Organization.Data
{
    public class OrganizationDbContext : DbContext
    {
        public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
            : base(options) {}

        public DbSet<Entities.Organization> Organizations { get; set; }
        public DbSet<OrganizationSettings> OrganizationSettings { get; set; }
        public DbSet<Location> Locations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply any entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
