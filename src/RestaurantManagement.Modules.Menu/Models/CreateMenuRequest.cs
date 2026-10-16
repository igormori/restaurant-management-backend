using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Menu.Models
{
    public class CreateMenuRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string? Description { get; set; }

        public List<Guid>? LocationIds { get; set; }
    }
}
