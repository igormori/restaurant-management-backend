using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RestaurantManagement.Modules.Menu.Entities
{
    [Index(nameof(LocationId))]
    public class LocationMenu
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid LocationId { get; set; }

        [ForeignKey("Menu")]
        public Guid MenuId { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Menu Menu { get; set; } = null!;
    }
}
