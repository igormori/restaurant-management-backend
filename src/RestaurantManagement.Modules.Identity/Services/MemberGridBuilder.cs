using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class MemberGridBuilder : IMemberGridBuilder
    {
        private readonly IdentityDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MemberGridBuilder(IdentityDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public async Task<BuiltGrid> BuildAsync(
            Guid organizationId, IReadOnlyCollection<string> roleNames, IReadOnlyDictionary<AccessModule, AccessLevel> changedLevels)
        {
            var names = roleNames.Distinct().ToList();
            var roles = await _db.OrganizationRoles
                .Where(r => r.OrganizationId == organizationId && names.Contains(r.Name))
                .ToListAsync();
            if (roles.Count != names.Count)
                throw new BusinessException(_localizer["RoleNotFound"].Value, 404);

            var roleIds = roles.Select(r => r.Id).ToList();
            var defaults = await _db.OrganizationRoleLevels
                .Where(l => l.OrganizationId == organizationId && roleIds.Contains(l.RoleId))
                .ToListAsync();

            var merged = defaults
                .GroupBy(l => l.Module)
                .ToDictionary(g => g.Key, g => g.Max(l => l.Level));
            foreach (var (module, level) in changedLevels)
                merged[module] = level;

            var levels = merged.Where(m => m.Value != AccessLevel.None).ToDictionary(m => m.Key, m => m.Value);
            return new BuiltGrid(roles, levels);
        }
    }
}
