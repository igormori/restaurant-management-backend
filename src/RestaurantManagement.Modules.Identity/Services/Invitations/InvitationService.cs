using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Invitations;
using RestaurantManagement.Modules.Identity.Models.Invitations;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Modules.Identity.Services.Members;
using RestaurantManagement.Modules.Identity.Services.Passwords;

namespace RestaurantManagement.Modules.Identity.Services.Invitations
{
    public partial class InvitationService : IInvitationService
    {
        private const int InvitationLifetimeDays = 7;

        private readonly IdentityDbContext _db;
        private readonly IOrganizationLookup _organizationLookup;
        private readonly ITenantContext _tenantContext;
        private readonly ICurrentUser _currentUser;
        private readonly IMemberGridBuilder _gridBuilder;
        private readonly IMemberDelegationRules _rules;
        private readonly IAuditLog _auditLog;
        private readonly IEmailService _emailService;
        private readonly IOptions<InvitationOptions> _invitationOptions;
        private readonly IOptions<EmailOptions> _emailOptions;
        private readonly TimeProvider _timeProvider;
        private readonly ITenantScope _tenantScope;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<InvitationService> _logger;

        public InvitationService(
            IdentityDbContext db,
            IOrganizationLookup organizationLookup,
            ITenantContext tenantContext,
            ICurrentUser currentUser,
            IMemberGridBuilder gridBuilder,
            IMemberDelegationRules rules,
            IAuditLog auditLog,
            IEmailService emailService,
            IOptions<InvitationOptions> invitationOptions,
            IOptions<EmailOptions> emailOptions,
            TimeProvider timeProvider,
            ITenantScope tenantScope,
            IPasswordHasher passwordHasher,
            IStringLocalizer<SharedResource> localizer,
            ILogger<InvitationService> logger)
        {
            _db = db;
            _organizationLookup = organizationLookup;
            _tenantContext = tenantContext;
            _currentUser = currentUser;
            _gridBuilder = gridBuilder;
            _rules = rules;
            _auditLog = auditLog;
            _emailService = emailService;
            _invitationOptions = invitationOptions;
            _emailOptions = emailOptions;
            _timeProvider = timeProvider;
            _tenantScope = tenantScope;
            _passwordHasher = passwordHasher;
            _localizer = localizer;
            _logger = logger;
        }

        public async Task<InvitationResponse> CreateAsync(CreateInvitationRequest request, string language)
        {
            var organizationId = _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);
            var email = request.Email.Trim().ToLowerInvariant();
            RequireWellFormedLocations(email, request);

            await RequireNotMemberAsync(email, organizationId);
            var grids = await BuildGridsAsync(request, organizationId);
            await _rules.RequireAsync(null, grids.Select(g => new LocationGrant(g.LocationId, g.Grid.Levels)).ToList());

            var replaced = await _db.OrganizationInvitations
                .FirstOrDefaultAsync(i => i.OrganizationId == organizationId && i.Email == email
                    && i.UsedAt == null && i.CancelledAt == null && i.ReplacedAt == null);
            var before = replaced is null ? null : await DescribeAsync(replaced);
            var token = InvitationTokens.Generate();
            var invitation = NewInvitation(organizationId, email, language, token);
            if (replaced is not null)
                replaced.ReplacedAt = invitation.CreatedAt;
            _db.OrganizationInvitations.Add(invitation);
            InvitationGrids.AddRows(_db, invitation, grids);
            await SaveOpenInvitationAsync();

            await RecordInvitedAsync(invitation, before, InvitationGrids.Describe(email, grids));
            await SendEmailAsync(invitation, token, grids);
            return new InvitationResponse { Id = invitation.Id, Email = email, ExpiresAt = invitation.ExpiresAt };
        }

        // A null entry or a repeated location is a bad request, not a crash or a database error.
        private void RequireWellFormedLocations(string email, CreateInvitationRequest request)
        {
            if (email.Length == 0 || request.Locations.Count == 0 || request.Locations.Any(l => l is null)
                || request.Locations.Select(l => l.LocationId).Distinct().Count() != request.Locations.Count)
                throw new BusinessException(_localizer["ValidationFailed"].Value, 400);
        }

