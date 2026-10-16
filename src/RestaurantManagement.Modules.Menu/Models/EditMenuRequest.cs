using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Menu.Models
{
    public class EditMenuRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}
