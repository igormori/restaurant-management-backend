using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface ISessionService
    {
        Task<SessionResult> LoginAsync(LoginRequest request, ClientInfo client);
        Task<SessionResult> RefreshTokenAsync(string? refreshToken);
        Task<SessionResult> SelectOrganizationAsync(Guid userId, SelectOrganizationRequest request, string? refreshToken, ClientInfo client);

        // Stubs (tester): T4.
        Task LogoutAsync(string? refreshToken);
        Task LogoutAllAsync(Guid userId);
    }
}
