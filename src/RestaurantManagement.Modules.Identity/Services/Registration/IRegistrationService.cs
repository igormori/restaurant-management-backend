using RestaurantManagement.Modules.Identity.Models.Registration;
using RestaurantManagement.Modules.Identity.Models.Sessions;

namespace RestaurantManagement.Modules.Identity.Services.Registration
{
    public interface IRegistrationService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
    }
}
