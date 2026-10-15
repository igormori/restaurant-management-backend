namespace RestaurantManagement.Modules.Identity.Models
{
    // Stub (tester): the response body plus the refresh token the controller puts in the cookie (task T3).
    public class SessionResult
    {
        public AuthResponse Auth { get; init; } = null!;
        public string RefreshToken { get; init; } = string.Empty;
        public DateTime RefreshTokenExpiresAt { get; init; }
    }
}
