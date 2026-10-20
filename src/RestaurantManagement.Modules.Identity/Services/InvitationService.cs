using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class InvitationService : IInvitationService
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
            if (email.Length == 0 || request.Locations.Count == 0)
                throw new BusinessException(_localizer["ValidationFailed"].Value, 400);

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
            AddGrids(invitation, grids);
            await _db.SaveChangesAsync();

            await RecordInvitedAsync(invitation, before, DescribeGrids(email, grids));
            return new InvitationResponse { Id = invitation.Id, Email = email, ExpiresAt = invitation.ExpiresAt };
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

        private async Task<List<(Guid LocationId, BuiltGrid Grid)>> BuildGridsAsync(CreateInvitationRequest request, Guid organizationId)
        {
            var grids = new List<(Guid, BuiltGrid)>();
            foreach (var location in request.Locations)
            {
                var summary = await _organizationLookup.GetLocationAsync(location.LocationId);
                if (summary is null || summary.OrganizationId != organizationId)
                    throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

                var grid = await _gridBuilder.BuildAsync(organizationId, location.RoleNames, ParseLevels(location.Levels));
                grids.Add((location.LocationId, grid));
            }

            return grids;
        }

        private Dictionary<AccessModule, AccessLevel> ParseLevels(Dictionary<string, string> levels)
        {
            var parsed = new Dictionary<AccessModule, AccessLevel>();
            foreach (var (moduleName, levelName) in levels)
            {
                if (!Enum.TryParse<AccessModule>(moduleName, true, out var module) || !Enum.IsDefined(module)
                    || !Enum.TryParse<AccessLevel>(levelName, true, out var level) || !Enum.IsDefined(level))
                    throw new BusinessException(_localizer["ValidationFailed"].Value, 400);

                parsed[module] = level;
            }

            return parsed;
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

        private void AddGrids(OrganizationInvitation invitation, List<(Guid LocationId, BuiltGrid Grid)> grids)
        {
            foreach (var (locationId, grid) in grids)
            {
                foreach (var role in grid.Roles)
                    _db.OrganizationInvitationRoles.Add(new OrganizationInvitationRole
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = invitation.OrganizationId,
                        InvitationId = invitation.Id,
                        LocationId = locationId,
                        RoleId = role.Id,
                        CreatedAt = invitation.CreatedAt,
                        UpdatedAt = invitation.CreatedAt
                    });
                foreach (var (module, level) in grid.Levels)
                    _db.OrganizationInvitationLevels.Add(new OrganizationInvitationLevel
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = invitation.OrganizationId,
                        InvitationId = invitation.Id,
                        LocationId = locationId,
                        Module = module,
                        Level = level,
                        CreatedAt = invitation.CreatedAt,
                        UpdatedAt = invitation.CreatedAt
                    });
            }
        }

        private static string DescribeGrids(string email, List<(Guid LocationId, BuiltGrid Grid)> grids)
            => JsonSerializer.Serialize(new
            {
                Email = email,
                Locations = grids.Select(g => new
                {
                    g.LocationId,
                    Roles = g.Grid.Roles.Select(r => r.Name).OrderBy(n => n).ToList(),
                    Levels = g.Grid.Levels.OrderBy(l => l.Key).ToDictionary(l => l.Key.ToString(), l => l.Value.ToString())
                })
            });

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

        public Task CancelAsync(Guid invitationId) => throw new NotImplementedException();

        public Task AcceptAsync(AcceptInvitationRequest request) => throw new NotImplementedException();

        public Task AcceptWithNewAccountAsync(AcceptInvitationWithAccountRequest request) => throw new NotImplementedException();
    }
}
