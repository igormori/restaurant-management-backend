using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Shared.Data.Entities;

namespace RestaurantManagement.Modules.Menu.Entities
{
    [Index(nameof(LocationId))]
    public class LocationMenu : TenantEntity
    {
        public Guid LocationId { get; set; }

        [ForeignKey("Menu")]
        public Guid MenuId { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation
        public Menu Menu { get; set; } = null!;
    }
}
