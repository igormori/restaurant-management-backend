using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IPasswordResetService
    {
        /// <summary>Sends a reset code to an eligible account; always returns the same acknowledgement (task T6).</summary>
        Task<MessageResponse> RequestResetCodeAsync(ForgotPasswordRequest request);

        /// <summary>Replaces the password with a valid code and ends every session of the user (task T7).</summary>
        Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request);
    }
}
