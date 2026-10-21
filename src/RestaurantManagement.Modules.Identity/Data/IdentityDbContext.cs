using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;

namespace RestaurantManagement.Modules.Identity.Data
{
    public class IdentityDbContext : DbContext
    {
        public const string UniqueEmailIndexName = "ix_users_email_normalized";
        public const string UniqueRoleNameIndexName = "ix_organization_roles_organization_id_name";
        public const string UniqueMemberLevelIndexName = "ix_member_location_levels_cell";
        public const string UniqueMemberRoleIndexName = "ix_member_location_roles_label";
        private const int AccessCellMaxLength = 20;
        private const int InvitationEmailMaxLength = 320;
        private const int InvitationLanguageMaxLength = 5;
        public const string OpenInvitationIndexName = "ix_organization_invitations_open_email";
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
        public DbSet<MemberLocationLevel> MemberLocationLevels { get; set; }
        public DbSet<MemberLocationRole> MemberLocationRoles { get; set; }
        public DbSet<OrganizationInvitation> OrganizationInvitations { get; set; }
        public DbSet<OrganizationInvitationRole> OrganizationInvitationRoles { get; set; }
        public DbSet<OrganizationInvitationLevel> OrganizationInvitationLevels { get; set; }

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

            // One row per cell of a member's grid: a member has one level per module and location.
            // The location is an ID only; it belongs to the Organization module.
            modelBuilder.Entity<MemberLocationLevel>(entity =>
            {
                entity.ToTable("member_location_levels");
                entity.Property(l => l.Module).HasConversion<string>().HasMaxLength(AccessCellMaxLength);
                entity.Property(l => l.Level).HasConversion<string>().HasMaxLength(AccessCellMaxLength);
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(l => l.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(l => new { l.OrganizationId, l.UserId, l.LocationId, l.Module })
                    .IsUnique()
                    .HasDatabaseName(UniqueMemberLevelIndexName);
            });

            // A label points to its role by ID, so renaming a role shows everywhere; a role in use is not deleted.
            modelBuilder.Entity<MemberLocationRole>(entity =>
            {
                entity.ToTable("member_location_roles");
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(l => l.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<OrganizationRole>()
                    .WithMany()
                    .HasForeignKey(l => l.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(l => new { l.OrganizationId, l.UserId, l.LocationId, l.RoleId })
                    .IsUnique()
                    .HasDatabaseName(UniqueMemberRoleIndexName);
            });

            ConfigureInvitations(modelBuilder);

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

        // The token is stored only as a fingerprint. At most one open invitation exists per organization
        // and email; the email is always stored in lower case.
        private static void ConfigureInvitations(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrganizationInvitation>(entity =>
            {
                entity.ToTable("organization_invitations");
                entity.Property(i => i.Email).IsRequired().HasMaxLength(InvitationEmailMaxLength);
                entity.Property(i => i.Language).IsRequired().HasMaxLength(InvitationLanguageMaxLength);
                entity.Property(i => i.TokenHash).IsRequired();
                entity.HasIndex(i => i.TokenHash).IsUnique();
                // The index filter below is raw SQL, so the column names are fixed to match it in every provider.
                entity.Property(i => i.UsedAt).HasColumnName("used_at");
                entity.Property(i => i.CancelledAt).HasColumnName("cancelled_at");
                entity.Property(i => i.ReplacedAt).HasColumnName("replaced_at");
                entity.HasIndex(i => new { i.OrganizationId, i.Email })
                    .IsUnique()
                    .HasDatabaseName(OpenInvitationIndexName)
                    .HasFilter("used_at IS NULL AND cancelled_at IS NULL AND replaced_at IS NULL");
            });

            modelBuilder.Entity<OrganizationInvitationRole>(entity =>
            {
                entity.ToTable("organization_invitation_roles");
                entity.HasOne<OrganizationInvitation>().WithMany().HasForeignKey(r => r.InvitationId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<OrganizationRole>().WithMany().HasForeignKey(r => r.RoleId).OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(r => new { r.InvitationId, r.LocationId, r.RoleId }).IsUnique();
            });

            modelBuilder.Entity<OrganizationInvitationLevel>(entity =>
            {
                entity.ToTable("organization_invitation_levels");
                entity.Property(l => l.Module).HasConversion<string>().HasMaxLength(AccessCellMaxLength);
                entity.Property(l => l.Level).HasConversion<string>().HasMaxLength(AccessCellMaxLength);
                entity.HasOne<OrganizationInvitation>().WithMany().HasForeignKey(l => l.InvitationId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(l => new { l.InvitationId, l.LocationId, l.Module }).IsUnique();
            });
        }
    }
}
