using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IVerificationService
    {
        Task<string> VerifyEmailAsync(VerifyEmailRequest request);
        Task<string> ResendVerificationCodeAsync(ResendVerificationRequest request);
    }
}
