using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.Models.Menus
{
    public class EditMenuRequest
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}
