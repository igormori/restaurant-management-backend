using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Sentry;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Lockout;
using RestaurantManagement.Modules.Identity.Models.Sessions;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Modules.Identity.Services.Passwords;

namespace RestaurantManagement.Modules.Identity.Services.Sessions
{
    public class SessionService : ISessionService
    {
        // A valid bcrypt hash (at the production work factor) for a password nobody knows.
        // Verifying against it when no user row exists keeps login's response time and code
        // path the same whether or not the email is registered.
        private const string DummyPasswordHash = "$2a$12$z5AP5prcIZEKwcL7mduukuQ20TNSkKv4d3UTPLfXQSYByQbNKlPUG";

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
        private readonly LoginLockout _lockout;
        private readonly IRefreshTokenService _refreshTokens;
        private readonly AccessTokenFactory _accessTokens;
        private readonly IPasswordHasher _passwordHasher;

        public SessionService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IOptions<JwtOptions> jwtOptions,
            IStringLocalizer<SharedResource> localizer,
            IHub hub,
            IOrganizationLookup organizationLookup,
            IRefreshTokenService refreshTokenService,
            IPasswordHasher passwordHasher)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _localizer = localizer;
            _hub = hub;
            _organizations = new OrganizationMembership(db, organizationLookup);
            _lockout = new LoginLockout(db, _securityOptions);
            _refreshTokens = refreshTokenService;
            _accessTokens = new AccessTokenFactory(jwtOptions.Value);
            _passwordHasher = passwordHasher;
        }

        public async Task LogoutAsync(string? refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return;

            // Unknown or already revoked tokens are not an error: logout answers the same either way.
            await _refreshTokens.RevokeAsync(refreshToken);
        }

        public async Task LogoutAllAsync(Guid userId)
            => await _refreshTokens.RevokeAllForUserAsync(userId);

        public async Task<SessionResult> LoginAsync(LoginRequest request, ClientInfo client, string? previousRefreshToken)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            // 🔑 Always verify a password, even when no user row exists, so an unregistered
            // email does equivalent work to a wrong password for a real account.
            var passwordHash = user?.PasswordHash ?? DummyPasswordHash;
            var isPasswordValid = _passwordHasher.Verify(request.Password, passwordHash);

            // A locked account or address is not told it is locked, and attempts while locked are not counted.
            var isLocked = user != null && await _lockout.IsLockedAsync(user, client.IpAddress);

            if (user != null && !isLocked && !isPasswordValid)
                await RecordFailedAttemptAsync(user, client.IpAddress);

            // 🔑 An unknown email runs the same database commands, so response time doesn't reveal it.
            if (user == null)
                await _lockout.SimulateFailureAsync(client.IpAddress);

            // Unknown email, wrong password, locked, unverified and deactivated accounts all collapse into the
            // same response so none of them can be told apart from the outside.
            if (user == null || isLocked || !isPasswordValid || !user.IsVerified || !user.IsActive)
                throw new BusinessException(_localizer["InvalidEmailOrPassword"].Value, 401);

            // A correct password restarts this address's count; the account's count ages out of its window.
            await _lockout.ClearAddressAsync(user, client.IpAddress);

            // The lock has expired (a live one refused the login above), so the stale end time goes.
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // The browser's earlier session ends only once the new login is accepted.
            if (!string.IsNullOrWhiteSpace(previousRefreshToken))
                await _refreshTokens.RevokeAsync(previousRefreshToken);

            // Issued only after every check passed, so a refused login leaves no token behind.
            var issued = await _refreshTokens.IssueAsync(user.Id, null, client.DeviceDescription, client.IpAddress);
            var memberOrganizationIds = await _organizations.GetOrganizationIdsAsync(user.Id);
            var organizations = await _organizations.GetActiveChoicesAsync(memberOrganizationIds);

            var auth = SessionResults.BuildAuth(user, _accessTokens.Create(user, memberOrganizationIds, null));
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
                .FirstOrDefaultAsync(u => u.Id == rotated.UserId);

            // Same gates as login, so a refresh token can't outlive deactivation, unverified or locked state.
            var isLocked = user != null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow;
            var membershipLost = user != null && rotated.OrganizationId.HasValue && !await _organizations.IsMemberOfAsync(user!.Id, rotated.OrganizationId.Value);
            if (user == null || !user.IsActive || !user.IsVerified || isLocked || membershipLost)
            {
                // The replacement was already issued; revoke it so no active token is left behind.
                await _refreshTokens.RevokeAsync(rotated.Token);
                throw InvalidSession();
            }

            var memberOrganizationIds = await _organizations.GetOrganizationIdsAsync(user.Id);
            return SessionResults.Build(SessionResults.BuildAuth(user, _accessTokens.Create(user, memberOrganizationIds, rotated.OrganizationId)), rotated);
        }

        public async Task<SessionResult> SelectOrganizationAsync(
            Guid userId, SelectOrganizationRequest request, string? refreshToken, ClientInfo client)
        {
            if (request.OrganizationId == Guid.Empty)
                throw new BusinessException(_localizer[OrganizationRequiredKey].Value, BadRequestStatusCode);

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            var isLocked = user != null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow;
            if (user == null || !user.IsActive || !user.IsVerified || isLocked)
                throw InvalidSession();

            // Same rejection for non-member, unknown and inactive organizations so none can be told apart.
            if (!await _organizations.CanSelectAsync(user, request.OrganizationId))
                throw new BusinessException(_localizer[NotMemberKey].Value, ForbiddenStatusCode);

            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Only the caller's own cookie token may be rotated; anything else starts a new family.
            var issued = string.IsNullOrWhiteSpace(refreshToken)
                ? null
                : await _refreshTokens.RotateAsync(refreshToken, request.OrganizationId, userId);
            issued ??= await _refreshTokens.IssueAsync(userId, request.OrganizationId, client.DeviceDescription, client.IpAddress);

            var memberOrganizationIds = await _organizations.GetOrganizationIdsAsync(user.Id);
            return SessionResults.Build(SessionResults.BuildAuth(user, _accessTokens.Create(user, memberOrganizationIds, request.OrganizationId)), issued);
        }

        private BusinessException InvalidSession()
            => new(_localizer[InvalidSessionKey].Value, UnauthorizedStatusCode);

        private async Task RecordFailedAttemptAsync(User user, string? ipAddress)
        {
            if (await _lockout.RecordFailureAsync(user, ipAddress) != LockOutcome.None)
                ReportLockout(user);
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
