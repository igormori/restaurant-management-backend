using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Organization.Entities;

namespace RestaurantManagement.Modules.Organization.Models
{
    public class CreateLocationRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string State { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Country { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, IanaTimeZone]
        public string? TimeZone { get; set; }

        [RegularExpression("^(Store|CentralKitchen)$", ErrorMessage = "Type must be Store or CentralKitchen.")]
        public string? Type { get; set; }

        private string? _code;

        [Required, MaxLength(10), RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Code allows letters and digits only.")]
        public string? Code
        {
            get => _code;
            set => _code = value?.Trim();
        }
    }

    public class EditLocationRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string State { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Country { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, IanaTimeZone]
        public string? TimeZone { get; set; }

        [RegularExpression("^(Store|CentralKitchen)$", ErrorMessage = "Type must be Store or CentralKitchen.")]
        public string? Type { get; set; }

        private string? _code;

        [Required, MaxLength(10), RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Code allows letters and digits only.")]
        public string? Code
        {
            get => _code;
            set => _code = value?.Trim();
        }

        public LocationStatus? Status { get; set; }
    }

    public class LocationResponse
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public LocationStatus Status { get; set; }

        public string TimeZone { get; set; } = string.Empty;
        public LocationType Type { get; set; }
        public string Code { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
