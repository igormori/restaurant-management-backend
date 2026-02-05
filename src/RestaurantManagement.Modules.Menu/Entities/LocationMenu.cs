using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantManagement.Modules.Organization.Entities;

namespace RestaurantManagement.Modules.Menu.Entities
{
    public class LocationMenu
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [ForeignKey("Location")]
        public Guid LocationId { get; set; }
        
        [ForeignKey("Menu")]
        public Guid MenuId { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Location Location { get; set; } = null!;
        public Menu Menu { get; set; } = null!;
    }
}
