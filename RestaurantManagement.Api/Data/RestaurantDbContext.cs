using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Entities.Organizations;
using RestaurantManagement.Api.Entities.Locations;

namespace RestaurantManagement.Api.Data
{
    public class RestaurantDbContext : DbContext
    {
        public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options)
            : base(options) {}

        public DbSet<User> Users { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationSettings> OrganizationSettings { get; set; }
        public DbSet<Location> Locations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // automatically applies all IEntityTypeConfiguration classes
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(RestaurantDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}