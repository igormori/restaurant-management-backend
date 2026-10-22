using RestaurantManagement.Modules.Identity.Entities.Sessions;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Models.Sessions;

namespace RestaurantManagement.Modules.Identity.Services.Sessions
{
    internal static class SessionResults
    {
        public static AuthResponse BuildAuth(User user, string accessToken) => new()
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Token = accessToken
        };

        public static SessionResult Build(AuthResponse auth, IssuedRefreshToken issued) => new()
        {
            Auth = auth,
            RefreshToken = issued.Token,
            RefreshTokenExpiresAt = issued.ExpiresAt
        };
    }
}
