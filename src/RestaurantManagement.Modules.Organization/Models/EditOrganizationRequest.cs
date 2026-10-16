using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Organization.Models
{
    public class EditOrganizationRequest : IValidatableObject
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

        [Required, Range(typeof(decimal), "0", "100", ErrorMessage = "ServiceChargeRate must be between 0 and 100.")]
        public decimal? ServiceChargeRate { get; set; }

        [Required, Range(typeof(decimal), "0", "100", ErrorMessage = "DiscountApprovalLimit must be between 0 and 100.")]
        public decimal? DiscountApprovalLimit { get; set; }

        [Required, RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "BusinessDayCutoff must be in HH:mm format.")]
        public string? BusinessDayCutoff { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (HasMoreThanTwoDecimals(ServiceChargeRate))
                yield return new ValidationResult("ServiceChargeRate allows at most 2 decimals.", new[] { nameof(ServiceChargeRate) });
            if (HasMoreThanTwoDecimals(DiscountApprovalLimit))
                yield return new ValidationResult("DiscountApprovalLimit allows at most 2 decimals.", new[] { nameof(DiscountApprovalLimit) });
        }

        private static bool HasMoreThanTwoDecimals(decimal? value) => value.HasValue && decimal.Round(value.Value, 2) != value.Value;

    }
}