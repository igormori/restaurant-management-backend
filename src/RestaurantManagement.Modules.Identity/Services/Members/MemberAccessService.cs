using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Modules.Identity.Services.Invitations;

namespace RestaurantManagement.Modules.Identity.Services.Members
{
    public class MemberAccessService : IMemberAccessService
    {
        private readonly IdentityDbContext _db;
        private readonly IOrganizationLookup _organizationLookup;
        private readonly ITenantContext _tenantContext;
        private readonly IAuditLog _auditLog;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<MemberAccessService> _logger;
        private readonly IMemberDelegationRules _rules;
        private readonly IMemberGridBuilder _gridBuilder;

        public MemberAccessService(
            IdentityDbContext db,
            IOrganizationLookup organizationLookup,
            ITenantContext tenantContext,
            IAuditLog auditLog,
            IStringLocalizer<SharedResource> localizer,
            ILogger<MemberAccessService> logger,
            IMemberDelegationRules rules,
            IMemberGridBuilder gridBuilder)
        {
            _db = db;
            _organizationLookup = organizationLookup;
            _tenantContext = tenantContext;
            _auditLog = auditLog;
            _localizer = localizer;
            _logger = logger;
            _rules = rules;
            _gridBuilder = gridBuilder;
        }

        public Task<MemberLocationAccessResponse> SetAccessAsync(
            Guid userId,
            Guid locationId,
            IReadOnlyCollection<string> roleNames,
            Dictionary<string, string> levelNames)
            => SetAccessAsync(userId, locationId, roleNames, InvitationGrids.ParseLevels(levelNames, _localizer));

        public async Task<MemberLocationAccessResponse> SetAccessAsync(
            Guid userId,
            Guid locationId,
            IReadOnlyCollection<string> roleNames,
            IReadOnlyDictionary<AccessModule, AccessLevel> changedLevels)
        {
            var organizationId = RequireOrganizationId();
            await RequireEditableMemberAsync(userId, organizationId);
            await RequireLocationAsync(locationId, organizationId);
            var roles = await RequireRolesAsync(roleNames, organizationId);

            var finalLevels = await MergeLevelsAsync(roles, changedLevels, organizationId);
            await _rules.RequireAsync(userId, new[] { new LocationGrant(locationId, finalLevels) });

            var before = await LoadStateAsync(userId, locationId, organizationId);
            await StoreLevelsAsync(userId, locationId, organizationId, finalLevels);
            await StoreLabelsAsync(userId, locationId, organizationId, roles);
            await _db.SaveChangesAsync();

            var after = await LoadStateAsync(userId, locationId, organizationId);
            await RecordChangeAsync(userId, locationId, before, after);
            return (await GetAccessAsync(userId)).FirstOrDefault(a => a.LocationId == locationId)
                ?? new MemberLocationAccessResponse { LocationId = locationId };
        }

        public async Task<List<MemberLocationAccessResponse>> GetAccessAsync(Guid userId)
        {
            var organizationId = RequireOrganizationId();
            var levels = await _db.MemberLocationLevels
                .Where(l => l.UserId == userId && l.OrganizationId == organizationId)
                .ToListAsync();
            var labels = await LoadLabelsAsync(userId, organizationId);

            return levels.Select(l => l.LocationId).Concat(labels.Select(l => l.LocationId))
                .Distinct()
                .Select(locationId => new MemberLocationAccessResponse
                {
                    LocationId = locationId,
                    RoleNames = labels.Where(l => l.LocationId == locationId).Select(l => l.RoleName).OrderBy(n => n).ToList(),
                    Levels = levels.Where(l => l.LocationId == locationId).OrderBy(l => l.Module)
                        .Select(l => new AccessCellDto { LocationId = locationId, Module = l.Module, Level = l.Level }).ToList()
                })
                .ToList();
        }

        private Guid RequireOrganizationId()
        {
            return _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);
        }

        private async Task RequireEditableMemberAsync(Guid userId, Guid organizationId)
        {
            var member = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId);
            if (member is null)
                throw new BusinessException(_localizer["UserNotMemberOfOrganization"].Value, 404);

