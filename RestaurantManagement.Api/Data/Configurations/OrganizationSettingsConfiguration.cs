using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantManagement.Api.Entities.Organizations;

namespace RestaurantManagement.Api.Data.Configurations
{
    public class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
    {
        public void Configure(EntityTypeBuilder<OrganizationSettings> builder)
        {
            builder.ToTable("organization_settings");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.PlanType)
                .IsRequired()
                .HasMaxLength(30)
                .HasDefaultValue("FREE");

            builder.Property(s => s.MaxLocations)
                .IsRequired()
                .HasDefaultValue(1);

            // Relationship (1:1)
            builder.HasOne(s => s.Organization)
                .WithOne(o => o.Settings)
                .HasForeignKey<OrganizationSettings>(s => s.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}