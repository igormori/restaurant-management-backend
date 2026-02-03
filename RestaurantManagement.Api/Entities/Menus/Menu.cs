using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Api.Entities.Organizations;

namespace RestaurantManagement.Api.Entities.Menus
{
    public class Menu
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Organization Organization { get; set; } = null!;
        public ICollection<LocationMenu> LocationMenus { get; set; } = new List<LocationMenu>();
    }
}
