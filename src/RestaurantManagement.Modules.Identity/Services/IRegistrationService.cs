using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IRegistrationService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
    }
}
