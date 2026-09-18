using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Services.Identity;

namespace RestaurantManagement.Modules.Identity.Services
{
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

        public async Task<string?> GetRoleAsync(Guid userId, Guid organizationId)
        {
            var userRole = await _db.UserRoles
                .FirstOrDefaultAsync(r => r.UserId == userId && r.OrganizationId == organizationId);

            return userRole?.Role;
        }

        public async Task<List<Guid>> GetOrganizationIdsForUserAsync(Guid userId)
        {
            return await _db.UserRoles
                .Where(r => r.UserId == userId && r.OrganizationId != null)
                .Select(r => r.OrganizationId!.Value)
                .ToListAsync();
        }
    }
}
