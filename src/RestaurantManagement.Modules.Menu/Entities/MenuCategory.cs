using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Modules.Menu.Entities
{
    [Table("menu_categories")]
    public class MenuCategory : TenantEntity
    {
        [Required]
        [Column("menu_id")]
        public Guid MenuId { get; set; }

        [Column("location_id")]
        public Guid? LocationId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        // Navigation properties
        [ForeignKey("MenuId")]
        public virtual Entities.Menu? Menu { get; set; }
    }
}
