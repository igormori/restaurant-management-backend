using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sentry;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class SessionService : ISessionService
    {
        // A valid bcrypt hash (at the production work factor) for a password nobody knows.
        // Verifying against it when no user row exists keeps login's response time and code
        // path the same whether or not the email is registered.
        private const string DummyPasswordHash = "$2a$11$aanIzxoCRxQATB.lcuy8zOgcxV.Fq4zTZHoJMKJ1gLzIU4nEcpnF2";

        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly JwtOptions _jwtOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IHub _hub;

        public SessionService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IOptions<JwtOptions> jwtOptions,
            IStringLocalizer<SharedResource> localizer,
            IHub hub)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _jwtOptions = jwtOptions.Value;
            _localizer = localizer;
            _hub = hub;
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email == email);

            // 🔑 Always verify a password, even when no user row exists, so an unregistered
            // email does equivalent work to a wrong password for a real account.
            var passwordHash = user?.PasswordHash ?? DummyPasswordHash;
            var isPasswordValid = VerifyPassword(request.Password, passwordHash);

            // A locked account is not told it is locked, and attempts while locked do not extend the lock.
            var isLocked = user != null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow;

            if (user != null && !isLocked && !isPasswordValid)
            {
                user.FailedAttempts++;

                if (user.FailedAttempts >= _securityOptions.MaxFailedLoginAttempts)
                {
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(_securityOptions.LockoutDurationMinutes);
                    user.FailedAttempts = 0;

                    ReportLockout(user);
                }

                await _db.SaveChangesAsync();
            }

            // Unknown email, wrong password, locked, unverified and deactivated accounts all collapse into the
            // same response so none of them can be told apart from the outside.
            if (user == null || isLocked || !isPasswordValid || !user.IsVerified || !user.IsActive)
                throw new BusinessException(_localizer["InvalidEmailOrPassword"].Value, 401);

            // ✅ Success → reset counters
            user.FailedAttempts = 0;
            user.LockedUntil = null;

            var jwt = GenerateJwt(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken); ;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpireDays);
            user.LastLoginAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Token = jwt,
                RefreshToken = refreshToken
            };
        }

        // The user ID, never the email, so personal data stays out of Sentry.
        private void ReportLockout(User user)
        {
            _hub.CaptureEvent(new SentryEvent
            {
                Level = SentryLevel.Info,
                Message = new SentryMessage { Formatted = $"{_localizer["AccountLocked"].Value}: {user.Id}" }
            });
        }

        public async Task<AuthResponse> RefreshTokenAsync(RefreshRequest request)
        {
            var user = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || user.RefreshTokenHash == null || user.RefreshTokenExpiry == null)
                throw new BusinessException(_localizer["InvlideOrExpiredToken"].Value, 401);

            // Same gates as login, so a refresh token can't outlive deactivation, unverified or locked state.
            if (!user.IsActive || !user.IsVerified || (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow))
                throw new BusinessException(_localizer["InvlideOrExpiredToken"].Value, 401);

            if (user.RefreshTokenExpiry < DateTime.UtcNow)
                throw new BusinessException(_localizer["InvlideOrExpiredToken"].Value, 401);

            if (!BCrypt.Net.BCrypt.Verify(request.RefreshToken, user.RefreshTokenHash))
                throw new BusinessException(_localizer["InvalidRefreshToken"].Value, 401);

            string newJwt = GenerateJwt(user);

            var newRefreshToken = GenerateRefreshToken();
            user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(newRefreshToken);

            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpireDays);
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Token = newJwt,
                RefreshToken = newRefreshToken
            };
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private string GenerateJwt(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
            };

            foreach (var role in user.Roles.Select(r => r.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var organizationIds = user.Roles
                .Where(r => r.OrganizationId.HasValue)
                .Select(r => r.OrganizationId!.Value)
                .Distinct()
                .ToList();

            // Users in several organizations must pick one, so only a single org is claimed automatically.
            if (organizationIds.Count == 1)
                claims.Add(new Claim(AppClaimTypes.OrganizationId, organizationIds[0].ToString()));

            var jwtKey = _jwtOptions.Key;
            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("JWT key is not configured.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expireMinutes = _jwtOptions.ExpireMinutes > 0
                ? _jwtOptions.ExpireMinutes
                : 60;

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private bool VerifyPassword(string plainPassword, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(hashedPassword))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(plainPassword, hashedPassword);
            }
            catch
            {
                return false;
            }
        }
    }
}
