using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Identity.Models
{
    public class SelectOrganizationRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        public Guid OrganizationId { get; set; }
    }
}
