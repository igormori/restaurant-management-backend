using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface ISessionService
    {
        Task<SessionResult> LoginAsync(LoginRequest request, ClientInfo client, string? previousRefreshToken = null);
        Task<SessionResult> RefreshTokenAsync(string? refreshToken);
        Task<SessionResult> SelectOrganizationAsync(Guid userId, SelectOrganizationRequest request, string? refreshToken, ClientInfo client);

        Task LogoutAsync(string? refreshToken);
        Task LogoutAllAsync(Guid userId);
    }
}
