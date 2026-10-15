using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    public class SelectOrganizationRequest
    {
        [Required]
        public Guid OrganizationId { get; set; }
    }
}
