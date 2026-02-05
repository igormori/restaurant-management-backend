using System;
using System.Collections.Generic;

namespace RestaurantManagement.Modules.Menu.Models
{
    public class MenuResponse
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public List<Guid> LocationIds { get; set; } = new List<Guid>();
    }
}
