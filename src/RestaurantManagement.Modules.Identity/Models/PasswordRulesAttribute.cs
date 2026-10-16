using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared;

namespace RestaurantManagement.Modules.Identity.Models
{
    /// <summary>
    /// Password rules shared by registration, reset and change: 8 to 128 characters with at least one
    /// uppercase letter, one digit and one symbol. An empty value is left to [Required].
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class PasswordRulesAttribute : ValidationAttribute
    {
        private const int MinLength = 8;
        private const int MaxLength = 128;

        // Default key so the catalog completeness check sees a real key; the broken rule picks the message.
        public PasswordRulesAttribute() => ErrorMessage = "PasswordComplexity_ValidationError";

        // The platform only translates the messages of its own attributes, so the catalog text is read here.
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string password || password.Length == 0) return ValidationResult.Success;

            var (key, length) = FindBrokenRule(password);
            if (key is null) return ValidationResult.Success;

            var localizer = (IStringLocalizer<SharedResource>?)validationContext.GetService(typeof(IStringLocalizer<SharedResource>));
            var field = validationContext.MemberName ?? validationContext.DisplayName;
            var message = localizer is null
                ? $"{field} does not meet the password rules."
                : localizer[key, length is null ? new object[] { field } : new object[] { field, length.Value }].Value;
            return new ValidationResult(message, validationContext.MemberName is null ? null : new[] { validationContext.MemberName });
        }

        private static (string? Key, int? Length) FindBrokenRule(string password)
        {
            if (password.Length < MinLength) return ("MinLengthAttribute_ValidationError", MinLength);
            if (password.Length > MaxLength) return ("MaxLengthAttribute_ValidationError", MaxLength);

            var hasUpper = password.Any(char.IsUpper);
            var hasDigit = password.Any(char.IsDigit);
            var hasSymbol = password.Any(c => !char.IsLetterOrDigit(c) && c != '_' && !char.IsWhiteSpace(c));
            return hasUpper && hasDigit && hasSymbol ? (null, null) : ("PasswordComplexity_ValidationError", null);
        }
    }
}
