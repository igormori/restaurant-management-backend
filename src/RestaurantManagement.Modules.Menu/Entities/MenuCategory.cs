using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagement.Modules.Menu.Entities
{
    [Table("menu_categories")]
    public class MenuCategory
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("menu_id")]
        public Guid MenuId { get; set; }

        [Required]
        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

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

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("MenuId")]
        public virtual Entities.Menu? Menu { get; set; }
    }
}
