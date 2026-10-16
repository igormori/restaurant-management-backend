using System.ComponentModel.DataAnnotations.Schema;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Modules.Organization.Entities;

namespace RestaurantManagement.Modules.Organization.Entities
{
    public class Organization : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? AccentColor { get; set; }
        public bool IsActive { get; set; } = true;

        // Stub (tester): not mapped yet; T1 maps it and adds the migration. Remove [NotMapped] then.
        [NotMapped]
        public Currency Currency { get; set; }

        // Navigation Properties
        public OrganizationSettings? Settings { get; set; }
        public ICollection<Location> Locations { get; set; } = new List<Location>();
    }
}