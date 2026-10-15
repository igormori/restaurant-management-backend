using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IRefreshTokenService
    {
        /// <summary>Starts a new family for a sign-in and returns the raw token (shown once) and its expiry.</summary>
        Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid? organizationId, string? deviceDescription, string? ipAddress);

        /// <summary>
        /// Replaces an active token with a new one in the same family. A null organization keeps the
        /// token's own organization. Returns null when the token is unknown, expired or already revoked,
        /// or belongs to someone other than <paramref name="expectedUserId"/> (that token is left untouched).
        /// </summary>
        Task<IssuedRefreshToken?> RotateAsync(string rawToken, Guid? organizationId = null, Guid? expectedUserId = null);

        /// <summary>
        /// Revokes one token. Returns false when it is unknown or already revoked; an already revoked
        /// token is treated as reuse and its whole family is revoked.
        /// </summary>
        Task<bool> RevokeAsync(string rawToken);

        /// <summary>Revokes every active token of a user and returns how many were revoked.</summary>
        Task<int> RevokeAllForUserAsync(Guid userId);
    }
}
