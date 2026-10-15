using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface ISessionService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshTokenAsync(RefreshRequest request);
    }
}
