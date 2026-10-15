using Microsoft.AspNetCore.Http;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Controllers
{
    // The refresh token travels only in this cookie, never in a response body.
    internal static class RefreshTokenCookie
    {
        public const string Name = "refresh_token";
        private const string CookiePath = "/api/auth";

        public static string? Read(HttpRequest request)
            => request.Cookies.TryGetValue(Name, out var token) ? token : null;

        public static void Write(HttpResponse response, string token, DateTime expiresAtUtc)
            => response.Cookies.Append(Name, token, Options(expiresAtUtc));

        public static void Clear(HttpResponse response)
            => response.Cookies.Append(Name, string.Empty, Options(DateTime.UtcNow.AddDays(-1)));

        private static CookieOptions Options(DateTime expiresAtUtc) => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc))
        };

        public static ClientInfo ClientOf(HttpContext context) => new()
        {
            DeviceDescription = Truncate(context.Request.Headers.UserAgent.ToString(), RefreshToken.DeviceDescriptionMaxLength),
            IpAddress = context.Connection.RemoteIpAddress?.ToString()
        };

        private static string? Truncate(string value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
    }
}
