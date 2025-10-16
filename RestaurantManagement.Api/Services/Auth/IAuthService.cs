using RestaurantManagement.Api.Models.Auth;
using System.Threading.Tasks;

namespace RestaurantManagement.Api.Services.Auth
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshTokenAsync(RefreshRequest request);
        Task<string> VerifyEmailAsync(VerifyEmailRequest request);
        Task<string> ResendVerificationCodeAsync(ResendVerificationRequest request);
        
    }
}