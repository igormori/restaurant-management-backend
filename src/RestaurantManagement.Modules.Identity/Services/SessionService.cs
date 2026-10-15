using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Sentry;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
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
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IHub _hub;
        private readonly OrganizationMembership _organizations;
        private readonly IRefreshTokenService _refreshTokens;
        private readonly AccessTokenFactory _accessTokens;

        public SessionService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IOptions<JwtOptions> jwtOptions,
            IStringLocalizer<SharedResource> localizer,
            IHub hub,
            IOrganizationLookup organizationLookup,
            IRefreshTokenService refreshTokenService)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _localizer = localizer;
            _hub = hub;
            _organizations = new OrganizationMembership(organizationLookup);
            _refreshTokens = refreshTokenService;
            _accessTokens = new AccessTokenFactory(jwtOptions.Value);
        }

        // Stub (tester): T4.
        public Task LogoutAsync(string? refreshToken)
            => throw new NotImplementedException();

        // Stub (tester): T4.
        public Task LogoutAllAsync(Guid userId)
            => throw new NotImplementedException();

        public async Task<SessionResult> LoginAsync(LoginRequest request, ClientInfo client)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email == email);

            // 🔑 Always verify a password, even when no user row exists, so an unregistered
            // email does equivalent work to a wrong password for a real account.
            var passwordHash = user?.PasswordHash ?? DummyPasswordHash;
            var isPasswordValid = PasswordVerifier.Verify(request.Password, passwordHash);

            // A locked account is not told it is locked, and attempts while locked do not extend the lock.
            var isLocked = user != null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow;

            if (user != null && !isLocked && !isPasswordValid)
                await RecordFailedAttemptAsync(user);

            // Unknown email, wrong password, locked, unverified and deactivated accounts all collapse into the
            // same response so none of them can be told apart from the outside.
            if (user == null || isLocked || !isPasswordValid || !user.IsVerified || !user.IsActive)
                throw new BusinessException(_localizer["InvalidEmailOrPassword"].Value, 401);

            // ✅ Success → reset counters
            user.FailedAttempts = 0;
            user.LockedUntil = null;

            // A fresh login starts without a selected organization.
            user.SelectedOrganizationId = null;
            user.LastLoginAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Issued only after every check passed, so a refused login leaves no token behind.
            var issued = await _refreshTokens.IssueAsync(user.Id, null, client.DeviceDescription, client.IpAddress);
            var organizations = await _organizations.GetActiveChoicesAsync(user);

            var auth = SessionResults.BuildAuth(user, _accessTokens.Create(user, null));
            auth.PhoneNumber = null;
            auth.Organizations = organizations;

            return SessionResults.Build(auth, issued);
        }

        public async Task<SessionResult> RefreshTokenAsync(string? refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw InvalidSession();

            var rotated = await _refreshTokens.RotateAsync(refreshToken);
            if (rotated == null)
                throw InvalidSession();

            var user = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == rotated.UserId);

            // Same gates as login, so a refresh token can't outlive deactivation, unverified or locked state.
            var isLocked = user != null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow;
            var membershipLost = user != null && rotated.OrganizationId.HasValue && !OrganizationMembership.IsMemberOf(user, rotated.OrganizationId.Value);
            if (user == null || !user.IsActive || !user.IsVerified || isLocked || membershipLost)
            {
                // The replacement was already issued; revoke it so no active token is left behind.
                await _refreshTokens.RevokeAsync(rotated.Token);
                throw InvalidSession();
            }

            return SessionResults.Build(SessionResults.BuildAuth(user, _accessTokens.Create(user, rotated.OrganizationId)), rotated);
        }

        public async Task<SessionResult> SelectOrganizationAsync(
            Guid userId, SelectOrganizationRequest request, string? refreshToken, ClientInfo client)
        {
            if (request.OrganizationId == Guid.Empty)
                throw new BusinessException(_localizer[OrganizationRequiredKey].Value, BadRequestStatusCode);

            var user = await _db.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId);

            var isLocked = user != null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow;
            if (user == null || !user.IsActive || !user.IsVerified || isLocked)
                throw InvalidSession();

            // Same rejection for non-member, unknown and inactive organizations so none can be told apart.
            if (!await _organizations.CanSelectAsync(user, request.OrganizationId))
                throw new BusinessException(_localizer[NotMemberKey].Value, ForbiddenStatusCode);

            user.SelectedOrganizationId = request.OrganizationId;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Only the caller's own cookie token may be rotated; anything else starts a new family.
            var issued = string.IsNullOrWhiteSpace(refreshToken)
                ? null
                : await _refreshTokens.RotateAsync(refreshToken, request.OrganizationId, userId);
            issued ??= await _refreshTokens.IssueAsync(userId, request.OrganizationId, client.DeviceDescription, client.IpAddress);

            return SessionResults.Build(SessionResults.BuildAuth(user, _accessTokens.Create(user, request.OrganizationId)), issued);
        }

        private BusinessException InvalidSession()
            => new(_localizer[InvalidSessionKey].Value, UnauthorizedStatusCode);

        private async Task RecordFailedAttemptAsync(User user)
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

        // The user ID, never the email, so personal data stays out of Sentry.
        private void ReportLockout(User user)
        {
            _hub.CaptureEvent(new SentryEvent
            {
                Level = SentryLevel.Info,
                Message = new SentryMessage { Formatted = $"{_localizer["AccountLocked"].Value}: {user.Id}" }
            });
        }
    }
}
