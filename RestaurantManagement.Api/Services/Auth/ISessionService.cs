using RestaurantManagement.Api.Models.Auth;

namespace RestaurantManagement.Api.Services.Auth
{
    public interface ISessionService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshTokenAsync(RefreshRequest request);
    }
}
