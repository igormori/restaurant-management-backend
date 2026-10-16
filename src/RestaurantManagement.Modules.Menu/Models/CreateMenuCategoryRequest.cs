using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RestaurantManagement.Shared.Models;

namespace RestaurantManagement.Modules.Menu.Models
{
    public class CreateMenuCategoryRequest
    {
        [RequiredId(ErrorMessage = "RequiredAttribute_ValidationError")]
        [JsonConverter(typeof(LenientGuidConverter))]
        public Guid MenuId { get; set; }

        public Guid? LocationId { get; set; }

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(255, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;
    }
}
