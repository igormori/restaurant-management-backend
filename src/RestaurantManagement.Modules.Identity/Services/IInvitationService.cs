using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IInvitationService
    {
        /// <summary>Creates (or replaces the pending) invitation, audits it and emails the link. Language is "en" or "pt".</summary>
        Task<InvitationResponse> CreateAsync(CreateInvitationRequest request, string language);

        /// <summary>Cancels a pending invitation of the token organization (404 when not found or not pending).</summary>
        Task CancelAsync(Guid invitationId);

        /// <summary>Signed in: joins with the signed-in account when its email equals the invited email.</summary>
        Task AcceptAsync(AcceptInvitationRequest request);

        /// <summary>Public: creates a verified account for the invited email and joins.</summary>
        Task AcceptWithNewAccountAsync(AcceptInvitationWithAccountRequest request);
    }
}
