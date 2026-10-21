using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services.Sessions
{
    public interface ISessionService
    {
        Task<SessionResult> LoginAsync(LoginRequest request, ClientInfo client, string? previousRefreshToken);
        Task<SessionResult> RefreshTokenAsync(string? refreshToken);
        Task<SessionResult> SelectOrganizationAsync(Guid userId, SelectOrganizationRequest request, string? refreshToken, ClientInfo client);

        Task LogoutAsync(string? refreshToken);
        Task LogoutAllAsync(Guid userId);
    }
}
