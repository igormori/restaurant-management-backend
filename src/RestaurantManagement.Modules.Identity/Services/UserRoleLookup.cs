using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Identity;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Owner-only stand-in until the grid replaces the role contracts: an Owner reads as Owner for all
    // locations, anyone else has no role and no location access.
    public class UserRoleLookup : IUserRoleLookup
    {
        private readonly IdentityDbContext _db;

        public UserRoleLookup(IdentityDbContext db)
        {
            _db = db;
        }

        public async Task<bool> UserExistsAsync(Guid userId)
        {
            return await _db.Users.AnyAsync(u => u.Id == userId);
        }

        public async Task<List<RoleAssignmentDto>> GetRoleAssignmentsAsync(Guid userId, Guid organizationId)
        {
            var isOwner = await IsOwnerAsync(userId, organizationId);

            return isOwner
                ? new List<RoleAssignmentDto> { new() { Role = Roles.Owner, LocationId = null } }
                : new List<RoleAssignmentDto>();
        }

        public async Task<List<Guid>> GetOrganizationIdsForUserAsync(Guid userId)
        {
            return await _db.OrganizationMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.OrganizationId)
                .ToListAsync();
        }

        public async Task<bool> HasLocationAccessAsync(Guid userId, Guid organizationId, Guid locationId)
        {
            return await IsOwnerAsync(userId, organizationId);
        }

        private async Task<bool> IsOwnerAsync(Guid userId, Guid organizationId)
        {
            return await _db.OrganizationMembers.AnyAsync(m =>
                m.UserId == userId && m.OrganizationId == organizationId && m.IsOwner);
        }
    }
}
