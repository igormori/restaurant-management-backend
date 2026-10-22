using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RestaurantManagement.Shared.Models;

namespace RestaurantManagement.Modules.Identity.Models.Sessions
{
    public class SelectOrganizationRequest
    {
        [RequiredId(ErrorMessage = "RequiredAttribute_ValidationError")]
        [JsonConverter(typeof(LenientGuidConverter))]
        public Guid OrganizationId { get; set; }
    }
}
