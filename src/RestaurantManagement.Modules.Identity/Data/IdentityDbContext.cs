using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;

namespace RestaurantManagement.Modules.Identity.Data
{
    public class IdentityDbContext : DbContext
    {
        public const string UniqueEmailIndexName = "ix_users_email_normalized";

        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<UserVerificationCode> UserVerificationCodes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply any entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

            // Case-insensitive uniqueness on email, enforced at the database level via a
            // stored computed shadow column so the constraint holds regardless of write path.
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property<string>("EmailNormalized")
                    .HasComputedColumnSql("lower(email)", stored: true);

                entity.HasIndex("EmailNormalized")
                    .IsUnique()
                    .HasDatabaseName(UniqueEmailIndexName);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
