using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Menu.Models
{
    public class CreateMenuRequest
    {
        [Required]
        public Guid OrganizationId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public List<Guid>? LocationIds { get; set; }
    }
}
