using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface ISessionService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshTokenAsync(RefreshRequest request);
        Task<AuthResponse> SelectOrganizationAsync(Guid userId, SelectOrganizationRequest request);

        // Stubs (tester): per-device refresh tokens. T3 replaces the three methods above with these.
        Task<SessionResult> LoginAsync(LoginRequest request, ClientInfo client);
        Task<SessionResult> RefreshTokenAsync(string? refreshToken);
        Task<SessionResult> SelectOrganizationAsync(Guid userId, SelectOrganizationRequest request, string? refreshToken, ClientInfo client);

        // Stubs (tester): T4.
        Task LogoutAsync(string? refreshToken);
        Task LogoutAllAsync(Guid userId);
    }
}
