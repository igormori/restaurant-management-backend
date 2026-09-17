using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Menu.Models
{
    public class CreateMenuCategoryRequest
    {
        [Required(ErrorMessage = "Menu ID is required")]
        public Guid MenuId { get; set; }

        [Required(ErrorMessage = "Organization ID is required")]
        public Guid OrganizationId { get; set; }

        public Guid? LocationId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;
    }
}
