using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Menu.Models
{
    public class UpdateMenuCategoryRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(255, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;
    }
}
