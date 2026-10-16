using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared;

namespace RestaurantManagement.Modules.Organization.Models
{
    /// <summary>
    /// Accepts only IANA (Internet Assigned Numbers Authority) time zone names such as America/Toronto.
    /// On Linux and macOS .NET also resolves Windows names, so the shape of the name is checked too.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IanaTimeZoneAttribute : ValidationAttribute
    {
        public IanaTimeZoneAttribute() => ErrorMessage = "IanaTimeZone_ValidationError";

        // The platform only translates the messages of its own attributes, so the catalog text is read here.
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (IsValid(value)) return ValidationResult.Success;

            var localizer = (IStringLocalizer<SharedResource>?)validationContext.GetService(typeof(IStringLocalizer<SharedResource>));
            var field = validationContext.MemberName ?? validationContext.DisplayName;
            var message = localizer is null ? $"{field} must be a valid IANA time zone name." : localizer[ErrorMessageString, field].Value;
            return new ValidationResult(message, validationContext.MemberName is null ? null : new[] { validationContext.MemberName });
        }

        public override bool IsValid(object? value)
        {
            if (value is null) return true;
            if (value is not string name || name != name.Trim()) return false;
            if (!name.Contains('/') && name != "UTC") return false;

            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(name);
                return true;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return false;
            }
        }
    }
}
