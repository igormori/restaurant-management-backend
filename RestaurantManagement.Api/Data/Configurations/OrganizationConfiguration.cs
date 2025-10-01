using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantManagement.Api.Entities.Organizations;

namespace RestaurantManagement.Api.Data.Configurations
{
    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            builder.ToTable("organizations");
            builder.HasKey(o => o.Id);

            builder.HasOne(o => o.Settings)
                .WithOne(s => s.Organization)
                .HasForeignKey<OrganizationSettings>(s => s.OrganizationId);

            builder.HasMany(o => o.Locations)
                .WithOne(l => l.Organization)
                .HasForeignKey(l => l.OrganizationId);
        }
    }
}