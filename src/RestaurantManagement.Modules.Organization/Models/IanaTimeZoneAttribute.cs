using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Modules.Organization.Models
{
    /// <summary>
    /// Accepts only IANA (Internet Assigned Numbers Authority) time zone names such as America/Toronto.
    /// On Linux and macOS .NET also resolves Windows names, so the shape of the name is checked too.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IanaTimeZoneAttribute : ValidationAttribute
    {
        public IanaTimeZoneAttribute() : base("TimeZone must be a valid IANA time zone name.") { }

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
