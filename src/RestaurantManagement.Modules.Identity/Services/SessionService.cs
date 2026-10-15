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
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class SessionService : ISessionService
    {
        // A valid bcrypt hash (at the production work factor) for a password nobody knows.
        // Verifying against it when no user row exists keeps login's response time and code
        // path the same whether or not the email is registered.
        private const string DummyPasswordHash = "$2a$11$aanIzxoCRxQATB.lcuy8zOgcxV.Fq4zTZHoJMKJ1gLzIU4nEcpnF2";

        private const string OrganizationRequiredKey = "OrganizationRequired";
        private const string NotMemberKey = "UserNotMemberOfOrganization";
        private const string InvalidSessionKey = "InvalidOrExpiredToken";
        private const int BadRequestStatusCode = 400;
        private const int UnauthorizedStatusCode = 401;
        private const int ForbiddenStatusCode = 403;

        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly JwtOptions _jwtOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IHub _hub;
        private readonly IOrganizationLookup _organizationLookup;

        public SessionService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IOptions<JwtOptions> jwtOptions,
            IStringLocalizer<SharedResource> localizer,
            IHub hub,
            IOrganizationLookup organizationLookup)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _jwtOptions = jwtOptions.Value;
            _localizer = localizer;
            _hub = hub;
            _organizationLookup = organizationLookup;
        }

        // Stub (tester): T3 makes this the only constructor and uses the refresh token service.
        public SessionService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IOptions<JwtOptions> jwtOptions,
            IStringLocalizer<SharedResource> localizer,
            IHub hub,
            IOrganizationLookup organizationLookup,
            IRefreshTokenService refreshTokenService)
            : this(db, securityOptions, jwtOptions, localizer, hub, organizationLookup)
        {
        }

        // Stub (tester): T3.
        public Task<SessionResult> LoginAsync(LoginRequest request, ClientInfo client)
            => throw new NotImplementedException();

        // Stub (tester): T3.
        public Task<SessionResult> RefreshTokenAsync(string? refreshToken)
            => throw new NotImplementedException();

        // Stub (tester): T3.
        public Task<SessionResult> SelectOrganizationAsync(Guid userId, SelectOrganizationRequest request, string? refreshToken, ClientInfo client)
            => throw new NotImplementedException();

        // Stub (tester): T4.
        public Task LogoutAsync(string? refreshToken)
            => throw new NotImplementedException();

        // Stub (tester): T4.
        public Task LogoutAllAsync(Guid userId)
            => throw new NotImplementedException();

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

            // A fresh login starts without a selected organization.
            user.SelectedOrganizationId = null;

            var jwt = GenerateJwt(user, null);
            var refreshToken = GenerateRefreshToken();
            var organizations = await GetActiveOrganizationChoicesAsync(user);

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
                RefreshToken = refreshToken,
                Organizations = organizations
            };
        }

        private async Task<List<OrganizationChoice>> GetActiveOrganizationChoicesAsync(User user)
        {
            var organizationIds = user.Roles
                .Where(r => r.OrganizationId.HasValue)
                .Select(r => r.OrganizationId!.Value)
                .Distinct()
                .ToList();
            if (organizationIds.Count == 0)
                return new List<OrganizationChoice>();

            var organizations = await _organizationLookup.GetOrganizationsAsync(organizationIds);

            return (organizations ?? new List<OrganizationSummaryDto>())
                .Where(o => o.IsActive)
                .Select(o => new OrganizationChoice { Id = o.Id, Name = o.Name })
                .ToList();
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

            if (user.SelectedOrganizationId.HasValue && !IsMemberOf(user, user.SelectedOrganizationId.Value))
                throw new BusinessException(_localizer["InvlideOrExpiredToken"].Value, 401);

            string newJwt = GenerateJwt(user, user.SelectedOrganizationId);

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

        public async Task<AuthResponse> SelectOrganizationAsync(Guid userId, SelectOrganizationRequest request)
        {
            if (request.OrganizationId == Guid.Empty)
                throw new BusinessException(_localizer[OrganizationRequiredKey].Value, BadRequestStatusCode);

            var user = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId);

            var isLocked = user != null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow;
            if (user == null || !user.IsActive || !user.IsVerified || isLocked)
                throw new BusinessException(_localizer[InvalidSessionKey].Value, UnauthorizedStatusCode);

            // Same rejection for non-member, unknown and inactive organizations so none can be told apart.
            if (!await CanSelectOrganizationAsync(user, request.OrganizationId))
                throw new BusinessException(_localizer[NotMemberKey].Value, ForbiddenStatusCode);

            user.SelectedOrganizationId = request.OrganizationId;

            var jwt = GenerateJwt(user, request.OrganizationId);
            var refreshToken = GenerateRefreshToken();

            // Overwriting the hash revokes the refresh token issued before the selection.
            user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);
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
                Token = jwt,
                RefreshToken = refreshToken
            };
        }

        private async Task<bool> CanSelectOrganizationAsync(User user, Guid organizationId)
        {
            if (!IsMemberOf(user, organizationId))
                return false;

            var organizations = await _organizationLookup.GetOrganizationsAsync(new[] { organizationId });
            var organization = organizations?.FirstOrDefault(o => o.Id == organizationId);

            return organization is { IsActive: true };
        }

        private static bool IsMemberOf(User user, Guid organizationId)
        {
            return user.Roles.Any(r => r.OrganizationId == organizationId);
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private string GenerateJwt(User user, Guid? selectedOrganizationId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
            };

            var roles = selectedOrganizationId.HasValue
                ? user.Roles.Where(r => r.OrganizationId == selectedOrganizationId).Select(r => r.Role).OrderBy(r => r)
                : user.Roles.Select(r => r.Role);

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            if (selectedOrganizationId.HasValue)
                claims.Add(new Claim(AppClaimTypes.OrganizationId, selectedOrganizationId.Value.ToString()));

            var organizationIds = user.Roles
                .Where(r => r.OrganizationId.HasValue)
                .Select(r => r.OrganizationId!.Value)
                .Distinct()
                .ToList();

            // Users in several organizations must pick one, so only a single org is claimed automatically.
            if (!selectedOrganizationId.HasValue && organizationIds.Count == 1)
                claims.Add(new Claim(AppClaimTypes.OrganizationId, organizationIds[0].ToString()));

            var jwtKey = _jwtOptions.Key;
            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("JWT key is not configured.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpireMinutes),
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
