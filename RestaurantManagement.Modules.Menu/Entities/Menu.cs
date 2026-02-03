using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Menu.Entities
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

        // Navigation property (within same module)
        public ICollection<LocationMenu> LocationMenus { get; set; } = new List<LocationMenu>();
    }
}
