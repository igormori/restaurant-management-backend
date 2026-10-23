using System.Net;
using System.Reflection;

namespace RestaurantManagement.Modules.Identity.Services.Lockout
{
    /// <summary>
    /// The account-locked email, English only. It holds no code, no link and no IP address, so it can
    /// never help someone else unlock the account.
    /// </summary>
    public static class AccountLockedEmailTemplate
    {
        private static readonly string Template = Load("account-locked-email-en.html");

        public static string Subject() => "Sign-in to your account is paused";

        public static string Build(string firstName, int lockoutMinutes)
            => Template
                .Replace("{{NAME}}", WebUtility.HtmlEncode(firstName))
                .Replace("{{MINUTES}}", lockoutMinutes.ToString());

        private static string Load(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"RestaurantManagement.Modules.Identity.Templates.{fileName}";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found in assembly '{assembly.FullName}'.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
