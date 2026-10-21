using RestaurantManagement.Shared.Data.Entities;
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

        public string TimeZone { get; set; } = null!;
        public LocationType Type { get; set; }
        public string Code { get; set; } = null!;


        // Navigation
        public Organization Organization { get; set; } = null!;
    }
}