using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IdentityDbContext _db;
        private readonly JwtOptions _jwtOptions;

        public RefreshTokenService(IdentityDbContext db, IOptions<JwtOptions> jwtOptions)
        {
            _db = db;
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid? organizationId, string? deviceDescription, string? ipAddress)
        {
            var (row, issued) = BuildRow(userId, organizationId, Guid.NewGuid(), deviceDescription, ipAddress);
            _db.RefreshTokens.Add(row);
            await _db.SaveChangesAsync();
            return issued;
        }

        public async Task<IssuedRefreshToken?> RotateAsync(string rawToken, Guid? organizationId = null, Guid? expectedUserId = null)
        {
            var existing = await FindAsync(rawToken);
            var now = DateTime.UtcNow;
            if (existing == null || existing.ExpiresAt <= now)
            {
                return null;
            }

            if (expectedUserId.HasValue && existing.UserId != expectedUserId.Value)
            {
                return null;
            }

            if (existing.RevokedAt != null)
            {
                await RevokeFamilyAsync(existing.FamilyId);
                return null;
            }

            return await ReplaceAsync(existing, organizationId, now);
        }

        public async Task<bool> RevokeAsync(string rawToken)
        {
            var existing = await FindAsync(rawToken);
            if (existing == null)
            {
                return false;
            }

            if (existing.RevokedAt != null)
            {
                await RevokeFamilyAsync(existing.FamilyId);
                return false;
            }

            var now = DateTime.UtcNow;
            var changed = await _db.RefreshTokens
                .Where(t => t.Id == existing.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevocationReason, RefreshToken.ReasonLogout));
            if (changed == 0)
            {
                await RevokeFamilyAsync(existing.FamilyId);
                return false;
            }

            return true;
        }

        public async Task<int> RevokeAllForUserAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            return await _db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevocationReason, RefreshToken.ReasonLogoutEverywhere));
        }

        private async Task<IssuedRefreshToken?> ReplaceAsync(RefreshToken existing, Guid? organizationId, DateTime now)
        {
            var (row, issued) = BuildRow(
                existing.UserId, organizationId ?? existing.OrganizationId, existing.FamilyId,
                existing.DeviceDescription, existing.IpAddress);

            await using var transaction = await _db.Database.BeginTransactionAsync();
            var changed = await _db.RefreshTokens
                .Where(t => t.Id == existing.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevocationReason, RefreshToken.ReasonRotated)
                    .SetProperty(t => t.ReplacedByTokenId, row.Id));
            if (changed == 0)
            {
                await transaction.RollbackAsync();
                await RevokeFamilyAsync(existing.FamilyId);
                return null;
            }

            _db.RefreshTokens.Add(row);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return issued;
        }

        private async Task<RefreshToken?> FindAsync(string rawToken)
        {
            if (string.IsNullOrEmpty(rawToken))
            {
                return null;
            }

            var hash = Fingerprint(rawToken);
            return await _db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash);
        }

        private Task<int> RevokeFamilyAsync(Guid familyId)
        {
            var now = DateTime.UtcNow;
            return _db.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevocationReason, RefreshToken.ReasonReuseDetected));
        }

        private (RefreshToken Row, IssuedRefreshToken Issued) BuildRow(
            Guid userId, Guid? organizationId, Guid familyId, string? deviceDescription, string? ipAddress)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var now = DateTime.UtcNow;
            var expiresAt = now.AddDays(_jwtOptions.RefreshTokenExpireDays);
            var row = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                FamilyId = familyId,
                TokenHash = Fingerprint(rawToken),
                CreatedAt = now,
                ExpiresAt = expiresAt,
                DeviceDescription = Truncate(deviceDescription, RefreshToken.DeviceDescriptionMaxLength),
                IpAddress = Truncate(ipAddress, RefreshToken.IpAddressMaxLength)
            };
            var issued = new IssuedRefreshToken
            {
                Token = rawToken,
                ExpiresAt = expiresAt,
                UserId = userId,
                OrganizationId = organizationId,
                FamilyId = familyId
            };
            return (row, issued);
        }

        private static string Fingerprint(string rawToken)
            => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        private static string? Truncate(string? value, int maxLength)
            => value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
    }
}
