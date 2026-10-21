using System.Globalization;
using System.Net;
using System.Reflection;

namespace RestaurantManagement.Modules.Identity.Services.Invitations
{
    /// <summary>
    /// The invitation email in English or Portuguese. Every value is HTML-encoded; the email shows
    /// role names and locations only, never levels, and never a password.
    /// </summary>
    public static class InvitationEmailTemplate
    {
        private const string PortugueseLanguage = "pt";
        private static readonly CultureInfo PortugueseCulture = new("pt-BR");
        private static readonly CultureInfo EnglishCulture = new("en-US");

        private static readonly Dictionary<string, string> Templates = new()
        {
            ["en"] = Load("invitation-email-en.html"),
            ["pt"] = Load("invitation-email-pt.html")
        };

        public static string Subject(string language, string organizationName, string productName)
            => IsPortuguese(language)
                ? $"Você foi convidado para {organizationName} no {productName}"
                : $"You are invited to join {organizationName} on {productName}";

        /// <summary>Roles are (role name, location name) pairs, one line each.</summary>
        public static string Build(
            string language, string inviterName, string organizationName, string productName,
            IEnumerable<(string RoleName, string LocationName)> roles, string acceptPageUrl, string token, DateTime expiresAt)
        {
            var portuguese = IsPortuguese(language);
            var connector = portuguese ? "em" : "at";
            var roleLines = string.Concat(roles.Select(r =>
                $"<li>{Encode(r.RoleName)} {connector} {Encode(r.LocationName)}</li>"));

            return Templates[portuguese ? PortugueseLanguage : "en"]
                .Replace("{{INVITER}}", Encode(inviterName))
                .Replace("{{ORGANIZATION}}", Encode(organizationName))
                .Replace("{{PRODUCT}}", Encode(productName))
                .Replace("{{ROLES}}", roleLines)
                .Replace("{{EXPIRY}}", Encode(FormatExpiry(expiresAt, portuguese)))
                .Replace("{{LINK}}", Encode(BuildLink(acceptPageUrl, token)));
        }

        private static string BuildLink(string acceptPageUrl, string token)
            => $"{acceptPageUrl}{(acceptPageUrl.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(token)}";

        private static string FormatExpiry(DateTime expiresAt, bool portuguese)
            => portuguese
                ? expiresAt.ToString("d 'de' MMMM 'de' yyyy", PortugueseCulture) + " (UTC)"
                : expiresAt.ToString("MMMM d, yyyy", EnglishCulture) + " (UTC)";

        private static bool IsPortuguese(string language) => language == PortugueseLanguage;

        private static string Encode(string value) => WebUtility.HtmlEncode(value);

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
