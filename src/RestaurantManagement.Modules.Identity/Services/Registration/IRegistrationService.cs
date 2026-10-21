using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services.Registration
{
    public interface IRegistrationService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
    }
}
