using Microsoft.Extensions.Options;

namespace RestaurantManagement.Modules.Identity.Services.Invitations
{
    /// <summary>Settings of invitations (section "Invitation").</summary>
    public class InvitationOptions
    {
        public const string SectionName = "Invitation";

        /// <summary>Address of the page where the invitee accepts; the email link adds the token to it.</summary>
        public string AcceptPageUrl { get; set; } = string.Empty;
    }

    /// <summary>Refuses to start the app without an absolute accept page address, naming the setting.</summary>
    public class InvitationOptionsValidator : IValidateOptions<InvitationOptions>
    {
        public ValidateOptionsResult Validate(string? name, InvitationOptions options)
        {
            // On Unix a path such as "/accept" also parses as an absolute (file) address, so the scheme is checked.
            if (!Uri.TryCreate(options.AcceptPageUrl?.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return ValidateOptionsResult.Fail("Invitation:AcceptPageUrl must be an absolute address.");

            return ValidateOptionsResult.Success;
        }
    }
}
