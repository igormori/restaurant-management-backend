using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Shared;

namespace RestaurantManagement.Modules.Identity.Models
{
    /// <summary>
    /// Rejects a password over 72 UTF-8 bytes (the most BCrypt reads) without judging its strength, so login can
    /// refuse it before the user lookup. An empty value is left to [Required].
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class MaxPasswordBytesAttribute : ValidationAttribute
    {
        // Default key so the catalog completeness check sees a real key.
        public MaxPasswordBytesAttribute() => ErrorMessage = "PasswordTooManyBytes_ValidationError";

        // The platform only translates the messages of its own attributes, so the catalog text is read here.
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string password || !BcryptPasswordHasher.ExceedsByteLimit(password))
                return ValidationResult.Success;

            var localizer = (IStringLocalizer<SharedResource>?)validationContext.GetService(typeof(IStringLocalizer<SharedResource>));
            var field = validationContext.MemberName ?? validationContext.DisplayName;
            var message = localizer is null
                ? $"{field} must be a maximum of {BcryptPasswordHasher.MaxPasswordBytes} bytes."
                : localizer[ErrorMessage!, field, BcryptPasswordHasher.MaxPasswordBytes].Value;
            return new ValidationResult(message, validationContext.MemberName is null ? null : new[] { validationContext.MemberName });
        }
    }
}
