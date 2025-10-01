using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.Models.Organizations
{
    public class CreateOrganizationRequest
    {
        private string _name = string.Empty;
        private string? _description;
        private string? _logoUrl;
        private string? _primaryColor;
        private string? _secondaryColor;
        private string? _accentColor;

        // Organization fields
        [Required, MaxLength(200)]
        public string Name
        {
            get => _name;
            set => _name = value?.Trim() ?? string.Empty;
        }

        [MaxLength(1000)]
        public string? Description
        {
            get => _description;
            set => _description = value?.Trim();
        }

        [Url, MaxLength(512)]
        public string? LogoUrl
        {
            get => _logoUrl;
            set => _logoUrl = value?.Trim();
        }

        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "PrimaryColor must be in hex format (#RRGGBB).")]
        public string? PrimaryColor
        {
            get => _primaryColor;
            set => _primaryColor = value?.Trim();
        }

        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "SecondaryColor must be in hex format (#RRGGBB).")]
        public string? SecondaryColor
        {
            get => _secondaryColor;
            set => _secondaryColor = value?.Trim();
        }

        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "AccentColor must be in hex format (#RRGGBB).")]
        public string? AccentColor
        {
            get => _accentColor;
            set => _accentColor = value?.Trim();
        }

        // Location fields (for first location during trial)
        [Required, MaxLength(150)]
        public string LocationName { get; set; } = null!;

        [MaxLength(255)]
        public string? Address { get; set; }

        [MaxLength(120)]
        public string? City { get; set; }

        [MaxLength(120)]
        public string? State { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [MaxLength(120)]
        public string? Country { get; set; }

        [MaxLength(30)]
        public string? PhoneNumber { get; set; }
    }
}