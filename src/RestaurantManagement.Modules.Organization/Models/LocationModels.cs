using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Modules.Organization.Entities;

namespace RestaurantManagement.Modules.Organization.Models
{
    public class CreateLocationRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(255, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string State { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string PostalCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Country { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), IanaTimeZone]
        public string? TimeZone { get; set; }

        [RegularExpression("^(Store|CentralKitchen)$", ErrorMessage = "LocationType_ValidationError")]
        public string? Type { get; set; }

        private string? _code;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), MaxLength(10, ErrorMessage = "MaxLengthAttribute_ValidationError"), RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "LettersAndDigits_ValidationError")]
        public string? Code
        {
            get => _code;
            set => _code = value?.Trim();
        }
    }

    public class EditLocationRequest
    {
        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(255, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string State { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string PostalCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(100, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Country { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError")]
        [MaxLength(20, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), IanaTimeZone]
        public string? TimeZone { get; set; }

        [RegularExpression("^(Store|CentralKitchen)$", ErrorMessage = "LocationType_ValidationError")]
        public string? Type { get; set; }

        private string? _code;

        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), MaxLength(10, ErrorMessage = "MaxLengthAttribute_ValidationError"), RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "LettersAndDigits_ValidationError")]
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