            // The Owner is never restricted, so no grid change may touch the Owner.
            if (member.IsOwner)
                throw new BusinessException(_localizer["AccessDenied"].Value, 403);
        }

        private async Task RequireLocationAsync(Guid locationId, Guid organizationId)
        {
            var location = await _organizationLookup.GetLocationAsync(locationId);
            if (location is null || location.OrganizationId != organizationId)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);
        }

        private async Task<List<OrganizationRole>> RequireRolesAsync(IReadOnlyCollection<string> roleNames, Guid organizationId)
        {
            var names = roleNames.Distinct().ToList();
            var roles = await _db.OrganizationRoles
                .Where(r => r.OrganizationId == organizationId && names.Contains(r.Name))
                .ToListAsync();
            if (roles.Count != names.Count)
                throw new BusinessException(_localizer["RoleNotFound"].Value, 404);

            return roles;
        }

        private async Task<Dictionary<AccessModule, AccessLevel>> MergeLevelsAsync(
            List<OrganizationRole> roles, IReadOnlyDictionary<AccessModule, AccessLevel> changedLevels, Guid organizationId)
        {
            var roleIds = roles.Select(r => r.Id).ToList();
            var defaults = await _db.OrganizationRoleLevels
                .Where(l => l.OrganizationId == organizationId && roleIds.Contains(l.RoleId))
                .ToListAsync();

            var merged = defaults
                .GroupBy(l => l.Module)
                .ToDictionary(g => g.Key, g => g.Max(l => l.Level));
            foreach (var (module, level) in changedLevels)
                merged[module] = level;

            return merged.Where(m => m.Value != AccessLevel.None).ToDictionary(m => m.Key, m => m.Value);
        }

        // Rows are updated in place: deleting and re-adding the same cell in one save would hit the unique index.
        private async Task StoreLevelsAsync(
            Guid userId, Guid locationId, Guid organizationId, Dictionary<AccessModule, AccessLevel> finalLevels)
        {
            var stored = await _db.MemberLocationLevels
                .Where(l => l.UserId == userId && l.LocationId == locationId && l.OrganizationId == organizationId)
                .ToListAsync();

            _db.MemberLocationLevels.RemoveRange(stored.Where(l => !finalLevels.ContainsKey(l.Module)));
            foreach (var (module, level) in finalLevels)
            {
                var cell = stored.FirstOrDefault(l => l.Module == module);
                if (cell is null)
                    _db.MemberLocationLevels.Add(NewLevel(userId, locationId, organizationId, module, level));
                else if (cell.Level != level)
                {
                    cell.Level = level;
                    cell.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        private async Task StoreLabelsAsync(Guid userId, Guid locationId, Guid organizationId, List<OrganizationRole> roles)
        {
            var stored = await _db.MemberLocationRoles
                .Where(l => l.UserId == userId && l.LocationId == locationId && l.OrganizationId == organizationId)
                .ToListAsync();
            var roleIds = roles.Select(r => r.Id).ToHashSet();

            _db.MemberLocationRoles.RemoveRange(stored.Where(l => !roleIds.Contains(l.RoleId)));
            var now = DateTime.UtcNow;
            foreach (var roleId in roleIds.Where(id => stored.All(l => l.RoleId != id)))
            {
                _db.MemberLocationRoles.Add(new MemberLocationRole
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    UserId = userId,
                    LocationId = locationId,
                    RoleId = roleId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        private static MemberLocationLevel NewLevel(Guid userId, Guid locationId, Guid organizationId, AccessModule module, AccessLevel level)
        {
            var now = DateTime.UtcNow;
            return new MemberLocationLevel
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                LocationId = locationId,
                Module = module,
                Level = level,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        private async Task<List<(Guid LocationId, string RoleName)>> LoadLabelsAsync(Guid userId, Guid organizationId)
        {
            var labels = await (
                from label in _db.MemberLocationRoles
                join role in _db.OrganizationRoles on label.RoleId equals role.Id
                where label.UserId == userId && label.OrganizationId == organizationId
                select new { label.LocationId, role.Name })
                .ToListAsync();

            return labels.Select(l => (l.LocationId, l.Name)).ToList();
        }

        private async Task<string> LoadStateAsync(Guid userId, Guid locationId, Guid organizationId)
        {
            var roleNames = (await LoadLabelsAsync(userId, organizationId))
                .Where(l => l.LocationId == locationId).Select(l => l.RoleName).OrderBy(n => n).ToList();
            var levels = await _db.MemberLocationLevels
                .Where(l => l.UserId == userId && l.LocationId == locationId && l.OrganizationId == organizationId)
                .ToListAsync();

            return JsonSerializer.Serialize(new
            {
                Roles = roleNames,
                Levels = levels.OrderBy(l => l.Module).ToDictionary(l => l.Module.ToString(), l => l.Level.ToString())
            });
        }

        // The change is already saved; a failed audit write must not undo it or fail the request.
        private async Task RecordChangeAsync(Guid userId, Guid locationId, string before, string after)
        {
            if (before == after)
                return;

            try
            {
                await _auditLog.RecordAsync(new AuditEntryDto
                {
                    Action = AuditActions.MemberAccessChanged,
                    EntityType = "Member",
                    EntityId = userId,
                    LocationId = locationId,
                    Before = before,
                    After = after
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record the access change of member {UserId} at location {LocationId}.", userId, locationId);
            }
        }
    }
}
