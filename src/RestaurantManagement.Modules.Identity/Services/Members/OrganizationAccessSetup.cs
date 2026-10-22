using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Entities.Roles;
using RestaurantManagement.Shared.Services.Identity;

namespace RestaurantManagement.Modules.Identity.Services.Members
{
    public class OrganizationAccessSetup : IOrganizationAccessSetup
    {
        private readonly IdentityDbContext _db;

        public OrganizationAccessSetup(IdentityDbContext db)
        {
            _db = db;
        }

        public async Task AddOwnerWithDefaultRolesAsync(Guid ownerUserId, Guid organizationId)
        {
            var now = DateTime.UtcNow;
            _db.OrganizationMembers.Add(new OrganizationMember
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                OrganizationId = organizationId,
                IsOwner = true,
                CreatedAt = now,
                UpdatedAt = now
            });

            foreach (var (name, levels) in OrganizationDefaultRoles.Levels)
            {
                var role = new OrganizationRole
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Name = name,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.OrganizationRoles.Add(role);
                _db.OrganizationRoleLevels.AddRange(levels.Select(l => new OrganizationRoleLevel
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    RoleId = role.Id,
                    Module = l.Key,
                    Level = l.Value,
                    CreatedAt = now,
                    UpdatedAt = now
                }));
            }

            await _db.SaveChangesAsync();
        }

        public async Task RemoveOwnerAndRolesAsync(Guid ownerUserId, Guid organizationId)
        {
            var members = await _db.OrganizationMembers
                .Where(m => m.OrganizationId == organizationId && m.UserId == ownerUserId && m.IsOwner)
                .ToListAsync();
            var roles = await _db.OrganizationRoles.Where(r => r.OrganizationId == organizationId).ToListAsync();
            var levels = await _db.OrganizationRoleLevels.Where(l => l.OrganizationId == organizationId).ToListAsync();

            _db.OrganizationRoleLevels.RemoveRange(levels);
            _db.OrganizationRoles.RemoveRange(roles);
            _db.OrganizationMembers.RemoveRange(members);
            await _db.SaveChangesAsync();
        }
    }
}