        // Two requests for the same address at once: the open-email index lets only one win.
        private async Task SaveOpenInvitationAsync()
        {
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { ConstraintName: IdentityDbContext.OpenInvitationIndexName })
            {
                throw new BusinessException(_localizer["ValidationFailed"].Value, 409);
            }
        }

        private async Task RequireNotMemberAsync(string email, Guid organizationId)
        {
            var isMember = await (
                from member in _db.OrganizationMembers
                join user in _db.Users on member.UserId equals user.Id
                where member.OrganizationId == organizationId && user.Email.ToLower() == email
                select member.Id).AnyAsync();
            if (isMember)
                throw new BusinessException(_localizer["AlreadyMemberOfOrganization"].Value, 400);
        }

        private async Task<List<LocationGridPlan>> BuildGridsAsync(CreateInvitationRequest request, Guid organizationId)
        {
            var grids = new List<LocationGridPlan>();
            foreach (var location in request.Locations)
            {
                var summary = await _organizationLookup.GetLocationAsync(location.LocationId);
                if (summary is null || summary.OrganizationId != organizationId)
                    throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

                var grid = await _gridBuilder.BuildAsync(organizationId, location.RoleNames, InvitationGrids.ParseLevels(location.Levels, _localizer));
                // A location with no roles and no levels stores no rows, so a cancel could not find it.
                if (grid.Roles.Count == 0 && grid.Levels.Count == 0)
                    throw new BusinessException(_localizer["ValidationFailed"].Value, 400);

                grids.Add(new LocationGridPlan(location.LocationId, summary.Name, grid));
            }

            return grids;
        }

        private OrganizationInvitation NewInvitation(Guid organizationId, string email, string language, string token)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            return new OrganizationInvitation
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Email = email,
                InvitedByUserId = _currentUser.UserId ?? Guid.Empty,
                Language = language,
                TokenHash = InvitationTokens.Hash(token),
                ExpiresAt = now.AddDays(InvitationLifetimeDays),
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        private async Task<string> DescribeAsync(OrganizationInvitation invitation)
        {
            var roles = await (
                from label in _db.OrganizationInvitationRoles
                join role in _db.OrganizationRoles on label.RoleId equals role.Id
                where label.InvitationId == invitation.Id
                select new { label.LocationId, role.Name }).ToListAsync();
            var levels = await _db.OrganizationInvitationLevels.Where(l => l.InvitationId == invitation.Id).ToListAsync();

            return JsonSerializer.Serialize(new
            {
                invitation.Email,
                Locations = roles.Select(r => r.LocationId).Concat(levels.Select(l => l.LocationId)).Distinct().Select(locationId => new
                {
                    LocationId = locationId,
                    Roles = roles.Where(r => r.LocationId == locationId).Select(r => r.Name).OrderBy(n => n).ToList(),
                    Levels = levels.Where(l => l.LocationId == locationId).OrderBy(l => l.Module)
                        .ToDictionary(l => l.Module.ToString(), l => l.Level.ToString())
                })
            });
        }

        // The invitation is already saved; a failed send is logged by invitation ID only (never the address or token).
        private async Task SendEmailAsync(OrganizationInvitation invitation, string token, List<LocationGridPlan> grids)
        {
            try
            {
                var inviter = await _db.Users.Where(u => u.Id == invitation.InvitedByUserId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync() ?? string.Empty;
                var organization = (await _organizationLookup.GetOrganizationsAsync(new[] { invitation.OrganizationId })).FirstOrDefault();
                var organizationName = organization?.Name ?? string.Empty;
                var productName = _emailOptions.Value.FromName;
                var roles = grids.SelectMany(g => g.Grid.Roles.OrderBy(r => r.Name).Select(r => (r.Name, g.LocationName)));

                var html = InvitationEmailTemplate.Build(
                    invitation.Language, inviter.Trim(), organizationName, productName, roles,
                    _invitationOptions.Value.AcceptPageUrl, token, invitation.ExpiresAt);
                var subject = InvitationEmailTemplate.Subject(invitation.Language, organizationName, productName);
                await _emailService.SendEmailAsync(invitation.Email, subject, html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send the email of invitation {InvitationId}.", invitation.Id);
            }
        }

        // The invitation is already saved; a failed audit write must not undo it or fail the request.
        private async Task RecordInvitedAsync(OrganizationInvitation invitation, string? before, string after)
        {
            try
            {
                await _auditLog.RecordAsync(new AuditEntryDto
                {
                    Action = AuditActions.MemberInvited,
                    EntityType = "Invitation",
                    EntityId = invitation.Id,
                    Before = before,
                    After = after
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record the audit entry of invitation {InvitationId}.", invitation.Id);
            }
        }

        public async Task CancelAsync(Guid invitationId)
        {
            var organizationId = _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);
            var invitation = await _db.OrganizationInvitations.FirstOrDefaultAsync(i => i.Id == invitationId
                    && i.OrganizationId == organizationId && i.UsedAt == null && i.CancelledAt == null && i.ReplacedAt == null)
                ?? throw new BusinessException(_localizer["InvitationNotFound"].Value, 404);

            // Only the Members = Manage check applies to a cancel, so the cap never refuses it.
            var locationIds = await _db.OrganizationInvitationLevels.Where(l => l.InvitationId == invitation.Id)
                .Select(l => l.LocationId).Union(_db.OrganizationInvitationRoles.Where(r => r.InvitationId == invitation.Id)
                    .Select(r => r.LocationId)).ToListAsync();
            var membersOnly = new Dictionary<AccessModule, AccessLevel> { [AccessModule.Members] = AccessLevel.Manage };
            await _rules.RequireAsync(null, locationIds.Select(id => new LocationGrant(id, membersOnly)).ToList());

            var before = await DescribeAsync(invitation);
            invitation.CancelledAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync();
            await RecordCancelledAsync(invitation, before);
        }

        // The invitation is already cancelled; a failed audit write must not undo it or fail the request.
        private async Task RecordCancelledAsync(OrganizationInvitation invitation, string before)
        {
            try
            {
                await _auditLog.RecordAsync(new AuditEntryDto
                {
                    Action = AuditActions.MemberInvitationCancelled,
                    EntityType = "Invitation",
                    EntityId = invitation.Id,
                    Before = before,
                    After = JsonSerializer.Serialize(new { State = "cancelled" })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record the audit entry of cancelled invitation {InvitationId}.", invitation.Id);
            }
        }
    }
}
