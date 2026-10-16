using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared;

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

        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), Range(typeof(decimal), "0", "100", ErrorMessage = "RangeAttribute_ValidationError")]
        public decimal? ServiceChargeRate { get; set; }

        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), Range(typeof(decimal), "0", "100", ErrorMessage = "RangeAttribute_ValidationError")]
        public decimal? DiscountApprovalLimit { get; set; }

        [Required(ErrorMessage = "RequiredAttribute_ValidationError"), RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "TimeFormat_ValidationError")]
        public string? BusinessDayCutoff { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var localizer = (IStringLocalizer<SharedResource>?)validationContext.GetService(typeof(IStringLocalizer<SharedResource>));
            if (HasMoreThanTwoDecimals(ServiceChargeRate))
                yield return TwoDecimalsError(localizer, nameof(ServiceChargeRate));
            if (HasMoreThanTwoDecimals(DiscountApprovalLimit))
                yield return TwoDecimalsError(localizer, nameof(DiscountApprovalLimit));
        }

        private static ValidationResult TwoDecimalsError(IStringLocalizer<SharedResource>? localizer, string field)
        {
            var message = localizer is null
                ? $"{field} allows at most 2 decimals."
                : localizer["TwoDecimals_ValidationError", field].Value;
            return new ValidationResult(message, new[] { field });
        }

        private static bool HasMoreThanTwoDecimals(decimal? value) => value.HasValue && decimal.Round(value.Value, 2) != value.Value;

    }
}