using RestaurantManagement.Shared.Data;
using RestaurantManagement.Modules.Organization.Entities;

namespace RestaurantManagement.Modules.Organization.Entities
{
    public class Location : TenantEntity
    {

        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string PostalCode { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public LocationStatus Status { get; set; } = LocationStatus.Active;


        // Navigation
        public Organization Organization { get; set; } = null!;
    }
}