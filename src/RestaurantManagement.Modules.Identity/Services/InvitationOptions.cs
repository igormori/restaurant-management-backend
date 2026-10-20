using Microsoft.Extensions.Options;

namespace RestaurantManagement.Modules.Identity.Services
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
            => throw new NotImplementedException();
    }
}
