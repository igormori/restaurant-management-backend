using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Organization.Models
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

    }
}