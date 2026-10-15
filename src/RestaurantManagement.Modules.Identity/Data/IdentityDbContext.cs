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
        public DbSet<RefreshToken> RefreshTokens { get; set; }

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

            modelBuilder.Entity<UserRole>()
                .HasOne(r => r.User)
                .WithMany(u => u.Roles)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Codes are owned by the user and useless without them, so they go with the user.
            modelBuilder.Entity<UserVerificationCode>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One row per device session; tokens go with the user. OrganizationId has no foreign key
            // because it belongs to another module.
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasOne(t => t.User)
                    .WithMany()
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(t => t.TokenHash).IsUnique();
                entity.HasIndex(t => t.UserId);
                entity.HasIndex(t => t.FamilyId);
                entity.Property(t => t.DeviceDescription).HasMaxLength(RefreshToken.DeviceDescriptionMaxLength);
                entity.Property(t => t.IpAddress).HasMaxLength(RefreshToken.IpAddressMaxLength);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
