using RestaurantManagement.Api.Models.Auth;

namespace RestaurantManagement.Api.Services.Auth
{
    public interface IRegistrationService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
    }
}
