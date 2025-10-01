using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantManagement.Api.Entities.Locations;

namespace RestaurantManagement.Api.Data.Configurations
{
    public class LocationConfiguration : IEntityTypeConfiguration<Location>
    {
        public void Configure(EntityTypeBuilder<Location> builder)
        {
            builder.ToTable("locations");
            builder.HasKey(l => l.Id);

            builder.Property(l => l.Name)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(l => l.Address)
                .HasMaxLength(255);

            builder.Property(l => l.City)
                .HasMaxLength(120);

            builder.Property(l => l.State)
                .HasMaxLength(120);

            builder.Property(l => l.PostalCode)
                .HasMaxLength(20);

            builder.Property(l => l.Country)
                .HasMaxLength(120);

            builder.Property(l => l.PhoneNumber)
                .HasMaxLength(30);
        }
    }
}