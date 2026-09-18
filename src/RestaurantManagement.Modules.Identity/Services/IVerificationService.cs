using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IVerificationService
    {
        Task<MessageResponse> VerifyEmailAsync(VerifyEmailRequest request);
        Task<MessageResponse> ResendVerificationCodeAsync(ResendVerificationRequest request);
    }
}
