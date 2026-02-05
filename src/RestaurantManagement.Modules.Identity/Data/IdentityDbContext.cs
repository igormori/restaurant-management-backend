using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;

namespace RestaurantManagement.Modules.Identity.Data
{
    public class IdentityDbContext : DbContext
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options) {}

        public DbSet<User> Users { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<UserVerificationCode> UserVerificationCodes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply any entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
