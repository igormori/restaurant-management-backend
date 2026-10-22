using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Roles;
using RestaurantManagement.Modules.Identity.Models.Roles;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Modules.Identity.Services.Invitations;

namespace RestaurantManagement.Modules.Identity.Services.Roles
{
    public class RoleService : IRoleService
    {
        private const string OwnerRoleName = "Owner";

        private readonly IdentityDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IAccessGuard _accessGuard;
        private readonly IRoleManagementRules _rules;
        private readonly IAuditLog _auditLog;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<RoleService> _logger;

        public RoleService(
            IdentityDbContext db,
            ITenantContext tenantContext,
            IAccessGuard accessGuard,
            IRoleManagementRules rules,
            IAuditLog auditLog,
            IStringLocalizer<SharedResource> localizer,
            ILogger<RoleService> logger)
        {
            _db = db;
            _tenantContext = tenantContext;
            _accessGuard = accessGuard;
            _rules = rules;
            _auditLog = auditLog;
            _localizer = localizer;
            _logger = logger;
        }

        public async Task<List<RoleResponse>> ListAsync()
        {
            var organizationId = RequireOrganizationId();
            await _accessGuard.RequireAnyAccessAsync();
            var roles = await _db.OrganizationRoles.Where(r => r.OrganizationId == organizationId).OrderBy(r => r.Name).ToListAsync();
            var levels = await _db.OrganizationRoleLevels.Where(l => l.OrganizationId == organizationId).ToListAsync();

            return roles.Select(r => MapToResponse(r, levels.Where(l => l.RoleId == r.Id))).ToList();
        }

        public async Task<RoleResponse> GetAsync(Guid roleId)
        {
            var organizationId = RequireOrganizationId();
            await _accessGuard.RequireAnyAccessAsync();
            var (role, levels) = await LoadRoleAsync(roleId, organizationId);
            return MapToResponse(role, levels);
        }

        public async Task<RoleResponse> CreateAsync(CreateRoleRequest request)
        {
            var organizationId = RequireOrganizationId();
            var name = RequireValidName(request.Name);
            var proposed = InvitationGrids.ParseLevels(request.Levels, _localizer);
            await RequireNameFreeAsync(name, organizationId, null);
            await _rules.RequireAsync(new Dictionary<AccessModule, AccessLevel>(), proposed);

            var now = DateTime.UtcNow;
            var role = new OrganizationRole { Id = Guid.NewGuid(), OrganizationId = organizationId, Name = name, CreatedAt = now, UpdatedAt = now };
            _db.OrganizationRoles.Add(role);
            foreach (var (module, level) in proposed.Where(p => p.Value != AccessLevel.None))
                _db.OrganizationRoleLevels.Add(NewLevel(role.Id, organizationId, module, level));

            await SaveRoleAsync();
            await RecordChangeAsync(AuditActions.RoleCreated, role.Id, null, DescribeRole(name, proposed));
            return MapToResponse(role, await _db.OrganizationRoleLevels.Where(l => l.RoleId == role.Id && l.OrganizationId == organizationId).ToListAsync());
        }

        public async Task<RoleResponse> UpdateLevelsAsync(Guid roleId, UpdateRoleLevelsRequest request)
        {
            var organizationId = RequireOrganizationId();
            var (role, stored) = await LoadRoleAsync(roleId, organizationId);
            var proposed = InvitationGrids.ParseLevels(request.Levels, _localizer);
            var current = stored.ToDictionary(l => l.Module, l => l.Level);
            await _rules.RequireAsync(current, proposed);

            var before = DescribeLevels(current);
            StoreLevels(role.Id, organizationId, stored, proposed);
            await SaveRoleAsync();

            var after = DescribeLevels(proposed);
            if (!before.SequenceEqual(after))
                await RecordChangeAsync(AuditActions.RoleLevelsChanged, role.Id,
                    JsonSerializer.Serialize(new { role.Name, Levels = before }),
                    JsonSerializer.Serialize(new { role.Name, Levels = after }));
            return MapToResponse(role, await _db.OrganizationRoleLevels.Where(l => l.RoleId == role.Id && l.OrganizationId == organizationId).ToListAsync());
        }

        public async Task<RoleResponse> RenameAsync(Guid roleId, RenameRoleRequest request)
        {
            var organizationId = RequireOrganizationId();
            var (role, levels) = await LoadRoleAsync(roleId, organizationId);
            var name = RequireValidName(request.Name);
            await RequireNameFreeAsync(name, organizationId, role.Id);
            await _rules.RequireAsync(levels.ToDictionary(l => l.Module, l => l.Level), new Dictionary<AccessModule, AccessLevel>());

            var oldName = role.Name;
            if (oldName == name)
                return MapToResponse(role, levels);

            role.Name = name;
            role.UpdatedAt = DateTime.UtcNow;
            await SaveRoleAsync();
            await RecordChangeAsync(AuditActions.RoleRenamed, role.Id,
                JsonSerializer.Serialize(new { Name = oldName }), JsonSerializer.Serialize(new { Name = name }));
            return MapToResponse(role, levels);
        }

