using RestaurantManagement.Api.Models.Auth;

namespace RestaurantManagement.Api.Services.Auth
{
    public interface IVerificationService
    {
        Task<string> VerifyEmailAsync(VerifyEmailRequest request);
        Task<string> ResendVerificationCodeAsync(ResendVerificationRequest request);
    }
}
