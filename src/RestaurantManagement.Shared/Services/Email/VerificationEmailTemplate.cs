using System.Net;
using System.Reflection;

namespace RestaurantManagement.Shared.Services.Email
{
    public static class VerificationEmailTemplate
    {
        public const string Subject = "Verify your email";

        private const string ResourceName = "RestaurantManagement.Shared.Templates.verification-email.html";
        private const string DefaultGreetingName = "there";

        private const string FirstNameToken = "{{FIRST_NAME}}";
        private const string CodeToken = "{{CODE}}";
        private const string ExpiryMinutesToken = "{{EXPIRY_MINUTES}}";
        private const string EmailToken = "{{EMAIL}}";

        private static readonly string TemplateHtml = LoadTemplate();

        public static string Build(string to, string firstName, string code, int expiryMinutes)
        {
            var greetingName = string.IsNullOrWhiteSpace(firstName) ? DefaultGreetingName : firstName.Trim();
            var encodedGreetingName = WebUtility.HtmlEncode(greetingName);
            var encodedTo = WebUtility.HtmlEncode(to);

            return TemplateHtml
                .Replace(FirstNameToken, encodedGreetingName)
                .Replace(CodeToken, code)
                .Replace(ExpiryMinutesToken, expiryMinutes.ToString())
                .Replace(EmailToken, encodedTo);
        }

        private static string LoadTemplate()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
                throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found in assembly '{assembly.FullName}'.");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