        // Rows are updated in place: deleting and re-adding the same cell in one save would hit the unique index.
        private void StoreLevels(
            Guid roleId, Guid organizationId, List<OrganizationRoleLevel> stored, IReadOnlyDictionary<AccessModule, AccessLevel> proposed)
        {
            _db.OrganizationRoleLevels.RemoveRange(
                stored.Where(l => proposed.GetValueOrDefault(l.Module, AccessLevel.None) == AccessLevel.None));
            foreach (var (module, level) in proposed.Where(p => p.Value != AccessLevel.None))
            {
                var cell = stored.FirstOrDefault(l => l.Module == module);
                if (cell is null)
                    _db.OrganizationRoleLevels.Add(NewLevel(roleId, organizationId, module, level));
                else if (cell.Level != level)
                {
                    cell.Level = level;
                    cell.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        private Guid RequireOrganizationId()
        {
            return _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);
        }

        private async Task<(OrganizationRole Role, List<OrganizationRoleLevel> Levels)> LoadRoleAsync(Guid roleId, Guid organizationId)
        {
            var role = await _db.OrganizationRoles.FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId)
                ?? throw new BusinessException(_localizer["RoleNotFound"].Value, 404);
            var levels = await _db.OrganizationRoleLevels.Where(l => l.RoleId == roleId && l.OrganizationId == organizationId).ToListAsync();
            return (role, levels);
        }

        private string RequireValidName(string? rawName)
        {
            var name = (rawName ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > OrganizationRole.NameMaxLength
                || string.Equals(name, OwnerRoleName, StringComparison.OrdinalIgnoreCase))
                throw new BusinessException(_localizer["RoleNameInvalid"].Value, 400);

            return name;
        }

        private async Task RequireNameFreeAsync(string name, Guid organizationId, Guid? ownRoleId)
        {
            var lowered = name.ToLower();
            var used = await _db.OrganizationRoles
                .AnyAsync(r => r.OrganizationId == organizationId && r.Id != ownRoleId && r.Name.ToLower() == lowered);
            if (used)
                throw new BusinessException(_localizer["RoleNameAlreadyUsed"].Value, 400);
        }

        // The index is the real guard against two simultaneous creates; the check above is a fast path.
        private async Task SaveRoleAsync()
        {
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateNameViolation(ex))
            {
                throw new BusinessException(_localizer["RoleNameAlreadyUsed"].Value, 400);
            }
        }

        private static bool IsDuplicateNameViolation(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException postgresException
                && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                && postgresException.ConstraintName == IdentityDbContext.UniqueRoleNameIndexName;
        }

        private static OrganizationRoleLevel NewLevel(Guid roleId, Guid organizationId, AccessModule module, AccessLevel level)
        {
            var now = DateTime.UtcNow;
            return new OrganizationRoleLevel
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                RoleId = roleId,
                Module = module,
                Level = level,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        private static RoleResponse MapToResponse(OrganizationRole role, IEnumerable<OrganizationRoleLevel> levels)
        {
            var stored = levels.ToDictionary(l => l.Module, l => l.Level);
            return new RoleResponse
            {
                Id = role.Id,
                Name = role.Name,
                Levels = Enum.GetValues<AccessModule>().ToDictionary(m => m.ToString(), m => stored.GetValueOrDefault(m, AccessLevel.None).ToString())
            };
        }

        private static string DescribeRole(string name, IReadOnlyDictionary<AccessModule, AccessLevel> levels)
            => JsonSerializer.Serialize(new { Name = name, Levels = DescribeLevels(levels) });

        private static Dictionary<string, string> DescribeLevels(IReadOnlyDictionary<AccessModule, AccessLevel> levels)
            => levels.Where(l => l.Value != AccessLevel.None).OrderBy(l => l.Key)
                .ToDictionary(l => l.Key.ToString(), l => l.Value.ToString());

        // The change is already saved; a failed audit write must not undo it or fail the request.
        private async Task RecordChangeAsync(string action, Guid roleId, string? before, string after)
        {
            try
            {
                await _auditLog.RecordAsync(new AuditEntryDto
                {
                    Action = action,
                    EntityType = "Role",
                    EntityId = roleId,
                    Before = before,
                    After = after
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record {Action} for role {RoleId}.", action, roleId);
            }
        }
    }
}
