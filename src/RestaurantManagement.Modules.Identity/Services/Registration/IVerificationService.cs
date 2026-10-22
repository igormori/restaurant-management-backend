using RestaurantManagement.Modules.Identity.Models.Common;
using RestaurantManagement.Modules.Identity.Models.Registration;

namespace RestaurantManagement.Modules.Identity.Services.Registration
{
    public interface IVerificationService
    {
        Task<MessageResponse> VerifyEmailAsync(VerifyEmailRequest request);
        Task<MessageResponse> ResendVerificationCodeAsync(ResendVerificationRequest request);
    }
}
