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
        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), MaxLength(200, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string Name
        {
            get => _name;
            set => _name = value?.Trim() ?? string.Empty;
        }

        [MaxLength(1000, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string? Description
        {
            get => _description;
            set => _description = value?.Trim();
        }

        [Url(ErrorMessage = "UrlAttribute_ValidationError"), MaxLength(512, ErrorMessage = "MaxLengthAttribute_ValidationError")]
        public string? LogoUrl
        {
            get => _logoUrl;
            set => _logoUrl = value?.Trim();
        }

        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "HexColor_ValidationError")]
        public string? PrimaryColor
        {
            get => _primaryColor;
            set => _primaryColor = value?.Trim();
        }

        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "HexColor_ValidationError")]
        public string? SecondaryColor
        {
            get => _secondaryColor;
            set => _secondaryColor = value?.Trim();
        }

        [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "HexColor_ValidationError")]
        public string? AccentColor
        {
            get => _accentColor;
            set => _accentColor = value?.Trim();
        }

        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), RegularExpression("^(BRL|USD|CAD)$", ErrorMessage = "Currency_ValidationError")]
        public string? Currency { get; set; }
    }
}