using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;

namespace RestaurantManagement.Modules.Identity.Data
{
    public class IdentityDbContext : DbContext
    {
        public const string UniqueEmailIndexName = "ix_users_email_normalized";
        public const string UniqueRoleNameIndexName = "ix_organization_roles_organization_id_name";
        private const int AccessCellMaxLength = 20;
        public const string UniqueMemberIndexName = "ix_organization_members_user_id_organization_id";

        public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<OrganizationMember> OrganizationMembers { get; set; }
        public DbSet<UserVerificationCode> UserVerificationCodes { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<PasswordResetCode> PasswordResetCodes { get; set; }
        public DbSet<PendingEmail> PendingEmails { get; set; }
        public DbSet<OrganizationRole> OrganizationRoles { get; set; }
        public DbSet<OrganizationRoleLevel> OrganizationRoleLevels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply any entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

            // Test stub for T3-T5, remove with the real mapping and migrations: the new access entities are in the
            // model only on SQLite (unit tests), so the PostgreSQL model still matches the migrations until then.
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                modelBuilder.Entity<MemberLocationLevel>();
                modelBuilder.Entity<MemberLocationRole>();
            }

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

            // A membership blocks deleting its user. Identity has no query filter: login reads every
            // membership of a user.
            modelBuilder.Entity<OrganizationMember>(entity =>
            {
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => new { m.UserId, m.OrganizationId })
                    .IsUnique()
                    .HasDatabaseName(UniqueMemberIndexName);

                // The check below is raw SQL, so the column name is fixed to match it in every provider.
                entity.Property(m => m.OrganizationId).HasColumnName("organization_id");

                // An unset Guid is all zeros, so reject it: a membership must belong to a real organization.
                entity.ToTable(t => t.HasCheckConstraint(
                    "ck_organization_members_organization_id_not_empty",
                    "organization_id <> '00000000-0000-0000-0000-000000000000'"));
            });

            // A role name is used once per organization; a role's default levels go with the role.
            modelBuilder.Entity<OrganizationRole>(entity =>
            {
                entity.ToTable("organization_roles");
                entity.Property(r => r.Name).IsRequired().HasMaxLength(OrganizationRole.NameMaxLength);
                entity.HasIndex(r => new { r.OrganizationId, r.Name })
                    .IsUnique()
                    .HasDatabaseName(UniqueRoleNameIndexName);
            });

            modelBuilder.Entity<OrganizationRoleLevel>(entity =>
            {
                entity.ToTable("organization_role_levels");
                entity.Property(l => l.Module).HasConversion<string>().HasMaxLength(AccessCellMaxLength);
                entity.Property(l => l.Level).HasConversion<string>().HasMaxLength(AccessCellMaxLength);
                entity.HasOne<OrganizationRole>()
                    .WithMany()
                    .HasForeignKey(l => l.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(l => new { l.RoleId, l.Module }).IsUnique();
            });

            // Codes are owned by the user and useless without them, so they go with the user.
            modelBuilder.Entity<UserVerificationCode>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reset codes are owned by the user and go with the user.
            modelBuilder.Entity<PasswordResetCode>(entity =>
            {
                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(c => c.UserId);
            });

            // A pending email has no user (the email may belong to nobody), so no foreign key.
            modelBuilder.Entity<PendingEmail>(entity =>
            {
                entity.ToTable("pending_emails");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Purpose).IsRequired();
                entity.HasIndex(r => r.NextAttemptAt);
                // The purge of old failed rows runs every pass and would otherwise scan the whole table.
                entity.HasIndex(r => r.FailedAt);
            });

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
